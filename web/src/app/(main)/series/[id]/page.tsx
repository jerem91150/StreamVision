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
  Loader2,
  ChevronDown,
} from 'lucide-react';
import { Button } from '@/components/ui/button';
import { Card, CardContent } from '@/components/ui/card';

interface Episode {
  id: string;
  seasonNum: number;
  episodeNum: number;
  name: string;
  plot: string | null;
  streamUrl: string;
  duration: number | null;
}

interface SeriesDetail {
  id: string;
  name: string;
  posterUrl: string | null;
  backdropUrl: string | null;
  plot: string | null;
  genre: string | null;
  year: number | null;
  rating: number | null;
  episodes: Episode[];
  seasons: number[];
}

interface PageProps {
  params: Promise<{ id: string }>;
}

export default function SeriesDetailPage({ params }: PageProps) {
  const { id } = use(params);
  const router = useRouter();
  const [series, setSeries] = useState<SeriesDetail | null>(null);
  const [isLoading, setIsLoading] = useState(true);
  const [selectedSeason, setSelectedSeason] = useState<number>(1);
  const [showSeasonDropdown, setShowSeasonDropdown] = useState(false);

  useEffect(() => {
    const fetchSeries = async () => {
      try {
        const token = localStorage.getItem('accessToken');
        const response = await fetch(`/api/vod/series/${id}`, {
          headers: { Authorization: `Bearer ${token}` },
        });

        if (response.status === 401) {
          router.push('/login');
          return;
        }

        if (response.ok) {
          const data = await response.json();
          setSeries(data);
          if (data.seasons && data.seasons.length > 0) {
            setSelectedSeason(data.seasons[0]);
          }
        }
      } catch (err) {
        console.error('Failed to fetch series:', err);
      } finally {
        setIsLoading(false);
      }
    };

    fetchSeries();
  }, [id, router]);

  const filteredEpisodes = series?.episodes.filter(
    (ep) => ep.seasonNum === selectedSeason
  ) || [];

  const formatDuration = (seconds: number | null) => {
    if (!seconds) return '';
    const minutes = Math.floor(seconds / 60);
    return `${minutes} min`;
  };

  if (isLoading) {
    return (
      <div className="flex items-center justify-center min-h-[400px]">
        <Loader2 className="w-8 h-8 animate-spin text-orange-500" />
      </div>
    );
  }

  if (!series) {
    return (
      <div className="p-6 text-center">
        <p className="text-gray-400">Série non trouvée</p>
        <Link href="/series">
          <Button variant="outline" className="mt-4">
            Retour aux séries
          </Button>
        </Link>
      </div>
    );
  }

  return (
    <div className="min-h-screen">
      {/* Hero backdrop */}
      <div className="relative h-[40vh] md:h-[50vh]">
        {series.backdropUrl || series.posterUrl ? (
          <Image
            src={series.backdropUrl || series.posterUrl || ''}
            alt={series.name}
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
      <div className="relative -mt-32 z-10 px-6 pb-6">
        <div className="max-w-6xl mx-auto">
          <div className="flex flex-col md:flex-row gap-6">
            {/* Poster */}
            <div className="flex-shrink-0">
              <div className="w-48 aspect-[2/3] rounded-xl overflow-hidden bg-gray-800 shadow-2xl">
                {series.posterUrl ? (
                  <Image
                    src={series.posterUrl}
                    alt={series.name}
                    width={192}
                    height={288}
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
              <h1 className="text-3xl font-bold text-white mb-2">{series.name}</h1>

              <div className="flex flex-wrap items-center gap-4 text-sm text-gray-400 mb-4">
                {series.year && (
                  <span className="flex items-center gap-1">
                    <Calendar className="w-4 h-4" />
                    {series.year}
                  </span>
                )}
                {series.rating && (
                  <span className="flex items-center gap-1">
                    <Star className="w-4 h-4 text-yellow-400" />
                    {series.rating.toFixed(1)}
                  </span>
                )}
                {series.genre && <span>{series.genre}</span>}
                <span>{series.episodes.length} épisodes</span>
              </div>

              {series.plot && (
                <p className="text-gray-300 mb-6 max-w-2xl">{series.plot}</p>
              )}

              <div className="flex gap-3">
                {filteredEpisodes.length > 0 && (
                  <Link href={`/player/episode/${filteredEpisodes[0].id}`}>
                    <Button className="bg-orange-500 hover:bg-orange-600">
                      <Play className="w-4 h-4 mr-2" />
                      Regarder S{selectedSeason}E1
                    </Button>
                  </Link>
                )}
                <Button variant="outline" className="border-gray-700">
                  <Heart className="w-4 h-4 mr-2" />
                  Favoris
                </Button>
              </div>
            </div>
          </div>

          {/* Episodes */}
          <div className="mt-8">
            <div className="flex items-center justify-between mb-4">
              <h2 className="text-xl font-semibold text-white">Épisodes</h2>

              {/* Season selector */}
              {series.seasons.length > 1 && (
                <div className="relative">
                  <Button
                    variant="outline"
                    onClick={() => setShowSeasonDropdown(!showSeasonDropdown)}
                    className="border-gray-700"
                  >
                    Saison {selectedSeason}
                    <ChevronDown className="w-4 h-4 ml-2" />
                  </Button>

                  {showSeasonDropdown && (
                    <div className="absolute right-0 top-full mt-1 bg-gray-800 border border-gray-700 rounded-lg shadow-lg py-1 z-20 min-w-[120px]">
                      {series.seasons.map((season) => (
                        <button
                          key={season}
                          onClick={() => {
                            setSelectedSeason(season);
                            setShowSeasonDropdown(false);
                          }}
                          className={`w-full px-4 py-2 text-left text-sm hover:bg-gray-700 ${
                            selectedSeason === season
                              ? 'text-orange-400'
                              : 'text-gray-300'
                          }`}
                        >
                          Saison {season}
                        </button>
                      ))}
                    </div>
                  )}
                </div>
              )}
            </div>

            {filteredEpisodes.length > 0 ? (
              <div className="space-y-3">
                {filteredEpisodes.map((episode) => (
                  <Link
                    key={episode.id}
                    href={`/player/episode/${episode.id}`}
                  >
                    <Card className="bg-gray-800/50 border-gray-700 hover:bg-gray-800 transition-colors cursor-pointer">
                      <CardContent className="flex items-center gap-4 p-4">
                        <div className="w-10 h-10 rounded-full bg-orange-500/20 flex items-center justify-center flex-shrink-0">
                          <span className="text-orange-400 font-semibold">
                            {episode.episodeNum}
                          </span>
                        </div>
                        <div className="flex-1 min-w-0">
                          <h3 className="font-medium text-white truncate">
                            {episode.name}
                          </h3>
                          {episode.plot && (
                            <p className="text-sm text-gray-400 line-clamp-1">
                              {episode.plot}
                            </p>
                          )}
                        </div>
                        {episode.duration && (
                          <span className="text-sm text-gray-500 flex-shrink-0">
                            {formatDuration(episode.duration)}
                          </span>
                        )}
                        <Play className="w-5 h-5 text-gray-500 flex-shrink-0" />
                      </CardContent>
                    </Card>
                  </Link>
                ))}
              </div>
            ) : (
              <div className="text-center py-8 text-gray-400">
                <p>Aucun épisode disponible pour cette saison</p>
              </div>
            )}
          </div>
        </div>
      </div>
    </div>
  );
}
