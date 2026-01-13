'use client';

import { useEffect, useState } from 'react';
import Link from 'next/link';
import { Play, Tv, Film, Clapperboard, Plus, ArrowRight } from 'lucide-react';
import { Button } from '@/components/ui/button';
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from '@/components/ui/card';
import { ContentRow } from '@/components/content/ContentRow';

// Demo data - sera remplace par les vraies donnees de l'API
const demoContent = {
  continueWatching: [
    { id: '1', title: 'Breaking Bad', type: 'series' as const, progress: 65, imageUrl: '' },
    { id: '2', title: 'The Matrix', type: 'movie' as const, progress: 30, imageUrl: '' },
  ],
  recommendations: [
    { id: '3', title: 'Stranger Things', type: 'series' as const, year: 2016, rating: 8.7, imageUrl: '' },
    { id: '4', title: 'Inception', type: 'movie' as const, year: 2010, rating: 8.8, imageUrl: '' },
    { id: '5', title: 'The Witcher', type: 'series' as const, year: 2019, rating: 8.2, imageUrl: '' },
    { id: '6', title: 'Interstellar', type: 'movie' as const, year: 2014, rating: 8.7, imageUrl: '' },
    { id: '7', title: 'Dark', type: 'series' as const, year: 2017, rating: 8.8, imageUrl: '' },
  ],
  trending: [
    { id: '8', title: 'Dune: Part Two', type: 'movie' as const, year: 2024, rating: 8.5, imageUrl: '' },
    { id: '9', title: 'The Last of Us', type: 'series' as const, year: 2023, rating: 8.9, imageUrl: '' },
    { id: '10', title: 'Oppenheimer', type: 'movie' as const, year: 2023, rating: 8.6, imageUrl: '' },
    { id: '11', title: 'House of the Dragon', type: 'series' as const, year: 2022, rating: 8.4, imageUrl: '' },
  ],
};

export default function DashboardPage() {
  const [hasPlaylist, setHasPlaylist] = useState(false);

  useEffect(() => {
    // Check if user has a playlist configured
    const checkPlaylist = async () => {
      try {
        const accessToken = localStorage.getItem('accessToken');
        const response = await fetch('/api/playlists', {
          headers: { Authorization: `Bearer ${accessToken}` },
        });
        if (response.ok) {
          const playlists = await response.json();
          setHasPlaylist(playlists.length > 0);
        }
      } catch (error) {
        console.error('Failed to check playlists:', error);
      }
    };
    checkPlaylist();
  }, []);

  return (
    <div className="space-y-8">
      {/* Welcome / Setup Card */}
      {!hasPlaylist && (
        <Card className="bg-gradient-to-r from-primary/10 via-card to-card border-primary/20">
          <CardHeader>
            <CardTitle>Bienvenue sur StreamVision !</CardTitle>
            <CardDescription>
              Ajoutez votre premiere playlist pour commencer a regarder du contenu.
            </CardDescription>
          </CardHeader>
          <CardContent>
            <Link href="/settings/playlists">
              <Button className="gap-2">
                <Plus className="w-4 h-4" />
                Ajouter une playlist
              </Button>
            </Link>
          </CardContent>
        </Card>
      )}

      {/* Quick Access */}
      <div className="grid grid-cols-2 md:grid-cols-4 gap-4">
        <Link href="/live">
          <Card className="hover:border-primary/50 transition-colors cursor-pointer">
            <CardContent className="flex items-center gap-3 p-4">
              <div className="w-10 h-10 bg-primary/10 rounded-lg flex items-center justify-center">
                <Tv className="w-5 h-5 text-primary" />
              </div>
              <div>
                <p className="font-medium text-white">TV en direct</p>
                <p className="text-xs text-muted-foreground">Regarder</p>
              </div>
            </CardContent>
          </Card>
        </Link>
        <Link href="/films">
          <Card className="hover:border-primary/50 transition-colors cursor-pointer">
            <CardContent className="flex items-center gap-3 p-4">
              <div className="w-10 h-10 bg-primary/10 rounded-lg flex items-center justify-center">
                <Film className="w-5 h-5 text-primary" />
              </div>
              <div>
                <p className="font-medium text-white">Films</p>
                <p className="text-xs text-muted-foreground">Explorer</p>
              </div>
            </CardContent>
          </Card>
        </Link>
        <Link href="/series">
          <Card className="hover:border-primary/50 transition-colors cursor-pointer">
            <CardContent className="flex items-center gap-3 p-4">
              <div className="w-10 h-10 bg-primary/10 rounded-lg flex items-center justify-center">
                <Clapperboard className="w-5 h-5 text-primary" />
              </div>
              <div>
                <p className="font-medium text-white">Series</p>
                <p className="text-xs text-muted-foreground">Decouvrir</p>
              </div>
            </CardContent>
          </Card>
        </Link>
        <Link href="/epg">
          <Card className="hover:border-primary/50 transition-colors cursor-pointer">
            <CardContent className="flex items-center gap-3 p-4">
              <div className="w-10 h-10 bg-primary/10 rounded-lg flex items-center justify-center">
                <Play className="w-5 h-5 text-primary" />
              </div>
              <div>
                <p className="font-medium text-white">Guide TV</p>
                <p className="text-xs text-muted-foreground">Programme</p>
              </div>
            </CardContent>
          </Card>
        </Link>
      </div>

      {/* Continue Watching */}
      {demoContent.continueWatching.length > 0 && (
        <ContentRow
          title="Reprendre la lecture"
          items={demoContent.continueWatching}
          seeAllHref="/history"
        />
      )}

      {/* Recommendations */}
      <ContentRow
        title="Recommande pour vous"
        items={demoContent.recommendations}
      />

      {/* Trending */}
      <ContentRow
        title="Tendances"
        items={demoContent.trending}
      />

      {/* Empty state if no playlist */}
      {!hasPlaylist && (
        <Card className="bg-card border-border">
          <CardContent className="py-12 text-center">
            <div className="w-16 h-16 bg-card-hover rounded-full flex items-center justify-center mx-auto mb-4">
              <Tv className="w-8 h-8 text-muted-foreground" />
            </div>
            <h3 className="text-lg font-medium text-white mb-2">
              Aucune playlist configuree
            </h3>
            <p className="text-muted-foreground mb-6 max-w-md mx-auto">
              Pour profiter de StreamVision, ajoutez votre playlist M3U ou
              connectez-vous a votre serveur Xtream Codes.
            </p>
            <Link href="/settings/playlists">
              <Button className="gap-2">
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
