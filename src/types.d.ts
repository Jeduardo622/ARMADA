import { PrismaClient } from '@prisma/client';
import Redis from 'ioredis';
import { Client as MinioClient } from 'minio';

export interface FlagClient {
  isEnabled: (flagName: string, context?: Record<string, unknown>) => boolean;
  getVariant: (flagName: string, context?: Record<string, unknown>) => unknown;
  ready: () => boolean;
}

export interface AuthUser {
  id: string;
  externalId?: string;
}

declare module 'fastify' {
  interface FastifyInstance {
    prisma: PrismaClient;
    redis: Redis;
    storage: MinioClient;
    flags: FlagClient;
  }

  interface FastifyRequest {
    user?: AuthUser;
  }
}

