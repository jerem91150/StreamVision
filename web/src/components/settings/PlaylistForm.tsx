'use client';

import { useState } from 'react';
import { X, Globe, Server, Plus } from 'lucide-react';
import { Button } from '@/components/ui/button';
import { Input } from '@/components/ui/input';
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card';

interface PlaylistFormProps {
  onSubmit: (data: PlaylistFormData) => Promise<void>;
  onCancel: () => void;
}

export interface PlaylistFormData {
  name: string;
  type: 'm3u' | 'xtream';
  url?: string;
  xtreamServer?: string;
  xtreamUsername?: string;
  xtreamPassword?: string;
  epgUrl?: string;
}

export function PlaylistForm({ onSubmit, onCancel }: PlaylistFormProps) {
  const [type, setType] = useState<'m3u' | 'xtream'>('m3u');
  const [name, setName] = useState('');
  const [url, setUrl] = useState('');
  const [xtreamServer, setXtreamServer] = useState('');
  const [xtreamUsername, setXtreamUsername] = useState('');
  const [xtreamPassword, setXtreamPassword] = useState('');
  const [epgUrl, setEpgUrl] = useState('');
  const [isSubmitting, setIsSubmitting] = useState(false);
  const [error, setError] = useState('');

  const handleSubmit = async (e: React.FormEvent) => {
    e.preventDefault();
    setError('');

    // Validation
    if (!name.trim()) {
      setError('Le nom est requis');
      return;
    }

    if (type === 'm3u' && !url.trim()) {
      setError("L'URL M3U est requise");
      return;
    }

    if (type === 'xtream') {
      if (!xtreamServer.trim() || !xtreamUsername.trim() || !xtreamPassword.trim()) {
        setError('Tous les champs Xtream sont requis');
        return;
      }
    }

    setIsSubmitting(true);
    try {
      await onSubmit({
        name: name.trim(),
        type,
        url: type === 'm3u' ? url.trim() : undefined,
        xtreamServer: type === 'xtream' ? xtreamServer.trim() : undefined,
        xtreamUsername: type === 'xtream' ? xtreamUsername.trim() : undefined,
        xtreamPassword: type === 'xtream' ? xtreamPassword.trim() : undefined,
        epgUrl: epgUrl.trim() || undefined,
      });
    } catch (err) {
      setError(err instanceof Error ? err.message : 'Une erreur est survenue');
    } finally {
      setIsSubmitting(false);
    }
  };

  return (
    <Card className="bg-gray-900 border-gray-700">
      <CardHeader className="flex flex-row items-center justify-between pb-2">
        <CardTitle className="text-lg text-white">Ajouter une playlist</CardTitle>
        <Button variant="ghost" size="sm" onClick={onCancel} className="h-8 w-8 p-0">
          <X className="h-4 w-4" />
        </Button>
      </CardHeader>
      <CardContent>
        <form onSubmit={handleSubmit} className="space-y-4">
          {/* Type selection */}
          <div>
            <label className="block text-sm font-medium text-gray-300 mb-2">
              Type de source
            </label>
            <div className="grid grid-cols-2 gap-2">
              <button
                type="button"
                onClick={() => setType('m3u')}
                className={`p-3 rounded-lg border-2 flex items-center gap-2 transition-colors ${
                  type === 'm3u'
                    ? 'border-orange-500 bg-orange-500/10 text-orange-400'
                    : 'border-gray-700 bg-gray-800 text-gray-400 hover:border-gray-600'
                }`}
              >
                <Globe className="h-5 w-5" />
                <div className="text-left">
                  <p className="font-medium">M3U / M3U8</p>
                  <p className="text-xs opacity-70">URL de playlist</p>
                </div>
              </button>
              <button
                type="button"
                onClick={() => setType('xtream')}
                className={`p-3 rounded-lg border-2 flex items-center gap-2 transition-colors ${
                  type === 'xtream'
                    ? 'border-orange-500 bg-orange-500/10 text-orange-400'
                    : 'border-gray-700 bg-gray-800 text-gray-400 hover:border-gray-600'
                }`}
              >
                <Server className="h-5 w-5" />
                <div className="text-left">
                  <p className="font-medium">Xtream Codes</p>
                  <p className="text-xs opacity-70">Serveur + identifiants</p>
                </div>
              </button>
            </div>
          </div>

          {/* Name */}
          <div>
            <label htmlFor="name" className="block text-sm font-medium text-gray-300 mb-1">
              Nom de la playlist
            </label>
            <Input
              id="name"
              type="text"
              value={name}
              onChange={(e) => setName(e.target.value)}
              placeholder="Ma playlist IPTV"
              className="bg-gray-800 border-gray-700 text-white"
            />
          </div>

          {/* M3U URL */}
          {type === 'm3u' && (
            <div>
              <label htmlFor="url" className="block text-sm font-medium text-gray-300 mb-1">
                URL M3U
              </label>
              <Input
                id="url"
                type="url"
                value={url}
                onChange={(e) => setUrl(e.target.value)}
                placeholder="http://example.com/playlist.m3u"
                className="bg-gray-800 border-gray-700 text-white"
              />
            </div>
          )}

          {/* Xtream fields */}
          {type === 'xtream' && (
            <>
              <div>
                <label
                  htmlFor="xtreamServer"
                  className="block text-sm font-medium text-gray-300 mb-1"
                >
                  Serveur
                </label>
                <Input
                  id="xtreamServer"
                  type="url"
                  value={xtreamServer}
                  onChange={(e) => setXtreamServer(e.target.value)}
                  placeholder="http://server.example.com:8080"
                  className="bg-gray-800 border-gray-700 text-white"
                />
              </div>
              <div className="grid grid-cols-2 gap-3">
                <div>
                  <label
                    htmlFor="xtreamUsername"
                    className="block text-sm font-medium text-gray-300 mb-1"
                  >
                    Nom d&apos;utilisateur
                  </label>
                  <Input
                    id="xtreamUsername"
                    type="text"
                    value={xtreamUsername}
                    onChange={(e) => setXtreamUsername(e.target.value)}
                    placeholder="username"
                    className="bg-gray-800 border-gray-700 text-white"
                  />
                </div>
                <div>
                  <label
                    htmlFor="xtreamPassword"
                    className="block text-sm font-medium text-gray-300 mb-1"
                  >
                    Mot de passe
                  </label>
                  <Input
                    id="xtreamPassword"
                    type="password"
                    value={xtreamPassword}
                    onChange={(e) => setXtreamPassword(e.target.value)}
                    placeholder="password"
                    className="bg-gray-800 border-gray-700 text-white"
                  />
                </div>
              </div>
            </>
          )}

          {/* EPG URL (optional) */}
          <div>
            <label htmlFor="epgUrl" className="block text-sm font-medium text-gray-300 mb-1">
              URL EPG (optionnel)
            </label>
            <Input
              id="epgUrl"
              type="url"
              value={epgUrl}
              onChange={(e) => setEpgUrl(e.target.value)}
              placeholder="http://example.com/epg.xml"
              className="bg-gray-800 border-gray-700 text-white"
            />
            <p className="text-xs text-gray-500 mt-1">
              Guide des programmes au format XMLTV
            </p>
          </div>

          {/* Error */}
          {error && (
            <div className="p-3 bg-red-500/10 border border-red-500/50 rounded-lg text-red-400 text-sm">
              {error}
            </div>
          )}

          {/* Actions */}
          <div className="flex gap-2 pt-2">
            <Button
              type="button"
              variant="outline"
              onClick={onCancel}
              className="flex-1 border-gray-700"
            >
              Annuler
            </Button>
            <Button
              type="submit"
              disabled={isSubmitting}
              className="flex-1 bg-orange-500 hover:bg-orange-600"
            >
              <Plus className="h-4 w-4 mr-2" />
              {isSubmitting ? 'Ajout...' : 'Ajouter'}
            </Button>
          </div>
        </form>
      </CardContent>
    </Card>
  );
}
