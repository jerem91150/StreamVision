/**
 * M3U/M3U8 Playlist Parser
 * Supports extended M3U format with EXTINF tags
 */

export interface M3UChannel {
  name: string;
  logoUrl: string | null;
  groupTitle: string | null;
  streamUrl: string;
  epgId: string | null;
  number: number | null;
  catchupDays: number | null;
  catchupSource: string | null;
  type: 'live' | 'movie' | 'series';
  seriesInfo?: {
    seriesName: string;
    seasonNum: number;
    episodeNum: number;
  };
}

export interface M3UParseResult {
  channels: M3UChannel[];
  movies: M3UChannel[];
  series: M3UChannel[];
  totalItems: number;
}

interface ExtInfAttributes {
  tvgId?: string;
  tvgName?: string;
  tvgLogo?: string;
  groupTitle?: string;
  tvgChno?: string;
  catchupDays?: string;
  catchupSource?: string;
}

/**
 * Parse EXTINF attributes from the attribute string
 */
function parseExtInfAttributes(attributeString: string): ExtInfAttributes {
  const attributes: ExtInfAttributes = {};

  // Regex to match key="value" or key='value' patterns
  const attrRegex = /([a-zA-Z0-9_-]+)=["']([^"']*)["']/g;
  let match;

  while ((match = attrRegex.exec(attributeString)) !== null) {
    const key = match[1].toLowerCase().replace(/-/g, '');
    const value = match[2];

    switch (key) {
      case 'tvgid':
        attributes.tvgId = value;
        break;
      case 'tvgname':
        attributes.tvgName = value;
        break;
      case 'tvglogo':
        attributes.tvgLogo = value;
        break;
      case 'grouptitle':
        attributes.groupTitle = value;
        break;
      case 'tvgchno':
        attributes.tvgChno = value;
        break;
      case 'catchupdays':
      case 'catchup-days':
        attributes.catchupDays = value;
        break;
      case 'catchupsource':
      case 'catchup-source':
        attributes.catchupSource = value;
        break;
    }
  }

  return attributes;
}

/**
 * Extract channel name from EXTINF line (after the comma)
 */
function extractChannelName(extinfLine: string): string {
  const commaIndex = extinfLine.lastIndexOf(',');
  if (commaIndex !== -1) {
    return extinfLine.substring(commaIndex + 1).trim();
  }
  return 'Unknown Channel';
}

/**
 * Detect content type based on group title and URL patterns
 */
function detectContentType(groupTitle: string | null, url: string, name: string): 'live' | 'movie' | 'series' {
  const lowerGroup = (groupTitle || '').toLowerCase();
  const lowerUrl = url.toLowerCase();
  const lowerName = name.toLowerCase();

  // VOD Movie patterns
  const moviePatterns = [
    'vod', 'movie', 'film', 'cinema',
    'movies', 'films', 'cinéma'
  ];

  // VOD Series patterns
  const seriesPatterns = [
    'series', 'série', 'séries', 'tv show',
    'tvshow', 'episode', 'épisode'
  ];

  // Check URL patterns for VOD
  if (lowerUrl.includes('/movie/') || lowerUrl.includes('/vod/')) {
    return 'movie';
  }

  if (lowerUrl.includes('/series/') || lowerUrl.includes('/episode/')) {
    return 'series';
  }

  // Check group title
  for (const pattern of moviePatterns) {
    if (lowerGroup.includes(pattern)) {
      return 'movie';
    }
  }

  for (const pattern of seriesPatterns) {
    if (lowerGroup.includes(pattern)) {
      return 'series';
    }
  }

  // Check for series pattern in name (e.g., "Show Name S01 E05")
  const seriesRegex = /s\d{1,2}\s*e\d{1,2}|season\s*\d|episode\s*\d|\d{1,2}x\d{1,2}/i;
  if (seriesRegex.test(lowerName)) {
    return 'series';
  }

  return 'live';
}

/**
 * Parse series info from episode name
 * Supports formats: "Show S01E05", "Show 1x05", "Show Season 1 Episode 5"
 */
function parseSeriesInfo(name: string): { seriesName: string; seasonNum: number; episodeNum: number } | null {
  // Pattern: S01E05 or S1E5
  let match = name.match(/(.+?)\s*[Ss](\d{1,2})\s*[Ee](\d{1,2})/);
  if (match) {
    return {
      seriesName: match[1].trim(),
      seasonNum: parseInt(match[2], 10),
      episodeNum: parseInt(match[3], 10)
    };
  }

  // Pattern: 1x05
  match = name.match(/(.+?)\s*(\d{1,2})x(\d{1,2})/);
  if (match) {
    return {
      seriesName: match[1].trim(),
      seasonNum: parseInt(match[2], 10),
      episodeNum: parseInt(match[3], 10)
    };
  }

  // Pattern: Season 1 Episode 5
  match = name.match(/(.+?)\s*Season\s*(\d{1,2}).*Episode\s*(\d{1,2})/i);
  if (match) {
    return {
      seriesName: match[1].trim(),
      seasonNum: parseInt(match[2], 10),
      episodeNum: parseInt(match[3], 10)
    };
  }

  return null;
}

