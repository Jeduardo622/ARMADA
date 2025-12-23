import fp from 'fastify-plugin';
import Redis from 'ioredis';
import { env } from '../config.js';

declare module 'fastify' {
  interface FastifyInstance {
    redis: Redis;
  }
}

export const redisPlugin = fp(async (fastify) => {
  const client = new Redis(env.REDIS_URL);
  fastify.decorate('redis', client);

  fastify.addHook('onClose', async () => {
    await client.quit();
  });
});

