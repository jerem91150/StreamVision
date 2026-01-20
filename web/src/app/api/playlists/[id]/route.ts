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

    const playlist = await prisma.playlist.findFirst({
      where: {
        id,
        userId: user.id,
      },
    });

    if (!playlist) {
      return NextResponse.json({ error: 'Playlist non trouvée' }, { status: 404 });
    }

    return NextResponse.json(playlist);
  } catch (error) {
    console.error('Get playlist error:', error);
    return NextResponse.json({ error: 'Erreur serveur' }, { status: 500 });
  }
}

export async function PUT(request: Request, { params }: RouteParams) {
  try {
    const user = await getCurrentUser(request);
    if (!user) {
      return NextResponse.json({ error: 'Non authentifié' }, { status: 401 });
    }

    const { id } = await params;

    // Verify ownership
    const existingPlaylist = await prisma.playlist.findFirst({
      where: {
        id,
        userId: user.id,
      },
    });

    if (!existingPlaylist) {
      return NextResponse.json({ error: 'Playlist non trouvée' }, { status: 404 });
    }

    const body = await request.json();
    const { name, url, xtreamServer, xtreamUsername, xtreamPassword, epgUrl, isActive } = body;

    const playlist = await prisma.playlist.update({
      where: { id },
      data: {
        name: name !== undefined ? name : existingPlaylist.name,
        url: url !== undefined ? url : existingPlaylist.url,
        xtreamServer: xtreamServer !== undefined ? xtreamServer : existingPlaylist.xtreamServer,
        xtreamUsername: xtreamUsername !== undefined ? xtreamUsername : existingPlaylist.xtreamUsername,
        xtreamPassword: xtreamPassword !== undefined ? xtreamPassword : existingPlaylist.xtreamPassword,
        epgUrl: epgUrl !== undefined ? epgUrl : existingPlaylist.epgUrl,
        isActive: isActive !== undefined ? isActive : existingPlaylist.isActive,
      },
    });

    return NextResponse.json(playlist);
  } catch (error) {
    console.error('Update playlist error:', error);
    return NextResponse.json({ error: 'Erreur serveur' }, { status: 500 });
  }
}

export async function DELETE(request: Request, { params }: RouteParams) {
  try {
    const user = await getCurrentUser(request);
    if (!user) {
      return NextResponse.json({ error: 'Non authentifié' }, { status: 401 });
    }

    const { id } = await params;

    // Verify ownership
    const playlist = await prisma.playlist.findFirst({
      where: {
        id,
        userId: user.id,
      },
    });

    if (!playlist) {
      return NextResponse.json({ error: 'Playlist non trouvée' }, { status: 404 });
    }

    // Delete playlist (cascade will delete related channels, VOD items, etc.)
    await prisma.playlist.delete({
      where: { id },
    });

    return NextResponse.json({ success: true });
  } catch (error) {
    console.error('Delete playlist error:', error);
    return NextResponse.json({ error: 'Erreur serveur' }, { status: 500 });
  }
}
