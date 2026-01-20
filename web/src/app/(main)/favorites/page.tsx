'use client';

import { useState, useEffect } from 'react';
import { useRouter } from 'next/navigation';
import { Heart, Tv, Film, Clapperboard, Loader2, Trash2 } from 'lucide-react';
import { Button } from '@/components/ui/button';
import { ContentCard } from '@/components/content/ContentCard';
import { ChannelCard } from '@/components/content/ChannelCard';

interface FavoriteItem {
  id: string;
  createdAt: string;
  type: 'channel' | 'movie' | 'series';
  item: {
    id: string;
    name: string;
    logoUrl?: string;
    posterUrl?: string;
    groupTitle?: string;
    number?: number;
    year?: number;
    rating?: number;
    genre?: string;
  };
}

type FilterType = 'all' | 'channel' | 'movie' | 'series';

const filters: { value: FilterType; label: string; icon: React.ElementType }[] = [
  { value: 'all', label: 'Tout', icon: Heart },
  { value: 'channel', label: 'TV', icon: Tv },
  { value: 'movie', label: 'Films', icon: Film },
  { value: 'series', label: 'Séries', icon: Clapperboard },
];

export default function FavoritesPage() {
  const router = useRouter();
  const [favorites, setFavorites] = useState<FavoriteItem[]>([]);
  const [isLoading, setIsLoading] = useState(true);
  const [filter, setFilter] = useState<FilterType>('all');
  const [deletingId, setDeletingId] = useState<string | null>(null);

  const fetchFavorites = async () => {
    try {
      const token = localStorage.getItem('accessToken');
      const params = filter !== 'all' ? `?type=${filter}` : '';
      const response = await fetch(`/api/favorites${params}`, {
        headers: { Authorization: `Bearer ${token}` },
      });

      if (response.status === 401) {
        router.push('/login');
        return;
      }

      if (response.ok) {
        const data = await response.json();
        setFavorites(data);
      }
    } catch (err) {
      console.error('Failed to fetch favorites:', err);
    } finally {
      setIsLoading(false);
    }
  };

  useEffect(() => {
    setIsLoading(true);
    fetchFavorites();
  }, [filter]);

  const handleRemove = async (favoriteId: string) => {
    setDeletingId(favoriteId);
    try {
      const token = localStorage.getItem('accessToken');
      const response = await fetch(`/api/favorites/${favoriteId}`, {
        method: 'DELETE',
        headers: { Authorization: `Bearer ${token}` },
      });

      if (response.ok) {
        setFavorites((prev) => prev.filter((f) => f.id !== favoriteId));
      }
    } catch (err) {
      console.error('Failed to remove favorite:', err);
    } finally {
      setDeletingId(null);
    }
  };

  const filteredFavorites =
    filter === 'all'
      ? favorites
      : favorites.filter((f) => f.type === filter);

  const channels = filteredFavorites.filter((f) => f.type === 'channel');
  const movies = filteredFavorites.filter((f) => f.type === 'movie');
  const series = filteredFavorites.filter((f) => f.type === 'series');

  return (
    <div className="p-6">
      <div className="max-w-7xl mx-auto">
        {/* Header */}
        <div className="flex items-center justify-between mb-6">
          <div>
            <h1 className="text-2xl font-bold text-white flex items-center gap-2">
              <Heart className="w-6 h-6 text-red-500" />
              Mes favoris
            </h1>
            <p className="text-gray-400">{favorites.length} éléments</p>
          </div>
        </div>

        {/* Filters */}
        <div className="flex gap-2 mb-6">
          {filters.map((f) => (
            <Button
              key={f.value}
              variant={filter === f.value ? 'default' : 'outline'}
              size="sm"
              onClick={() => setFilter(f.value)}
              className={filter === f.value ? 'bg-orange-500 hover:bg-orange-600' : ''}
            >
              <f.icon className="w-4 h-4 mr-2" />
              {f.label}
            </Button>
          ))}
        </div>

        {/* Loading */}
        {isLoading && (
          <div className="flex items-center justify-center py-12">
            <Loader2 className="w-8 h-8 animate-spin text-orange-500" />
          </div>
        )}

        {/* Content */}
        {!isLoading && filteredFavorites.length === 0 && (
          <div className="py-12 text-center">
            <div className="w-16 h-16 bg-gray-800 rounded-full flex items-center justify-center mx-auto mb-4">
              <Heart className="w-8 h-8 text-gray-400" />
            </div>
            <h3 className="text-lg font-medium text-white mb-2">
              Aucun favori
            </h3>
            <p className="text-gray-400">
              Ajoutez du contenu à vos favoris pour le retrouver facilement.
            </p>
          </div>
        )}

        {!isLoading && filteredFavorites.length > 0 && (
          <div className="space-y-8">
            {/* Channels */}
            {channels.length > 0 && (filter === 'all' || filter === 'channel') && (
              <section>
                <h2 className="text-lg font-semibold text-white mb-4 flex items-center gap-2">
                  <Tv className="w-5 h-5 text-blue-400" />
                  Chaînes TV ({channels.length})
                </h2>
                <div className="grid grid-cols-2 sm:grid-cols-3 md:grid-cols-4 lg:grid-cols-5 xl:grid-cols-6 gap-4">
                  {channels.map((fav) => (
                    <div key={fav.id} className="relative group">
                      <ChannelCard
                        id={fav.item.id}
                        name={fav.item.name}
                        logoUrl={fav.item.logoUrl}
                        number={fav.item.number}
                      />
                      <button
                        onClick={(e) => {
                          e.preventDefault();
                          handleRemove(fav.id);
                        }}
                        disabled={deletingId === fav.id}
                        className="absolute top-2 right-2 p-2 bg-red-500/80 rounded-full opacity-0 group-hover:opacity-100 transition-opacity hover:bg-red-500"
                      >
                        {deletingId === fav.id ? (
                          <Loader2 className="w-4 h-4 animate-spin text-white" />
                        ) : (
                          <Trash2 className="w-4 h-4 text-white" />
                        )}
                      </button>
                    </div>
                  ))}
                </div>
              </section>
            )}

            {/* Movies */}
            {movies.length > 0 && (filter === 'all' || filter === 'movie') && (
              <section>
                <h2 className="text-lg font-semibold text-white mb-4 flex items-center gap-2">
                  <Film className="w-5 h-5 text-orange-400" />
                  Films ({movies.length})
                </h2>
                <div className="grid grid-cols-2 sm:grid-cols-3 md:grid-cols-4 lg:grid-cols-5 xl:grid-cols-6 gap-4">
                  {movies.map((fav) => (
                    <div key={fav.id} className="relative group">
                      <ContentCard
                        id={fav.item.id}
                        title={fav.item.name}
                        imageUrl={fav.item.posterUrl}
                        type="movie"
                        year={fav.item.year}
                        rating={fav.item.rating}
                      />
                      <button
                        onClick={(e) => {
                          e.preventDefault();
                          handleRemove(fav.id);
                        }}
                        disabled={deletingId === fav.id}
                        className="absolute top-2 right-2 p-2 bg-red-500/80 rounded-full opacity-0 group-hover:opacity-100 transition-opacity hover:bg-red-500 z-10"
                      >
                        {deletingId === fav.id ? (
                          <Loader2 className="w-4 h-4 animate-spin text-white" />
                        ) : (
                          <Trash2 className="w-4 h-4 text-white" />
                        )}
                      </button>
                    </div>
                  ))}
                </div>
              </section>
            )}

            {/* Series */}
            {series.length > 0 && (filter === 'all' || filter === 'series') && (
              <section>
                <h2 className="text-lg font-semibold text-white mb-4 flex items-center gap-2">
                  <Clapperboard className="w-5 h-5 text-green-400" />
                  Séries ({series.length})
                </h2>
                <div className="grid grid-cols-2 sm:grid-cols-3 md:grid-cols-4 lg:grid-cols-5 xl:grid-cols-6 gap-4">
                  {series.map((fav) => (
                    <div key={fav.id} className="relative group">
                      <ContentCard
                        id={fav.item.id}
                        title={fav.item.name}
                        imageUrl={fav.item.posterUrl}
                        type="series"
                        year={fav.item.year}
                        rating={fav.item.rating}
                      />
                      <button
                        onClick={(e) => {
                          e.preventDefault();
                          handleRemove(fav.id);
                        }}
                        disabled={deletingId === fav.id}
                        className="absolute top-2 right-2 p-2 bg-red-500/80 rounded-full opacity-0 group-hover:opacity-100 transition-opacity hover:bg-red-500 z-10"
                      >
                        {deletingId === fav.id ? (
                          <Loader2 className="w-4 h-4 animate-spin text-white" />
                        ) : (
                          <Trash2 className="w-4 h-4 text-white" />
                        )}
                      </button>
                    </div>
                  ))}
                </div>
              </section>
            )}
          </div>
        )}
      </div>
    </div>
  );
}
