'use client';

import { useEffect, useState } from 'react';
import { useParams, useRouter } from 'next/navigation';
import { ArrowLeft, Heart, Share2 } from 'lucide-react';
import { Button } from '@/components/ui/button';
import { VideoPlayer } from '@/components/player/VideoPlayer';

interface StreamData {
  id: string;
  name: string;
  streamUrl: string;
  logoUrl?: string;
  description?: string;
}

export default function PlayerPage() {
  const params = useParams();
  const router = useRouter();
  const [streamData, setStreamData] = useState<StreamData | null>(null);
  const [isLoading, setIsLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);

  const type = params.type as string; // 'live', 'movie', 'episode'
  const id = params.id as string;

  useEffect(() => {
    const fetchStreamData = async () => {
      try {
        const accessToken = localStorage.getItem('accessToken');
        let endpoint = '';

        switch (type) {
          case 'live':
            endpoint = `/api/channels/${id}/stream`;
            break;
          case 'movie':
            endpoint = `/api/vod/movies/${id}/stream`;
            break;
          case 'episode':
            endpoint = `/api/vod/episodes/${id}/stream`;
            break;
          default:
            throw new Error('Type de contenu invalide');
        }

        const response = await fetch(endpoint, {
          headers: { Authorization: `Bearer ${accessToken}` },
        });

        if (!response.ok) {
          throw new Error('Impossible de charger le flux');
        }

        const data = await response.json();
        setStreamData(data);
      } catch (err) {
        setError(err instanceof Error ? err.message : 'Erreur inconnue');
      } finally {
        setIsLoading(false);
      }
    };

    fetchStreamData();
  }, [type, id]);

  const handleProgress = async (progress: number, duration: number) => {
    try {
      const accessToken = localStorage.getItem('accessToken');
      await fetch('/api/history', {
        method: 'POST',
        headers: {
          'Content-Type': 'application/json',
          Authorization: `Bearer ${accessToken}`,
        },
        body: JSON.stringify({
          contentId: id,
          contentType: type,
          progress: Math.floor(progress),
          duration: Math.floor(duration),
        }),
      });
    } catch (error) {
      console.error('Failed to save progress:', error);
    }
  };

  if (isLoading) {
    return (
      <div className="fixed inset-0 bg-black flex items-center justify-center">
        <div className="w-12 h-12 border-4 border-primary border-t-transparent rounded-full animate-spin" />
      </div>
    );
  }

  if (error || !streamData) {
    return (
      <div className="fixed inset-0 bg-black flex items-center justify-center">
        <div className="text-center">
          <p className="text-destructive mb-4">{error || 'Contenu non trouve'}</p>
          <Button onClick={() => router.back()}>Retour</Button>
        </div>
      </div>
    );
  }

  return (
    <div className="fixed inset-0 bg-black z-50">
      {/* Top bar */}
      <div className="absolute top-0 left-0 right-0 z-10 p-4 bg-gradient-to-b from-black/80 to-transparent">
        <div className="flex items-center justify-between">
          <Button
            variant="ghost"
            size="icon"
            onClick={() => router.back()}
            className="text-white"
          >
            <ArrowLeft className="w-6 h-6" />
          </Button>

          <div className="flex items-center gap-2">
            <Button variant="ghost" size="icon" className="text-white">
              <Heart className="w-5 h-5" />
            </Button>
            <Button variant="ghost" size="icon" className="text-white">
              <Share2 className="w-5 h-5" />
            </Button>
          </div>
        </div>
      </div>

      {/* Video Player */}
      <VideoPlayer
        src={streamData.streamUrl}
        title={streamData.name}
        poster={streamData.logoUrl}
        autoPlay
        onProgress={handleProgress}
        className="w-full h-full"
      />
    </div>
  );
}
