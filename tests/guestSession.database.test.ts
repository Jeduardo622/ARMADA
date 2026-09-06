import { createHash } from 'node:crypto';
import Fastify from 'fastify';
import { PrismaClient } from '@prisma/client';
import { afterAll, beforeAll, describe, expect, it } from 'vitest';
import { authPlugin } from '../src/plugins/auth.js';
import { registerAuthRoutes } from '../src/routes/auth.js';
import { registerPlayerRoutes } from '../src/routes/player.js';
import { registerMissionRoutes } from '../src/routes/missions.js';
import { campaignWinFixtures } from './fixtures/campaignWins.js';

// Opt-in against an independently created, migrated, disposable database only.
// Never falls back to DATABASE_URL or the main local Armada database.
const url = process.env.GUEST_SESSION_TEST_DATABASE_URL;
if (url) {
  const parsed = new URL(url);
  if (!['localhost', '127.0.0.1'].includes(parsed.hostname) || parsed.pathname !== '/armada_guest_session_test') {
    throw new Error('Guest-session integration requires a loopback armada_guest_session_test database');
  }
}

describe.runIf(Boolean(url))('guest credentials on isolated PostgreSQL', () => {
  let prisma: PrismaClient;
  const playerIds: string[] = [];
  const missionCodes: string[] = [];
  beforeAll(async () => {
    prisma = new PrismaClient({ datasources: { db: { url } } });
    await prisma.$connect();
  });
  afterAll(async () => {
    if (!prisma) return;
    await prisma.player.deleteMany({ where: { id: { in: playerIds } } });
    await prisma.mission.deleteMany({ where: { code: { in: missionCodes } } });
    await prisma.$disconnect();
  });

  async function server() {
    const app = Fastify({ logger: false });
    app.decorate('prisma', prisma);
    app.decorate('flags', { isEnabled: () => true, ready: () => true, getVariant: () => ({ name: 'default', enabled: true }) });
    await app.register(authPlugin);
    registerAuthRoutes(app);
    registerPlayerRoutes(app);
    registerMissionRoutes(app);
    await app.ready();
    return app;
  }

  it('restores identity and persisted mission progress after a server restart, then refuses revocation', async () => {
    const first = await server();
    let session: { player: { id: string }; guestCredential: string };
    try {
      const response = await first.inject({ method: 'POST', url: '/auth/guest', payload: {} });
      expect(response.statusCode).toBe(200);
      session = response.json();
      playerIds.push(session.player.id);
      const stored = await prisma.guestCredential.findMany({ where: { playerId: session.player.id } });
      expect(stored).toHaveLength(1);
      expect(stored[0].digest).toBe(createHash('sha256').update(session.guestCredential).digest('hex'));
      expect(JSON.stringify(stored)).not.toContain(session.guestCredential);
      const code = `guest-session-test-${session.player.id}`;
      missionCodes.push(code);
      const mission = await prisma.mission.create({ data: { code, name: 'Owned test mission' } });
      await prisma.missionProgress.create({ data: {
        playerId: session.player.id, missionId: mission.id, status: 'COMPLETED', lastResult: { result: 'win' }
      } });
    } finally { await first.close(); }
    const second = await server();
    try {
      const restored = await second.inject({ method: 'POST', url: '/auth/refresh', payload: { guestCredential: session!.guestCredential } });
      expect(restored.statusCode).toBe(200);
      expect(restored.json().player.id).toBe(session!.player.id);
      const progress = await second.inject({ url: '/players/me/progress', headers: { authorization: `Bearer ${restored.json().token}` } });
      expect(progress.json()).toEqual({ progress: [{ missionCode: missionCodes[0], status: 'COMPLETED', lastResult: { result: 'win' }, verifiedStars: null, verifiedResult: null }] });
      await prisma.guestCredential.updateMany({ where: { playerId: session!.player.id }, data: { revokedAt: new Date() } });
      const revoked = await second.inject({ method: 'POST', url: '/auth/refresh', payload: { guestCredential: session!.guestCredential } });
      expect(revoked.statusCode).toBe(401);
    } finally { await second.close(); }
  });

  it('rolls back player creation when credential storage fails, and enforces digest uniqueness', async () => {
    const before = await prisma.player.count();
    await expect(prisma.$transaction(async (tx) => {
      const player = await tx.player.create({ data: {} });
      const data = { playerId: player.id, digest: 'transaction-rollback-fixture', expiresAt: new Date(Date.now() + 86400000) };
      await tx.guestCredential.create({ data });
      await tx.guestCredential.create({ data });
    })).rejects.toMatchObject({ code: 'P2002' });
    expect(await prisma.player.count()).toBe(before);
    expect(await prisma.guestCredential.count({ where: { digest: 'transaction-rollback-fixture' } })).toBe(0);
  });

  it('retains the best matching star metadata under concurrent real completions and isolates the owner', async () => {
    const app = await server();
    const fixture = campaignWinFixtures[0];
    try {
      const alice = (await app.inject({ method: 'POST', url: '/auth/guest', payload: {} })).json();
      const bob = (await app.inject({ method: 'POST', url: '/auth/guest', payload: {} })).json();
      playerIds.push(alice.player.id, bob.player.id);
      missionCodes.push(fixture.code);
      const mission = await prisma.mission.create({ data: { code: fixture.code, name: 'Verified star fixture' } });
      const headers = { authorization: `Bearer ${alice.token}` };
      const complete = (seed: number) => app.inject({ method: 'POST', url: `/missions/${fixture.code}/complete`, headers,
        payload: { playerId: alice.player.id, seed, turns: fixture.turns, result: { stars: 999 } } });
      const results = await Promise.all([complete(12), complete(16)]);
      expect(results.map((response) => response.statusCode)).toEqual([200, 200]);
      expect(results.filter((response) => response.json().rewardsGranted.length > 0)).toHaveLength(1);
      expect((await prisma.inventoryItem.findUnique({ where: { playerId_itemKey: { playerId: alice.player.id, itemKey: 'gold' } } }))?.quantity).toBe(100);
      const owned = await app.inject({ url: '/players/me/progress', headers });
      expect(owned.json().progress[0]).toMatchObject({ verifiedStars: 3, verifiedResult: {
        result: 'win', seed: 12, bonusObjectives: { underHullDamageThreshold: true, withinTurnTarget: true }
      } });
      const other = await app.inject({ url: '/players/me/progress', headers: { authorization: `Bearer ${bob.token}` } });
      expect(other.json()).toEqual({ progress: [] });
      const spoof = await app.inject({ method: 'POST', url: `/missions/${fixture.code}/complete`, headers,
        payload: { playerId: bob.player.id, seed: 12, turns: fixture.turns } });
      expect(spoof.statusCode).toBe(403);
      // Pair integrity is enforced by PostgreSQL, not only by the route.
      await expect(prisma.missionProgress.update({
        where: { playerId_missionId: { playerId: alice.player.id, missionId: mission.id } }, data: { verifiedStars: null }
      })).rejects.toThrow();
      const saved = await prisma.missionProgress.findUnique({ where: { playerId_missionId: { playerId: alice.player.id, missionId: mission.id } } });
      expect(saved?.verifiedStars).toBe(3);
    } finally { await app.close(); }
  });
});
