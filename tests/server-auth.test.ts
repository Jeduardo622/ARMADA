import { afterAll, beforeAll, describe, expect, it, vi } from 'vitest';
import jwt from 'jsonwebtoken';
import { env } from '../src/config.js';
import { buildServer } from '../src/app.js';

// Keep the real server composition and auth plugin; replace only infrastructure.
vi.mock('../src/plugins/prisma.js', async () => {
  const { default: fp } = await import('fastify-plugin');
  return { prismaPlugin: fp(async (app) => {
    app.decorate('prisma', {
      player: {
        create: async () => ({ id: '11111111-1111-4111-8111-111111111111' }),
        findUnique: async ({ where }: { where: { id: string } }) => ({ id: where.id })
      },
      mission: { findMany: async () => [] },
      $queryRaw: async () => [{ value: 1 }]
    } as unknown as typeof app.prisma);
  }) };
});
vi.mock('../src/plugins/redis.js', async () => {
  const { default: fp } = await import('fastify-plugin');
  return { redisPlugin: fp(async (app) => { app.decorate('redis', { ping: async () => 'PONG' } as unknown as typeof app.redis); }) };
});
vi.mock('../src/plugins/storage.js', async () => {
  const { default: fp } = await import('fastify-plugin');
  return { storagePlugin: fp(async (app) => { app.decorate('storage', { bucketExists: async () => true } as unknown as typeof app.storage); }) };
});
vi.mock('../src/plugins/flags.js', async () => {
  const { default: fp } = await import('fastify-plugin');
  return { flagPlugin: fp(async (app) => { app.decorate('flags', {
    ready: () => true, isEnabled: () => true, getVariant: () => ({ name: 'default', enabled: true })
  }); }) };
});

const app = buildServer();
const playerId = '11111111-1111-4111-8111-111111111111';
beforeAll(async () => { await app.ready(); });
afterAll(async () => { await app.close(); });

describe('production server authentication wiring', () => {
  it.each(['/healthz', '/readyz'])('keeps %s public', async (url) => {
    expect((await app.inject({ method: 'GET', url })).statusCode).toBe(200);
  });

  it('lets a guest use the issued token to access their own player', async () => {
    const auth = await app.inject({ method: 'POST', url: '/auth/guest', payload: {} });
    expect(auth.statusCode).toBe(200);
    const result = await app.inject({ method: 'GET', url: `/players/${playerId}`,
      headers: { authorization: `Bearer ${auth.json().token}` } });
    expect(result.statusCode).toBe(200);
    expect(result.json().player.id).toBe(playerId);
  });

  it.each([undefined, 'Bearer invalid', `Bearer ${jwt.sign({ sub: playerId }, 'wrong-signing-key')}`,
    `Bearer ${jwt.sign({ sub: playerId }, env.JWT_SECRET, { expiresIn: -1 })}`])(
    'rejects missing, malformed, wrongly signed and expired tokens', async (authorization) => {
      const result = await app.inject({ method: 'GET', url: '/missions',
        headers: authorization ? { authorization } : {} });
      expect(result.statusCode).toBe(401);
      expect(result.json().error).toBe('unauthorized');
    });

  it('rejects another player even with a valid token', async () => {
    const token = jwt.sign({ sub: playerId }, env.JWT_SECRET);
    const result = await app.inject({ method: 'GET', url: '/players/22222222-2222-4222-8222-222222222222',
      headers: { authorization: `Bearer ${token}` } });
    expect(result.statusCode).toBe(403);
  });
});
