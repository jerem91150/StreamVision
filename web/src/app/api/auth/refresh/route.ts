import { NextResponse } from 'next/server';
import { refreshSession } from '@/lib/auth';

export async function POST(request: Request) {
  try {
    const body = await request.json();
    const { refreshToken } = body;

    if (!refreshToken) {
      return NextResponse.json(
        { error: 'Refresh token requis' },
        { status: 400 }
      );
    }

    const tokens = await refreshSession(refreshToken);

    return NextResponse.json(tokens);
  } catch (error) {
    console.error('Refresh error:', error);
    return NextResponse.json(
      { error: 'Session expiree, veuillez vous reconnecter' },
      { status: 401 }
    );
  }
}
