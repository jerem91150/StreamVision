'use client';

import { useState, useEffect, useCallback } from 'react';
import { useSearchParams, useRouter } from 'next/navigation';
import { Search, Tv, Film, Clapperboard, Loader2 } from 'lucide-react';
import { Input } from '@/components/ui/input';
import { Button } from '@/components/ui/button';
import { ContentCard } from '@/components/content/ContentCard';
import { ChannelCard } from '@/components/content/ChannelCard';

interface Channel {
  id: string;
  name: string;
  logoUrl?: string;
  groupTitle?: string;
  number?: number;
}

interface Movie {
  id: string;
  name: string;
  posterUrl?: string;
  year?: number;
  rating?: number;
  genre?: string;
}

interface Series {
  id: string;
  name: string;
  posterUrl?: string;
  year?: number;
  rating?: number;
  genre?: string;
  episodeCount?: number;
}

interface SearchResults {
  channels: Channel[];
  movies: Movie[];
  series: Series[];
  totalResults: number;
}

type FilterType = 'all' | 'live' | 'movie' | 'series';

const filters: { value: FilterType; label: string; icon: React.ElementType }[] = [
  { value: 'all', label: 'Tout', icon: Search },
  { value: 'live', label: 'TV', icon: Tv },
  { value: 'movie', label: 'Films', icon: Film },
  { value: 'series', label: 'Séries', icon: Clapperboard },
];

