import { NextResponse } from 'next/server';
import prisma from '@/lib/prisma';
import { getCurrentUser } from '@/lib/auth';

export async function GET(request: Request) {
  try {
    const user = await getCurrentUser(request);
    if (!user) {
      return NextResponse.json({ error: 'Non authentifié' }, { status: 401 });
    }

    const { searchParams } = new URL(request.url);
    const search = searchParams.get('search');
    const genre = searchParams.get('genre');
    const playlistId = searchParams.get('playlistId');
    const page = parseInt(searchParams.get('page') || '1', 10);
    const limit = parseInt(searchParams.get('limit') || '50', 10);

    const where: Record<string, unknown> = {
      type: 'movie',
      playlist: {
        userId: user.id,
        isActive: true,
      },
    };

    if (playlistId) {
      where.playlistId = playlistId;
    }

    if (search) {
      where.name = {
        contains: search,
      };
    }

    if (genre) {
      where.genre = {
        contains: genre,
      };
    }

    const [movies, total] = await Promise.all([
      prisma.vodItem.findMany({
        where,
        orderBy: [{ createdAt: 'desc' }, { name: 'asc' }],
        skip: (page - 1) * limit,
        take: limit,
        select: {
          id: true,
          name: true,
          posterUrl: true,
          backdropUrl: true,
          plot: true,
          genre: true,
          year: true,
          rating: true,
          duration: true,
          streamUrl: true,
        },
      }),
      prisma.vodItem.count({ where }),
    ]);

    return NextResponse.json({
      movies,
      pagination: {
        page,
        limit,
        total,
        totalPages: Math.ceil(total / limit),
      },
    });
  } catch (error) {
    console.error('Get movies error:', error);
    return NextResponse.json({ error: 'Erreur serveur' }, { status: 500 });
  }
}
