import { NextResponse } from 'next/server';
import prisma from '@/lib/prisma';
import { getCurrentUser } from '@/lib/auth';

export async function GET(request: Request) {
  try {
    const user = await getCurrentUser(request);
    if (!user) {
      return NextResponse.json({ error: 'Non authentifie' }, { status: 401 });
    }

    const { searchParams } = new URL(request.url);
    const playlistId = searchParams.get('playlistId');
    const group = searchParams.get('group');
    const search = searchParams.get('search');

    const where: Record<string, unknown> = {
      playlist: {
        userId: user.id,
        isActive: true,
      },
    };

    if (playlistId) {
      where.playlistId = playlistId;
    }

    if (group) {
      where.groupTitle = group;
    }

    if (search) {
      where.name = {
        contains: search,
      };
    }

    const channels = await prisma.channel.findMany({
      where,
      orderBy: [{ number: 'asc' }, { name: 'asc' }],
    });

    return NextResponse.json(channels);
  } catch (error) {
    console.error('Get channels error:', error);
    return NextResponse.json({ error: 'Erreur serveur' }, { status: 500 });
  }
}
