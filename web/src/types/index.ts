// User types
export interface User {
  id: string;
  email: string;
  username: string;
  avatarUrl?: string;
  createdAt: Date;
}

export interface UserPreferences {
  preferredLanguages: string[];
  showMovies: boolean;
  showSeries: boolean;
  showLiveTV: boolean;
  showAnime: boolean;
  animePreferSubbed: boolean;
  preferredGenres: string[];
  preferredSports: string[];
  autoplay: boolean;
  defaultQuality: string;
}

// Auth types
export interface AuthTokens {
  accessToken: string;
  refreshToken: string;
}

export interface LoginCredentials {
  email: string;
  password: string;
}

export interface RegisterData {
  username: string;
  email: string;
  password: string;
}

// Playlist types
export type PlaylistType = 'm3u' | 'xtream';

export interface Playlist {
  id: string;
  name: string;
  type: PlaylistType;
  url?: string;
  xtreamServer?: string;
  xtreamUsername?: string;
  xtreamPassword?: string;
  isActive: boolean;
  lastSync?: Date;
}

// Channel types
export interface Channel {
  id: string;
  name: string;
  logoUrl?: string;
  streamUrl: string;
  groupTitle?: string;
  epgId?: string;
  number?: number;
}

export interface ChannelGroup {
  name: string;
  channels: Channel[];
}

// VOD types
export interface Movie {
  id: string;
  name: string;
  posterUrl?: string;
  backdropUrl?: string;
  description?: string;
  rating?: number;
  year?: number;
  duration?: number;
  genres?: string[];
  streamUrl: string;
}

export interface Series {
  id: string;
  name: string;
  posterUrl?: string;
  backdropUrl?: string;
  description?: string;
  rating?: number;
  year?: number;
  genres?: string[];
  seasons: Season[];
}

export interface Season {
  number: number;
  episodes: Episode[];
}

export interface Episode {
  id: string;
  number: number;
  name: string;
  description?: string;
  duration?: number;
  streamUrl: string;
}

// EPG types
export interface EPGProgram {
  id: string;
  channelId: string;
  title: string;
  description?: string;
  startTime: Date;
  endTime: Date;
  category?: string;
  posterUrl?: string;
}

export interface EPGChannel {
  id: string;
  name: string;
  logoUrl?: string;
  programs: EPGProgram[];
}

// Watch history
export interface WatchHistoryItem {
  id: string;
  channelId?: string;
  vodId?: string;
  vodType?: 'movie' | 'episode';
  progress: number;
  duration?: number;
  watchedAt: Date;
  channel?: Channel;
  movie?: Movie;
  episode?: Episode;
}

// Subscription types
export type SubscriptionTier = 'free' | 'basic' | 'premium';

export interface Subscription {
  tier: SubscriptionTier;
  status: 'active' | 'cancelled' | 'expired';
  expiresAt?: Date;
}

export interface PricingPlan {
  id: SubscriptionTier;
  name: string;
  price: number;
  currency: string;
  features: string[];
  recommended?: boolean;
}

// API Response types
export interface ApiResponse<T> {
  success: boolean;
  data?: T;
  error?: string;
}

// Xtream Codes API types
export interface XtreamAuthResponse {
  user_info: {
    username: string;
    status: string;
    exp_date: string;
    max_connections: string;
  };
  server_info: {
    url: string;
    port: string;
    https_port: string;
    server_protocol: string;
  };
}

export interface XtreamCategory {
  category_id: string;
  category_name: string;
  parent_id: number;
}

export interface XtreamLiveStream {
  num: number;
  name: string;
  stream_type: string;
  stream_id: number;
  stream_icon: string;
  epg_channel_id: string;
  added: string;
  category_id: string;
  tv_archive: number;
  tv_archive_duration: number;
}

export interface XtreamVodInfo {
  stream_id: number;
  name: string;
  stream_type: string;
  stream_icon: string;
  rating: string;
  added: string;
  category_id: string;
  container_extension: string;
}

export interface XtreamSeriesInfo {
  series_id: number;
  name: string;
  cover: string;
  plot: string;
  cast: string;
  director: string;
  genre: string;
  release_date: string;
  rating: string;
  category_id: string;
}
