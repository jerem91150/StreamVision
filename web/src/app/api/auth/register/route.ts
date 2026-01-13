import { NextResponse } from 'next/server';
import prisma from '@/lib/prisma';
import { hashPassword, createSession } from '@/lib/auth';

export async function POST(request: Request) {
  try {
    const body = await request.json();
    const { username, email, password } = body;

    // Validation
    if (!username || !email || !password) {
      return NextResponse.json(
        { error: 'Tous les champs sont requis' },
        { status: 400 }
      );
    }

    if (password.length < 8) {
      return NextResponse.json(
        { error: 'Le mot de passe doit contenir au moins 8 caracteres' },
        { status: 400 }
      );
    }

    // Check if user already exists
    const existingUser = await prisma.user.findUnique({
      where: { email },
    });

    if (existingUser) {
      return NextResponse.json(
        { error: 'Cet email est deja utilise' },
        { status: 400 }
      );
    }

    // Create user
    const passwordHash = await hashPassword(password);
    const user = await prisma.user.create({
      data: {
        username,
        email,
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

    // Create session and get tokens
    const deviceInfo = request.headers.get('User-Agent') || undefined;
    const tokens = await createSession(user.id, deviceInfo);

    return NextResponse.json({
      user: {
        id: user.id,
        username: user.username,
        email: user.email,
      },
      ...tokens,
    });
  } catch (error) {
    console.error('Register error:', error);
    return NextResponse.json(
      { error: 'Erreur lors de l\'inscription' },
      { status: 500 }
    );
  }
}
