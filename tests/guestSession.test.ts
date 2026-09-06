import { createHash, randomUUID } from 'node:crypto';
import Fastify from 'fastify';
import type { PrismaClient } from '@prisma/client';
import jwt from 'jsonwebtoken';
import { afterEach, describe, expect, it } from 'vitest';
import { authPlugin } from '../src/plugins/auth.js';
import { registerAuthRoutes } from '../src/routes/auth.js';
import { registerPlayerRoutes } from '../src/routes/player.js';

type Player = { id: string; externalId: string | null; displayName?: string; region?: string };
type Credential = { id: string; playerId: string; digest: string; expiresAt: Date; revokedAt: Date | null };
const servers: ReturnType<typeof Fastify>[] = [];
afterEach(async () => { await Promise.all(servers.splice(0).map((app) => app.close())); });

async function server() {
  const players = new Map<string, Player>();
  const credentials = new Map<string, Credential>();
  const progress = new Map<string, object[]>();
  const db = {
    player: {
      create: async ({ data }: { data: Partial<Player> }) => {
        const player = { ...data, id: randomUUID(), externalId: data.externalId ?? null };
        players.set(player.id, player); return player;
      },
      findUnique: async ({ where }: { where: { id?: string; externalId?: string } }) =>
        [...players.values()].find((p) => where.id ? p.id === where.id : p.externalId === where.externalId) ?? null
    },
    guestCredential: {
      create: async ({ data }: { data: Omit<Credential, 'id' | 'revokedAt'> }) => {
        const row = { ...data, id: randomUUID(), revokedAt: null };
        credentials.set(row.digest, row); return row;
      },
      findUnique: async ({ where }: { where: { digest: string } }) => {
        const row = credentials.get(where.digest);
        return row ? { ...row, player: players.get(row.playerId) ?? null } : null;
      }
    },
    missionProgress: {
      findMany: async ({ where }: { where: { playerId: string } }) => progress.get(where.playerId) ?? []
    },
    $transaction: async <T>(callback: (tx: unknown) => Promise<T>): Promise<T> => callback(db)
  };
  const app = Fastify({ logger: false });
  servers.push(app);
  app.decorate('prisma', db as unknown as PrismaClient);
  // Exercise the real authentication hook, never buildServer({testing:true}).
  await app.register(authPlugin);
  registerAuthRoutes(app);
  registerPlayerRoutes(app);
  await app.ready();
  return { app, players, credentials, progress };
}

