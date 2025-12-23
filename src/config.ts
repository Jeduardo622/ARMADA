import 'dotenv/config';
import { z } from 'zod';

const envSchema = z.object({
  NODE_ENV: z.enum(['development', 'test', 'production']).default('development'),
  PORT: z.coerce.number().default(4500),
  LOG_LEVEL: z.string().default('info'),
  JWT_SECRET: z.string().min(8),
  TOKEN_TTL_HOURS: z.coerce.number().positive().default(12),
  DATABASE_URL: z.string().url(),
  REDIS_URL: z.string().url(),
  CONFIG_SERVICE_URL: z.string().url(),
  CONFIG_SIGNING_KEY: z.string(),
  CORS_ORIGIN: z.string().default('*'),
  RATE_LIMIT_WINDOW_MS: z.coerce.number().int().positive().default(60_000),
  RATE_LIMIT_MAX: z.coerce.number().int().positive().default(100),
  BODY_LIMIT_BYTES: z.coerce.number().int().positive().default(1_000_000),
  FLAG_SERVICE_URL: z.string().url(),
  FLAG_SERVICE_API_TOKEN: z.string().optional(),
  TELEMETRY_ENDPOINT: z.string().optional(),
  TELEMETRY_API_KEY: z.string().optional(),
  SENTRY_DSN: z.string().optional(),
  STORAGE_ENDPOINT: z.string().url(),
  STORAGE_ACCESS_KEY: z.string(),
  STORAGE_SECRET_KEY: z.string(),
  STORAGE_REGION: z.string().optional(),
  ASSET_BUCKET: z.string(),
  ASSET_CDN_BASE: z.string().url().optional()
});

const parsed = envSchema.safeParse(process.env);

if (!parsed.success) {
  console.error('Invalid environment configuration', parsed.error.format());
  throw new Error('Invalid environment configuration');
}

export const env = parsed.data;

