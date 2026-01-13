import { NextResponse } from 'next/server';
import prisma from '@/lib/prisma';
import { getCurrentUser } from '@/lib/auth';

export async function GET(
  request: Request,
  { params }: { params: { id: string } }
) {
  try {
    const user = await getCurrentUser(request);
    if (!user) {
      return NextResponse.json({ error: 'Non authentifie' }, { status: 401 });
    }

    const channel = await prisma.channel.findFirst({
      where: {
        id: params.id,
        playlist: {
          userId: user.id,
        },
      },
    });

    if (!channel) {
      return NextResponse.json({ error: 'Chaine non trouvee' }, { status: 404 });
    }

    return NextResponse.json({
      id: channel.id,
      name: channel.name,
      streamUrl: channel.streamUrl,
      logoUrl: channel.logoUrl,
    });
  } catch (error) {
    console.error('Get channel stream error:', error);
    return NextResponse.json({ error: 'Erreur serveur' }, { status: 500 });
  }
}
