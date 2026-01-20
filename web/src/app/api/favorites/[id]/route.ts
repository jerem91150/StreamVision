import { NextResponse } from 'next/server';
import prisma from '@/lib/prisma';
import { getCurrentUser } from '@/lib/auth';

interface RouteParams {
  params: Promise<{ id: string }>;
}

export async function DELETE(request: Request, { params }: RouteParams) {
  try {
    const user = await getCurrentUser(request);
    if (!user) {
      return NextResponse.json({ error: 'Non authentifié' }, { status: 401 });
    }

    const { id } = await params;

    // Verify ownership
    const favorite = await prisma.favorite.findFirst({
      where: {
        id,
        userId: user.id,
      },
    });

    if (!favorite) {
      return NextResponse.json({ error: 'Favori non trouvé' }, { status: 404 });
    }

    await prisma.favorite.delete({
      where: { id },
    });

    return NextResponse.json({ success: true });
  } catch (error) {
    console.error('Delete favorite error:', error);
    return NextResponse.json({ error: 'Erreur serveur' }, { status: 500 });
  }
}
