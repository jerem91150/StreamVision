'use client';

import { useEffect, useState } from 'react';
import Link from 'next/link';
import { useRouter } from 'next/navigation';
import {
  Play,
  Tv,
  Film,
  Clapperboard,
  Plus,
  ArrowRight,
  Heart,
  ListVideo,
  Loader2,
} from 'lucide-react';
import { Button } from '@/components/ui/button';
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from '@/components/ui/card';
import { ContentCard } from '@/components/content/ContentCard';
import { ChannelCard } from '@/components/content/ChannelCard';

interface DashboardStats {
  playlists: number;
  channels: number;
  movies: number;
  series: number;
  favorites: number;
}

interface ContinueWatchingItem {
  id: string;
  type: 'live' | 'movie' | 'series' | 'episode';
  name: string;
  imageUrl: string | null;
  progress: number;
  seriesId?: string;
}

interface ChannelItem {
  id: string;
  name: string;
  logoUrl: string | null;
  groupTitle: string | null;
  number: number | null;
}

interface VodItem {
  id: string;
  name: string;
  posterUrl: string | null;
  year: number | null;
  rating: number | null;
  genre: string | null;
}

interface DashboardData {
  stats: DashboardStats;
  continueWatching: ContinueWatchingItem[];
  channels: ChannelItem[];
  movies: VodItem[];
  series: VodItem[];
  hasContent: boolean;
}

