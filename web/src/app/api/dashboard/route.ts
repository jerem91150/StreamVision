import { NextResponse } from 'next/server';
import prisma from '@/lib/prisma';
import { getCurrentUser } from '@/lib/auth';

export async function GET(request: Request) {
  try {
    const user = await getCurrentUser(request);
    if (!user) {
      return NextResponse.json({ error: 'Non authentifié' }, { status: 401 });
    }

    // Get all data in parallel
    const [
      playlists,
      continueWatching,
      recentChannels,
      recentMovies,
      recentSeries,
      favorites,
    ] = await Promise.all([
      // User's playlists with counts
      prisma.playlist.findMany({
        where: { userId: user.id, isActive: true },
        select: {
          id: true,
          name: true,
          channelCount: true,
          movieCount: true,
          seriesCount: true,
        },
      }),

      // Continue watching (from history)
      prisma.watchHistory.findMany({
        where: {
          userId: user.id,
          progress: { gt: 0 },
          duration: { gt: 0 },
        },
        include: {
          channel: {
            select: {
              id: true,
              name: true,
              logoUrl: true,
            },
          },
          vodItem: {
            select: {
              id: true,
              type: true,
              name: true,
              posterUrl: true,
            },
          },
          episode: {
            select: {
              id: true,
              name: true,
              seasonNum: true,
              episodeNum: true,
              vodItem: {
                select: {
                  id: true,
                  name: true,
                  posterUrl: true,
                },
              },
            },
          },
        },
        orderBy: { watchedAt: 'desc' },
        take: 10,
      }),

      // Recent/popular channels
      prisma.channel.findMany({
        where: {
          playlist: {
            userId: user.id,
            isActive: true,
          },
        },
        select: {
          id: true,
          name: true,
          logoUrl: true,
          groupTitle: true,
          number: true,
        },
        take: 12,
        orderBy: { number: 'asc' },
      }),

      // Recent movies
      prisma.vodItem.findMany({
        where: {
          type: 'movie',
          playlist: {
            userId: user.id,
            isActive: true,
          },
        },
        select: {
          id: true,
          name: true,
          posterUrl: true,
          year: true,
          rating: true,
          genre: true,
        },
        take: 12,
        orderBy: { createdAt: 'desc' },
      }),

      // Recent series
      prisma.vodItem.findMany({
        where: {
          type: 'series',
          playlist: {
            userId: user.id,
            isActive: true,
          },
        },
        select: {
          id: true,
          name: true,
          posterUrl: true,
          year: true,
          rating: true,
          genre: true,
        },
        take: 12,
        orderBy: { createdAt: 'desc' },
      }),

      // User favorites count
      prisma.favorite.count({
        where: { userId: user.id },
      }),
    ]);

    // Calculate totals
    const totalChannels = playlists.reduce((sum, p) => sum + p.channelCount, 0);
    const totalMovies = playlists.reduce((sum, p) => sum + p.movieCount, 0);
    const totalSeries = playlists.reduce((sum, p) => sum + p.seriesCount, 0);

    // Transform continue watching
    const continueWatchingTransformed = continueWatching
      .filter((h) => {
        // Only include items that are not fully watched (< 90%)
        const progress = h.duration ? (h.progress / h.duration) * 100 : 0;
        return progress < 90 && progress > 0;
      })
      .map((h) => {
        const progress = h.duration ? Math.round((h.progress / h.duration) * 100) : 0;

        if (h.channel) {
          return {
            id: h.channel.id,
            type: 'live' as const,
            name: h.channel.name,
            imageUrl: h.channel.logoUrl,
            progress,
            watchedAt: h.watchedAt,
          };
        }

        if (h.episode) {
          return {
            id: h.episode.id,
            type: 'episode' as const,
            name: `${h.episode.vodItem.name} - S${h.episode.seasonNum}E${h.episode.episodeNum}`,
            imageUrl: h.episode.vodItem.posterUrl,
            progress,
            watchedAt: h.watchedAt,
            seriesId: h.episode.vodItem.id,
          };
        }

        if (h.vodItem) {
          return {
            id: h.vodItem.id,
            type: h.vodItem.type as 'movie' | 'series',
            name: h.vodItem.name,
            imageUrl: h.vodItem.posterUrl,
            progress,
            watchedAt: h.watchedAt,
          };
        }

        return null;
      })
      .filter(Boolean);

    return NextResponse.json({
      stats: {
        playlists: playlists.length,
        channels: totalChannels,
        movies: totalMovies,
        series: totalSeries,
        favorites,
      },
      continueWatching: continueWatchingTransformed,
      channels: recentChannels,
      movies: recentMovies,
      series: recentSeries,
      hasContent: totalChannels > 0 || totalMovies > 0 || totalSeries > 0,
    });
  } catch (error) {
    console.error('Get dashboard error:', error);
    return NextResponse.json({ error: 'Erreur serveur' }, { status: 500 });
  }
}
