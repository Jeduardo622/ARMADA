import Fastify from 'fastify';
import type { PrismaClient } from '@prisma/client';
import jwt from 'jsonwebtoken';
import { afterEach, describe, expect, it } from 'vitest';
import { authPlugin } from '../src/plugins/auth.js';
import { registerPlayerRoutes } from '../src/routes/player.js';

const servers: ReturnType<typeof Fastify>[] = [];
afterEach(async () => { await Promise.all(servers.splice(0).map(app => app.close())); });
async function server() {
  const profiles = new Map<string, { playerId: string; xp: number; trainingSequence: number; firstMate: string | null; gunneryChief: string | null }>();
  const shards = new Map([['alice', 40], ['bob', 0]]);
  const db = {
    player: { findUnique: async ({ where }: { where: { id: string } }) => ['alice', 'bob'].includes(where.id) ? { id: where.id } : null },
    playerProgression: {
      upsert: async ({ where }: { where: { playerId: string } }) => {
        if (!profiles.has(where.playerId)) profiles.set(where.playerId, { playerId: where.playerId, xp: 0, trainingSequence: 0, firstMate: null, gunneryChief: null });
        return profiles.get(where.playerId)!;
      },
      findUniqueOrThrow: async ({ where }: { where: { playerId: string } }) => profiles.get(where.playerId)!,
      update: async ({ where, data }: { where: { playerId: string }; data: object }) => Object.assign(profiles.get(where.playerId)!, data)
    },
    inventoryItem: { updateMany: async ({ where }: { where: { playerId: string } }) => {
      const count = shards.get(where.playerId) ?? 0;
      if (count < 1) return { count: 0 };
      shards.set(where.playerId, count - 1); return { count: 1 };
    } },
    $queryRaw: async () => [],
    $executeRaw: async (_sql: TemplateStringsArray, playerId: string) => db.playerProgression.upsert({ where: { playerId } }),
    $transaction: async <T>(callback: (tx: unknown) => Promise<T>): Promise<T> => callback(db)
  };
  const app = Fastify({ logger: false }); servers.push(app);
  app.decorate('prisma', db as unknown as PrismaClient);
  await app.register(authPlugin); registerPlayerRoutes(app); await app.ready();
  const headers = (id = 'alice') => ({ authorization: `Bearer ${jwt.sign({}, process.env.JWT_SECRET!, { subject: id })}` });
  return { app, headers, profiles, shards };
}

describe('owned starter progression', () => {
  it('starts with the documented captain and unassigned owned crew, without active bonuses', async () => {
    const { app, headers } = await server();
    const result = await app.inject({ url: '/players/me/progression', headers: headers() });
    expect(result.statusCode).toBe(200);
    expect(result.headers['cache-control']).toBe('no-store');
    expect(result.json()).toMatchObject({ captain: { id: 'aurora_black', name: 'Aurora Black', rarity: 'Rare', xp: 0, level: 1, nextLevelXp: 100 }, training: { sequence: 0, nextSequence: 1 }, crew: { firstMate: null, gunneryChief: null } });
    expect(result.json().crew.roster.map((member: { id: string }) => member.id)).toEqual(['calico_jim', 'one_eyed_ella']);
    expect(JSON.stringify(result.json())).not.toMatch(/damage|bonus/i);
  });

  it('spends once per exact next sequence, derives levels and never spends at the cap', async () => {
    const { app, headers, shards, profiles } = await server();
    const train = (sequence: number) => app.inject({ method: 'POST', url: '/players/me/progression/train', headers: headers(), payload: { sequence } });
    expect((await train(2)).statusCode).toBe(409);
    for (let sequence = 1; sequence <= 28; sequence++) {
      const response = await train(sequence);
      expect(response.statusCode).toBe(200);
      if ([4, 10, 18, 28].includes(sequence)) expect(response.json().captain.level).toBe([4, 10, 18, 28].indexOf(sequence) + 2);
    }
    expect(profiles.get('alice')).toMatchObject({ xp: 700, trainingSequence: 28 });
    expect(shards.get('alice')).toBe(12);
    const retry = await train(1);
    expect(retry.statusCode).toBe(200);
    expect(retry.json().captain).toMatchObject({ level: 5, xp: 700, nextLevelXp: null });
    expect((await train(29)).json().error).toBe('captain_at_cap');
    expect(shards.get('alice')).toBe(12);
    expect((await app.inject({ method: 'POST', url: '/players/me/progression/train', headers: headers('bob'), payload: { sequence: 1 } })).json().error).toBe('insufficient_shards');
    expect(profiles.get('bob')?.xp).toBe(0);
  });

  it('validates slots, persists assignments, clears them and rejects public identity or XP authority', async () => {
    const { app, headers } = await server();
    const equip = (payload: object) => app.inject({ method: 'POST', url: '/players/me/progression/crew', headers: headers(), payload });
    expect((await equip({ firstMate: 'one_eyed_ella', gunneryChief: null })).statusCode).toBe(400);
    expect((await equip({ firstMate: 'unowned', gunneryChief: null })).statusCode).toBe(400);
    expect((await equip({ firstMate: 'calico_jim', gunneryChief: 'one_eyed_ella' })).statusCode).toBe(200);
    const read = (await app.inject({ url: '/players/me/progression', headers: headers() })).json();
    expect(read.crew).toMatchObject({ firstMate: 'calico_jim', gunneryChief: 'one_eyed_ella' });
    const bob = (await app.inject({ url: '/players/me/progression', headers: headers('bob') })).json();
    expect(bob.crew).toMatchObject({ firstMate: null, gunneryChief: null });
    expect((await equip({ firstMate: null, gunneryChief: null })).statusCode).toBe(200);
    expect((await equip({ firstMate: null, gunneryChief: null, playerId: 'bob' })).statusCode).toBe(400);
    expect((await app.inject({ url: '/players/me/progression?playerId=bob', headers: headers() })).statusCode).toBe(400);
    expect((await app.inject({ method: 'POST', url: '/players/me/progression/train', headers: headers(), payload: { sequence: 1, xp: 700 } })).statusCode).toBe(400);
    expect((await app.inject({ url: '/players/me/progression' })).statusCode).toBe(401);
  });
});
