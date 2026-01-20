import { NextResponse } from 'next/server';
import prisma from '@/lib/prisma';
import { getCurrentUser } from '@/lib/auth';

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

    const movie = await prisma.vodItem.findFirst({
      where: {
        id,
        type: 'movie',
        playlist: {
          userId: user.id,
        },
      },
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
    });

    if (!movie) {
      return NextResponse.json({ error: 'Film non trouvé' }, { status: 404 });
    }

    return NextResponse.json(movie);
  } catch (error) {
    console.error('Get movie detail error:', error);
    return NextResponse.json({ error: 'Erreur serveur' }, { status: 500 });
  }
}