export default function SearchPage() {
  const router = useRouter();
  const searchParams = useSearchParams();
  const initialQuery = searchParams.get('q') || '';

  const [query, setQuery] = useState(initialQuery);
  const [filter, setFilter] = useState<FilterType>('all');
  const [results, setResults] = useState<SearchResults | null>(null);
  const [isLoading, setIsLoading] = useState(false);
  const [hasSearched, setHasSearched] = useState(false);

  const performSearch = useCallback(async (searchQuery: string, searchFilter: FilterType) => {
    if (!searchQuery || searchQuery.length < 2) {
      setResults(null);
      setHasSearched(false);
      return;
    }

    setIsLoading(true);
    setHasSearched(true);

    try {
      const accessToken = localStorage.getItem('accessToken');
      const params = new URLSearchParams({
        q: searchQuery,
        type: searchFilter,
      });

      const response = await fetch(`/api/search?${params.toString()}`, {
        headers: { Authorization: `Bearer ${accessToken}` },
      });

      if (response.ok) {
        const data = await response.json();
        setResults(data);
      }
    } catch (error) {
      console.error('Search failed:', error);
    } finally {
      setIsLoading(false);
    }
  }, []);

  // Search on initial load if query exists
  useEffect(() => {
    if (initialQuery) {
      performSearch(initialQuery, filter);
    }
  }, []);

  // Debounced search
  useEffect(() => {
    const timeoutId = setTimeout(() => {
      if (query !== initialQuery || filter !== 'all') {
        performSearch(query, filter);
        // Update URL
        if (query) {
          router.replace(`/search?q=${encodeURIComponent(query)}`, { scroll: false });
        }
      }
    }, 300);

    return () => clearTimeout(timeoutId);
  }, [query, filter, initialQuery, performSearch, router]);

  const handleSubmit = (e: React.FormEvent) => {
    e.preventDefault();
    performSearch(query, filter);
  };

  return (
    <div className="p-6 max-w-7xl mx-auto">
      {/* Search header */}
      <div className="mb-8">
        <h1 className="text-2xl font-bold text-white mb-4">Recherche</h1>

        <form onSubmit={handleSubmit} className="relative mb-4">
          <Search className="absolute left-4 top-1/2 -translate-y-1/2 w-5 h-5 text-gray-400" />
          <Input
            type="search"
            placeholder="Rechercher des chaînes, films, séries..."
            value={query}
            onChange={(e) => setQuery(e.target.value)}
            className="pl-12 pr-4 py-3 text-lg bg-gray-800 border-gray-700 text-white"
            autoFocus
          />
        </form>

        {/* Filters */}
        <div className="flex gap-2">
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
      </div>

      {/* Loading */}
      {isLoading && (
        <div className="flex items-center justify-center py-12">
          <Loader2 className="w-8 h-8 animate-spin text-orange-500" />
        </div>
      )}

      {/* Results */}
      {!isLoading && results && (
        <div className="space-y-8">
          {/* Summary */}
          <p className="text-gray-400">
            {results.totalResults} résultat{results.totalResults !== 1 ? 's' : ''} pour &quot;{query}&quot;
          </p>

          {/* Channels */}
          {results.channels.length > 0 && (filter === 'all' || filter === 'live') && (
            <section>
              <h2 className="text-lg font-semibold text-white mb-4 flex items-center gap-2">
                <Tv className="w-5 h-5 text-blue-400" />
                Chaînes TV ({results.channels.length})
              </h2>
              <div className="grid grid-cols-2 sm:grid-cols-3 md:grid-cols-4 lg:grid-cols-5 xl:grid-cols-6 gap-4">
                {results.channels.map((channel) => (
                  <ChannelCard
                    key={channel.id}
                    id={channel.id}
                    name={channel.name}
                    logoUrl={channel.logoUrl}
                    number={channel.number}
                  />
                ))}
              </div>
            </section>
          )}

          {/* Movies */}
          {results.movies.length > 0 && (filter === 'all' || filter === 'movie') && (
            <section>
              <h2 className="text-lg font-semibold text-white mb-4 flex items-center gap-2">
                <Film className="w-5 h-5 text-orange-400" />
                Films ({results.movies.length})
              </h2>
              <div className="grid grid-cols-2 sm:grid-cols-3 md:grid-cols-4 lg:grid-cols-5 xl:grid-cols-6 gap-4">
                {results.movies.map((movie) => (
                  <ContentCard
                    key={movie.id}
                    id={movie.id}
                    title={movie.name}
                    imageUrl={movie.posterUrl}
                    type="movie"
                    year={movie.year}
                    rating={movie.rating}
                  />
                ))}
              </div>
            </section>
          )}

          {/* Series */}
          {results.series.length > 0 && (filter === 'all' || filter === 'series') && (
            <section>
              <h2 className="text-lg font-semibold text-white mb-4 flex items-center gap-2">
                <Clapperboard className="w-5 h-5 text-green-400" />
                Séries ({results.series.length})
              </h2>
              <div className="grid grid-cols-2 sm:grid-cols-3 md:grid-cols-4 lg:grid-cols-5 xl:grid-cols-6 gap-4">
                {results.series.map((s) => (
                  <ContentCard
                    key={s.id}
                    id={s.id}
                    title={s.name}
                    imageUrl={s.posterUrl}
                    type="series"
                    year={s.year}
                    rating={s.rating}
                  />
                ))}
              </div>
            </section>
          )}

          {/* No results */}
          {results.totalResults === 0 && (
            <div className="py-12 text-center">
              <div className="w-16 h-16 bg-gray-800 rounded-full flex items-center justify-center mx-auto mb-4">
                <Search className="w-8 h-8 text-gray-400" />
              </div>
              <h3 className="text-lg font-medium text-white mb-2">
                Aucun résultat
              </h3>
              <p className="text-gray-400">
                Essayez avec d&apos;autres termes de recherche.
              </p>
            </div>
          )}
        </div>
      )}

      {/* Initial state */}
      {!isLoading && !hasSearched && (
        <div className="py-12 text-center">
          <div className="w-16 h-16 bg-gray-800 rounded-full flex items-center justify-center mx-auto mb-4">
            <Search className="w-8 h-8 text-gray-400" />
          </div>
          <h3 className="text-lg font-medium text-white mb-2">
            Rechercher du contenu
          </h3>
          <p className="text-gray-400">
            Tapez au moins 2 caractères pour commencer la recherche.
          </p>
        </div>
      )}
    </div>
  );
}
