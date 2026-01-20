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
    const channelId = searchParams.get('channelId');
    const date = searchParams.get('date'); // Format: YYYY-MM-DD
    const hours = parseInt(searchParams.get('hours') || '24', 10);

    // Calculate time range
    let startTime: Date;
    let endTime: Date;

    if (date) {
      startTime = new Date(date);
      startTime.setHours(0, 0, 0, 0);
      endTime = new Date(startTime);
      endTime.setHours(23, 59, 59, 999);
    } else {
      startTime = new Date();
      endTime = new Date(startTime.getTime() + hours * 60 * 60 * 1000);
    }

    const where: Record<string, unknown> = {
      startTime: {
        gte: startTime,
      },
      endTime: {
        lte: endTime,
      },
      channel: {
        playlist: {
          userId: user.id,
          isActive: true,
        },
      },
    };

    if (channelId) {
      where.channelId = channelId;
    }

    const programs = await prisma.epgProgram.findMany({
      where,
      include: {
        channel: {
          select: {
            id: true,
            name: true,
            logoUrl: true,
            number: true,
          },
        },
      },
      orderBy: [
        { channel: { number: 'asc' } },
        { startTime: 'asc' },
      ],
    });

    // Get unique channels for the grid
    const channelsMap = new Map();
    programs.forEach((p) => {
      if (!channelsMap.has(p.channelId)) {
        channelsMap.set(p.channelId, p.channel);
      }
    });

    const channels = Array.from(channelsMap.values());

    // Group programs by channel
    const programsByChannel: Record<string, typeof programs> = {};
    programs.forEach((p) => {
      if (!programsByChannel[p.channelId]) {
        programsByChannel[p.channelId] = [];
      }
      programsByChannel[p.channelId].push(p);
    });

    return NextResponse.json({
      channels,
      programsByChannel,
      timeRange: {
        start: startTime.toISOString(),
        end: endTime.toISOString(),
      },
    });
  } catch (error) {
    console.error('Get EPG error:', error);
    return NextResponse.json({ error: 'Erreur serveur' }, { status: 500 });
  }
}
