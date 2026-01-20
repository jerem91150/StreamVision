'use client';

import { useState } from 'react';
import {
  Tv,
  Film,
  Clapperboard,
  RefreshCw,
  Trash2,
  MoreVertical,
  Check,
  X,
  Calendar,
  Globe,
  Server,
} from 'lucide-react';
import { Button } from '@/components/ui/button';
import { Card, CardContent } from '@/components/ui/card';

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

interface PlaylistCardProps {
  playlist: Playlist;
  onSync: (id: string) => Promise<void>;
  onDelete: (id: string) => Promise<void>;
  onToggleActive: (id: string, isActive: boolean) => Promise<void>;
}

export function PlaylistCard({
  playlist,
  onSync,
  onDelete,
  onToggleActive,
}: PlaylistCardProps) {
  const [isSyncing, setIsSyncing] = useState(false);
  const [isDeleting, setIsDeleting] = useState(false);
  const [showMenu, setShowMenu] = useState(false);

  const handleSync = async () => {
    setIsSyncing(true);
    try {
      await onSync(playlist.id);
    } finally {
      setIsSyncing(false);
    }
  };

  const handleDelete = async () => {
    if (!confirm('Êtes-vous sûr de vouloir supprimer cette playlist ?')) {
      return;
    }
    setIsDeleting(true);
    try {
      await onDelete(playlist.id);
    } finally {
      setIsDeleting(false);
    }
  };

  const handleToggle = async () => {
    await onToggleActive(playlist.id, !playlist.isActive);
  };

  const formatDate = (dateString: string | null) => {
    if (!dateString) return 'Jamais';
    const date = new Date(dateString);
    return date.toLocaleDateString('fr-FR', {
      day: '2-digit',
      month: '2-digit',
      year: 'numeric',
      hour: '2-digit',
      minute: '2-digit',
    });
  };

  const totalItems = playlist.channelCount + playlist.movieCount + playlist.seriesCount;

  return (
    <Card className={`relative ${!playlist.isActive ? 'opacity-60' : ''}`}>
      <CardContent className="p-4">
        <div className="flex items-start justify-between mb-3">
          <div className="flex items-center gap-3">
            <div
              className={`p-2 rounded-lg ${
                playlist.type === 'xtream'
                  ? 'bg-purple-500/20 text-purple-400'
                  : 'bg-blue-500/20 text-blue-400'
              }`}
            >
              {playlist.type === 'xtream' ? (
                <Server className="h-5 w-5" />
              ) : (
                <Globe className="h-5 w-5" />
              )}
            </div>
            <div>
              <h3 className="font-semibold text-white">{playlist.name}</h3>
              <p className="text-xs text-gray-400">
                {playlist.type === 'xtream' ? 'Xtream Codes' : 'M3U/M3U8'}
              </p>
            </div>
          </div>

          <div className="relative">
            <Button
              variant="ghost"
              size="sm"
              onClick={() => setShowMenu(!showMenu)}
              className="h-8 w-8 p-0"
            >
              <MoreVertical className="h-4 w-4" />
            </Button>

            {showMenu && (
              <div className="absolute right-0 top-full mt-1 bg-gray-800 border border-gray-700 rounded-lg shadow-lg py-1 z-10 min-w-[150px]">
                <button
                  onClick={() => {
                    handleToggle();
                    setShowMenu(false);
                  }}
                  className="w-full px-3 py-2 text-left text-sm text-gray-300 hover:bg-gray-700 flex items-center gap-2"
                >
                  {playlist.isActive ? (
                    <>
                      <X className="h-4 w-4" /> Désactiver
                    </>
                  ) : (
                    <>
                      <Check className="h-4 w-4" /> Activer
                    </>
                  )}
                </button>
                <button
                  onClick={() => {
                    handleDelete();
                    setShowMenu(false);
                  }}
                  disabled={isDeleting}
                  className="w-full px-3 py-2 text-left text-sm text-red-400 hover:bg-gray-700 flex items-center gap-2"
                >
                  <Trash2 className="h-4 w-4" />
                  {isDeleting ? 'Suppression...' : 'Supprimer'}
                </button>
              </div>
            )}
          </div>
        </div>

        {/* Stats */}
        <div className="grid grid-cols-3 gap-2 mb-4">
          <div className="bg-gray-800/50 rounded-lg p-2 text-center">
            <div className="flex items-center justify-center gap-1 text-blue-400 mb-1">
              <Tv className="h-4 w-4" />
            </div>
            <p className="text-lg font-bold text-white">{playlist.channelCount}</p>
            <p className="text-xs text-gray-400">Chaînes</p>
          </div>
          <div className="bg-gray-800/50 rounded-lg p-2 text-center">
            <div className="flex items-center justify-center gap-1 text-orange-400 mb-1">
              <Film className="h-4 w-4" />
            </div>
            <p className="text-lg font-bold text-white">{playlist.movieCount}</p>
            <p className="text-xs text-gray-400">Films</p>
          </div>
          <div className="bg-gray-800/50 rounded-lg p-2 text-center">
            <div className="flex items-center justify-center gap-1 text-green-400 mb-1">
              <Clapperboard className="h-4 w-4" />
            </div>
            <p className="text-lg font-bold text-white">{playlist.seriesCount}</p>
            <p className="text-xs text-gray-400">Séries</p>
          </div>
        </div>

        {/* Last sync */}
        <div className="flex items-center gap-2 text-xs text-gray-400 mb-4">
          <Calendar className="h-3 w-3" />
          <span>Dernière sync: {formatDate(playlist.lastSync)}</span>
        </div>

        {/* Actions */}
        <div className="flex gap-2">
          <Button
            onClick={handleSync}
            disabled={isSyncing || !playlist.isActive}
            className="flex-1 bg-orange-500 hover:bg-orange-600"
          >
            <RefreshCw className={`h-4 w-4 mr-2 ${isSyncing ? 'animate-spin' : ''}`} />
            {isSyncing ? 'Synchronisation...' : 'Synchroniser'}
          </Button>
        </div>

        {totalItems === 0 && playlist.isActive && (
          <p className="text-xs text-yellow-500 mt-2 text-center">
            Aucun contenu. Cliquez sur Synchroniser pour charger les données.
          </p>
        )}
      </CardContent>
    </Card>
  );
}
