import { NextResponse } from 'next/server';
import prisma from '@/lib/prisma';
import { getCurrentUser } from '@/lib/auth';

export async function GET(request: Request) {
  try {
    const user = await getCurrentUser(request);
    if (!user) {
      return NextResponse.json({ error: 'Non authentifie' }, { status: 401 });
    }

    const playlists = await prisma.playlist.findMany({
      where: { userId: user.id },
      orderBy: { createdAt: 'desc' },
    });

    return NextResponse.json(playlists);
  } catch (error) {
    console.error('Get playlists error:', error);
    return NextResponse.json({ error: 'Erreur serveur' }, { status: 500 });
  }
}

export async function POST(request: Request) {
  try {
    const user = await getCurrentUser(request);
    if (!user) {
      return NextResponse.json({ error: 'Non authentifie' }, { status: 401 });
    }

    const body = await request.json();
    const { name, type, url, xtreamServer, xtreamUsername, xtreamPassword } = body;

    // Validation
    if (!name || !type) {
      return NextResponse.json({ error: 'Nom et type requis' }, { status: 400 });
    }

    if (type === 'm3u' && !url) {
      return NextResponse.json({ error: 'URL M3U requise' }, { status: 400 });
    }

    if (type === 'xtream' && (!xtreamServer || !xtreamUsername || !xtreamPassword)) {
      return NextResponse.json({ error: 'Informations Xtream requises' }, { status: 400 });
    }

    const playlist = await prisma.playlist.create({
      data: {
        userId: user.id,
        name,
        type,
        url: type === 'm3u' ? url : null,
        xtreamServer: type === 'xtream' ? xtreamServer : null,
        xtreamUsername: type === 'xtream' ? xtreamUsername : null,
        xtreamPassword: type === 'xtream' ? xtreamPassword : null,
      },
    });

    return NextResponse.json(playlist);
  } catch (error) {
    console.error('Create playlist error:', error);
    return NextResponse.json({ error: 'Erreur serveur' }, { status: 500 });
  }
}
