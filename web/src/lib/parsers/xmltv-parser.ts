/**
 * XMLTV EPG Parser
 * Parses XMLTV format for Electronic Program Guide data
 */

export interface XmltvProgram {
  channelId: string;
  title: string;
  description: string | null;
  startTime: Date;
  endTime: Date;
  category: string | null;
  iconUrl: string | null;
}

export interface XmltvChannel {
  id: string;
  name: string;
  iconUrl: string | null;
}

export interface XmltvParseResult {
  channels: XmltvChannel[];
  programs: XmltvProgram[];
}

/**
 * Parse date string in XMLTV format (YYYYMMDDHHmmss +ZZZZ)
 */
function parseXmltvDate(dateStr: string): Date {
  // Format: 20231215140000 +0100
  const match = dateStr.match(/^(\d{4})(\d{2})(\d{2})(\d{2})(\d{2})(\d{2})\s*([+-]\d{4})?$/);

  if (!match) {
    return new Date(dateStr);
  }

  const [, year, month, day, hour, minute, second, tz] = match;

  let dateString = `${year}-${month}-${day}T${hour}:${minute}:${second}`;

  if (tz) {
    const tzHours = tz.slice(0, 3);
    const tzMinutes = tz.slice(3);
    dateString += `${tzHours}:${tzMinutes}`;
  } else {
    dateString += 'Z';
  }

  return new Date(dateString);
}

/**
 * Get text content from element
 */
function getTextContent(element: Element, tagName: string): string | null {
  const child = element.getElementsByTagName(tagName)[0];
  return child?.textContent || null;
}

/**
 * Get attribute value from element
 */
function getAttribute(element: Element, attr: string): string | null {
  return element.getAttribute(attr);
}

/**
 * Parse XMLTV content
 */
export async function parseXmltv(xmlContent: string): Promise<XmltvParseResult> {
  const parser = new DOMParser();
  const doc = parser.parseFromString(xmlContent, 'text/xml');

  const channels: XmltvChannel[] = [];
  const programs: XmltvProgram[] = [];

  // Parse channels
  const channelElements = doc.getElementsByTagName('channel');
  for (let i = 0; i < channelElements.length; i++) {
    const element = channelElements[i];
    const id = getAttribute(element, 'id');

    if (id) {
      const displayName = getTextContent(element, 'display-name');
      const iconElement = element.getElementsByTagName('icon')[0];
      const iconUrl = iconElement ? getAttribute(iconElement, 'src') : null;

      channels.push({
        id,
        name: displayName || id,
        iconUrl,
      });
    }
  }

  // Parse programs
  const programElements = doc.getElementsByTagName('programme');
  for (let i = 0; i < programElements.length; i++) {
    const element = programElements[i];
    const channelId = getAttribute(element, 'channel');
    const startStr = getAttribute(element, 'start');
    const stopStr = getAttribute(element, 'stop');

    if (channelId && startStr && stopStr) {
      const title = getTextContent(element, 'title');
      const description = getTextContent(element, 'desc');
      const category = getTextContent(element, 'category');
      const iconElement = element.getElementsByTagName('icon')[0];
      const iconUrl = iconElement ? getAttribute(iconElement, 'src') : null;

      programs.push({
        channelId,
        title: title || 'Sans titre',
        description,
        startTime: parseXmltvDate(startStr),
        endTime: parseXmltvDate(stopStr),
        category,
        iconUrl,
      });
    }
  }

  return { channels, programs };
}

/**
 * Fetch and parse XMLTV from URL
 */
export async function fetchAndParseXmltv(url: string): Promise<XmltvParseResult> {
  const response = await fetch(url, {
    headers: {
      'User-Agent': 'Visiora/1.0',
    },
  });

  if (!response.ok) {
    throw new Error(`Failed to fetch EPG: ${response.status} ${response.statusText}`);
  }

  const content = await response.text();
  return parseXmltv(content);
}

/**
 * Get current program for a channel
 */
export function getCurrentProgram(
  programs: XmltvProgram[],
  channelId: string,
  now: Date = new Date()
): XmltvProgram | null {
  return programs.find(
    (p) =>
      p.channelId === channelId &&
      p.startTime <= now &&
      p.endTime > now
  ) || null;
}

/**
 * Get upcoming programs for a channel
 */
export function getUpcomingPrograms(
  programs: XmltvProgram[],
  channelId: string,
  limit: number = 5,
  now: Date = new Date()
): XmltvProgram[] {
  return programs
    .filter((p) => p.channelId === channelId && p.startTime > now)
    .sort((a, b) => a.startTime.getTime() - b.startTime.getTime())
    .slice(0, limit);
}

/**
 * Get programs for a specific time range
 */
export function getProgramsInRange(
  programs: XmltvProgram[],
  channelId: string,
  start: Date,
  end: Date
): XmltvProgram[] {
  return programs
    .filter(
      (p) =>
        p.channelId === channelId &&
        ((p.startTime >= start && p.startTime < end) ||
          (p.endTime > start && p.endTime <= end) ||
          (p.startTime <= start && p.endTime >= end))
    )
    .sort((a, b) => a.startTime.getTime() - b.startTime.getTime());
}
