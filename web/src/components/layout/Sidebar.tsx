'use client';

import Link from 'next/link';
import { usePathname } from 'next/navigation';
import {
  Home,
  Tv,
  Film,
  Clapperboard,
  Calendar,
  Heart,
  History,
  Settings,
  Plus,
  X,
} from 'lucide-react';
import { cn } from '@/lib/utils';
import { Button } from '@/components/ui/button';

const mainNavItems = [
  { href: '/dashboard', icon: Home, label: 'Accueil' },
  { href: '/live', icon: Tv, label: 'TV en direct' },
  { href: '/films', icon: Film, label: 'Films' },
  { href: '/series', icon: Clapperboard, label: 'Series' },
  { href: '/epg', icon: Calendar, label: 'Guide TV' },
];

const libraryItems = [
  { href: '/favorites', icon: Heart, label: 'Favoris' },
  { href: '/history', icon: History, label: 'Historique' },
];

interface SidebarProps {
  isOpen?: boolean;
  onClose?: () => void;
}

export function Sidebar({ isOpen = true, onClose }: SidebarProps) {
  const pathname = usePathname();

  return (
    <>
      {/* Mobile overlay */}
      {isOpen && (
        <div
          className="fixed inset-0 bg-black/50 z-40 lg:hidden"
          onClick={onClose}
        />
      )}

      {/* Sidebar */}
      <aside
        className={cn(
          'fixed top-16 left-0 bottom-0 w-64 bg-card border-r border-border z-40 transition-transform duration-300',
          'lg:translate-x-0',
          isOpen ? 'translate-x-0' : '-translate-x-full'
        )}
      >
        <div className="flex flex-col h-full p-4">
          {/* Mobile close button */}
          <div className="flex justify-end lg:hidden mb-4">
            <Button variant="ghost" size="icon" onClick={onClose}>
              <X className="w-5 h-5" />
            </Button>
          </div>

          {/* Main Navigation */}
          <nav className="space-y-1">
            {mainNavItems.map((item) => (
              <Link
                key={item.href}
                href={item.href}
                onClick={onClose}
                className={cn(
                  'flex items-center gap-3 px-3 py-2.5 rounded-lg text-sm font-medium transition-colors',
                  pathname === item.href || pathname.startsWith(item.href + '/')
                    ? 'bg-primary/10 text-primary'
                    : 'text-muted-foreground hover:text-white hover:bg-card-hover'
                )}
              >
                <item.icon className="w-5 h-5" />
                {item.label}
              </Link>
            ))}
          </nav>

          {/* Divider */}
          <div className="my-6 border-t border-border" />

          {/* Library */}
          <div>
            <h3 className="px-3 text-xs font-semibold text-muted-foreground uppercase tracking-wider mb-2">
              Ma bibliotheque
            </h3>
            <nav className="space-y-1">
              {libraryItems.map((item) => (
                <Link
                  key={item.href}
                  href={item.href}
                  onClick={onClose}
                  className={cn(
                    'flex items-center gap-3 px-3 py-2.5 rounded-lg text-sm font-medium transition-colors',
                    pathname === item.href
                      ? 'bg-primary/10 text-primary'
                      : 'text-muted-foreground hover:text-white hover:bg-card-hover'
                  )}
                >
                  <item.icon className="w-5 h-5" />
                  {item.label}
                </Link>
              ))}
            </nav>
          </div>

          {/* Divider */}
          <div className="my-6 border-t border-border" />

          {/* Playlists */}
          <div className="flex-1 overflow-y-auto">
            <div className="flex items-center justify-between px-3 mb-2">
              <h3 className="text-xs font-semibold text-muted-foreground uppercase tracking-wider">
                Mes playlists
              </h3>
              <Link href="/settings/playlists">
                <Button variant="ghost" size="icon" className="w-6 h-6">
                  <Plus className="w-4 h-4" />
                </Button>
              </Link>
            </div>
            <p className="px-3 text-xs text-muted-foreground">
              Ajoutez votre playlist M3U ou Xtream pour commencer.
            </p>
          </div>

          {/* Settings */}
          <div className="mt-auto pt-4 border-t border-border">
            <Link
              href="/settings"
              onClick={onClose}
              className={cn(
                'flex items-center gap-3 px-3 py-2.5 rounded-lg text-sm font-medium transition-colors',
                pathname === '/settings'
                  ? 'bg-primary/10 text-primary'
                  : 'text-muted-foreground hover:text-white hover:bg-card-hover'
              )}
            >
              <Settings className="w-5 h-5" />
              Parametres
            </Link>
          </div>
        </div>
      </aside>
    </>
  );
}
