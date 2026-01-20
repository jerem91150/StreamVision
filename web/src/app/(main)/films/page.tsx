'use client';

import { useState, useEffect } from 'react';
import { Search, Filter, Film } from 'lucide-react';
import { Input } from '@/components/ui/input';
import { Button } from '@/components/ui/button';
import { ContentCard } from '@/components/content/ContentCard';

interface Movie {
  id: string;
  name: string;
  posterUrl?: string;
  year?: number;
  rating?: number;
  genre?: string;
}

const genres = [
  'Tous',
  'Action',
  'Comedie',
  'Drame',
  'Horreur',
  'Science-Fiction',
  'Thriller',
  'Animation',
  'Documentaire',
];

export default function FilmsPage() {
  const [movies, setMovies] = useState<Movie[]>([]);
  const [filteredMovies, setFilteredMovies] = useState<Movie[]>([]);
  const [searchQuery, setSearchQuery] = useState('');
  const [selectedGenre, setSelectedGenre] = useState('Tous');
  const [isLoading, setIsLoading] = useState(true);

  useEffect(() => {
    const fetchMovies = async () => {
      try {
        const accessToken = localStorage.getItem('accessToken');
        const params = new URLSearchParams();
        if (searchQuery) params.set('search', searchQuery);
        if (selectedGenre !== 'Tous') params.set('genre', selectedGenre);

        const response = await fetch(`/api/vod/movies?${params.toString()}`, {
          headers: { Authorization: `Bearer ${accessToken}` },
        });

        if (response.ok) {
          const data = await response.json();
          setMovies(data.movies || []);
          setFilteredMovies(data.movies || []);
        }
      } catch (error) {
        console.error('Failed to fetch movies:', error);
      } finally {
        setIsLoading(false);
      }
    };

    fetchMovies();
  }, [searchQuery, selectedGenre]);

  // Filtering is now handled server-side via API params
  useEffect(() => {
    setFilteredMovies(movies);
  }, [movies]);

  return (
    <div className="space-y-6">
      {/* Header */}
      <div className="flex flex-col sm:flex-row sm:items-center justify-between gap-4">
        <div>
          <h1 className="text-2xl font-bold text-white">Films</h1>
          <p className="text-muted-foreground">
            {filteredMovies.length} films disponibles
          </p>
        </div>

        <div className="relative">
          <Search className="absolute left-3 top-1/2 -translate-y-1/2 w-4 h-4 text-muted-foreground" />
          <Input
            type="search"
            placeholder="Rechercher un film..."
            value={searchQuery}
            onChange={(e) => setSearchQuery(e.target.value)}
            className="pl-9 w-64"
          />
        </div>
      </div>

      {/* Genres */}
      <div className="flex gap-2 overflow-x-auto pb-2">
        {genres.map((genre) => (
          <Button
            key={genre}
            variant={selectedGenre === genre ? 'default' : 'outline'}
            size="sm"
            onClick={() => setSelectedGenre(genre)}
            className="shrink-0"
          >
            {genre}
          </Button>
        ))}
      </div>

      {/* Content */}
      {isLoading ? (
        <div className="grid grid-cols-2 sm:grid-cols-3 md:grid-cols-4 lg:grid-cols-5 xl:grid-cols-6 gap-4">
          {Array.from({ length: 18 }).map((_, i) => (
            <div key={i} className="aspect-[2/3] bg-card animate-pulse rounded-xl" />
          ))}
        </div>
      ) : filteredMovies.length > 0 ? (
        <div className="grid grid-cols-2 sm:grid-cols-3 md:grid-cols-4 lg:grid-cols-5 xl:grid-cols-6 gap-4">
          {filteredMovies.map((movie) => (
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
      ) : (
        <div className="py-12 text-center">
          <div className="w-16 h-16 bg-card-hover rounded-full flex items-center justify-center mx-auto mb-4">
            <Film className="w-8 h-8 text-muted-foreground" />
          </div>
          <h3 className="text-lg font-medium text-white mb-2">
            Aucun film trouve
          </h3>
          <p className="text-muted-foreground">
            {movies.length === 0
              ? 'Ajoutez une playlist avec des films VOD.'
              : 'Essayez de modifier vos filtres.'}
          </p>
        </div>
      )}
    </div>
  );
}
