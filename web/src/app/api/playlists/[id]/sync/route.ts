import { NextResponse } from 'next/server';
import prisma from '@/lib/prisma';
import { getCurrentUser } from '@/lib/auth';
import { fetchAndParseM3U, groupSeriesByName } from '@/lib/parsers/m3u-parser';
import { createXtreamService } from '@/lib/services/xtream-service';
import { decryptPassword } from '@/lib/crypto';

interface RouteParams {
  params: Promise<{ id: string }>;
}

export async function POST(request: Request, { params }: RouteParams) {
  try {
    const user = await getCurrentUser(request);
    if (!user) {
      return NextResponse.json({ error: 'Non authentifié' }, { status: 401 });
    }

    const { id } = await params;

    // Get playlist and verify ownership
    const playlist = await prisma.playlist.findFirst({
      where: {
        id,
        userId: user.id,
      },
    });

    if (!playlist) {
      return NextResponse.json({ error: 'Playlist non trouvée' }, { status: 404 });
    }

    let channelCount = 0;
    let movieCount = 0;
    let seriesCount = 0;

    // Delete existing content for this playlist
    await prisma.$transaction([
      prisma.epgProgram.deleteMany({
        where: { channel: { playlistId: id } },
      }),
      prisma.episode.deleteMany({
        where: { vodItem: { playlistId: id } },
      }),
      prisma.watchHistory.deleteMany({
        where: {
          OR: [
            { channel: { playlistId: id } },
            { vodItem: { playlistId: id } },
          ],
        },
      }),
      prisma.favorite.deleteMany({
        where: {
          OR: [
            { channel: { playlistId: id } },
            { vodItem: { playlistId: id } },
          ],
        },
      }),
      prisma.channel.deleteMany({
        where: { playlistId: id },
      }),
      prisma.vodItem.deleteMany({
        where: { playlistId: id },
      }),
    ]);

    if (playlist.type === 'm3u' && playlist.url) {
      // Sync M3U playlist
      const result = await fetchAndParseM3U(playlist.url);

      // Insert channels
      if (result.channels.length > 0) {
        await prisma.channel.createMany({
          data: result.channels.map((ch) => ({
            playlistId: id,
            name: ch.name,
            logoUrl: ch.logoUrl,
            streamUrl: ch.streamUrl,
            groupTitle: ch.groupTitle,
            epgId: ch.epgId,
            number: ch.number,
            catchupDays: ch.catchupDays,
          })),
        });
        channelCount = result.channels.length;
      }

      // Insert movies
      if (result.movies.length > 0) {
        await prisma.vodItem.createMany({
          data: result.movies.map((movie) => ({
            playlistId: id,
            type: 'movie',
            name: movie.name,
            posterUrl: movie.logoUrl,
            streamUrl: movie.streamUrl,
            genre: movie.groupTitle,
          })),
        });
        movieCount = result.movies.length;
      }

      // Insert series (grouped by name)
      if (result.series.length > 0) {
        const groupedSeries = groupSeriesByName(result.series);

        for (const [seriesName, episodes] of Object.entries(groupedSeries)) {
          const firstEpisode = episodes[0];

          // Create series
          const series = await prisma.vodItem.create({
            data: {
              playlistId: id,
              type: 'series',
              name: seriesName,
              posterUrl: firstEpisode.logoUrl,
              genre: firstEpisode.groupTitle,
            },
          });

          // Create episodes
          await prisma.episode.createMany({
            data: episodes.map((ep) => ({
              vodItemId: series.id,
              seasonNum: ep.seriesInfo?.seasonNum || 1,
              episodeNum: ep.seriesInfo?.episodeNum || 1,
              name: ep.name,
              streamUrl: ep.streamUrl,
            })),
          });
        }
        seriesCount = Object.keys(groupedSeries).length;
      }
    } else if (playlist.type === 'xtream' && playlist.xtreamServer && playlist.xtreamUsername && playlist.xtreamPassword) {
      // Sync Xtream Codes playlist - decrypt password first
      const decryptedPassword = decryptPassword(playlist.xtreamPassword);
      if (!decryptedPassword) {
        return NextResponse.json({ error: 'Impossible de déchiffrer le mot de passe' }, { status: 500 });
      }

      const xtream = createXtreamService(
        playlist.xtreamServer,
        playlist.xtreamUsername,
        decryptedPassword
      );

      // Authenticate first
      await xtream.authenticate();

      // Get live channels
      const { channels } = await xtream.getTransformedChannels();
      if (channels.length > 0) {
        await prisma.channel.createMany({
          data: channels.map((ch) => ({
            playlistId: id,
            name: ch.name,
            logoUrl: ch.logoUrl,
            streamUrl: ch.streamUrl,
            groupTitle: ch.groupTitle,
            epgId: ch.epgId,
            number: ch.number,
            catchupDays: ch.catchupDays,
            xtreamId: ch.xtreamId,
          })),
        });
        channelCount = channels.length;
      }

      // Get movies
      const { movies } = await xtream.getTransformedMovies();
      if (movies.length > 0) {
        await prisma.vodItem.createMany({
          data: movies.map((movie) => ({
            playlistId: id,
            type: 'movie',
            name: movie.name,
            posterUrl: movie.posterUrl,
            rating: movie.rating,
            streamUrl: movie.streamUrl,
            xtreamId: movie.xtreamId,
            containerExt: movie.containerExtension,
          })),
        });
        movieCount = movies.length;
      }

      // Get series
      const { series } = await xtream.getTransformedSeries();
      if (series.length > 0) {
        await prisma.vodItem.createMany({
          data: series.map((s) => ({
            playlistId: id,
            type: 'series',
            name: s.name,
            posterUrl: s.posterUrl,
            backdropUrl: s.backdropUrl,
            plot: s.plot,
            genre: s.genre,
            year: s.year,
            rating: s.rating,
            xtreamId: s.xtreamId,
          })),
        });
        seriesCount = series.length;
      }
    } else {
      return NextResponse.json(
        { error: 'Configuration de playlist invalide' },
        { status: 400 }
      );
    }

    // Update playlist with counts and lastSync
    await prisma.playlist.update({
      where: { id },
      data: {
        lastSync: new Date(),
        channelCount,
        movieCount,
        seriesCount,
      },
    });

    return NextResponse.json({
      success: true,
      channelCount,
      movieCount,
      seriesCount,
      totalItems: channelCount + movieCount + seriesCount,
    });
  } catch (error) {
    console.error('Sync playlist error:', error);
    return NextResponse.json(
      { error: error instanceof Error ? error.message : 'Erreur de synchronisation' },
      { status: 500 }
    );
  }
}
