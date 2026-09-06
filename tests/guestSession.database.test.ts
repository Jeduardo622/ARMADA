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
      expect((await prisma.playerProgression.findUnique({ where: { playerId: alice.player.id } }))?.xp).toBe(25);
      const bobProfile = await app.inject({ url: '/players/me/progression', headers: { authorization: `Bearer ${bob.token}` } });
      expect(bobProfile.json().captain.xp).toBe(0);
      expect((await app.inject({ url: `/players/me/progression?playerId=${alice.player.id}`, headers: { authorization: `Bearer ${bob.token}` } })).statusCode).toBe(400);
    } finally { await app.close(); }
  });

  it('serializes duplicate training, legacy clear XP and concurrent training without double spending or lost XP', async () => {
    const app = await server();
    try {
      const session = (await app.inject({ method: 'POST', url: '/auth/guest', payload: {} })).json();
      const playerId = session.player.id; playerIds.push(playerId);
      const headers = { authorization: `Bearer ${session.token}` };
      const fixture = campaignWinFixtures[1]; missionCodes.push(fixture.code);
      const mission = await prisma.mission.create({ data: { code: fixture.code, name: 'Legacy XP fixture' } });
      await prisma.missionProgress.create({ data: { playerId, missionId: mission.id, status: 'COMPLETED', lastResult: { xp: 700 } } });
      await prisma.inventoryItem.create({ data: { playerId, itemKey: 'captain_shard', quantity: 3 } });
      const train = (sequence: number) => app.inject({ method: 'POST', url: '/players/me/progression/train', headers, payload: { sequence } });
      const clear = () => app.inject({ method: 'POST', url: `/missions/${fixture.code}/complete`, headers,
        payload: { playerId, seed: fixture.seed, turns: fixture.turns } });
      const results = await Promise.all([train(1), train(1), clear(), clear()]);
      expect(results.map(result => result.statusCode)).toEqual([200, 200, 200, 200]);
      expect(results.slice(2).map(result => result.json().rewardsGranted)).toEqual([[], []]);
      const profile = await prisma.playerProgression.findUniqueOrThrow({ where: { playerId } });
      expect(profile).toMatchObject({ xp: 50, trainingSequence: 1 });
      expect((await prisma.inventoryItem.findUniqueOrThrow({ where: { playerId_itemKey: { playerId, itemKey: 'captain_shard' } } })).quantity).toBe(2);
      expect((await prisma.missionProgress.findUniqueOrThrow({ where: { playerId_missionId: { playerId, missionId: mission.id } } })).captainXpGranted).toBe(true);
      expect((await train(3)).statusCode).toBe(409);
      expect((await train(1)).statusCode).toBe(200);
      await app.inject({ method: 'POST', url: '/players/me/progression/crew', headers, payload: { firstMate: 'calico_jim', gunneryChief: 'one_eyed_ella' } });
      // A fresh authenticated server reads the same persisted profile.
      const reopened = await server();
      try {
        const read = await reopened.inject({ url: '/players/me/progression', headers });
        expect(read.json()).toMatchObject({ captain: { xp: 50, level: 1 }, crew: { firstMate: 'calico_jim', gunneryChief: 'one_eyed_ella' } });
      } finally { await reopened.close(); }
      await prisma.playerProgression.update({ where: { playerId }, data: { xp: 675 } });
      expect((await train(2)).json().captain.xp).toBe(700);
      expect((await train(3)).json().error).toBe('captain_at_cap');
      expect((await prisma.inventoryItem.findUniqueOrThrow({ where: { playerId_itemKey: { playerId, itemKey: 'captain_shard' } } })).quantity).toBe(1);
    } finally { await app.close(); }
  });

  it('validates frozen owned loadouts through real authentication across all ten missions and concurrent profile changes', async () => {
    const app = await server();
    try {
      const owner = (await app.inject({ method: 'POST', url: '/auth/guest', payload: {} })).json();
      const other = (await app.inject({ method: 'POST', url: '/auth/guest', payload: {} })).json();
      playerIds.push(owner.player.id, other.player.id);
      await prisma.playerProgression.create({ data: { playerId: owner.player.id, xp: 250, firstMate: 'calico_jim', gunneryChief: 'one_eyed_ella' } });
      await prisma.playerShipUpgrade.createMany({ data: ['cannon', 'hull'].map(component => ({ playerId: owner.player.id, component, tier: 1 })) });
      await prisma.inventoryItem.create({ data: { playerId: owner.player.id, itemKey: 'captain_shard', quantity: 1 } });
      const headers = { authorization: `Bearer ${owner.token}` };
      const loadout = { schemaVersion: 1, captainLevel: 3, firstMate: 'calico_jim', gunneryChief: 'one_eyed_ella' };
      const upgrades = { cannon: 1, sail: 0, hull: 1 };
      const resolve = (fixture: typeof campaignWinFixtures[number], token = owner.token) => app.inject({ method: 'POST', url: `/missions/${fixture.code}/resolve`,
        headers: { authorization: `Bearer ${token}` }, payload: { seed: fixture.seed, turns: fixture.turns, upgrades, loadout } });
      const first = await resolve(campaignWinFixtures[0]);
      const [training, crew, concurrent, forbidden] = await Promise.all([
        app.inject({ method: 'POST', url: '/players/me/progression/train', headers, payload: { sequence: 1 } }),
        app.inject({ method: 'POST', url: '/players/me/progression/crew', headers, payload: { firstMate: null, gunneryChief: null } }),
        resolve(campaignWinFixtures[0]), resolve(campaignWinFixtures[0], other.token)
      ]);
      expect([training.statusCode, crew.statusCode, concurrent.statusCode, forbidden.statusCode]).toEqual([200, 200, 200, 409]);
      expect(concurrent.json()).toEqual(first.json());
      expect((await resolve(campaignWinFixtures[0])).json()).toEqual(first.json());
      for (const fixture of campaignWinFixtures) {
        missionCodes.push(fixture.code);
        await prisma.mission.upsert({ where: { code: fixture.code }, create: { code: fixture.code, name: 'Frozen loadout fixture' }, update: {} });
        const resolved = await resolve(fixture);
        expect(resolved.statusCode).toBe(200);
        expect(resolved.json().outcome.result).toBe('win');
        const complete = await app.inject({ method: 'POST', url: `/missions/${fixture.code}/complete`, headers,
          payload: { playerId: owner.player.id, seed: fixture.seed, turns: fixture.turns, upgrades, loadout } });
        expect(complete.statusCode).toBe(200);
        expect(complete.json().progress.verifiedResult.bonusObjectives).toEqual(resolved.json().outcome.bonusObjectives);
      }
    } finally { await app.close(); }
  });
});
