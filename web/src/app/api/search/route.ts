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
    const query = searchParams.get('q');
    const type = searchParams.get('type'); // all, live, movie, series
    const limit = parseInt(searchParams.get('limit') || '20', 10);

    if (!query || query.length < 2) {
      return NextResponse.json({
        channels: [],
        movies: [],
        series: [],
        totalResults: 0,
      });
    }

    const searchFilter = {
      name: {
        contains: query,
      },
    };

    const playlistFilter = {
      playlist: {
        userId: user.id,
        isActive: true,
      },
    };

    // Fetch results based on type filter
    const shouldSearchChannels = !type || type === 'all' || type === 'live';
    const shouldSearchMovies = !type || type === 'all' || type === 'movie';
    const shouldSearchSeries = !type || type === 'all' || type === 'series';

    const [channels, movies, series] = await Promise.all([
      shouldSearchChannels
        ? prisma.channel.findMany({
            where: {
              ...searchFilter,
              ...playlistFilter,
            },
            take: limit,
            select: {
              id: true,
              name: true,
              logoUrl: true,
              groupTitle: true,
              number: true,
            },
          })
        : [],
      shouldSearchMovies
        ? prisma.vodItem.findMany({
            where: {
              ...searchFilter,
              ...playlistFilter,
              type: 'movie',
            },
            take: limit,
            select: {
              id: true,
              name: true,
              posterUrl: true,
              year: true,
              rating: true,
              genre: true,
            },
          })
        : [],
      shouldSearchSeries
        ? prisma.vodItem.findMany({
            where: {
              ...searchFilter,
              ...playlistFilter,
              type: 'series',
            },
            take: limit,
            select: {
              id: true,
              name: true,
              posterUrl: true,
              year: true,
              rating: true,
              genre: true,
              _count: {
                select: { episodes: true },
              },
            },
          })
        : [],
    ]);

    // Transform series to include episode count
    const transformedSeries = series.map((s) => ({
      id: s.id,
      name: s.name,
      posterUrl: s.posterUrl,
      year: s.year,
      rating: s.rating,
      genre: s.genre,
      episodeCount: s._count.episodes,
    }));

    const totalResults = channels.length + movies.length + series.length;

    return NextResponse.json({
      channels,
      movies,
      series: transformedSeries,
      totalResults,
    });
  } catch (error) {
    console.error('Search error:', error);
    return NextResponse.json({ error: 'Erreur serveur' }, { status: 500 });
  }
}
