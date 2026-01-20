/**
 * Xtream Codes API Service
 * Handles communication with Xtream Codes compatible IPTV servers
 */

// API Response Types
export interface XtreamUserInfo {
  username: string;
  password: string;
  message: string;
  auth: number;
  status: string;
  exp_date: string;
  is_trial: string;
  active_cons: string;
  created_at: string;
  max_connections: string;
  allowed_output_formats: string[];
}

export interface XtreamServerInfo {
  url: string;
  port: string;
  https_port: string;
  server_protocol: string;
  rtmp_port: string;
  timezone: string;
  timestamp_now: number;
  time_now: string;
}

export interface XtreamAuthResponse {
  user_info: XtreamUserInfo;
  server_info: XtreamServerInfo;
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
  custom_sid: string;
  tv_archive: number;
  direct_source: string;
  tv_archive_duration: number;
}

export interface XtreamVodStream {
  num: number;
  name: string;
  stream_type: string;
  stream_id: number;
  stream_icon: string;
  rating: string;
  rating_5based: number;
  added: string;
  category_id: string;
  container_extension: string;
  custom_sid: string;
  direct_source: string;
}

export interface XtreamSeriesInfo {
  series_id: number;
  name: string;
  cover: string;
  plot: string;
  cast: string;
  director: string;
  genre: string;
  releaseDate: string;
  last_modified: string;
  rating: string;
  rating_5based: number;
  backdrop_path: string[];
  youtube_trailer: string;
  episode_run_time: string;
  category_id: string;
}

export interface XtreamEpisode {
  id: string;
  episode_num: number;
  title: string;
  container_extension: string;
  info: {
    movie_image?: string;
    plot?: string;
    releasedate?: string;
    rating?: number;
    duration_secs?: number;
    duration?: string;
  };
  custom_sid: string;
  added: string;
  season: number;
  direct_source: string;
}

export interface XtreamSeriesDetails {
  seasons: Array<{
    air_date: string;
    episode_count: number;
    id: number;
    name: string;
    overview: string;
    season_number: number;
    cover: string;
    cover_big: string;
  }>;
  info: {
    name: string;
    cover: string;
    plot: string;
    cast: string;
    director: string;
    genre: string;
    releaseDate: string;
    last_modified: string;
    rating: string;
    rating_5based: number;
    backdrop_path: string[];
    youtube_trailer: string;
    episode_run_time: string;
    category_id: string;
  };
  episodes: Record<string, XtreamEpisode[]>;
}

export interface XtreamVodDetails {
  info: {
    movie_image: string;
    tmdb_id: string;
    name: string;
    o_name: string;
    cover_big: string;
    releasedate: string;
    episode_run_time: number;
    youtube_trailer: string;
    director: string;
    actors: string;
    cast: string;
    description: string;
    plot: string;
    age: string;
    country: string;
    genre: string;
    backdrop_path: string[];
    duration_secs: number;
    duration: string;
    bitrate: number;
    rating: string;
  };
  movie_data: {
    stream_id: number;
    name: string;
    added: string;
    category_id: string;
    container_extension: string;
    custom_sid: string;
    direct_source: string;
  };
}

export interface XtreamEpgListing {
  id: string;
  epg_id: string;
  title: string;
  lang: string;
  start: string;
  end: string;
  description: string;
  channel_id: string;
  start_timestamp: string;
  stop_timestamp: string;
}

// Transformed types for our app
export interface TransformedChannel {
  name: string;
  logoUrl: string | null;
  groupTitle: string | null;
  streamUrl: string;
  epgId: string | null;
  number: number;
  catchupDays: number | null;
  xtreamId: number;
}

export interface TransformedMovie {
  name: string;
  posterUrl: string | null;
  streamUrl: string;
  rating: number | null;
  genre: string | null;
  year: number | null;
  plot: string | null;
  duration: number | null;
  xtreamId: number;
  containerExtension: string;
}

export interface TransformedSeries {
  name: string;
  posterUrl: string | null;
  backdropUrl: string | null;
  rating: number | null;
  genre: string | null;
  year: number | null;
  plot: string | null;
  xtreamId: number;
}

export interface TransformedEpisode {
  name: string;
  seasonNum: number;
  episodeNum: number;
  streamUrl: string;
  plot: string | null;
  duration: number | null;
  xtreamId: string;
}

/**
 * Xtream Codes API Service Class
 */
export class XtreamService {
  private baseUrl: string;
  private username: string;
  private password: string;

