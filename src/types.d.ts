import { PrismaClient } from '@prisma/client';
import Redis from 'ioredis';
import { Client as MinioClient } from 'minio';
import type { FlagClient } from './plugins/flags.js';

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