describe('durable guest credentials through real authentication', () => {
  it('stores only a digest and restores the same player with a credential', async () => {
    const { app, credentials, players } = await server();
    const created = await app.inject({ method: 'POST', url: '/auth/guest', payload: { displayName: 'Captain' } });
    expect(created.statusCode).toBe(200);
    expect(created.headers['cache-control']).toBe('no-store');
    const body = created.json();
    expect(body.guestCredential).toMatch(/^[A-Za-z0-9_-]{43}$/);
    expect(new Date(body.credentialExpiresAt).getTime() - Date.now()).toBeGreaterThan(179 * 86400000);
    expect(new Date(body.accessExpiresAt).getTime()).toBeGreaterThan(Date.now());
    expect(credentials.size).toBe(1);
    expect([...credentials.values()][0].digest).toBe(createHash('sha256').update(body.guestCredential).digest('hex'));
    expect(JSON.stringify([...credentials.values()])).not.toContain(body.guestCredential);
    const resumed = await app.inject({ method: 'POST', url: '/auth/refresh', payload: { guestCredential: body.guestCredential } });
    expect(resumed.statusCode).toBe(200);
    expect(resumed.headers['cache-control']).toBe('no-store');
    expect(resumed.json().player.id).toBe(body.player.id);
    expect(resumed.json()).not.toHaveProperty('guestCredential');
    expect(players.size).toBe(1);
    const own = await app.inject({ url: `/players/${body.player.id}`, headers: { authorization: `Bearer ${resumed.json().token}` } });
    expect(own.statusCode).toBe(200);
  });

  it('concurrent refresh requests retain a single identity without issuing additional credentials', async () => {
    const { app, players, credentials } = await server();
    const created = (await app.inject({ method: 'POST', url: '/auth/guest', payload: {} })).json();
    const results = await Promise.all(Array.from({ length: 3 }, () => app.inject({
      method: 'POST', url: '/auth/refresh', payload: { guestCredential: created.guestCredential }
    })));
    for (const result of results) {
      expect(result.statusCode).toBe(200);
      expect(result.json().player.id).toBe(created.player.id);
    }
    expect(players.size).toBe(1);
    expect(credentials.size).toBe(1);
  });

  it('never recovers identity using a public external ID', async () => {
    const { app, players } = await server();
    players.set('legacy', { id: 'legacy', externalId: 'public-platform-id' });
    const response = await app.inject({ method: 'POST', url: '/auth/guest', payload: { externalId: 'public-platform-id' } });
    expect(response.statusCode).toBe(400);
    expect(response.json().error).toBe('external_id_not_supported');
    expect(players.size).toBe(1);
  });

  it('keeps empty legacy externalId compatible as new registration only', async () => {
    const { app } = await server();
    const response = await app.inject({ method: 'POST', url: '/auth/guest', payload: { externalId: '' } });
    expect(response.statusCode).toBe(200);
    expect(response.json().player.externalId).toBeNull();
  });

  it('returns one generic unauthorized error for missing, malformed, unknown, expired and revoked credentials', async () => {
    const { app, credentials } = await server();
    for (const payload of [{}, { guestCredential: 'short' }, { guestCredential: 'x'.repeat(43) }, { guestCredential: 'x'.repeat(43), playerId: 'other' }]) {
      const response = await app.inject({ method: 'POST', url: '/auth/refresh', payload });
      expect(response.statusCode).toBe(401);
      expect(response.json()).toEqual({ error: 'unauthorized' });
    }
    const created = (await app.inject({ method: 'POST', url: '/auth/guest', payload: {} })).json();
    const credential = [...credentials.values()][0];
    credential.expiresAt = new Date(0);
    expect((await app.inject({ method: 'POST', url: '/auth/refresh', payload: { guestCredential: created.guestCredential } })).statusCode).toBe(401);
    credential.expiresAt = new Date(Date.now() + 86400000);
    credential.revokedAt = new Date();
    expect((await app.inject({ method: 'POST', url: '/auth/refresh', payload: { guestCredential: created.guestCredential } })).statusCode).toBe(401);
  });

  it('enrolls a legacy JWT only for its authenticated subject', async () => {
    const { app, players, credentials } = await server();
    const owner = { id: randomUUID(), externalId: 'legacy-public-id' };
    players.set(owner.id, owner);
    const token = jwt.sign({ sub: owner.id }, process.env.JWT_SECRET!, { expiresIn: '1h' });
    const auth = { authorization: `Bearer ${token}` };
    const missing = await app.inject({ method: 'POST', url: '/auth/guest/credential', payload: {} });
    expect(missing.statusCode).toBe(401);
    const spoof = await app.inject({ method: 'POST', url: '/auth/guest/credential', payload: { playerId: randomUUID() }, headers: auth });
    expect(spoof.statusCode).toBe(400);
    const enrolled = await app.inject({ method: 'POST', url: '/auth/guest/credential', payload: {}, headers: auth });
    expect(enrolled.statusCode).toBe(200);
    expect(enrolled.json().player.id).toBe(owner.id);
    expect([...credentials.values()][0].playerId).toBe(owner.id);
    const expired = jwt.sign({ sub: owner.id }, process.env.JWT_SECRET!, { expiresIn: -1 });
    expect((await app.inject({ method: 'POST', url: '/auth/guest/credential', payload: {}, headers: { authorization: `Bearer ${expired}` } })).statusCode).toBe(401);
    const forged = jwt.sign({ sub: owner.id }, 'wrong-test-signing-secret', { expiresIn: '1h' });
    expect((await app.inject({ method: 'POST', url: '/auth/guest/credential', payload: {}, headers: { authorization: `Bearer ${forged}` } })).statusCode).toBe(401);
    players.delete(owner.id);
    expect((await app.inject({ method: 'POST', url: '/auth/guest/credential', payload: {}, headers: auth })).statusCode).toBe(401);
    expect((await app.inject({ method: 'POST', url: '/auth/refresh', payload: { guestCredential: enrolled.json().guestCredential } })).statusCode).toBe(401);
  });

  it('returns only progress owned by the bearer subject and rejects ownership selectors', async () => {
    const { app, progress } = await server();
    const alice = (await app.inject({ method: 'POST', url: '/auth/guest', payload: {} })).json();
    const bob = (await app.inject({ method: 'POST', url: '/auth/guest', payload: {} })).json();
    progress.set(alice.player.id, [{ mission: { code: 'mission-01-fair-wind' }, status: 'COMPLETED', bestScore: 9, lastResult: { result: 'win' } }]);
    progress.set(bob.player.id, [{ mission: { code: 'secret-bob-mission' }, status: 'FAILED', lastResult: null }]);
    const headers = { authorization: `Bearer ${alice.token}` };
    expect((await app.inject({ url: '/players/me/progress' })).statusCode).toBe(401);
    const own = await app.inject({ url: '/players/me/progress', headers });
    expect(own.statusCode).toBe(200);
    expect(own.json()).toEqual({ progress: [{ missionCode: 'mission-01-fair-wind', status: 'COMPLETED', lastResult: { result: 'win' } }] });
    expect((await app.inject({ url: `/players/me/progress?playerId=${bob.player.id}`, headers })).statusCode).toBe(400);
    expect((await app.inject({ url: `/players/${bob.player.id}`, headers })).statusCode).toBe(403);
  });
});
