'use client';

import { useState, useEffect } from 'react';
import { Search, Clapperboard } from 'lucide-react';
import { Input } from '@/components/ui/input';
import { Button } from '@/components/ui/button';
import { ContentCard } from '@/components/content/ContentCard';

interface Series {
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
  'Crime',
  'Science-Fiction',
  'Fantastique',
  'Animation',
  'Documentaire',
];

export default function SeriesPage() {
  const [series, setSeries] = useState<Series[]>([]);
  const [filteredSeries, setFilteredSeries] = useState<Series[]>([]);
  const [searchQuery, setSearchQuery] = useState('');
  const [selectedGenre, setSelectedGenre] = useState('Tous');
  const [isLoading, setIsLoading] = useState(true);

  useEffect(() => {
    const fetchSeries = async () => {
      try {
        const accessToken = localStorage.getItem('accessToken');
        const params = new URLSearchParams();
        if (searchQuery) params.set('search', searchQuery);
        if (selectedGenre !== 'Tous') params.set('genre', selectedGenre);

        const response = await fetch(`/api/vod/series?${params.toString()}`, {
          headers: { Authorization: `Bearer ${accessToken}` },
        });

        if (response.ok) {
          const data = await response.json();
          setSeries(data.series || []);
          setFilteredSeries(data.series || []);
        }
      } catch (error) {
        console.error('Failed to fetch series:', error);
      } finally {
        setIsLoading(false);
      }
    };

    fetchSeries();
  }, [searchQuery, selectedGenre]);

  // Filtering is now handled server-side via API params
  useEffect(() => {
    setFilteredSeries(series);
  }, [series]);

  return (
    <div className="space-y-6">
      {/* Header */}
      <div className="flex flex-col sm:flex-row sm:items-center justify-between gap-4">
        <div>
          <h1 className="text-2xl font-bold text-white">Series</h1>
          <p className="text-muted-foreground">
            {filteredSeries.length} series disponibles
          </p>
        </div>

        <div className="relative">
          <Search className="absolute left-3 top-1/2 -translate-y-1/2 w-4 h-4 text-muted-foreground" />
          <Input
            type="search"
            placeholder="Rechercher une serie..."
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
      ) : filteredSeries.length > 0 ? (
        <div className="grid grid-cols-2 sm:grid-cols-3 md:grid-cols-4 lg:grid-cols-5 xl:grid-cols-6 gap-4">
          {filteredSeries.map((s) => (
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
      ) : (
        <div className="py-12 text-center">
          <div className="w-16 h-16 bg-card-hover rounded-full flex items-center justify-center mx-auto mb-4">
            <Clapperboard className="w-8 h-8 text-muted-foreground" />
          </div>
          <h3 className="text-lg font-medium text-white mb-2">
            Aucune serie trouvee
          </h3>
          <p className="text-muted-foreground">
            {series.length === 0
              ? 'Ajoutez une playlist avec des series.'
              : 'Essayez de modifier vos filtres.'}
          </p>
        </div>
      )}
    </div>
  );
}
