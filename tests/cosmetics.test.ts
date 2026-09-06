import { afterAll, beforeEach, describe, expect, it, vi } from 'vitest';
import jwt from 'jsonwebtoken';
import { env } from '../src/config.js';
import { buildServer } from '../src/app.js';

const memory = vi.hoisted(() => ({ sails: new Map<string, string>() }));
vi.mock('../src/plugins/prisma.js', async () => {
  const { default: fp } = await import('fastify-plugin');
  return { prismaPlugin: fp(async (app) => {
    app.decorate('prisma', {
      player: { findUnique: async ({ where }: { where: { id: string } }) => ({ id: where.id }) },
      playerCosmetics: {
        findUnique: async ({ where }: { where: { playerId: string } }) => memory.sails.has(where.playerId)
          ? { playerId: where.playerId, equippedSail: memory.sails.get(where.playerId) } : null,
        upsert: async ({ where, create, update }: {
          where: { playerId: string }; create: { equippedSail: string }; update: { equippedSail: string }
        }) => {
          const equippedSail = memory.sails.has(where.playerId) ? update.equippedSail : create.equippedSail;
          memory.sails.set(where.playerId, equippedSail);
          return { playerId: where.playerId, equippedSail };
        }
      }
    } as unknown as typeof app.prisma);
  }) };
});
vi.mock('../src/plugins/redis.js', async () => {
  const { default: fp } = await import('fastify-plugin');
  return { redisPlugin: fp(async () => {}) };
});
vi.mock('../src/plugins/storage.js', async () => {
  const { default: fp } = await import('fastify-plugin');
  return { storagePlugin: fp(async () => {}) };
});
vi.mock('../src/plugins/flags.js', async () => {
  const { default: fp } = await import('fastify-plugin');
  return { flagPlugin: fp(async () => {}) };
});

const app = buildServer();
const alice = '11111111-1111-4111-8111-111111111111';
const bob = '22222222-2222-4222-8222-222222222222';
const auth = (id = alice) => ({ authorization: `Bearer ${jwt.sign({ sub: id }, env.JWT_SECRET)}` });
beforeEach(() => memory.sails.clear());
afterAll(async () => { await app.close(); });

describe('starter sail equipment', () => {
  it.each(['/players/me/cosmetics', '/players/me/cosmetics/equip'])('authenticates %s before storage', async (url) => {
    const method = url.endsWith('equip') ? 'POST' : 'GET';
    for (const headers of [{}, { authorization: 'Bearer invalid' }]) {
      expect((await app.inject({ method, url, headers, ...(method === 'POST' ? { payload: { sailId: 'harbor_blue' } } : {}) })).statusCode).toBe(401);
    }
    expect(memory.sails.size).toBe(0);
  });

  it('returns a free owned catalog and nonmutating default; persists explicit apply per player', async () => {
    const initial = await app.inject({ method: 'GET', url: '/players/me/cosmetics', headers: auth() });
    expect(initial.statusCode).toBe(200);
    expect(initial.headers['cache-control']).toBe('no-store');
    expect(initial.json()).toEqual({
      catalogVersion: 1,
      catalog: [
        { sailId: 'default', displayName: 'Default', sailColor: null },
        { sailId: 'harbor_blue', displayName: 'Harbor Blue', sailColor: '#6C9FC0' }
      ],
      ownedIds: ['default', 'harbor_blue'], equippedId: 'default'
    });
    expect(memory.sails.size).toBe(0);
    const applied = await app.inject({ method: 'POST', url: '/players/me/cosmetics/equip', headers: auth(), payload: { sailId: 'harbor_blue' } });
    expect(applied.statusCode).toBe(200);
    expect(applied.json().equippedId).toBe('harbor_blue');
    expect((await app.inject({ method: 'GET', url: '/players/me/cosmetics', headers: auth() })).json().equippedId).toBe('harbor_blue');
    expect((await app.inject({ method: 'GET', url: '/players/me/cosmetics', headers: auth(bob) })).json().equippedId).toBe('default');
    expect((await app.inject({ method: 'POST', url: '/players/me/cosmetics/equip', headers: auth(), payload: { sailId: 'default' } })).json().equippedId).toBe('default');
  });

  it.each([{}, { sailId: 'unknown' }, { sailId: null }, { sailId: 'harbor_blue', playerId: bob },
    { sailId: 'harbor_blue', damage: 999 }, { sailId: 'harbor_blue', upgrades: { hull: 3 } },
    { sailId: 'harbor_blue', price: 0 }])('rejects malformed and extra-field equipment payload %j', async (payload) => {
    const response = await app.inject({ method: 'POST', url: '/players/me/cosmetics/equip', headers: auth(), payload });
    expect(response.statusCode).toBe(400);
    expect(memory.sails.size).toBe(0);
  });

  it('rejects player selection through query parameters', async () => {
    for (const [method, url] of [['GET', '/players/me/cosmetics'], ['POST', '/players/me/cosmetics/equip']] as const) {
      const response = await app.inject({ method, url: `${url}?playerId=${bob}`, headers: auth(),
        ...(method === 'POST' ? { payload: { sailId: 'harbor_blue' } } : {}) });
      expect(response.statusCode).toBe(400);
    }
    expect(memory.sails.size).toBe(0);
  });
});
