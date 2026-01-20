import crypto from 'crypto';

const ALGORITHM = 'aes-256-gcm';
const IV_LENGTH = 16;
const TAG_LENGTH = 16;
const SALT_LENGTH = 32;

function getKey(): Buffer {
  const secret = process.env.ENCRYPTION_SECRET;
  if (!secret) {
    throw new Error('ENCRYPTION_SECRET is not set');
  }
  // Derive a 32-byte key from the secret
  return crypto.scryptSync(secret, 'salt', 32);
}

/**
 * Encrypt sensitive data (like Xtream credentials)
 */
export function encrypt(text: string): string {
  const key = getKey();
  const iv = crypto.randomBytes(IV_LENGTH);
  const cipher = crypto.createCipheriv(ALGORITHM, key, iv);

  let encrypted = cipher.update(text, 'utf8', 'hex');
  encrypted += cipher.final('hex');

  const tag = cipher.getAuthTag();

  // Format: iv:tag:encrypted
  return `${iv.toString('hex')}:${tag.toString('hex')}:${encrypted}`;
}

/**
 * Decrypt sensitive data
 */
export function decrypt(encryptedText: string): string {
  const key = getKey();
  const parts = encryptedText.split(':');

  if (parts.length !== 3) {
    throw new Error('Invalid encrypted format');
  }

  const iv = Buffer.from(parts[0], 'hex');
  const tag = Buffer.from(parts[1], 'hex');
  const encrypted = parts[2];

  const decipher = crypto.createDecipheriv(ALGORITHM, key, iv);
  decipher.setAuthTag(tag);

  let decrypted = decipher.update(encrypted, 'hex', 'utf8');
  decrypted += decipher.final('utf8');

  return decrypted;
}

/**
 * Hash a value (one-way, for tokens)
 */
export function hash(value: string): string {
  return crypto.createHash('sha256').update(value).digest('hex');
}

/**
 * Generate a secure random token
 */
export function generateSecureToken(length: number = 32): string {
  return crypto.randomBytes(length).toString('hex');
}

/**
 * Safely decrypt Xtream password with fallback for dev mode
 */
export function decryptPassword(encryptedPassword: string | null): string | null {
  if (!encryptedPassword) return null;

  try {
    // Check if it looks encrypted (has colons from our format)
    if (encryptedPassword.includes(':')) {
      return decrypt(encryptedPassword);
    }
    // Fallback for plaintext (dev mode)
    return encryptedPassword;
  } catch {
    // If decryption fails, return as-is (might be plaintext from dev)
    return encryptedPassword;
  }
}
