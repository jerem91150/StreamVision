'use client';

import { useState, useEffect, use } from 'react';
import { useRouter } from 'next/navigation';
import Image from 'next/image';
import Link from 'next/link';
import {
  ArrowLeft,
  Play,
  Heart,
  Star,
  Calendar,
  Clock,
  Loader2,
} from 'lucide-react';
import { Button } from '@/components/ui/button';

interface MovieDetail {
  id: string;
  name: string;
  posterUrl: string | null;
  backdropUrl: string | null;
  plot: string | null;
  genre: string | null;
  year: number | null;
  rating: number | null;
  duration: number | null;
  streamUrl: string | null;
}

interface PageProps {
  params: Promise<{ id: string }>;
}

export default function MovieDetailPage({ params }: PageProps) {
  const { id } = use(params);
  const router = useRouter();
  const [movie, setMovie] = useState<MovieDetail | null>(null);
  const [isLoading, setIsLoading] = useState(true);

  useEffect(() => {
    const fetchMovie = async () => {
      try {
        const token = localStorage.getItem('accessToken');
        const response = await fetch(`/api/vod/movies/${id}`, {
          headers: { Authorization: `Bearer ${token}` },
        });

        if (response.status === 401) {
          router.push('/login');
          return;
        }

        if (response.ok) {
          const data = await response.json();
          setMovie(data);
        }
      } catch (err) {
        console.error('Failed to fetch movie:', err);
      } finally {
        setIsLoading(false);
      }
    };

    fetchMovie();
  }, [id, router]);

  const formatDuration = (minutes: number | null) => {
    if (!minutes) return '';
    const hours = Math.floor(minutes / 60);
    const mins = minutes % 60;
    if (hours > 0) {
      return `${hours}h ${mins}min`;
    }
    return `${mins} min`;
  };

  if (isLoading) {
    return (
      <div className="flex items-center justify-center min-h-[400px]">
        <Loader2 className="w-8 h-8 animate-spin text-orange-500" />
      </div>
    );
  }

  if (!movie) {
    return (
      <div className="p-6 text-center">
        <p className="text-gray-400">Film non trouvé</p>
        <Link href="/films">
          <Button variant="outline" className="mt-4">
            Retour aux films
          </Button>
        </Link>
      </div>
    );
  }

  return (
    <div className="min-h-screen">
      {/* Hero backdrop */}
      <div className="relative h-[50vh] md:h-[60vh]">
        {movie.backdropUrl || movie.posterUrl ? (
          <Image
            src={movie.backdropUrl || movie.posterUrl || ''}
            alt={movie.name}
            fill
            className="object-cover"
            priority
          />
        ) : (
          <div className="absolute inset-0 bg-gradient-to-b from-gray-800 to-gray-900" />
        )}
        <div className="absolute inset-0 bg-gradient-to-t from-background via-background/60 to-transparent" />

        {/* Back button */}
        <div className="absolute top-4 left-4 z-10">
          <Button
            variant="ghost"
            size="sm"
            onClick={() => router.back()}
            className="bg-black/50 hover:bg-black/70"
          >
            <ArrowLeft className="w-4 h-4 mr-2" />
            Retour
          </Button>
        </div>
      </div>

      {/* Content */}
      <div className="relative -mt-40 z-10 px-6 pb-6">
        <div className="max-w-6xl mx-auto">
          <div className="flex flex-col md:flex-row gap-8">
            {/* Poster */}
            <div className="flex-shrink-0">
              <div className="w-56 aspect-[2/3] rounded-xl overflow-hidden bg-gray-800 shadow-2xl">
                {movie.posterUrl ? (
                  <Image
                    src={movie.posterUrl}
                    alt={movie.name}
                    width={224}
                    height={336}
                    className="w-full h-full object-cover"
                  />
                ) : (
                  <div className="w-full h-full flex items-center justify-center text-gray-500">
                    Pas d&apos;image
                  </div>
                )}
              </div>
            </div>

            {/* Info */}
            <div className="flex-1">
              <h1 className="text-4xl font-bold text-white mb-4">{movie.name}</h1>

              <div className="flex flex-wrap items-center gap-4 text-sm text-gray-400 mb-6">
                {movie.year && (
                  <span className="flex items-center gap-1">
                    <Calendar className="w-4 h-4" />
                    {movie.year}
                  </span>
                )}
                {movie.rating && (
                  <span className="flex items-center gap-1">
                    <Star className="w-4 h-4 text-yellow-400" />
                    {movie.rating.toFixed(1)}/10
                  </span>
                )}
                {movie.duration && (
                  <span className="flex items-center gap-1">
                    <Clock className="w-4 h-4" />
                    {formatDuration(movie.duration)}
                  </span>
                )}
                {movie.genre && (
                  <span className="px-2 py-1 bg-gray-800 rounded">{movie.genre}</span>
                )}
              </div>

              {movie.plot && (
                <div className="mb-8">
                  <h2 className="text-lg font-semibold text-white mb-2">Synopsis</h2>
                  <p className="text-gray-300 leading-relaxed max-w-3xl">
                    {movie.plot}
                  </p>
                </div>
              )}

              <div className="flex gap-4">
                <Link href={`/player/movie/${movie.id}`}>
                  <Button size="lg" className="bg-orange-500 hover:bg-orange-600">
                    <Play className="w-5 h-5 mr-2" />
                    Regarder
                  </Button>
                </Link>
                <Button size="lg" variant="outline" className="border-gray-700">
                  <Heart className="w-5 h-5 mr-2" />
                  Ajouter aux favoris
                </Button>
              </div>
            </div>
          </div>
        </div>
      </div>
    </div>
  );
}
