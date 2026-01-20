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
      type: 'series',
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

    const [series, total] = await Promise.all([
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
          _count: {
            select: { episodes: true },
          },
        },
      }),
      prisma.vodItem.count({ where }),
    ]);

    // Transform to include episode count
    const transformedSeries = series.map((s) => ({
      id: s.id,
      name: s.name,
      posterUrl: s.posterUrl,
      backdropUrl: s.backdropUrl,
      plot: s.plot,
      genre: s.genre,
      year: s.year,
      rating: s.rating,
      episodeCount: s._count.episodes,
    }));

    return NextResponse.json({
      series: transformedSeries,
      pagination: {
        page,
        limit,
        total,
        totalPages: Math.ceil(total / limit),
      },
    });
  } catch (error) {
    console.error('Get series error:', error);
    return NextResponse.json({ error: 'Erreur serveur' }, { status: 500 });
  }
}
