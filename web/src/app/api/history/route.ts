import { NextResponse } from 'next/server';
import prisma from '@/lib/prisma';
import { getCurrentUser } from '@/lib/auth';

export async function GET(request: Request) {
  try {
    const user = await getCurrentUser(request);
    if (!user) {
      return NextResponse.json({ error: 'Non authentifie' }, { status: 401 });
    }

    const history = await prisma.watchHistory.findMany({
      where: { userId: user.id },
      include: {
        channel: true,
      },
      orderBy: { watchedAt: 'desc' },
      take: 50,
    });

    return NextResponse.json(history);
  } catch (error) {
    console.error('Get history error:', error);
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
    const { contentId, contentType, progress, duration } = body;

    // Check if entry already exists
    const existing = await prisma.watchHistory.findFirst({
      where: {
        userId: user.id,
        OR: [
          { channelId: contentType === 'live' ? contentId : undefined },
          { vodId: contentType !== 'live' ? contentId : undefined },
        ],
      },
      orderBy: { watchedAt: 'desc' },
    });

    if (existing) {
      // Update existing entry
      await prisma.watchHistory.update({
        where: { id: existing.id },
        data: {
          progress,
          duration,
          watchedAt: new Date(),
        },
      });
    } else {
      // Create new entry
      await prisma.watchHistory.create({
        data: {
          userId: user.id,
          channelId: contentType === 'live' ? contentId : null,
          vodId: contentType !== 'live' ? contentId : null,
          vodType: contentType !== 'live' ? contentType : null,
          progress,
          duration,
        },
      });
    }

    return NextResponse.json({ success: true });
  } catch (error) {
    console.error('Save history error:', error);
    return NextResponse.json({ error: 'Erreur serveur' }, { status: 500 });
  }
}
