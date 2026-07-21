import Fastify, { LogController } from 'fastify';
import helmet from '@fastify/helmet';
import cors from '@fastify/cors';
import rateLimit from '@fastify/rate-limit';
import { PrismaClient } from '@prisma/client';
import Redis from 'ioredis';
import { Client as MinioClient } from 'minio';
import { randomUUID } from 'crypto';
import { env } from './config.js';
import { loggerOptions } from './logger.js';
import { prismaPlugin } from './plugins/prisma.js';
import { redisPlugin } from './plugins/redis.js';
import { storagePlugin } from './plugins/storage.js';
import { flagPlugin, FlagClient } from './plugins/flags.js';
import { authPlugin } from './plugins/auth.js';
import { registerHealthRoutes } from './routes/health.js';
import { registerAuthRoutes } from './routes/auth.js';
import { registerPlayerRoutes } from './routes/player.js';
import { registerInventoryRoutes } from './routes/inventory.js';
import { registerUpgradeRoutes } from './routes/upgrades.js';
import { registerMissionRoutes } from './routes/missions.js';
import { registerPvpRoutes } from './routes/pvp.js';
import { registerSimRoutes } from './routes/sim.js';
import { registerTelemetryRoutes } from './routes/telemetry.js';
import { registerConfigRoutes } from './routes/config.js';

type BuildOptions = {
  testing?: boolean;
};

export function buildServer(options?: BuildOptions) {
  const app = Fastify({
    logger: loggerOptions,
    logController: new LogController({ disableRequestLogging: env.NODE_ENV === 'test' }),
    bodyLimit: env.BODY_LIMIT_BYTES,
    genReqId: (request) => {
      const headerId = request.headers['x-request-id'];
      if (typeof headerId === 'string') return headerId;
      if (Array.isArray(headerId) && headerId.length > 0) return headerId[0]!;
      return randomUUID();
    }
  });

  app.register(helmet);
  app.register(cors, { origin: env.CORS_ORIGIN });

  if (options?.testing) {
    app.decorate('prisma', {
      $queryRaw: async () => 1,
      telemetryEvent: { create: async (data: unknown) => data },
      player: { create: async (data: unknown) => data, findUnique: async () => null },
      mission: { findMany: async () => [], findFirst: async () => null },
      missionProgress: { upsert: async (data: unknown) => data },
      inventoryItem: { findMany: async () => [], upsert: async (data: unknown) => data },
      playerShipUpgrade: {
        findMany: async () => [],
        findUnique: async () => null,
        updateMany: async () => ({ count: 0 }),
        create: async (data: unknown) => data
      },
      configSnapshot: { findFirst: async () => null, findUnique: async () => null },
      featureFlag: { findUnique: async () => ({ enabled: true }) },
      match: {
        create: async (data: unknown) => data,
        findUnique: async () => null,
        updateMany: async () => ({ count: 0 })
      },
      matchParticipant: {
        create: async (data: unknown) => data,
        findFirst: async () => null,
        findMany: async () => [],
        updateMany: async () => ({ count: 0 })
      }
    } as unknown as PrismaClient);
    app.decorate('redis', {
      ping: async () => 'PONG',
      quit: async () => {}
    } as unknown as Redis);
    app.decorate('storage', {
      bucketExists: async () => true
    } as unknown as MinioClient);
    app.decorate('flags', {
      isEnabled: () => true,
      getVariant: () => ({ name: 'default', enabled: true }),
      ready: () => true
    } as unknown as FlagClient);
    app.addHook('preHandler', async (request) => {
      request.user = { id: 'test-player' };
    });
  } else {
    app.register(prismaPlugin);
    app.register(redisPlugin);
    app.register(storagePlugin);
    app.register(flagPlugin);
    app.register(rateLimit, {
      max: env.RATE_LIMIT_MAX,
      timeWindow: env.RATE_LIMIT_WINDOW_MS,
      allowList: ['127.0.0.1', '::1']
    });
    app.register(authPlugin);
  }

  registerHealthRoutes(app);
  registerAuthRoutes(app);
  registerPlayerRoutes(app);
  registerInventoryRoutes(app);
  registerUpgradeRoutes(app);
  registerMissionRoutes(app);
  registerPvpRoutes(app);
  registerSimRoutes(app);
  registerTelemetryRoutes(app);
  registerConfigRoutes(app);

  return app;
}

