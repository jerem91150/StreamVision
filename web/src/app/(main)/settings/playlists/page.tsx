'use client';

import { useState, useEffect } from 'react';
import { useRouter } from 'next/navigation';
import { ArrowLeft, Plus, Loader2, ListVideo } from 'lucide-react';
import Link from 'next/link';
import { Button } from '@/components/ui/button';
import { PlaylistCard } from '@/components/settings/PlaylistCard';
import { PlaylistForm, PlaylistFormData } from '@/components/settings/PlaylistForm';

interface Playlist {
  id: string;
  name: string;
  type: string;
  url: string | null;
  xtreamServer: string | null;
  isActive: boolean;
  lastSync: string | null;
  channelCount: number;
  movieCount: number;
  seriesCount: number;
}

export default function PlaylistsPage() {
  const router = useRouter();
  const [playlists, setPlaylists] = useState<Playlist[]>([]);
  const [isLoading, setIsLoading] = useState(true);
  const [showForm, setShowForm] = useState(false);
  const [error, setError] = useState('');

  const fetchPlaylists = async () => {
    try {
      const token = localStorage.getItem('accessToken');
      const response = await fetch('/api/playlists', {
        headers: {
          Authorization: `Bearer ${token}`,
        },
      });

      if (response.status === 401) {
        router.push('/login');
        return;
      }

      if (!response.ok) {
        throw new Error('Erreur lors du chargement');
      }

      const data = await response.json();
      setPlaylists(data);
    } catch (err) {
      setError(err instanceof Error ? err.message : 'Erreur inconnue');
    } finally {
      setIsLoading(false);
    }
  };

  useEffect(() => {
    fetchPlaylists();
  }, []);

  const handleAddPlaylist = async (data: PlaylistFormData) => {
    const token = localStorage.getItem('accessToken');
    const response = await fetch('/api/playlists', {
      method: 'POST',
      headers: {
        'Content-Type': 'application/json',
        Authorization: `Bearer ${token}`,
      },
      body: JSON.stringify(data),
    });

    if (!response.ok) {
      const result = await response.json();
      throw new Error(result.error || "Erreur lors de l'ajout");
    }

    setShowForm(false);
    await fetchPlaylists();
  };

  const handleSync = async (id: string) => {
    const token = localStorage.getItem('accessToken');
    const response = await fetch(`/api/playlists/${id}/sync`, {
      method: 'POST',
      headers: {
        Authorization: `Bearer ${token}`,
      },
    });

    if (!response.ok) {
      const result = await response.json();
      throw new Error(result.error || 'Erreur de synchronisation');
    }

    await fetchPlaylists();
  };

  const handleDelete = async (id: string) => {
    const token = localStorage.getItem('accessToken');
    const response = await fetch(`/api/playlists/${id}`, {
      method: 'DELETE',
      headers: {
        Authorization: `Bearer ${token}`,
      },
    });

    if (!response.ok) {
      const result = await response.json();
      throw new Error(result.error || 'Erreur de suppression');
    }

    await fetchPlaylists();
  };

  const handleToggleActive = async (id: string, isActive: boolean) => {
    const token = localStorage.getItem('accessToken');
    const response = await fetch(`/api/playlists/${id}`, {
      method: 'PUT',
      headers: {
        'Content-Type': 'application/json',
        Authorization: `Bearer ${token}`,
      },
      body: JSON.stringify({ isActive }),
    });

    if (!response.ok) {
      const result = await response.json();
      throw new Error(result.error || 'Erreur de mise à jour');
    }

    await fetchPlaylists();
  };

  return (
    <div className="p-6 max-w-4xl mx-auto">
      {/* Header */}
      <div className="flex items-center gap-4 mb-6">
        <Link href="/settings">
          <Button variant="ghost" size="sm" className="h-8 w-8 p-0">
            <ArrowLeft className="h-4 w-4" />
          </Button>
        </Link>
        <div className="flex-1">
          <h1 className="text-2xl font-bold text-white">Playlists</h1>
          <p className="text-sm text-gray-400">Gérez vos sources IPTV</p>
        </div>
        {!showForm && (
          <Button
            onClick={() => setShowForm(true)}
            className="bg-orange-500 hover:bg-orange-600"
          >
            <Plus className="h-4 w-4 mr-2" />
            Ajouter
          </Button>
        )}
      </div>

      {/* Error */}
      {error && (
        <div className="mb-4 p-3 bg-red-500/10 border border-red-500/50 rounded-lg text-red-400">
          {error}
        </div>
      )}

      {/* Add form */}
      {showForm && (
        <div className="mb-6">
          <PlaylistForm onSubmit={handleAddPlaylist} onCancel={() => setShowForm(false)} />
        </div>
      )}

      {/* Loading */}
      {isLoading ? (
        <div className="flex items-center justify-center py-12">
          <Loader2 className="h-8 w-8 animate-spin text-orange-500" />
        </div>
      ) : playlists.length === 0 ? (
        /* Empty state */
        <div className="text-center py-12">
          <div className="inline-flex items-center justify-center w-16 h-16 rounded-full bg-gray-800 mb-4">
            <ListVideo className="h-8 w-8 text-gray-400" />
          </div>
          <h2 className="text-xl font-semibold text-white mb-2">Aucune playlist</h2>
          <p className="text-gray-400 mb-4">
            Ajoutez votre première source IPTV pour commencer
          </p>
          {!showForm && (
            <Button
              onClick={() => setShowForm(true)}
              className="bg-orange-500 hover:bg-orange-600"
            >
              <Plus className="h-4 w-4 mr-2" />
              Ajouter une playlist
            </Button>
          )}
        </div>
      ) : (
        /* Playlists grid */
        <div className="grid gap-4 md:grid-cols-2">
          {playlists.map((playlist) => (
            <PlaylistCard
              key={playlist.id}
              playlist={playlist}
              onSync={handleSync}
              onDelete={handleDelete}
              onToggleActive={handleToggleActive}
            />
          ))}
        </div>
      )}

      {/* Tips */}
      {playlists.length > 0 && (
        <div className="mt-8 p-4 bg-gray-800/50 rounded-lg border border-gray-700">
          <h3 className="text-sm font-medium text-white mb-2">Conseils</h3>
          <ul className="text-sm text-gray-400 space-y-1">
            <li>• Cliquez sur &quot;Synchroniser&quot; pour charger le contenu de vos playlists</li>
            <li>• Vous pouvez désactiver une playlist sans la supprimer</li>
            <li>• Les playlists Xtream Codes offrent plus de fonctionnalités (VOD, EPG, etc.)</li>
          </ul>
        </div>
      )}
    </div>
  );
}
