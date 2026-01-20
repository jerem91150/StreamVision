import { NextResponse } from 'next/server';
import prisma from '@/lib/prisma';
import { getCurrentUser } from '@/lib/auth';
import { createXtreamService } from '@/lib/services/xtream-service';

interface RouteParams {
  params: Promise<{ id: string }>;
}

export async function GET(request: Request, { params }: RouteParams) {
  try {
    const user = await getCurrentUser(request);
    if (!user) {
      return NextResponse.json({ error: 'Non authentifié' }, { status: 401 });
    }

    const { id } = await params;

    // Get series with episodes
    const series = await prisma.vodItem.findFirst({
      where: {
        id,
        type: 'series',
        playlist: {
          userId: user.id,
        },
      },
      include: {
        episodes: {
          orderBy: [{ seasonNum: 'asc' }, { episodeNum: 'asc' }],
        },
        playlist: true,
      },
    });

    if (!series) {
      return NextResponse.json({ error: 'Série non trouvée' }, { status: 404 });
    }

    // If no episodes loaded yet and it's an Xtream playlist, fetch them
    if (
      series.episodes.length === 0 &&
      series.xtreamId &&
      series.playlist.type === 'xtream' &&
      series.playlist.xtreamServer &&
      series.playlist.xtreamUsername &&
      series.playlist.xtreamPassword
    ) {
      const xtream = createXtreamService(
        series.playlist.xtreamServer,
        series.playlist.xtreamUsername,
        series.playlist.xtreamPassword
      );

      try {
        const { episodes, seasons } = await xtream.getTransformedEpisodes(series.xtreamId);

        // Save episodes to database
        if (episodes.length > 0) {
          await prisma.episode.createMany({
            data: episodes.map((ep) => ({
              vodItemId: series.id,
              seasonNum: ep.seasonNum,
              episodeNum: ep.episodeNum,
              name: ep.name,
              plot: ep.plot,
              streamUrl: ep.streamUrl,
              duration: ep.duration,
              xtreamId: ep.xtreamId,
            })),
          });

          // Return with loaded episodes
          const updatedSeries = await prisma.vodItem.findUnique({
            where: { id },
            include: {
              episodes: {
                orderBy: [{ seasonNum: 'asc' }, { episodeNum: 'asc' }],
              },
            },
          });

          return NextResponse.json({
            ...updatedSeries,
            seasons,
          });
        }
      } catch (error) {
        console.error('Failed to fetch episodes from Xtream:', error);
      }
    }

    // Group episodes by season
    const seasons = Array.from(new Set(series.episodes.map((ep) => ep.seasonNum))).sort(
      (a, b) => a - b
    );

    return NextResponse.json({
      id: series.id,
      name: series.name,
      posterUrl: series.posterUrl,
      backdropUrl: series.backdropUrl,
      plot: series.plot,
      genre: series.genre,
      year: series.year,
      rating: series.rating,
      episodes: series.episodes,
      seasons,
    });
  } catch (error) {
    console.error('Get series detail error:', error);
    return NextResponse.json({ error: 'Erreur serveur' }, { status: 500 });
  }
}
