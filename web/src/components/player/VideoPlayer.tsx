'use client';

import { useEffect, useRef, useState, useCallback } from 'react';
import Hls from 'hls.js';
import {
  Play,
  Pause,
  Volume2,
  VolumeX,
  Maximize,
  Minimize,
  Settings,
  SkipBack,
  SkipForward,
  Loader2,
} from 'lucide-react';
import { cn } from '@/lib/utils';
import { Button } from '@/components/ui/button';

interface VideoPlayerProps {
  src: string;
  title?: string;
  poster?: string;
  autoPlay?: boolean;
  onProgress?: (progress: number, duration: number) => void;
  onEnded?: () => void;
  className?: string;
}

export function VideoPlayer({
  src,
  title,
  poster,
  autoPlay = false,
  onProgress,
  onEnded,
  className,
}: VideoPlayerProps) {
  const videoRef = useRef<HTMLVideoElement>(null);
  const containerRef = useRef<HTMLDivElement>(null);
  const hlsRef = useRef<Hls | null>(null);
  const progressInterval = useRef<NodeJS.Timeout | null>(null);

  const [isPlaying, setIsPlaying] = useState(false);
  const [isMuted, setIsMuted] = useState(false);
  const [isFullscreen, setIsFullscreen] = useState(false);
  const [isLoading, setIsLoading] = useState(true);
  const [showControls, setShowControls] = useState(true);
  const [currentTime, setCurrentTime] = useState(0);
  const [duration, setDuration] = useState(0);
  const [volume, setVolume] = useState(1);
  const [qualities, setQualities] = useState<{ height: number; level: number }[]>([]);
  const [currentQuality, setCurrentQuality] = useState(-1);
  const [showSettings, setShowSettings] = useState(false);
  const [error, setError] = useState<string | null>(null);

  // Initialize HLS
  useEffect(() => {
    const video = videoRef.current;
    if (!video || !src) return;

    setIsLoading(true);
    setError(null);

    // Check if HLS is supported
    if (Hls.isSupported()) {
      const hls = new Hls({
        enableWorker: true,
        lowLatencyMode: true,
        backBufferLength: 90,
      });

      hlsRef.current = hls;

      hls.loadSource(src);
      hls.attachMedia(video);

      hls.on(Hls.Events.MANIFEST_PARSED, (_, data) => {
        setIsLoading(false);
        // Get available qualities
        const levels = data.levels.map((level, index) => ({
          height: level.height,
          level: index,
        }));
        setQualities(levels);

        if (autoPlay) {
          video.play().catch(() => {});
        }
      });

      hls.on(Hls.Events.LEVEL_SWITCHED, (_, data) => {
        setCurrentQuality(data.level);
      });

      hls.on(Hls.Events.ERROR, (_, data) => {
        if (data.fatal) {
          switch (data.type) {
            case Hls.ErrorTypes.NETWORK_ERROR:
              setError('Erreur reseau - Tentative de reconnexion...');
              hls.startLoad();
              break;
            case Hls.ErrorTypes.MEDIA_ERROR:
              setError('Erreur media - Tentative de recuperation...');
              hls.recoverMediaError();
              break;
            default:
              setError('Erreur de lecture video');
              hls.destroy();
              break;
          }
        }
      });

      return () => {
        hls.destroy();
      };
    } else if (video.canPlayType('application/vnd.apple.mpegurl')) {
      // Native HLS support (Safari)
      video.src = src;
      video.addEventListener('loadedmetadata', () => {
        setIsLoading(false);
        if (autoPlay) {
          video.play().catch(() => {});
        }
      });
    } else {
      setError('HLS non supporte par votre navigateur');
    }
  }, [src, autoPlay]);

  // Event listeners
  useEffect(() => {
    const video = videoRef.current;
    if (!video) return;

    const handlePlay = () => setIsPlaying(true);
    const handlePause = () => setIsPlaying(false);
    const handleTimeUpdate = () => {
      setCurrentTime(video.currentTime);
      setDuration(video.duration || 0);
    };
    const handleEnded = () => {
      setIsPlaying(false);
      onEnded?.();
    };
    const handleWaiting = () => setIsLoading(true);
    const handleCanPlay = () => setIsLoading(false);

    video.addEventListener('play', handlePlay);
    video.addEventListener('pause', handlePause);
    video.addEventListener('timeupdate', handleTimeUpdate);
    video.addEventListener('ended', handleEnded);
    video.addEventListener('waiting', handleWaiting);
    video.addEventListener('canplay', handleCanPlay);

    return () => {
      video.removeEventListener('play', handlePlay);
      video.removeEventListener('pause', handlePause);
      video.removeEventListener('timeupdate', handleTimeUpdate);
      video.removeEventListener('ended', handleEnded);
      video.removeEventListener('waiting', handleWaiting);
      video.removeEventListener('canplay', handleCanPlay);
    };
  }, [onEnded]);

  // Progress reporting
  useEffect(() => {
    if (progressInterval.current) {
      clearInterval(progressInterval.current);
    }

    if (isPlaying && onProgress) {
      progressInterval.current = setInterval(() => {
        if (videoRef.current) {
          onProgress(videoRef.current.currentTime, videoRef.current.duration || 0);
        }
      }, 10000); // Report every 10 seconds
    }

    return () => {
      if (progressInterval.current) {
        clearInterval(progressInterval.current);
      }
    };
  }, [isPlaying, onProgress]);

  // Fullscreen change listener
  useEffect(() => {
    const handleFullscreenChange = () => {
      setIsFullscreen(!!document.fullscreenElement);
    };

    document.addEventListener('fullscreenchange', handleFullscreenChange);
    return () => {
      document.removeEventListener('fullscreenchange', handleFullscreenChange);
    };
  }, []);

  // Auto-hide controls
  useEffect(() => {
    let timeout: NodeJS.Timeout;

    if (isPlaying && showControls) {
      timeout = setTimeout(() => setShowControls(false), 3000);
    }

    return () => clearTimeout(timeout);
  }, [isPlaying, showControls]);

  const togglePlay = useCallback(() => {
    const video = videoRef.current;
    if (!video) return;

    if (isPlaying) {
      video.pause();
    } else {
      video.play().catch(() => {});
    }
  }, [isPlaying]);

  const toggleMute = useCallback(() => {
    const video = videoRef.current;
    if (!video) return;

    video.muted = !video.muted;
    setIsMuted(video.muted);
  }, []);

  const handleVolumeChange = useCallback((e: React.ChangeEvent<HTMLInputElement>) => {
    const video = videoRef.current;
    if (!video) return;

    const newVolume = parseFloat(e.target.value);
    video.volume = newVolume;
    setVolume(newVolume);
    setIsMuted(newVolume === 0);
  }, []);

  const handleSeek = useCallback((e: React.ChangeEvent<HTMLInputElement>) => {
    const video = videoRef.current;
    if (!video) return;

    const time = parseFloat(e.target.value);
    video.currentTime = time;
    setCurrentTime(time);
  }, []);

  const skip = useCallback((seconds: number) => {
    const video = videoRef.current;
    if (!video) return;

    video.currentTime = Math.max(0, Math.min(video.currentTime + seconds, duration));
  }, [duration]);

  const toggleFullscreen = useCallback(() => {
    const container = containerRef.current;
    if (!container) return;

    if (isFullscreen) {
      document.exitFullscreen().catch(() => {});
    } else {
      container.requestFullscreen().catch(() => {});
    }
  }, [isFullscreen]);

  const setQuality = useCallback((level: number) => {
    if (hlsRef.current) {
      hlsRef.current.currentLevel = level;
    }
    setShowSettings(false);
  }, []);

  const formatTime = (seconds: number) => {
    const h = Math.floor(seconds / 3600);
    const m = Math.floor((seconds % 3600) / 60);
    const s = Math.floor(seconds % 60);

    if (h > 0) {
      return `${h}:${m.toString().padStart(2, '0')}:${s.toString().padStart(2, '0')}`;
    }
    return `${m}:${s.toString().padStart(2, '0')}`;
  };

  return (
    <div
      ref={containerRef}
      className={cn(
        'relative bg-black group player-container',
        isFullscreen ? 'fixed inset-0 z-50' : 'aspect-video rounded-xl overflow-hidden',
        className
      )}
      onMouseMove={() => setShowControls(true)}
      onMouseLeave={() => isPlaying && setShowControls(false)}
    >
      <video
        ref={videoRef}
        poster={poster}
        className="w-full h-full object-contain"
        onClick={togglePlay}
        playsInline
      />

      {/* Loading overlay */}
      {isLoading && (
        <div className="absolute inset-0 flex items-center justify-center bg-black/50">
          <Loader2 className="w-12 h-12 text-primary animate-spin" />
        </div>
      )}

      {/* Error overlay */}
      {error && (
        <div className="absolute inset-0 flex items-center justify-center bg-black/80">
          <div className="text-center">
            <p className="text-destructive mb-2">{error}</p>
            <Button onClick={() => window.location.reload()}>Recharger</Button>
          </div>
        </div>
      )}

      {/* Controls */}
      <div
        className={cn(
          'absolute inset-0 flex flex-col justify-end bg-gradient-to-t from-black/80 via-transparent to-black/30 transition-opacity duration-300 player-controls',
          showControls ? 'opacity-100' : 'opacity-0'
        )}
      >
        {/* Title */}
        {title && (
          <div className="absolute top-0 left-0 right-0 p-4">
            <h2 className="text-lg font-semibold text-white">{title}</h2>
          </div>
        )}

        {/* Center play button */}
        <div className="absolute inset-0 flex items-center justify-center">
          <Button
            size="icon"
            variant="ghost"
            className="w-16 h-16 bg-white/20 hover:bg-white/30 rounded-full"
            onClick={togglePlay}
          >
            {isPlaying ? (
              <Pause className="w-8 h-8 text-white" />
            ) : (
              <Play className="w-8 h-8 text-white ml-1" />
            )}
          </Button>
        </div>

        {/* Bottom controls */}
        <div className="p-4 space-y-2">
          {/* Progress bar */}
          <div className="flex items-center gap-2">
            <span className="text-xs text-white w-12">{formatTime(currentTime)}</span>
            <input
              type="range"
              min={0}
              max={duration || 100}
              value={currentTime}
              onChange={handleSeek}
              className="flex-1 h-1 bg-white/30 rounded-full appearance-none cursor-pointer [&::-webkit-slider-thumb]:appearance-none [&::-webkit-slider-thumb]:w-3 [&::-webkit-slider-thumb]:h-3 [&::-webkit-slider-thumb]:bg-primary [&::-webkit-slider-thumb]:rounded-full"
            />
            <span className="text-xs text-white w-12 text-right">{formatTime(duration)}</span>
          </div>

          {/* Control buttons */}
          <div className="flex items-center justify-between">
            <div className="flex items-center gap-2">
              <Button size="icon" variant="ghost" onClick={togglePlay}>
                {isPlaying ? (
                  <Pause className="w-5 h-5 text-white" />
                ) : (
                  <Play className="w-5 h-5 text-white" />
                )}
              </Button>
              <Button size="icon" variant="ghost" onClick={() => skip(-10)}>
                <SkipBack className="w-5 h-5 text-white" />
              </Button>
              <Button size="icon" variant="ghost" onClick={() => skip(10)}>
                <SkipForward className="w-5 h-5 text-white" />
              </Button>

              {/* Volume */}
              <div className="flex items-center gap-1 group/volume">
                <Button size="icon" variant="ghost" onClick={toggleMute}>
                  {isMuted || volume === 0 ? (
                    <VolumeX className="w-5 h-5 text-white" />
                  ) : (
                    <Volume2 className="w-5 h-5 text-white" />
                  )}
                </Button>
                <input
                  type="range"
                  min={0}
                  max={1}
                  step={0.1}
                  value={isMuted ? 0 : volume}
                  onChange={handleVolumeChange}
                  className="w-0 group-hover/volume:w-20 transition-all duration-200 h-1 bg-white/30 rounded-full appearance-none cursor-pointer [&::-webkit-slider-thumb]:appearance-none [&::-webkit-slider-thumb]:w-2 [&::-webkit-slider-thumb]:h-2 [&::-webkit-slider-thumb]:bg-white [&::-webkit-slider-thumb]:rounded-full"
                />
              </div>
            </div>

            <div className="flex items-center gap-2">
              {/* Quality settings */}
              {qualities.length > 0 && (
                <div className="relative">
                  <Button
                    size="icon"
                    variant="ghost"
                    onClick={() => setShowSettings(!showSettings)}
                  >
                    <Settings className="w-5 h-5 text-white" />
                  </Button>
                  {showSettings && (
                    <div className="absolute bottom-full right-0 mb-2 bg-card border border-border rounded-lg p-2 min-w-[120px]">
                      <p className="text-xs text-muted-foreground px-2 mb-1">Qualite</p>
                      <button
                        onClick={() => setQuality(-1)}
                        className={cn(
                          'block w-full text-left px-2 py-1 text-sm rounded hover:bg-card-hover',
                          currentQuality === -1 ? 'text-primary' : 'text-white'
                        )}
                      >
                        Auto
                      </button>
                      {qualities.map((q) => (
                        <button
                          key={q.level}
                          onClick={() => setQuality(q.level)}
                          className={cn(
                            'block w-full text-left px-2 py-1 text-sm rounded hover:bg-card-hover',
                            currentQuality === q.level ? 'text-primary' : 'text-white'
                          )}
                        >
                          {q.height}p
                        </button>
                      ))}
                    </div>
                  )}
                </div>
              )}

              {/* Fullscreen */}
              <Button size="icon" variant="ghost" onClick={toggleFullscreen}>
                {isFullscreen ? (
                  <Minimize className="w-5 h-5 text-white" />
                ) : (
                  <Maximize className="w-5 h-5 text-white" />
                )}
              </Button>
            </div>
          </div>
        </div>
      </div>
    </div>
  );
}
