'use client';

import { useState, useEffect } from 'react';
import { useRouter } from 'next/navigation';
import {
  User,
  Mail,
  Calendar,
  Crown,
  ListVideo,
  Heart,
  History,
  Edit2,
  Lock,
  Save,
  X,
  Loader2,
} from 'lucide-react';
import { Button } from '@/components/ui/button';
import { Input } from '@/components/ui/input';
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card';

interface Profile {
  id: string;
  email: string;
  username: string;
  avatarUrl: string | null;
  createdAt: string;
  subscription: {
    tier: string;
    status: string;
    expiresAt: string | null;
  } | null;
  stats: {
    playlists: number;
    favorites: number;
    watchHistory: number;
  };
}

export default function ProfilePage() {
  const router = useRouter();
  const [profile, setProfile] = useState<Profile | null>(null);
  const [isLoading, setIsLoading] = useState(true);
  const [isEditing, setIsEditing] = useState(false);
  const [isSaving, setIsSaving] = useState(false);
  const [editUsername, setEditUsername] = useState('');
  const [error, setError] = useState('');
  const [success, setSuccess] = useState('');

  // Password change state
  const [showPasswordForm, setShowPasswordForm] = useState(false);
  const [currentPassword, setCurrentPassword] = useState('');
  const [newPassword, setNewPassword] = useState('');
  const [confirmPassword, setConfirmPassword] = useState('');
  const [passwordError, setPasswordError] = useState('');
  const [isChangingPassword, setIsChangingPassword] = useState(false);

  useEffect(() => {
    const fetchProfile = async () => {
      try {
        const token = localStorage.getItem('accessToken');
        const response = await fetch('/api/user/profile', {
          headers: { Authorization: `Bearer ${token}` },
        });

        if (response.status === 401) {
          router.push('/login');
          return;
        }

        if (response.ok) {
          const data = await response.json();
          setProfile(data);
          setEditUsername(data.username);
        }
      } catch (err) {
        console.error('Failed to fetch profile:', err);
      } finally {
        setIsLoading(false);
      }
    };

    fetchProfile();
  }, [router]);

  const handleSaveProfile = async () => {
    if (!editUsername.trim() || editUsername.length < 2) {
      setError('Le nom d\'utilisateur doit contenir au moins 2 caractères');
      return;
    }

    setIsSaving(true);
    setError('');

    try {
      const token = localStorage.getItem('accessToken');
      const response = await fetch('/api/user/profile', {
        method: 'PUT',
        headers: {
          'Content-Type': 'application/json',
          Authorization: `Bearer ${token}`,
        },
        body: JSON.stringify({ username: editUsername }),
      });

      if (response.ok) {
        const data = await response.json();
        setProfile((prev) => prev ? { ...prev, username: data.username } : null);
        setIsEditing(false);
        setSuccess('Profil mis à jour');
        setTimeout(() => setSuccess(''), 3000);
      } else {
        const data = await response.json();
        setError(data.error || 'Erreur de mise à jour');
      }
    } catch (err) {
      setError('Erreur de connexion');
    } finally {
      setIsSaving(false);
    }
  };

  const handleChangePassword = async (e: React.FormEvent) => {
    e.preventDefault();
    setPasswordError('');

    if (newPassword.length < 8) {
      setPasswordError('Le mot de passe doit contenir au moins 8 caractères');
      return;
    }

    if (newPassword !== confirmPassword) {
      setPasswordError('Les mots de passe ne correspondent pas');
      return;
    }

    setIsChangingPassword(true);

    try {
      const token = localStorage.getItem('accessToken');
      const response = await fetch('/api/user/password', {
        method: 'PUT',
        headers: {
          'Content-Type': 'application/json',
          Authorization: `Bearer ${token}`,
        },
        body: JSON.stringify({ currentPassword, newPassword }),
      });

      if (response.ok) {
        setShowPasswordForm(false);
        setCurrentPassword('');
        setNewPassword('');
        setConfirmPassword('');
        setSuccess('Mot de passe modifié');
        setTimeout(() => setSuccess(''), 3000);
      } else {
        const data = await response.json();
        setPasswordError(data.error || 'Erreur de modification');
      }
    } catch (err) {
      setPasswordError('Erreur de connexion');
    } finally {
      setIsChangingPassword(false);
    }
  };

  const formatDate = (dateString: string) => {
    return new Date(dateString).toLocaleDateString('fr-FR', {
      day: 'numeric',
      month: 'long',
      year: 'numeric',
    });
  };

  const getTierLabel = (tier: string) => {
    switch (tier) {
      case 'premium':
        return 'Premium';
      case 'basic':
        return 'Basic';
      default:
        return 'Gratuit';
    }
  };

  const getTierColor = (tier: string) => {
    switch (tier) {
      case 'premium':
        return 'text-yellow-400 bg-yellow-500/20';
      case 'basic':
        return 'text-blue-400 bg-blue-500/20';
      default:
        return 'text-gray-400 bg-gray-500/20';
    }
  };

  if (isLoading) {
    return (
      <div className="flex items-center justify-center min-h-[400px]">
        <Loader2 className="w-8 h-8 animate-spin text-orange-500" />
      </div>
    );
  }

  if (!profile) {
    return null;
  }

  return (
    <div className="p-6 max-w-4xl mx-auto">
      <h1 className="text-2xl font-bold text-white mb-6">Mon profil</h1>

      {/* Success message */}
      {success && (
        <div className="mb-4 p-3 bg-green-500/10 border border-green-500/50 rounded-lg text-green-400">
          {success}
        </div>
      )}

      <div className="grid gap-6 md:grid-cols-2">
        {/* Profile info */}
        <Card className="bg-gray-900 border-gray-800">
          <CardHeader>
            <CardTitle className="text-white flex items-center justify-between">
              <span>Informations</span>
              {!isEditing && (
                <Button
                  variant="ghost"
                  size="sm"
                  onClick={() => setIsEditing(true)}
                  className="h-8"
                >
                  <Edit2 className="w-4 h-4" />
                </Button>
              )}
            </CardTitle>
          </CardHeader>
          <CardContent className="space-y-4">
            {/* Avatar */}
            <div className="flex items-center gap-4">
              <div className="w-16 h-16 rounded-full bg-orange-500/20 flex items-center justify-center">
                <User className="w-8 h-8 text-orange-400" />
              </div>
              <div>
                {isEditing ? (
                  <div className="space-y-2">
                    <Input
                      value={editUsername}
                      onChange={(e) => setEditUsername(e.target.value)}
                      className="bg-gray-800 border-gray-700 text-white"
                    />
                    {error && <p className="text-sm text-red-400">{error}</p>}
                    <div className="flex gap-2">
                      <Button
                        size="sm"
                        onClick={handleSaveProfile}
                        disabled={isSaving}
                        className="bg-orange-500 hover:bg-orange-600"
                      >
                        {isSaving ? (
                          <Loader2 className="w-4 h-4 animate-spin" />
                        ) : (
                          <Save className="w-4 h-4" />
                        )}
                      </Button>
                      <Button
                        size="sm"
                        variant="outline"
                        onClick={() => {
                          setIsEditing(false);
                          setEditUsername(profile.username);
                          setError('');
                        }}
                      >
                        <X className="w-4 h-4" />
                      </Button>
                    </div>
                  </div>
                ) : (
                  <>
                    <p className="font-semibold text-white">{profile.username}</p>
                    <div
                      className={`inline-flex items-center gap-1 px-2 py-0.5 rounded-full text-xs ${getTierColor(
                        profile.subscription?.tier || 'free'
                      )}`}
                    >
                      <Crown className="w-3 h-3" />
                      {getTierLabel(profile.subscription?.tier || 'free')}
                    </div>
                  </>
                )}
              </div>
            </div>

            {/* Email */}
            <div className="flex items-center gap-3 text-gray-400">
              <Mail className="w-4 h-4" />
              <span>{profile.email}</span>
            </div>

            {/* Member since */}
            <div className="flex items-center gap-3 text-gray-400">
              <Calendar className="w-4 h-4" />
              <span>Membre depuis {formatDate(profile.createdAt)}</span>
            </div>
          </CardContent>
        </Card>

        {/* Stats */}
        <Card className="bg-gray-900 border-gray-800">
          <CardHeader>
            <CardTitle className="text-white">Statistiques</CardTitle>
          </CardHeader>
          <CardContent>
            <div className="grid grid-cols-3 gap-4">
              <div className="text-center p-3 bg-gray-800/50 rounded-lg">
                <ListVideo className="w-5 h-5 text-orange-400 mx-auto mb-1" />
                <p className="text-2xl font-bold text-white">{profile.stats.playlists}</p>
                <p className="text-xs text-gray-400">Playlists</p>
              </div>
              <div className="text-center p-3 bg-gray-800/50 rounded-lg">
                <Heart className="w-5 h-5 text-red-400 mx-auto mb-1" />
                <p className="text-2xl font-bold text-white">{profile.stats.favorites}</p>
                <p className="text-xs text-gray-400">Favoris</p>
              </div>
              <div className="text-center p-3 bg-gray-800/50 rounded-lg">
                <History className="w-5 h-5 text-blue-400 mx-auto mb-1" />
                <p className="text-2xl font-bold text-white">{profile.stats.watchHistory}</p>
                <p className="text-xs text-gray-400">Vus</p>
              </div>
            </div>
          </CardContent>
        </Card>

        {/* Security */}
        <Card className="bg-gray-900 border-gray-800 md:col-span-2">
          <CardHeader>
            <CardTitle className="text-white flex items-center gap-2">
              <Lock className="w-5 h-5" />
              Sécurité
            </CardTitle>
          </CardHeader>
          <CardContent>
            {!showPasswordForm ? (
              <Button
                variant="outline"
                onClick={() => setShowPasswordForm(true)}
                className="border-gray-700"
              >
                Changer le mot de passe
              </Button>
            ) : (
              <form onSubmit={handleChangePassword} className="space-y-4 max-w-md">
                <div>
                  <label className="block text-sm text-gray-400 mb-1">
                    Mot de passe actuel
                  </label>
                  <Input
                    type="password"
                    value={currentPassword}
                    onChange={(e) => setCurrentPassword(e.target.value)}
                    className="bg-gray-800 border-gray-700 text-white"
                    required
                  />
                </div>
                <div>
                  <label className="block text-sm text-gray-400 mb-1">
                    Nouveau mot de passe
                  </label>
                  <Input
                    type="password"
                    value={newPassword}
                    onChange={(e) => setNewPassword(e.target.value)}
                    className="bg-gray-800 border-gray-700 text-white"
                    required
                  />
                </div>
                <div>
                  <label className="block text-sm text-gray-400 mb-1">
                    Confirmer le mot de passe
                  </label>
                  <Input
                    type="password"
                    value={confirmPassword}
                    onChange={(e) => setConfirmPassword(e.target.value)}
                    className="bg-gray-800 border-gray-700 text-white"
                    required
                  />
                </div>

                {passwordError && (
                  <p className="text-sm text-red-400">{passwordError}</p>
                )}

                <div className="flex gap-2">
                  <Button
                    type="submit"
                    disabled={isChangingPassword}
                    className="bg-orange-500 hover:bg-orange-600"
                  >
                    {isChangingPassword ? (
                      <Loader2 className="w-4 h-4 animate-spin mr-2" />
                    ) : null}
                    Modifier
                  </Button>
                  <Button
                    type="button"
                    variant="outline"
                    onClick={() => {
                      setShowPasswordForm(false);
                      setPasswordError('');
                      setCurrentPassword('');
                      setNewPassword('');
                      setConfirmPassword('');
                    }}
                    className="border-gray-700"
                  >
                    Annuler
                  </Button>
                </div>
              </form>
            )}
          </CardContent>
        </Card>
      </div>
    </div>
  );
}