  constructor(server: string, username: string, password: string) {
    // Normalize server URL
    this.baseUrl = server.endsWith('/') ? server.slice(0, -1) : server;
    this.username = username;
    this.password = password;
  }

  /**
   * Build API URL with authentication
   */
  private buildUrl(action?: string, params?: Record<string, string>): string {
    let url = `${this.baseUrl}/player_api.php?username=${this.username}&password=${this.password}`;

    if (action) {
      url += `&action=${action}`;
    }

    if (params) {
      for (const [key, value] of Object.entries(params)) {
        url += `&${key}=${value}`;
      }
    }

    return url;
  }

  /**
   * Build stream URL for live channels
   */
  public buildLiveStreamUrl(streamId: number, format: string = 'm3u8'): string {
    return `${this.baseUrl}/live/${this.username}/${this.password}/${streamId}.${format}`;
  }

  /**
   * Build stream URL for VOD
   */
  public buildVodStreamUrl(streamId: number, extension: string): string {
    return `${this.baseUrl}/movie/${this.username}/${this.password}/${streamId}.${extension}`;
  }

  /**
   * Build stream URL for series episodes
   */
  public buildSeriesStreamUrl(streamId: string, extension: string): string {
    return `${this.baseUrl}/series/${this.username}/${this.password}/${streamId}.${extension}`;
  }

  /**
   * Make API request
   */
  private async request<T>(url: string): Promise<T> {
    const response = await fetch(url, {
      headers: {
        'User-Agent': 'StreamVision/1.0'
      }
    });

    if (!response.ok) {
      throw new Error(`Xtream API error: ${response.status} ${response.statusText}`);
    }

    return response.json();
  }

  /**
   * Authenticate and get account info
   */
  async authenticate(): Promise<XtreamAuthResponse> {
    const url = this.buildUrl();
    const response = await this.request<XtreamAuthResponse>(url);

    if (!response.user_info || response.user_info.auth !== 1) {
      throw new Error('Authentication failed');
    }

    return response;
  }

  /**
   * Get live TV categories
   */
  async getLiveCategories(): Promise<XtreamCategory[]> {
    const url = this.buildUrl('get_live_categories');
    return this.request<XtreamCategory[]>(url);
  }

  /**
   * Get VOD categories
   */
  async getVodCategories(): Promise<XtreamCategory[]> {
    const url = this.buildUrl('get_vod_categories');
    return this.request<XtreamCategory[]>(url);
  }

  /**
   * Get series categories
   */
  async getSeriesCategories(): Promise<XtreamCategory[]> {
    const url = this.buildUrl('get_series_categories');
    return this.request<XtreamCategory[]>(url);
  }

  /**
   * Get all live streams
   */
  async getLiveStreams(categoryId?: string): Promise<XtreamLiveStream[]> {
    const params = categoryId ? { category_id: categoryId } : undefined;
    const url = this.buildUrl('get_live_streams', params);
    return this.request<XtreamLiveStream[]>(url);
  }

  /**
   * Get all VOD streams (movies)
   */
  async getVodStreams(categoryId?: string): Promise<XtreamVodStream[]> {
    const params = categoryId ? { category_id: categoryId } : undefined;
    const url = this.buildUrl('get_vod_streams', params);
    return this.request<XtreamVodStream[]>(url);
  }

  /**
   * Get all series
   */
  async getSeries(categoryId?: string): Promise<XtreamSeriesInfo[]> {
    const params = categoryId ? { category_id: categoryId } : undefined;
    const url = this.buildUrl('get_series', params);
    return this.request<XtreamSeriesInfo[]>(url);
  }

  /**
   * Get VOD details (movie info)
   */
  async getVodInfo(vodId: number): Promise<XtreamVodDetails> {
    const url = this.buildUrl('get_vod_info', { vod_id: vodId.toString() });
    return this.request<XtreamVodDetails>(url);
  }

  /**
   * Get series details with all episodes
   */
  async getSeriesInfo(seriesId: number): Promise<XtreamSeriesDetails> {
    const url = this.buildUrl('get_series_info', { series_id: seriesId.toString() });
    return this.request<XtreamSeriesDetails>(url);
  }

  /**
   * Get EPG for a specific stream
   */
  async getEpg(streamId: number, limit?: number): Promise<{ epg_listings: XtreamEpgListing[] }> {
    const params: Record<string, string> = { stream_id: streamId.toString() };
    if (limit) {
      params.limit = limit.toString();
    }
    const url = this.buildUrl('get_short_epg', params);
    return this.request<{ epg_listings: XtreamEpgListing[] }>(url);
  }

