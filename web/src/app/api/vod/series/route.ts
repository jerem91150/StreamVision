import { NextResponse } from 'next/server';
import { getCurrentUser } from '@/lib/auth';

// This is a placeholder - in production, this would fetch from Xtream API
export async function GET(request: Request) {
  try {
    const user = await getCurrentUser(request);
    if (!user) {
      return NextResponse.json({ error: 'Non authentifie' }, { status: 401 });
    }

    // Return empty array for now - will be populated when playlists are synced
    return NextResponse.json([]);
  } catch (error) {
    console.error('Get series error:', error);
    return NextResponse.json({ error: 'Erreur serveur' }, { status: 500 });
  }
}
