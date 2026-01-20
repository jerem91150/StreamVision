'use client';

import { useState, useEffect, useRef } from 'react';
import { useRouter } from 'next/navigation';
import Image from 'next/image';
import Link from 'next/link';
import {
  Calendar,
  ChevronLeft,
  ChevronRight,
  Clock,
  Loader2,
  Tv,
  Play,
} from 'lucide-react';
import { Button } from '@/components/ui/button';

interface Channel {
  id: string;
  name: string;
  logoUrl: string | null;
  number: number | null;
}

interface Program {
  id: string;
  channelId: string;
  title: string;
  description: string | null;
  startTime: string;
  endTime: string;
  category: string | null;
}

interface EpgData {
  channels: Channel[];
  programsByChannel: Record<string, Program[]>;
  timeRange: {
    start: string;
    end: string;
  };
}

export default function EpgPage() {
  const router = useRouter();
  const [epgData, setEpgData] = useState<EpgData | null>(null);
  const [isLoading, setIsLoading] = useState(true);
  const [selectedDate, setSelectedDate] = useState(new Date());
  const [selectedProgram, setSelectedProgram] = useState<Program | null>(null);
  const timelineRef = useRef<HTMLDivElement>(null);

  useEffect(() => {
    const fetchEpg = async () => {
      setIsLoading(true);
      try {
        const token = localStorage.getItem('accessToken');
        const dateStr = selectedDate.toISOString().split('T')[0];
        const response = await fetch(`/api/epg?date=${dateStr}`, {
          headers: { Authorization: `Bearer ${token}` },
        });

        if (response.status === 401) {
          router.push('/login');
          return;
        }

        if (response.ok) {
          const data = await response.json();
          setEpgData(data);
        }
      } catch (err) {
        console.error('Failed to fetch EPG:', err);
      } finally {
        setIsLoading(false);
      }
    };

    fetchEpg();
  }, [selectedDate, router]);

  // Generate time slots for the timeline
  const generateTimeSlots = () => {
    const slots = [];
    for (let hour = 0; hour < 24; hour++) {
      slots.push(`${hour.toString().padStart(2, '0')}:00`);
    }
    return slots;
  };

  const timeSlots = generateTimeSlots();

  // Calculate program position and width
  const calculateProgramStyle = (program: Program) => {
    const startTime = new Date(program.startTime);
    const endTime = new Date(program.endTime);

    const dayStart = new Date(selectedDate);
    dayStart.setHours(0, 0, 0, 0);

    const startMinutes = (startTime.getTime() - dayStart.getTime()) / 60000;
    const durationMinutes = (endTime.getTime() - startTime.getTime()) / 60000;

    // Each hour is 150px wide
    const left = (startMinutes / 60) * 150;
    const width = (durationMinutes / 60) * 150;

    return {
      left: `${Math.max(0, left)}px`,
      width: `${Math.max(50, width)}px`,
    };
  };

  const formatTime = (dateStr: string) => {
    return new Date(dateStr).toLocaleTimeString('fr-FR', {
      hour: '2-digit',
      minute: '2-digit',
    });
  };

  const isCurrentlyAiring = (program: Program) => {
    const now = new Date();
    const start = new Date(program.startTime);
    const end = new Date(program.endTime);
    return now >= start && now < end;
  };

  const goToPreviousDay = () => {
    const newDate = new Date(selectedDate);
    newDate.setDate(newDate.getDate() - 1);
    setSelectedDate(newDate);
  };

  const goToNextDay = () => {
    const newDate = new Date(selectedDate);
    newDate.setDate(newDate.getDate() + 1);
    setSelectedDate(newDate);
  };

  const goToToday = () => {
    setSelectedDate(new Date());
  };

  const scrollToNow = () => {
    if (timelineRef.current) {
      const now = new Date();
      const dayStart = new Date(selectedDate);
      dayStart.setHours(0, 0, 0, 0);

      if (now.toDateString() === selectedDate.toDateString()) {
        const minutesSinceMidnight = (now.getTime() - dayStart.getTime()) / 60000;
        const scrollPosition = (minutesSinceMidnight / 60) * 150 - 300;
        timelineRef.current.scrollLeft = Math.max(0, scrollPosition);
      }
    }
  };

  useEffect(() => {
    if (epgData) {
      scrollToNow();
    }
  }, [epgData]);

  return (
    <div className="h-full flex flex-col">
      {/* Header */}
      <div className="flex items-center justify-between p-4 border-b border-gray-800">
        <div className="flex items-center gap-4">
          <h1 className="text-xl font-bold text-white flex items-center gap-2">
            <Calendar className="w-5 h-5 text-orange-400" />
            Guide TV
          </h1>

          <div className="flex items-center gap-2">
            <Button variant="ghost" size="sm" onClick={goToPreviousDay}>
              <ChevronLeft className="w-4 h-4" />
            </Button>
            <span className="text-white min-w-[150px] text-center">
              {selectedDate.toLocaleDateString('fr-FR', {
                weekday: 'long',
                day: 'numeric',
                month: 'long',
              })}
            </span>
            <Button variant="ghost" size="sm" onClick={goToNextDay}>
              <ChevronRight className="w-4 h-4" />
            </Button>
          </div>

          <Button variant="outline" size="sm" onClick={goToToday}>
            Aujourd&apos;hui
          </Button>
        </div>

        <Button variant="outline" size="sm" onClick={scrollToNow}>
          <Clock className="w-4 h-4 mr-2" />
          Maintenant
        </Button>
      </div>

      {/* Loading */}
      {isLoading && (
        <div className="flex-1 flex items-center justify-center">
          <Loader2 className="w-8 h-8 animate-spin text-orange-500" />
        </div>
      )}

      {/* No data */}
      {!isLoading && (!epgData || epgData.channels.length === 0) && (
        <div className="flex-1 flex flex-col items-center justify-center text-center p-6">
          <div className="w-16 h-16 bg-gray-800 rounded-full flex items-center justify-center mb-4">
            <Tv className="w-8 h-8 text-gray-400" />
          </div>
          <h3 className="text-lg font-medium text-white mb-2">
            Pas de guide TV disponible
          </h3>
          <p className="text-gray-400 max-w-md">
            Ajoutez une playlist avec une URL EPG ou synchronisez une playlist Xtream
            pour voir le guide des programmes.
          </p>
        </div>
      )}

      {/* EPG Grid */}
      {!isLoading && epgData && epgData.channels.length > 0 && (
        <div className="flex-1 overflow-hidden">
          <div className="h-full flex">
            {/* Channel list (fixed) */}
            <div className="w-48 flex-shrink-0 border-r border-gray-800 bg-gray-900">
              {/* Time header spacer */}
              <div className="h-10 border-b border-gray-800" />

              {/* Channel rows */}
              <div className="overflow-y-auto" style={{ height: 'calc(100% - 40px)' }}>
                {epgData.channels.map((channel) => (
                  <div
                    key={channel.id}
                    className="h-20 px-3 flex items-center gap-3 border-b border-gray-800 hover:bg-gray-800/50"
                  >
                    <div className="w-10 h-10 rounded bg-gray-800 flex items-center justify-center overflow-hidden flex-shrink-0">
                      {channel.logoUrl ? (
                        <Image
                          src={channel.logoUrl}
                          alt={channel.name}
                          width={40}
                          height={40}
                          className="object-contain"
                        />
                      ) : (
                        <Tv className="w-5 h-5 text-gray-500" />
                      )}
                    </div>
                    <div className="min-w-0">
                      <p className="text-sm font-medium text-white truncate">
                        {channel.name}
                      </p>
                      {channel.number && (
                        <p className="text-xs text-gray-500">Ch. {channel.number}</p>
                      )}
                    </div>
                  </div>
                ))}
              </div>
            </div>

            {/* Timeline and programs */}
            <div className="flex-1 overflow-hidden" ref={timelineRef}>
              <div className="overflow-x-auto h-full">
                <div style={{ width: `${24 * 150}px` }}>
                  {/* Time header */}
                  <div className="h-10 flex border-b border-gray-800 bg-gray-900 sticky top-0 z-10">
                    {timeSlots.map((slot) => (
                      <div
                        key={slot}
                        className="w-[150px] flex-shrink-0 px-2 flex items-center text-sm text-gray-400 border-l border-gray-800"
                      >
                        {slot}
                      </div>
                    ))}
                  </div>

                  {/* Program rows */}
                  <div className="overflow-y-auto" style={{ height: 'calc(100% - 40px)' }}>
                    {epgData.channels.map((channel) => (
                      <div
                        key={channel.id}
                        className="h-20 relative border-b border-gray-800"
                      >
                        {/* Time grid lines */}
                        {timeSlots.map((slot, idx) => (
                          <div
                            key={slot}
                            className="absolute top-0 bottom-0 border-l border-gray-800/50"
                            style={{ left: `${idx * 150}px` }}
                          />
                        ))}

                        {/* Programs */}
                        {epgData.programsByChannel[channel.id]?.map((program) => {
                          const style = calculateProgramStyle(program);
                          const isCurrent = isCurrentlyAiring(program);

                          return (
                            <div
                              key={program.id}
                              className={`absolute top-1 bottom-1 rounded px-2 py-1 cursor-pointer transition-colors overflow-hidden ${
                                isCurrent
                                  ? 'bg-orange-500/30 border border-orange-500'
                                  : 'bg-gray-800 hover:bg-gray-700 border border-gray-700'
                              }`}
                              style={style}
                              onClick={() => setSelectedProgram(program)}
                            >
                              <p className="text-sm font-medium text-white truncate">
                                {program.title}
                              </p>
                              <p className="text-xs text-gray-400">
                                {formatTime(program.startTime)} -{' '}
                                {formatTime(program.endTime)}
                              </p>
                            </div>
                          );
                        })}
                      </div>
                    ))}
                  </div>
                </div>
              </div>
            </div>
          </div>
        </div>
      )}

      {/* Program detail modal */}
      {selectedProgram && (
        <div
          className="fixed inset-0 bg-black/70 flex items-center justify-center p-4 z-50"
          onClick={() => setSelectedProgram(null)}
        >
          <div
            className="bg-gray-900 rounded-xl max-w-lg w-full p-6"
            onClick={(e) => e.stopPropagation()}
          >
            <h2 className="text-xl font-bold text-white mb-2">
              {selectedProgram.title}
            </h2>

            <div className="flex items-center gap-4 text-sm text-gray-400 mb-4">
              <span>
                {formatTime(selectedProgram.startTime)} -{' '}
                {formatTime(selectedProgram.endTime)}
              </span>
              {selectedProgram.category && (
                <span className="px-2 py-0.5 bg-gray-800 rounded">
                  {selectedProgram.category}
                </span>
              )}
            </div>

            {selectedProgram.description && (
              <p className="text-gray-300 mb-4">{selectedProgram.description}</p>
            )}

            <div className="flex gap-3">
              <Link href={`/player/live/${selectedProgram.channelId}`}>
                <Button className="bg-orange-500 hover:bg-orange-600">
                  <Play className="w-4 h-4 mr-2" />
                  Regarder
                </Button>
              </Link>
              <Button
                variant="outline"
                onClick={() => setSelectedProgram(null)}
              >
                Fermer
              </Button>
            </div>
          </div>
        </div>
      )}
    </div>
  );
}