  /**
   * Get full EPG for all channels (XML format URL)
   */
  getFullEpgUrl(): string {
    return `${this.baseUrl}/xmltv.php?username=${this.username}&password=${this.password}`;
  }

  // ============ TRANSFORMED DATA METHODS ============

  /**
   * Get all live channels transformed for our app
   */
  async getTransformedChannels(): Promise<{ channels: TransformedChannel[]; categories: XtreamCategory[] }> {
    const [streams, categories] = await Promise.all([
      this.getLiveStreams(),
      this.getLiveCategories()
    ]);

    const categoryMap = new Map(categories.map(c => [c.category_id, c.category_name]));

    const channels: TransformedChannel[] = streams.map((stream, index) => ({
      name: stream.name,
      logoUrl: stream.stream_icon || null,
      groupTitle: categoryMap.get(stream.category_id) || null,
      streamUrl: this.buildLiveStreamUrl(stream.stream_id),
      epgId: stream.epg_channel_id || null,
      number: stream.num || index + 1,
      catchupDays: stream.tv_archive === 1 ? stream.tv_archive_duration : null,
      xtreamId: stream.stream_id
    }));

    return { channels, categories };
  }

  /**
   * Get all movies transformed for our app
   */
  async getTransformedMovies(): Promise<{ movies: TransformedMovie[]; categories: XtreamCategory[] }> {
    const [streams, categories] = await Promise.all([
      this.getVodStreams(),
      this.getVodCategories()
    ]);

    const movies: TransformedMovie[] = streams.map(stream => ({
      name: stream.name,
      posterUrl: stream.stream_icon || null,
      streamUrl: this.buildVodStreamUrl(stream.stream_id, stream.container_extension),
      rating: stream.rating_5based || null,
      genre: null, // Need to fetch details for genre
      year: null,
      plot: null,
      duration: null,
      xtreamId: stream.stream_id,
      containerExtension: stream.container_extension
    }));

    return { movies, categories };
  }

  /**
   * Get all series transformed for our app
   */
  async getTransformedSeries(): Promise<{ series: TransformedSeries[]; categories: XtreamCategory[] }> {
    const [seriesList, categories] = await Promise.all([
      this.getSeries(),
      this.getSeriesCategories()
    ]);

    const series: TransformedSeries[] = seriesList.map(s => ({
      name: s.name,
      posterUrl: s.cover || null,
      backdropUrl: s.backdrop_path?.[0] || null,
      rating: s.rating_5based || null,
      genre: s.genre || null,
      year: s.releaseDate ? parseInt(s.releaseDate.split('-')[0], 10) : null,
      plot: s.plot || null,
      xtreamId: s.series_id
    }));

    return { series, categories };
  }

  /**
   * Get series episodes transformed for our app
   */
  async getTransformedEpisodes(seriesId: number): Promise<{
    info: TransformedSeries;
    episodes: TransformedEpisode[];
    seasons: number[];
  }> {
    const details = await this.getSeriesInfo(seriesId);

    const info: TransformedSeries = {
      name: details.info.name,
      posterUrl: details.info.cover || null,
      backdropUrl: details.info.backdrop_path?.[0] || null,
      rating: details.info.rating_5based || null,
      genre: details.info.genre || null,
      year: details.info.releaseDate ? parseInt(details.info.releaseDate.split('-')[0], 10) : null,
      plot: details.info.plot || null,
      xtreamId: seriesId
    };

    const episodes: TransformedEpisode[] = [];
    const seasons = new Set<number>();

    for (const [seasonNum, seasonEpisodes] of Object.entries(details.episodes)) {
      const season = parseInt(seasonNum, 10);
      seasons.add(season);

      for (const ep of seasonEpisodes) {
        episodes.push({
          name: ep.title || `Episode ${ep.episode_num}`,
          seasonNum: season,
          episodeNum: ep.episode_num,
          streamUrl: this.buildSeriesStreamUrl(ep.id, ep.container_extension),
          plot: ep.info?.plot || null,
          duration: ep.info?.duration_secs || null,
          xtreamId: ep.id
        });
      }
    }

    // Sort episodes
    episodes.sort((a, b) => {
      if (a.seasonNum !== b.seasonNum) return a.seasonNum - b.seasonNum;
      return a.episodeNum - b.episodeNum;
    });

    return {
      info,
      episodes,
      seasons: Array.from(seasons).sort((a, b) => a - b)
    };
  }
}

/**
 * Create Xtream service instance
 */
export function createXtreamService(server: string, username: string, password: string): XtreamService {
  return new XtreamService(server, username, password);
}
