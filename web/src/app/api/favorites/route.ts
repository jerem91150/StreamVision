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
    const type = searchParams.get('type'); // all, channel, movie, series

    const where: Record<string, unknown> = {
      userId: user.id,
    };

    if (type === 'channel') {
      where.channelId = { not: null };
    } else if (type === 'movie' || type === 'series') {
      where.vodItemId = { not: null };
    }

    const favorites = await prisma.favorite.findMany({
      where,
      include: {
        channel: {
          select: {
            id: true,
            name: true,
            logoUrl: true,
            groupTitle: true,
            number: true,
          },
        },
        vodItem: {
          select: {
            id: true,
            type: true,
            name: true,
            posterUrl: true,
            year: true,
            rating: true,
            genre: true,
          },
        },
      },
      orderBy: { createdAt: 'desc' },
    });

    // Filter by VOD type if specified
    let filteredFavorites = favorites;
    if (type === 'movie') {
      filteredFavorites = favorites.filter((f) => f.vodItem?.type === 'movie');
    } else if (type === 'series') {
      filteredFavorites = favorites.filter((f) => f.vodItem?.type === 'series');
    }

    // Transform for frontend
    const transformed = filteredFavorites.map((fav) => ({
      id: fav.id,
      createdAt: fav.createdAt,
      type: fav.channelId ? 'channel' : fav.vodItem?.type || 'unknown',
      item: fav.channel || fav.vodItem,
    }));

    return NextResponse.json(transformed);
  } catch (error) {
    console.error('Get favorites error:', error);
    return NextResponse.json({ error: 'Erreur serveur' }, { status: 500 });
  }
}

export async function POST(request: Request) {
  try {
    const user = await getCurrentUser(request);
    if (!user) {
      return NextResponse.json({ error: 'Non authentifié' }, { status: 401 });
    }

    const body = await request.json();
    const { type, itemId } = body;

    if (!type || !itemId) {
      return NextResponse.json(
        { error: 'Type et itemId requis' },
        { status: 400 }
      );
    }

    // Check if already favorited
    const existing = await prisma.favorite.findFirst({
      where: {
        userId: user.id,
        OR: [
          { channelId: type === 'channel' ? itemId : undefined },
          { vodItemId: type !== 'channel' ? itemId : undefined },
        ],
      },
    });

    if (existing) {
      return NextResponse.json(
        { error: 'Déjà dans les favoris' },
        { status: 400 }
      );
    }

    // Verify the item exists and belongs to user
    if (type === 'channel') {
      const channel = await prisma.channel.findFirst({
        where: {
          id: itemId,
          playlist: { userId: user.id },
        },
      });
      if (!channel) {
        return NextResponse.json({ error: 'Chaîne non trouvée' }, { status: 404 });
      }
    } else {
      const vodItem = await prisma.vodItem.findFirst({
        where: {
          id: itemId,
          playlist: { userId: user.id },
        },
      });
      if (!vodItem) {
        return NextResponse.json({ error: 'Contenu non trouvé' }, { status: 404 });
      }
    }

    const favorite = await prisma.favorite.create({
      data: {
        userId: user.id,
        channelId: type === 'channel' ? itemId : null,
        vodItemId: type !== 'channel' ? itemId : null,
      },
    });

    return NextResponse.json(favorite);
  } catch (error) {
    console.error('Add favorite error:', error);
    return NextResponse.json({ error: 'Erreur serveur' }, { status: 500 });
  }
}
