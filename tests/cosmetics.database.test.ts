import Fastify from 'fastify';
import { PrismaClient } from '@prisma/client';
import jwt from 'jsonwebtoken';
import { afterAll, beforeAll, describe, expect, it } from 'vitest';
import { authPlugin } from '../src/plugins/auth.js';
import { registerCosmeticsRoutes } from '../src/routes/cosmetics.js';
import { env } from '../src/config.js';

// Explicit disposable local target only; never use the game's DATABASE_URL.
const url = process.env.COSMETICS_TEST_DATABASE_URL;
if (url) {
  const parsed = new URL(url);
  if (!['localhost', '127.0.0.1'].includes(parsed.hostname) || parsed.pathname !== '/armada_cosmetics_test') {
    throw new Error('Cosmetics integration requires loopback armada_cosmetics_test database');
  }
}

describe.runIf(Boolean(url))('cosmetic equipment on isolated PostgreSQL', () => {
  let prisma: PrismaClient;
  const ids: string[] = [];
  beforeAll(async () => {
    prisma = new PrismaClient({ datasources: { db: { url } } });
    await prisma.$connect();
  });
  afterAll(async () => {
    if (!prisma) return;
    await prisma.player.deleteMany({ where: { id: { in: ids } } });
    expect(await prisma.playerCosmetics.count({ where: { playerId: { in: ids } } })).toBe(0);
    await prisma.$disconnect();
  });
  const auth = (id: string) => ({ authorization: `Bearer ${jwt.sign({ sub: id }, env.JWT_SECRET)}` });
  async function server() {
    const app = Fastify({ logger: false });
    app.decorate('prisma', prisma);
    await app.register(authPlugin);
    registerCosmeticsRoutes(app);
    await app.ready();
    return app;
  }

  it('persists across server restart, isolates players, and applies concurrently to a new row', async () => {
    const alice = await prisma.player.create({ data: { displayName: 'Cosmetic test Alice' } });
    const bob = await prisma.player.create({ data: { displayName: 'Cosmetic test Bob' } });
    ids.push(alice.id, bob.id);
    const first = await server();
    try {
      expect((await first.inject({ method: 'GET', url: '/players/me/cosmetics', headers: auth(alice.id) })).json().equippedId).toBe('default');
      expect(await prisma.playerCosmetics.count({ where: { playerId: alice.id } })).toBe(0);
      const responses = await Promise.all(Array.from({ length: 8 }, () => first.inject({
        method: 'POST', url: '/players/me/cosmetics/equip', headers: auth(alice.id), payload: { sailId: 'harbor_blue' }
      })));
      expect(responses.map((response) => response.statusCode)).toEqual(Array(8).fill(200));
      expect(await prisma.playerCosmetics.count({ where: { playerId: alice.id } })).toBe(1);
      expect(await prisma.inventoryItem.count({ where: { playerId: { in: ids } } })).toBe(0);
    } finally {
      await first.close();
    }
    const restarted = await server();
    try {
      expect((await restarted.inject({ method: 'GET', url: '/players/me/cosmetics', headers: auth(alice.id) })).json().equippedId).toBe('harbor_blue');
      expect((await restarted.inject({ method: 'GET', url: '/players/me/cosmetics', headers: auth(bob.id) })).json().equippedId).toBe('default');
      const forged = await restarted.inject({ method: 'POST', url: '/players/me/cosmetics/equip', headers: auth(bob.id),
        payload: { sailId: 'default', playerId: alice.id, damage: 1000 } });
      expect(forged.statusCode).toBe(400);
      expect((await prisma.playerCosmetics.findUnique({ where: { playerId: alice.id } }))?.equippedSail).toBe('harbor_blue');
    } finally {
      await restarted.close();
    }
  });
});