/**
 * Main M3U parser function
 */
export async function parseM3U(content: string): Promise<M3UParseResult> {
  const lines = content.split(/\r?\n/);
  const channels: M3UChannel[] = [];
  const movies: M3UChannel[] = [];
  const series: M3UChannel[] = [];

  let currentExtInf: string | null = null;
  let channelNumber = 1;

  for (let i = 0; i < lines.length; i++) {
    const line = lines[i].trim();

    // Skip empty lines and M3U header
    if (!line || line === '#EXTM3U' || line.startsWith('#EXTM3U ')) {
      continue;
    }

    // Parse EXTINF line
    if (line.startsWith('#EXTINF:')) {
      currentExtInf = line.substring(8); // Remove '#EXTINF:'
      continue;
    }

    // Skip other directives
    if (line.startsWith('#')) {
      continue;
    }

    // This should be a URL line
    if (currentExtInf && (line.startsWith('http://') || line.startsWith('https://') || line.startsWith('rtmp://') || line.startsWith('rtsp://'))) {
      const attributes = parseExtInfAttributes(currentExtInf);
      const name = attributes.tvgName || extractChannelName(currentExtInf);
      const type = detectContentType(attributes.groupTitle || null, line, name);

      const channel: M3UChannel = {
        name,
        logoUrl: attributes.tvgLogo || null,
        groupTitle: attributes.groupTitle || null,
        streamUrl: line,
        epgId: attributes.tvgId || null,
        number: attributes.tvgChno ? parseInt(attributes.tvgChno, 10) : channelNumber,
        catchupDays: attributes.catchupDays ? parseInt(attributes.catchupDays, 10) : null,
        catchupSource: attributes.catchupSource || null,
        type
      };

      // Parse series info if it's a series
      if (type === 'series') {
        const seriesInfo = parseSeriesInfo(name);
        if (seriesInfo) {
          channel.seriesInfo = seriesInfo;
        }
      }

      // Categorize by type
      switch (type) {
        case 'movie':
          movies.push(channel);
          break;
        case 'series':
          series.push(channel);
          break;
        default:
          channels.push(channel);
          channelNumber++;
      }

      currentExtInf = null;
    }
  }

  return {
    channels,
    movies,
    series,
    totalItems: channels.length + movies.length + series.length
  };
}

/**
 * Fetch and parse M3U from URL
 */
export async function fetchAndParseM3U(url: string): Promise<M3UParseResult> {
  const response = await fetch(url, {
    headers: {
      'User-Agent': 'StreamVision/1.0'
    }
  });

  if (!response.ok) {
    throw new Error(`Failed to fetch M3U: ${response.status} ${response.statusText}`);
  }

  const content = await response.text();
  return parseM3U(content);
}

/**
 * Group channels by their groupTitle
 */
export function groupChannelsByCategory(channels: M3UChannel[]): Record<string, M3UChannel[]> {
  const groups: Record<string, M3UChannel[]> = {};

  for (const channel of channels) {
    const group = channel.groupTitle || 'Autres';
    if (!groups[group]) {
      groups[group] = [];
    }
    groups[group].push(channel);
  }

  return groups;
}

/**
 * Group series episodes by series name
 */
export function groupSeriesByName(series: M3UChannel[]): Record<string, M3UChannel[]> {
  const groups: Record<string, M3UChannel[]> = {};

  for (const episode of series) {
    const seriesName = episode.seriesInfo?.seriesName || episode.name.split(/[Ss]\d/)[0].trim();
    if (!groups[seriesName]) {
      groups[seriesName] = [];
    }
    groups[seriesName].push(episode);
  }

  // Sort episodes within each series
  for (const seriesName in groups) {
    groups[seriesName].sort((a, b) => {
      const aS = a.seriesInfo?.seasonNum || 0;
      const bS = b.seriesInfo?.seasonNum || 0;
      if (aS !== bS) return aS - bS;

      const aE = a.seriesInfo?.episodeNum || 0;
      const bE = b.seriesInfo?.episodeNum || 0;
      return aE - bE;
    });
  }

  return groups;
}
