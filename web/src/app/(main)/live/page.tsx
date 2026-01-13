'use client';

import { useState, useEffect } from 'react';
import { Search, Filter, Grid, List, Tv } from 'lucide-react';
import { Input } from '@/components/ui/input';
import { Button } from '@/components/ui/button';
import { ChannelCard } from '@/components/content/ChannelCard';
import { cn } from '@/lib/utils';

interface Channel {
  id: string;
  name: string;
  logoUrl?: string;
  groupTitle?: string;
  number?: number;
}

// Demo categories
const categories = [
  'Tous',
  'France',
  'Sport',
  'Cinema',
  'Documentaire',
  'Jeunesse',
  'Musique',
  'Info',
];

export default function LiveTVPage() {
  const [channels, setChannels] = useState<Channel[]>([]);
  const [filteredChannels, setFilteredChannels] = useState<Channel[]>([]);
  const [searchQuery, setSearchQuery] = useState('');
  const [selectedCategory, setSelectedCategory] = useState('Tous');
  const [viewMode, setViewMode] = useState<'grid' | 'list'>('grid');
  const [isLoading, setIsLoading] = useState(true);

  useEffect(() => {
    // Fetch channels from API
    const fetchChannels = async () => {
      try {
        const accessToken = localStorage.getItem('accessToken');
        const response = await fetch('/api/channels', {
          headers: { Authorization: `Bearer ${accessToken}` },
        });

        if (response.ok) {
          const data = await response.json();
          setChannels(data);
          setFilteredChannels(data);
        }
      } catch (error) {
        console.error('Failed to fetch channels:', error);
      } finally {
        setIsLoading(false);
      }
    };

    fetchChannels();
  }, []);

  useEffect(() => {
    let result = channels;

    // Filter by search query
    if (searchQuery) {
      result = result.filter((channel) =>
        channel.name.toLowerCase().includes(searchQuery.toLowerCase())
      );
    }

    // Filter by category
    if (selectedCategory !== 'Tous') {
      result = result.filter((channel) =>
        channel.groupTitle?.toLowerCase().includes(selectedCategory.toLowerCase())
      );
    }

    setFilteredChannels(result);
  }, [channels, searchQuery, selectedCategory]);

  return (
    <div className="space-y-6">
      {/* Header */}
      <div className="flex flex-col sm:flex-row sm:items-center justify-between gap-4">
        <div>
          <h1 className="text-2xl font-bold text-white">TV en direct</h1>
          <p className="text-muted-foreground">
            {filteredChannels.length} chaines disponibles
          </p>
        </div>

        <div className="flex items-center gap-2">
          {/* Search */}
          <div className="relative">
            <Search className="absolute left-3 top-1/2 -translate-y-1/2 w-4 h-4 text-muted-foreground" />
            <Input
              type="search"
              placeholder="Rechercher..."
              value={searchQuery}
              onChange={(e) => setSearchQuery(e.target.value)}
              className="pl-9 w-64"
            />
          </div>

          {/* View mode toggle */}
          <div className="flex border border-border rounded-lg p-1">
            <Button
              variant={viewMode === 'grid' ? 'secondary' : 'ghost'}
              size="icon"
              className="h-8 w-8"
              onClick={() => setViewMode('grid')}
            >
              <Grid className="w-4 h-4" />
            </Button>
            <Button
              variant={viewMode === 'list' ? 'secondary' : 'ghost'}
              size="icon"
              className="h-8 w-8"
              onClick={() => setViewMode('list')}
            >
              <List className="w-4 h-4" />
            </Button>
          </div>
        </div>
      </div>

      {/* Categories */}
      <div className="flex gap-2 overflow-x-auto pb-2">
        {categories.map((category) => (
          <Button
            key={category}
            variant={selectedCategory === category ? 'default' : 'outline'}
            size="sm"
            onClick={() => setSelectedCategory(category)}
            className="shrink-0"
          >
            {category}
          </Button>
        ))}
      </div>

      {/* Content */}
      {isLoading ? (
        <div className="grid grid-cols-2 sm:grid-cols-3 md:grid-cols-4 lg:grid-cols-5 xl:grid-cols-6 gap-4">
          {Array.from({ length: 12 }).map((_, i) => (
            <div key={i} className="aspect-video bg-card animate-pulse rounded-xl" />
          ))}
        </div>
      ) : filteredChannels.length > 0 ? (
        <div
          className={cn(
            viewMode === 'grid'
              ? 'grid grid-cols-2 sm:grid-cols-3 md:grid-cols-4 lg:grid-cols-5 xl:grid-cols-6 gap-4'
              : 'space-y-2'
          )}
        >
          {filteredChannels.map((channel) => (
            viewMode === 'grid' ? (
              <ChannelCard
                key={channel.id}
                id={channel.id}
                name={channel.name}
                logoUrl={channel.logoUrl}
                number={channel.number}
              />
            ) : (
              <div
                key={channel.id}
                className="flex items-center gap-4 p-3 rounded-lg bg-card border border-border hover:border-primary/50 transition-colors cursor-pointer"
              >
                <div className="w-16 h-10 bg-card-hover rounded flex items-center justify-center">
                  {channel.logoUrl ? (
                    <img src={channel.logoUrl} alt="" className="h-6 object-contain" />
                  ) : (
                    <Tv className="w-5 h-5 text-muted-foreground" />
                  )}
                </div>
                {channel.number && (
                  <span className="text-sm text-muted-foreground w-8">{channel.number}</span>
                )}
                <span className="text-sm font-medium text-white flex-1">{channel.name}</span>
                <Button size="sm" variant="ghost">
                  Regarder
                </Button>
              </div>
            )
          ))}
        </div>
      ) : (
        <div className="py-12 text-center">
          <div className="w-16 h-16 bg-card-hover rounded-full flex items-center justify-center mx-auto mb-4">
            <Tv className="w-8 h-8 text-muted-foreground" />
          </div>
          <h3 className="text-lg font-medium text-white mb-2">
            Aucune chaine trouvee
          </h3>
          <p className="text-muted-foreground">
            {channels.length === 0
              ? 'Ajoutez une playlist pour voir vos chaines.'
              : 'Essayez de modifier vos filtres.'}
          </p>
        </div>
      )}
    </div>
  );
}
