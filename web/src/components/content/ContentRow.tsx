'use client';

import { useRef } from 'react';
import { ChevronLeft, ChevronRight } from 'lucide-react';
import { Button } from '@/components/ui/button';
import { ContentCard } from './ContentCard';
import { cn } from '@/lib/utils';

interface ContentItem {
  id: string;
  title: string;
  imageUrl?: string;
  type: 'movie' | 'series' | 'channel';
  year?: number;
  rating?: number;
  progress?: number;
}

interface ContentRowProps {
  title: string;
  items: ContentItem[];
  seeAllHref?: string;
  className?: string;
}

export function ContentRow({ title, items, seeAllHref, className }: ContentRowProps) {
  const scrollRef = useRef<HTMLDivElement>(null);

  const scroll = (direction: 'left' | 'right') => {
    if (!scrollRef.current) return;
    const scrollAmount = scrollRef.current.clientWidth * 0.8;
    scrollRef.current.scrollBy({
      left: direction === 'left' ? -scrollAmount : scrollAmount,
      behavior: 'smooth',
    });
  };

  if (items.length === 0) return null;

  return (
    <section className={cn('relative', className)}>
      {/* Header */}
      <div className="flex items-center justify-between mb-4">
        <h2 className="text-xl font-semibold text-white">{title}</h2>
        {seeAllHref && (
          <a
            href={seeAllHref}
            className="text-sm text-primary hover:underline"
          >
            Voir tout
          </a>
        )}
      </div>

      {/* Scroll container */}
      <div className="relative group">
        {/* Left arrow */}
        <Button
          variant="ghost"
          size="icon"
          className="absolute left-0 top-1/2 -translate-y-1/2 z-10 bg-background/80 opacity-0 group-hover:opacity-100 transition-opacity -translate-x-1/2"
          onClick={() => scroll('left')}
        >
          <ChevronLeft className="w-6 h-6" />
        </Button>

        {/* Items */}
        <div
          ref={scrollRef}
          className="flex gap-4 overflow-x-auto scrollbar-hide scroll-smooth pb-4 -mb-4"
          style={{ scrollbarWidth: 'none', msOverflowStyle: 'none' }}
        >
          {items.map((item) => (
            <ContentCard
              key={item.id}
              id={item.id}
              title={item.title}
              imageUrl={item.imageUrl}
              type={item.type}
              year={item.year}
              rating={item.rating}
              progress={item.progress}
              className="w-[160px] sm:w-[180px] shrink-0"
            />
          ))}
        </div>

        {/* Right arrow */}
        <Button
          variant="ghost"
          size="icon"
          className="absolute right-0 top-1/2 -translate-y-1/2 z-10 bg-background/80 opacity-0 group-hover:opacity-100 transition-opacity translate-x-1/2"
          onClick={() => scroll('right')}
        >
          <ChevronRight className="w-6 h-6" />
        </Button>
      </div>
    </section>
  );
}
