import { NextResponse } from 'next/server';
import { invalidateSession } from '@/lib/auth';

export async function POST(request: Request) {
  try {
    const body = await request.json();
    const { refreshToken } = body;

    if (refreshToken) {
      await invalidateSession(refreshToken);
    }

    return NextResponse.json({ success: true });
  } catch (error) {
    console.error('Logout error:', error);
    // Return success anyway - logout should always succeed from user perspective
    return NextResponse.json({ success: true });
  }
}
