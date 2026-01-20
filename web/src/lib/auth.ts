import jwt from 'jsonwebtoken';
import bcrypt from 'bcryptjs';
import { cookies } from 'next/headers';
import prisma from './prisma';

const JWT_SECRET = process.env.JWT_SECRET!;
const JWT_REFRESH_SECRET = process.env.JWT_REFRESH_SECRET!;

if (!JWT_SECRET || !JWT_REFRESH_SECRET) {
  console.warn('WARNING: JWT secrets not set. Using fallback (UNSAFE in production)');
}

const ACCESS_TOKEN_EXPIRY = '15m';
const REFRESH_TOKEN_EXPIRY = '7d';

// Cookie config for security
export const COOKIE_OPTIONS = {
  httpOnly: true,
  secure: process.env.NODE_ENV === 'production',
  sameSite: 'lax' as const,
  path: '/',
};

export interface JWTPayload {
  userId: string;
  email: string;
}

export async function hashPassword(password: string): Promise<string> {
  return bcrypt.hash(password, 12);
}

export async function verifyPassword(password: string, hashedPassword: string): Promise<boolean> {
  return bcrypt.compare(password, hashedPassword);
}

export function generateAccessToken(payload: JWTPayload): string {
  return jwt.sign(payload, JWT_SECRET || 'dev-secret', { expiresIn: ACCESS_TOKEN_EXPIRY });
}

export function generateRefreshToken(payload: JWTPayload): string {
  return jwt.sign(payload, JWT_REFRESH_SECRET || 'dev-refresh-secret', { expiresIn: REFRESH_TOKEN_EXPIRY });
}

export function verifyAccessToken(token: string): JWTPayload | null {
  try {
    return jwt.verify(token, JWT_SECRET || 'dev-secret') as JWTPayload;
  } catch {
    return null;
  }
}

export function verifyRefreshToken(token: string): JWTPayload | null {
  try {
    return jwt.verify(token, JWT_REFRESH_SECRET || 'dev-refresh-secret') as JWTPayload;
  } catch {
    return null;
  }
}

export async function createSession(userId: string, deviceInfo?: string) {
  const user = await prisma.user.findUnique({ where: { id: userId } });
  if (!user) throw new Error('User not found');

  const payload: JWTPayload = { userId: user.id, email: user.email };
  const accessToken = generateAccessToken(payload);
  const refreshToken = generateRefreshToken(payload);

  // Store refresh token in database
  const expiresAt = new Date();
  expiresAt.setDate(expiresAt.getDate() + 7);

  await prisma.session.create({
    data: {
      userId,
      refreshToken,
      deviceInfo,
      expiresAt,
    },
  });

  return { accessToken, refreshToken };
}

export async function refreshSession(refreshToken: string) {
  const payload = verifyRefreshToken(refreshToken);
  if (!payload) {
    throw new Error('Invalid refresh token');
  }

  // Find session in database
  const session = await prisma.session.findUnique({
    where: { refreshToken },
    include: { user: true },
  });

  if (!session || session.expiresAt < new Date()) {
    throw new Error('Session expired or not found');
  }

  // Generate new tokens (token rotation for security)
  const newPayload: JWTPayload = { userId: session.userId, email: session.user.email };
  const newAccessToken = generateAccessToken(newPayload);
  const newRefreshToken = generateRefreshToken(newPayload);

  // Update session with new refresh token
  const newExpiresAt = new Date();
  newExpiresAt.setDate(newExpiresAt.getDate() + 7);

  await prisma.session.update({
    where: { id: session.id },
    data: {
      refreshToken: newRefreshToken,
      expiresAt: newExpiresAt,
    },
  });

  return { accessToken: newAccessToken, refreshToken: newRefreshToken };
}

export async function invalidateSession(refreshToken: string) {
  try {
    await prisma.session.delete({
      where: { refreshToken },
    });
  } catch {
    // Session might already be deleted
  }
}

export async function invalidateAllUserSessions(userId: string) {
  await prisma.session.deleteMany({
    where: { userId },
  });
}

export async function getCurrentUser(request: Request) {
  // Try Authorization header first (API clients)
  const authHeader = request.headers.get('Authorization');
  if (authHeader?.startsWith('Bearer ')) {
    const token = authHeader.slice(7);
    const payload = verifyAccessToken(token);
    if (payload) {
      return getUserById(payload.userId);
    }
  }

  // Try cookies (browser)
  const cookieHeader = request.headers.get('cookie');
  if (cookieHeader) {
    const accessToken = parseCookie(cookieHeader, 'accessToken');
    if (accessToken) {
      const payload = verifyAccessToken(accessToken);
      if (payload) {
        return getUserById(payload.userId);
      }
    }
  }

  return null;
}

async function getUserById(userId: string) {
  return prisma.user.findUnique({
    where: { id: userId },
    select: {
      id: true,
      email: true,
      username: true,
      avatarUrl: true,
      createdAt: true,
    },
  });
}

function parseCookie(cookieHeader: string, name: string): string | null {
  const match = cookieHeader.match(new RegExp(`(^| )${name}=([^;]+)`));
  return match ? match[2] : null;
}

export function getTokenFromCookies() {
  const cookieStore = cookies();
  return cookieStore.get('accessToken')?.value;
}

/**
 * Set auth cookies in response
 */
export function setAuthCookies(
  response: Response,
  accessToken: string,
  refreshToken: string
): Response {
  const headers = new Headers(response.headers);

  // Access token cookie (short-lived)
  headers.append(
    'Set-Cookie',
    `accessToken=${accessToken}; HttpOnly; ${
      process.env.NODE_ENV === 'production' ? 'Secure; ' : ''
    }SameSite=Lax; Path=/; Max-Age=${15 * 60}`
  );

  // Refresh token cookie (long-lived)
  headers.append(
    'Set-Cookie',
    `refreshToken=${refreshToken}; HttpOnly; ${
      process.env.NODE_ENV === 'production' ? 'Secure; ' : ''
    }SameSite=Lax; Path=/api/auth; Max-Age=${7 * 24 * 60 * 60}`
  );

  return new Response(response.body, {
    status: response.status,
    statusText: response.statusText,
    headers,
  });
}

/**
 * Clear auth cookies
 */
export function clearAuthCookies(response: Response): Response {
  const headers = new Headers(response.headers);

  headers.append(
    'Set-Cookie',
    `accessToken=; HttpOnly; ${
      process.env.NODE_ENV === 'production' ? 'Secure; ' : ''
    }SameSite=Lax; Path=/; Max-Age=0`
  );

  headers.append(
    'Set-Cookie',
    `refreshToken=; HttpOnly; ${
      process.env.NODE_ENV === 'production' ? 'Secure; ' : ''
    }SameSite=Lax; Path=/api/auth; Max-Age=0`
  );

  return new Response(response.body, {
    status: response.status,
    statusText: response.statusText,
    headers,
  });
}
