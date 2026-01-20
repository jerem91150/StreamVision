import { NextResponse } from 'next/server';
import prisma from '@/lib/prisma';
import { verifyPassword, createSession, setAuthCookies } from '@/lib/auth';
import { checkRateLimit, getClientIP, createRateLimitResponse, loginRateLimitConfig } from '@/lib/rate-limit';

export async function POST(request: Request) {
  try {
    // Rate limiting
    const clientIP = getClientIP(request);
    const rateLimitKey = `login:${clientIP}`;
    const rateLimit = checkRateLimit(rateLimitKey, loginRateLimitConfig);

    if (rateLimit.limited) {
      return createRateLimitResponse(rateLimit.resetIn);
    }

    const body = await request.json();
    const { email, password } = body;

    // Validation
    if (!email || !password) {
      return NextResponse.json(
        { error: 'Email et mot de passe requis' },
        { status: 400 }
      );
    }

    // Find user
    const user = await prisma.user.findUnique({
      where: { email: email.toLowerCase().trim() },
    });

    if (!user) {
      // Use same error message to prevent user enumeration
      return NextResponse.json(
        { error: 'Email ou mot de passe incorrect' },
        { status: 401 }
      );
    }

    // Verify password
    const isValid = await verifyPassword(password, user.passwordHash);
    if (!isValid) {
      return NextResponse.json(
        { error: 'Email ou mot de passe incorrect' },
        { status: 401 }
      );
    }

    // Create session
    const deviceInfo = request.headers.get('User-Agent') || undefined;
    const tokens = await createSession(user.id, deviceInfo);

    // Create response with user data
    const jsonResponse = NextResponse.json({
      user: {
        id: user.id,
        username: user.username,
        email: user.email,
        avatarUrl: user.avatarUrl,
      },
      // Also return tokens for API clients
      accessToken: tokens.accessToken,
      refreshToken: tokens.refreshToken,
    });

    // Set HTTP-only cookies for browser clients
    return setAuthCookies(jsonResponse, tokens.accessToken, tokens.refreshToken);
  } catch (error) {
    console.error('Login error:', error);
    return NextResponse.json(
      { error: 'Erreur de connexion' },
      { status: 500 }
    );
  }
}
