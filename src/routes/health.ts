import { FastifyInstance } from 'fastify';
import { env } from '../config.js';

export function registerHealthRoutes(app: FastifyInstance) {
  app.get('/healthz', async () => ({ status: 'ok' }));

  app.get('/readyz', async () => {
    await app.prisma.$queryRaw`SELECT 1`;
    await app.redis.ping();
    await app.storage.bucketExists(env.ASSET_BUCKET);
    if (!app.flags.ready()) {
      throw new Error('flags_unready');
    }
    return { status: 'ready' };
  });
}

