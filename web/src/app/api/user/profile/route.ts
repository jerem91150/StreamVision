import { NextResponse } from 'next/server';
import prisma from '@/lib/prisma';
import { getCurrentUser } from '@/lib/auth';

export async function GET(request: Request) {
  try {
    const user = await getCurrentUser(request);
    if (!user) {
      return NextResponse.json({ error: 'Non authentifié' }, { status: 401 });
    }

    const profile = await prisma.user.findUnique({
      where: { id: user.id },
      select: {
        id: true,
        email: true,
        username: true,
        avatarUrl: true,
        createdAt: true,
        subscription: {
          select: {
            tier: true,
            status: true,
            expiresAt: true,
          },
        },
        preferences: true,
        _count: {
          select: {
            playlists: true,
            favorites: true,
            watchHistory: true,
          },
        },
      },
    });

    if (!profile) {
      return NextResponse.json({ error: 'Utilisateur non trouvé' }, { status: 404 });
    }

    return NextResponse.json({
      ...profile,
      stats: {
        playlists: profile._count.playlists,
        favorites: profile._count.favorites,
        watchHistory: profile._count.watchHistory,
      },
    });
  } catch (error) {
    console.error('Get profile error:', error);
    return NextResponse.json({ error: 'Erreur serveur' }, { status: 500 });
  }
}

export async function PUT(request: Request) {
  try {
    const user = await getCurrentUser(request);
    if (!user) {
      return NextResponse.json({ error: 'Non authentifié' }, { status: 401 });
    }

    const body = await request.json();
    const { username, avatarUrl } = body;

    // Validate username
    if (username !== undefined && (typeof username !== 'string' || username.length < 2)) {
      return NextResponse.json(
        { error: 'Le nom d\'utilisateur doit contenir au moins 2 caractères' },
        { status: 400 }
      );
    }

    const updatedUser = await prisma.user.update({
      where: { id: user.id },
      data: {
        username: username !== undefined ? username : undefined,
        avatarUrl: avatarUrl !== undefined ? avatarUrl : undefined,
      },
      select: {
        id: true,
        email: true,
        username: true,
        avatarUrl: true,
      },
    });

    return NextResponse.json(updatedUser);
  } catch (error) {
    console.error('Update profile error:', error);
    return NextResponse.json({ error: 'Erreur serveur' }, { status: 500 });
  }
}
