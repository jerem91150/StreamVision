interface RateLimitEntry {
  count: number;
  resetTime: number;
}

const rateLimitMap = new Map<string, RateLimitEntry>();

interface RateLimitConfig {
  windowMs: number;  // Time window in milliseconds
  maxRequests: number;  // Max requests per window
}

const defaultConfig: RateLimitConfig = {
  windowMs: 60 * 1000,  // 1 minute
  maxRequests: 10,
};

// Stricter config for auth endpoints
export const authRateLimitConfig: RateLimitConfig = {
  windowMs: 15 * 60 * 1000,  // 15 minutes
  maxRequests: 5,  // 5 attempts per 15 minutes
};

// Stricter config for login specifically
export const loginRateLimitConfig: RateLimitConfig = {
  windowMs: 15 * 60 * 1000,  // 15 minutes
  maxRequests: 5,  // 5 login attempts per 15 minutes
};

/**
 * Check if a request should be rate limited
 * Returns { limited: boolean, remaining: number, resetIn: number }
 */
export function checkRateLimit(
  identifier: string,
  config: RateLimitConfig = defaultConfig
): { limited: boolean; remaining: number; resetIn: number } {
  const now = Date.now();
  const key = identifier;

  const entry = rateLimitMap.get(key);

  if (!entry || now > entry.resetTime) {
    // First request or window expired, create new entry
    rateLimitMap.set(key, {
      count: 1,
      resetTime: now + config.windowMs,
    });
    return {
      limited: false,
      remaining: config.maxRequests - 1,
      resetIn: config.windowMs,
    };
  }

  // Increment count
  entry.count++;

  if (entry.count > config.maxRequests) {
    return {
      limited: true,
      remaining: 0,
      resetIn: entry.resetTime - now,
    };
  }

  return {
    limited: false,
    remaining: config.maxRequests - entry.count,
    resetIn: entry.resetTime - now,
  };
}

/**
 * Get client IP from request
 */
export function getClientIP(request: Request): string {
  // Try various headers for proxied requests
  const forwarded = request.headers.get('x-forwarded-for');
  if (forwarded) {
    return forwarded.split(',')[0].trim();
  }

  const realIP = request.headers.get('x-real-ip');
  if (realIP) {
    return realIP;
  }

  // Fallback
  return 'unknown';
}

/**
 * Rate limit middleware helper
 */
export function createRateLimitResponse(resetIn: number) {
  return new Response(
    JSON.stringify({
      error: 'Trop de tentatives. Reessayez plus tard.',
      retryAfter: Math.ceil(resetIn / 1000),
    }),
    {
      status: 429,
      headers: {
        'Content-Type': 'application/json',
        'Retry-After': Math.ceil(resetIn / 1000).toString(),
      },
    }
  );
}

// Clean up old entries periodically (every 5 minutes)
if (typeof setInterval !== 'undefined') {
  setInterval(() => {
    const now = Date.now();
    const entries = Array.from(rateLimitMap.entries());
    for (const [key, entry] of entries) {
      if (now > entry.resetTime) {
        rateLimitMap.delete(key);
      }
    }
  }, 5 * 60 * 1000);
}
