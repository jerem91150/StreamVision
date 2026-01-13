'use client';

import Image from 'next/image';
import Link from 'next/link';
import { Play, Tv } from 'lucide-react';
import { cn } from '@/lib/utils';

interface ChannelCardProps {
  id: string;
  name: string;
  logoUrl?: string;
  currentProgram?: string;
  number?: number;
  className?: string;
}

export function ChannelCard({
  id,
  name,
  logoUrl,
  currentProgram,
  number,
  className,
}: ChannelCardProps) {
  return (
    <Link
      href={`/player/live/${id}`}
      className={cn(
        'group relative block rounded-xl overflow-hidden bg-card border border-border hover:border-primary/50 transition-all',
        className
      )}
    >
      <div className="aspect-video relative bg-card-hover p-4 flex items-center justify-center">
        {logoUrl ? (
          <Image
            src={logoUrl}
            alt={name}
            fill
            className="object-contain p-4"
            sizes="(max-width: 768px) 50vw, 25vw"
          />
        ) : (
          <Tv className="w-12 h-12 text-muted-foreground" />
        )}

        {/* Channel number */}
        {number && (
          <span className="absolute top-2 left-2 text-xs font-medium text-muted-foreground bg-background/80 px-1.5 py-0.5 rounded">
            {number}
          </span>
        )}

        {/* Play overlay */}
        <div className="absolute inset-0 bg-black/60 flex items-center justify-center opacity-0 group-hover:opacity-100 transition-opacity">
          <div className="w-12 h-12 bg-primary rounded-full flex items-center justify-center">
            <Play className="w-6 h-6 text-white" fill="white" />
          </div>
        </div>
      </div>

      <div className="p-3">
        <h3 className="font-medium text-white text-sm line-clamp-1">{name}</h3>
        {currentProgram && (
          <p className="text-xs text-muted-foreground line-clamp-1 mt-0.5">
            {currentProgram}
          </p>
        )}
      </div>
    </Link>
  );
}
