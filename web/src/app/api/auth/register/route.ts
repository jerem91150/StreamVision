import { NextResponse } from 'next/server';
import prisma from '@/lib/prisma';
import { hashPassword, createSession, setAuthCookies } from '@/lib/auth';
import { checkRateLimit, getClientIP, createRateLimitResponse, authRateLimitConfig } from '@/lib/rate-limit';

export async function POST(request: Request) {
  try {
    // Rate limiting
    const clientIP = getClientIP(request);
    const rateLimitKey = `register:${clientIP}`;
    const rateLimit = checkRateLimit(rateLimitKey, authRateLimitConfig);

    if (rateLimit.limited) {
      return createRateLimitResponse(rateLimit.resetIn);
    }

    const body = await request.json();
    const { email, username, password } = body;

    // Validation
    if (!email || !username || !password) {
      return NextResponse.json(
        { error: 'Tous les champs sont requis' },
        { status: 400 }
      );
    }

    // Email validation
    const emailRegex = /^[^\s@]+@[^\s@]+\.[^\s@]+$/;
    if (!emailRegex.test(email)) {
      return NextResponse.json(
        { error: 'Email invalide' },
        { status: 400 }
      );
    }

    // Password strength validation
    if (password.length < 8) {
      return NextResponse.json(
        { error: 'Le mot de passe doit contenir au moins 8 caracteres' },
        { status: 400 }
      );
    }

    // Check if email already exists
    const existingUser = await prisma.user.findUnique({
      where: { email: email.toLowerCase().trim() },
    });

    if (existingUser) {
      return NextResponse.json(
        { error: 'Cet email est deja utilise' },
        { status: 409 }
      );
    }

    // Hash password
    const passwordHash = await hashPassword(password);

    // Create user with subscription
    const user = await prisma.user.create({
      data: {
        email: email.toLowerCase().trim(),
        username: username.trim(),
        passwordHash,
        subscription: {
          create: {
            tier: 'free',
            status: 'active',
          },
        },
        preferences: {
          create: {},
        },
      },
    });

    // Create session
    const deviceInfo = request.headers.get('User-Agent') || undefined;
    const tokens = await createSession(user.id, deviceInfo);

    // Create response
    const jsonResponse = NextResponse.json({
      user: {
        id: user.id,
        username: user.username,
        email: user.email,
        avatarUrl: user.avatarUrl,
      },
      accessToken: tokens.accessToken,
      refreshToken: tokens.refreshToken,
    });

    // Set HTTP-only cookies
    return setAuthCookies(jsonResponse, tokens.accessToken, tokens.refreshToken);
  } catch (error) {
    console.error('Registration error:', error);
    return NextResponse.json(
      { error: 'Erreur lors de l\'inscription' },
      { status: 500 }
    );
  }
}
