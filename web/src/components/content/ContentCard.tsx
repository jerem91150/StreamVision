'use client';

import Image from 'next/image';
import Link from 'next/link';
import { Play, Heart, Info } from 'lucide-react';
import { cn } from '@/lib/utils';
import { Button } from '@/components/ui/button';

interface ContentCardProps {
  id: string;
  title: string;
  imageUrl?: string;
  type: 'movie' | 'series' | 'channel';
  year?: number;
  rating?: number;
  progress?: number;
  className?: string;
}

export function ContentCard({
  id,
  title,
  imageUrl,
  type,
  year,
  rating,
  progress,
  className,
}: ContentCardProps) {
  const href = type === 'channel' ? `/player/live/${id}` : `/${type === 'movie' ? 'films' : 'series'}/${id}`;

  return (
    <Link
      href={href}
      className={cn(
        'group relative block rounded-xl overflow-hidden bg-card border border-border content-card',
        className
      )}
    >
      {/* Image */}
      <div className="aspect-[2/3] relative bg-card-hover">
        {imageUrl ? (
          <Image
            src={imageUrl}
            alt={title}
            fill
            className="object-cover"
            sizes="(max-width: 768px) 50vw, (max-width: 1200px) 33vw, 20vw"
          />
        ) : (
          <div className="w-full h-full flex items-center justify-center">
            <Play className="w-12 h-12 text-muted-foreground" />
          </div>
        )}

        {/* Progress bar */}
        {progress !== undefined && progress > 0 && (
          <div className="absolute bottom-0 left-0 right-0 h-1 bg-card">
            <div
              className="h-full bg-primary"
              style={{ width: `${progress}%` }}
            />
          </div>
        )}

        {/* Hover overlay */}
        <div className="absolute inset-0 bg-gradient-to-t from-black/80 via-black/20 to-transparent opacity-0 group-hover:opacity-100 transition-opacity duration-300">
          <div className="absolute bottom-4 left-4 right-4 flex items-center gap-2">
            <Button size="sm" className="flex-1 gap-1">
              <Play className="w-4 h-4" />
              Lecture
            </Button>
            <Button size="icon" variant="outline" className="shrink-0">
              <Heart className="w-4 h-4" />
            </Button>
            <Button size="icon" variant="outline" className="shrink-0">
              <Info className="w-4 h-4" />
            </Button>
          </div>
        </div>
      </div>

      {/* Info */}
      <div className="p-3">
        <h3 className="font-medium text-white text-sm line-clamp-1">{title}</h3>
        <div className="flex items-center gap-2 mt-1">
          {year && <span className="text-xs text-muted-foreground">{year}</span>}
          {rating && (
            <span className="text-xs text-primary font-medium">
              {rating.toFixed(1)}
            </span>
          )}
        </div>
      </div>
    </Link>
  );
}