export default function DashboardPage() {
  const router = useRouter();
  const [data, setData] = useState<DashboardData | null>(null);
  const [isLoading, setIsLoading] = useState(true);

  useEffect(() => {
    const fetchDashboard = async () => {
      try {
        const accessToken = localStorage.getItem('accessToken');
        const response = await fetch('/api/dashboard', {
          headers: { Authorization: `Bearer ${accessToken}` },
        });

        if (response.status === 401) {
          router.push('/login');
          return;
        }

        if (response.ok) {
          const dashboardData = await response.json();
          setData(dashboardData);
        }
      } catch (error) {
        console.error('Failed to fetch dashboard:', error);
      } finally {
        setIsLoading(false);
      }
    };

    fetchDashboard();
  }, [router]);

  if (isLoading) {
    return (
      <div className="flex items-center justify-center min-h-[400px]">
        <Loader2 className="w-8 h-8 animate-spin text-orange-500" />
      </div>
    );
  }

  const hasPlaylist = data && data.stats.playlists > 0;
  const hasContent = data?.hasContent || false;

  return (
    <div className="space-y-8">
      {/* Welcome / Setup Card */}
      {!hasPlaylist && (
        <Card className="bg-gradient-to-r from-orange-500/10 via-gray-900 to-gray-900 border-orange-500/20">
          <CardHeader>
            <CardTitle className="text-white">Bienvenue sur Visiora !</CardTitle>
            <CardDescription>
              Ajoutez votre première playlist pour commencer à regarder du contenu.
            </CardDescription>
          </CardHeader>
          <CardContent>
            <Link href="/settings/playlists">
              <Button className="gap-2 bg-orange-500 hover:bg-orange-600">
                <Plus className="w-4 h-4" />
                Ajouter une playlist
              </Button>
            </Link>
          </CardContent>
        </Card>
      )}

      {/* Stats Cards */}
      {data && hasContent && (
        <div className="grid grid-cols-2 md:grid-cols-5 gap-4">
          <Card className="bg-gray-800/50 border-gray-700">
            <CardContent className="p-4 text-center">
              <ListVideo className="w-6 h-6 text-orange-400 mx-auto mb-2" />
              <p className="text-2xl font-bold text-white">{data.stats.playlists}</p>
              <p className="text-xs text-gray-400">Playlists</p>
            </CardContent>
          </Card>
          <Card className="bg-gray-800/50 border-gray-700">
            <CardContent className="p-4 text-center">
              <Tv className="w-6 h-6 text-blue-400 mx-auto mb-2" />
              <p className="text-2xl font-bold text-white">{data.stats.channels}</p>
              <p className="text-xs text-gray-400">Chaînes</p>
            </CardContent>
          </Card>
          <Card className="bg-gray-800/50 border-gray-700">
            <CardContent className="p-4 text-center">
              <Film className="w-6 h-6 text-orange-400 mx-auto mb-2" />
              <p className="text-2xl font-bold text-white">{data.stats.movies}</p>
              <p className="text-xs text-gray-400">Films</p>
            </CardContent>
          </Card>
          <Card className="bg-gray-800/50 border-gray-700">
            <CardContent className="p-4 text-center">
              <Clapperboard className="w-6 h-6 text-green-400 mx-auto mb-2" />
              <p className="text-2xl font-bold text-white">{data.stats.series}</p>
              <p className="text-xs text-gray-400">Séries</p>
            </CardContent>
          </Card>
          <Card className="bg-gray-800/50 border-gray-700">
            <CardContent className="p-4 text-center">
              <Heart className="w-6 h-6 text-red-400 mx-auto mb-2" />
              <p className="text-2xl font-bold text-white">{data.stats.favorites}</p>
              <p className="text-xs text-gray-400">Favoris</p>
            </CardContent>
          </Card>
        </div>
      )}

      {/* Quick Access */}
      <div className="grid grid-cols-2 md:grid-cols-4 gap-4">
        <Link href="/live">
          <Card className="hover:border-orange-500/50 transition-colors cursor-pointer bg-gray-900 border-gray-800">
            <CardContent className="flex items-center gap-3 p-4">
              <div className="w-10 h-10 bg-blue-500/10 rounded-lg flex items-center justify-center">
                <Tv className="w-5 h-5 text-blue-400" />
              </div>
              <div>
                <p className="font-medium text-white">TV en direct</p>
                <p className="text-xs text-gray-400">Regarder</p>
              </div>
            </CardContent>
          </Card>
        </Link>
        <Link href="/films">
          <Card className="hover:border-orange-500/50 transition-colors cursor-pointer bg-gray-900 border-gray-800">
            <CardContent className="flex items-center gap-3 p-4">
              <div className="w-10 h-10 bg-orange-500/10 rounded-lg flex items-center justify-center">
                <Film className="w-5 h-5 text-orange-400" />
              </div>
              <div>
                <p className="font-medium text-white">Films</p>
                <p className="text-xs text-gray-400">Explorer</p>
              </div>
            </CardContent>
          </Card>
        </Link>
        <Link href="/series">
          <Card className="hover:border-orange-500/50 transition-colors cursor-pointer bg-gray-900 border-gray-800">
            <CardContent className="flex items-center gap-3 p-4">
              <div className="w-10 h-10 bg-green-500/10 rounded-lg flex items-center justify-center">
                <Clapperboard className="w-5 h-5 text-green-400" />
              </div>
              <div>
                <p className="font-medium text-white">Séries</p>
                <p className="text-xs text-gray-400">Découvrir</p>
              </div>
            </CardContent>
          </Card>
        </Link>
        <Link href="/epg">
          <Card className="hover:border-orange-500/50 transition-colors cursor-pointer bg-gray-900 border-gray-800">
            <CardContent className="flex items-center gap-3 p-4">
              <div className="w-10 h-10 bg-purple-500/10 rounded-lg flex items-center justify-center">
                <Play className="w-5 h-5 text-purple-400" />
              </div>
              <div>
                <p className="font-medium text-white">Guide TV</p>
                <p className="text-xs text-gray-400">Programme</p>
              </div>
            </CardContent>
          </Card>
        </Link>
      </div>

      {/* Continue Watching */}
      {data && data.continueWatching.length > 0 && (
        <section>
          <div className="flex items-center justify-between mb-4">
            <h2 className="text-lg font-semibold text-white">Reprendre la lecture</h2>
          </div>
          <div className="grid grid-cols-2 sm:grid-cols-3 md:grid-cols-4 lg:grid-cols-5 xl:grid-cols-6 gap-4">
            {data.continueWatching.map((item) => (
              <ContentCard
                key={`${item.type}-${item.id}`}
                id={item.seriesId || item.id}
                title={item.name}
                imageUrl={item.imageUrl || undefined}
                type={item.type === 'episode' ? 'series' : item.type === 'live' ? 'movie' : item.type}
                progress={item.progress}
              />
            ))}
          </div>
        </section>
      )}

      {/* Channels */}
      {data && data.channels.length > 0 && (
        <section>
          <div className="flex items-center justify-between mb-4">
            <h2 className="text-lg font-semibold text-white">Chaînes TV</h2>
            <Link href="/live" className="text-sm text-orange-400 hover:text-orange-300">
              Voir tout →
            </Link>
          </div>
          <div className="grid grid-cols-2 sm:grid-cols-3 md:grid-cols-4 lg:grid-cols-5 xl:grid-cols-6 gap-4">
            {data.channels.slice(0, 6).map((channel) => (
              <ChannelCard
                key={channel.id}
                id={channel.id}
                name={channel.name}
                logoUrl={channel.logoUrl || undefined}
                number={channel.number || undefined}
              />
            ))}
          </div>
        </section>
      )}

      {/* Movies */}
      {data && data.movies.length > 0 && (
        <section>
          <div className="flex items-center justify-between mb-4">
            <h2 className="text-lg font-semibold text-white">Films récents</h2>
            <Link href="/films" className="text-sm text-orange-400 hover:text-orange-300">
              Voir tout →
            </Link>
          </div>
          <div className="grid grid-cols-2 sm:grid-cols-3 md:grid-cols-4 lg:grid-cols-5 xl:grid-cols-6 gap-4">
            {data.movies.slice(0, 6).map((movie) => (
              <ContentCard
                key={movie.id}
                id={movie.id}
                title={movie.name}
                imageUrl={movie.posterUrl || undefined}
                type="movie"
                year={movie.year || undefined}
                rating={movie.rating || undefined}
              />
            ))}
          </div>
        </section>
      )}

      {/* Series */}
      {data && data.series.length > 0 && (
        <section>
          <div className="flex items-center justify-between mb-4">
            <h2 className="text-lg font-semibold text-white">Séries récentes</h2>
            <Link href="/series" className="text-sm text-orange-400 hover:text-orange-300">
              Voir tout →
            </Link>
          </div>
          <div className="grid grid-cols-2 sm:grid-cols-3 md:grid-cols-4 lg:grid-cols-5 xl:grid-cols-6 gap-4">
            {data.series.slice(0, 6).map((serie) => (
              <ContentCard
                key={serie.id}
                id={serie.id}
                title={serie.name}
                imageUrl={serie.posterUrl || undefined}
                type="series"
                year={serie.year || undefined}
                rating={serie.rating || undefined}
              />
            ))}
          </div>
        </section>
      )}

      {/* Empty state if no content */}
      {hasPlaylist && !hasContent && (
        <Card className="bg-gray-900 border-gray-800">
          <CardContent className="py-12 text-center">
            <div className="w-16 h-16 bg-gray-800 rounded-full flex items-center justify-center mx-auto mb-4">
              <Tv className="w-8 h-8 text-gray-500" />
            </div>
            <h3 className="text-lg font-medium text-white mb-2">
              Aucun contenu synchronisé
            </h3>
            <p className="text-gray-400 mb-6 max-w-md mx-auto">
              Vos playlists sont configurées mais n&apos;ont pas encore été synchronisées.
              Synchronisez-les pour voir votre contenu.
            </p>
            <Link href="/settings/playlists">
              <Button className="gap-2 bg-orange-500 hover:bg-orange-600">
                Synchroniser mes playlists
                <ArrowRight className="w-4 h-4" />
              </Button>
            </Link>
          </CardContent>
        </Card>
      )}

      {/* Empty state if no playlist */}
      {!hasPlaylist && (
        <Card className="bg-gray-900 border-gray-800">
          <CardContent className="py-12 text-center">
            <div className="w-16 h-16 bg-gray-800 rounded-full flex items-center justify-center mx-auto mb-4">
              <Tv className="w-8 h-8 text-gray-500" />
            </div>
            <h3 className="text-lg font-medium text-white mb-2">
              Aucune playlist configurée
            </h3>
            <p className="text-gray-400 mb-6 max-w-md mx-auto">
              Pour profiter de Visiora, ajoutez votre playlist M3U ou
              connectez-vous à votre serveur Xtream Codes.
            </p>
            <Link href="/settings/playlists">
              <Button className="gap-2 bg-orange-500 hover:bg-orange-600">
                <Plus className="w-4 h-4" />
                Configurer ma playlist
                <ArrowRight className="w-4 h-4" />
              </Button>
            </Link>
          </CardContent>
        </Card>
      )}
    </div>
  );
}
