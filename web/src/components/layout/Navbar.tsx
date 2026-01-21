'use client';

import Link from 'next/link';
import { usePathname, useRouter } from 'next/navigation';
import { Play, Search, Bell, User, LogOut, Settings, Menu } from 'lucide-react';
import { Button } from '@/components/ui/button';
import { Input } from '@/components/ui/input';
import { useState } from 'react';
import { cn } from '@/lib/utils';

const navLinks = [
  { href: '/dashboard', label: 'Accueil' },
  { href: '/live', label: 'TV en direct' },
  { href: '/films', label: 'Films' },
  { href: '/series', label: 'Series' },
  { href: '/epg', label: 'Guide TV' },
];

interface NavbarProps {
  username?: string;
  onMenuClick?: () => void;
}

export function Navbar({ username = 'Utilisateur', onMenuClick }: NavbarProps) {
  const pathname = usePathname();
  const router = useRouter();
  const [showSearch, setShowSearch] = useState(false);
  const [showUserMenu, setShowUserMenu] = useState(false);
  const [searchQuery, setSearchQuery] = useState('');

  const handleSearch = (e: React.FormEvent) => {
    e.preventDefault();
    if (searchQuery.trim()) {
      router.push(`/search?q=${encodeURIComponent(searchQuery)}`);
      setShowSearch(false);
    }
  };

  const handleLogout = async () => {
    const refreshToken = localStorage.getItem('refreshToken');
    if (refreshToken) {
      await fetch('/api/auth/logout', {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({ refreshToken }),
      });
    }
    localStorage.removeItem('accessToken');
    localStorage.removeItem('refreshToken');
    router.push('/login');
  };

  return (
    <nav className="fixed top-0 left-0 right-0 z-50 bg-background/95 backdrop-blur-lg border-b border-border">
      <div className="max-w-[1800px] mx-auto px-4">
        <div className="flex items-center justify-between h-16">
          {/* Left: Logo + Nav */}
          <div className="flex items-center gap-8">
            <button
              onClick={onMenuClick}
              className="lg:hidden p-2 hover:bg-card-hover rounded-lg"
            >
              <Menu className="w-5 h-5" />
            </button>

            <Link href="/dashboard" className="flex items-center gap-2">
              <div className="w-9 h-9 bg-primary rounded-lg flex items-center justify-center">
                <Play className="w-5 h-5 text-white" fill="white" />
              </div>
              <span className="text-lg font-bold text-white hidden sm:block">Visiora</span>
            </Link>

            <div className="hidden lg:flex items-center gap-1">
              {navLinks.map((link) => (
                <Link
                  key={link.href}
                  href={link.href}
                  className={cn(
                    'px-3 py-2 text-sm font-medium rounded-lg transition-colors',
                    pathname === link.href || pathname.startsWith(link.href + '/')
                      ? 'text-primary bg-primary/10'
                      : 'text-muted-foreground hover:text-white hover:bg-card-hover'
                  )}
                >
                  {link.label}
                </Link>
              ))}
            </div>
          </div>

          {/* Right: Search + User */}
          <div className="flex items-center gap-2">
            {/* Search */}
            <div className="relative">
              {showSearch ? (
                <form onSubmit={handleSearch} className="absolute right-0 top-1/2 -translate-y-1/2">
                  <Input
                    type="search"
                    placeholder="Rechercher..."
                    value={searchQuery}
                    onChange={(e) => setSearchQuery(e.target.value)}
                    className="w-64"
                    autoFocus
                    onBlur={() => !searchQuery && setShowSearch(false)}
                  />
                </form>
              ) : (
                <Button
                  variant="ghost"
                  size="icon"
                  onClick={() => setShowSearch(true)}
                >
                  <Search className="w-5 h-5" />
                </Button>
              )}
            </div>

            {/* Notifications */}
            <Button variant="ghost" size="icon" className="relative">
              <Bell className="w-5 h-5" />
              <span className="absolute top-1.5 right-1.5 w-2 h-2 bg-primary rounded-full" />
            </Button>

            {/* User Menu */}
            <div className="relative">
              <Button
                variant="ghost"
                size="icon"
                onClick={() => setShowUserMenu(!showUserMenu)}
                className="relative"
              >
                <div className="w-8 h-8 bg-primary/20 rounded-full flex items-center justify-center">
                  <User className="w-4 h-4 text-primary" />
                </div>
              </Button>

              {showUserMenu && (
                <>
                  <div
                    className="fixed inset-0 z-10"
                    onClick={() => setShowUserMenu(false)}
                  />
                  <div className="absolute right-0 mt-2 w-56 bg-card border border-border rounded-xl shadow-xl z-20 py-2">
                    <div className="px-4 py-2 border-b border-border">
                      <p className="font-medium text-white">{username}</p>
                      <p className="text-xs text-muted-foreground">Compte gratuit</p>
                    </div>
                    <Link
                      href="/profile"
                      className="flex items-center gap-2 px-4 py-2 text-sm text-muted-foreground hover:text-white hover:bg-card-hover"
                      onClick={() => setShowUserMenu(false)}
                    >
                      <User className="w-4 h-4" />
                      Mon profil
                    </Link>
                    <Link
                      href="/settings"
                      className="flex items-center gap-2 px-4 py-2 text-sm text-muted-foreground hover:text-white hover:bg-card-hover"
                      onClick={() => setShowUserMenu(false)}
                    >
                      <Settings className="w-4 h-4" />
                      Parametres
                    </Link>
                    <button
                      onClick={handleLogout}
                      className="flex items-center gap-2 px-4 py-2 text-sm text-destructive hover:bg-card-hover w-full"
                    >
                      <LogOut className="w-4 h-4" />
                      Deconnexion
                    </button>
                  </div>
                </>
              )}
            </div>
          </div>
        </div>
      </div>
    </nav>
  );
}
