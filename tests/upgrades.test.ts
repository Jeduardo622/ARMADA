import { describe, it, expect, beforeAll, afterAll, beforeEach } from 'vitest';
import { buildServer } from '../src/app.js';
import {
  MAX_UPGRADE_TIER,
  UPGRADE_COMPONENTS,
  UPGRADE_COST_TABLE,
  upgradeCostsFor
} from '../src/economy/upgrades.js';

const PLAYER_ID = '11111111-1111-1111-1111-111111111111';
const OTHER_PLAYER_ID = '22222222-2222-2222-2222-222222222222';
const REWARD_ITEM_KEYS = ['gold', 'timber', 'ore', 'captain_shard', 'cosmetic_token'];

const app = buildServer({ testing: true });

// The testing preHandler in buildServer stamps a placeholder user id; this
// later hook wins so ownership checks pass for PLAYER_ID.
app.addHook('preHandler', async (request) => {
  request.user = { id: PLAYER_ID };
});

// In-memory stand-ins for the prisma stub so owned tiers and inventory
// quantities persist across requests within a test.
const upgradeStore = new Map<string, number>();
const inventoryStore = new Map<string, number>();
let transactionCalls = 0;
let inTransaction = false;
let claimUpdateArgs: Array<Record<string, unknown>> = [];
let costUpdateArgs: Array<Record<string, unknown>> = [];
let simulateClaimConflict = false;
let simulateLostCreateRace = false;
// Set when a simulated create race loses: the winner's committed row survives
// the loser's rollback, mirroring a real database.
let winnerRowAfterRace: { component: string; tier: number } | null = null;

/* eslint-disable @typescript-eslint/no-explicit-any */
const prisma = app.prisma as any;
prisma.player.findUnique = async (args: any) =>
  args?.where?.id === PLAYER_ID ? { id: PLAYER_ID } : null;
prisma.playerShipUpgrade.findMany = async (args: any) =>
  args?.where?.playerId === PLAYER_ID
    ? [...upgradeStore.entries()].map(([component, tier]) => ({
        playerId: PLAYER_ID,
        component,
        tier
      }))
    : [];
prisma.playerShipUpgrade.findUnique = async (args: any) => {
  const { playerId, component } = args.where.playerId_component;
  const tier = upgradeStore.get(component);
  return playerId === PLAYER_ID && tier !== undefined ? { playerId, component, tier } : null;
};
prisma.playerShipUpgrade.updateMany = async (args: any) => {
  if (!inTransaction) {
    throw new Error('playerShipUpgrade.updateMany called outside a transaction');
  }
  claimUpdateArgs.push(args);
  if (simulateClaimConflict) {
    // Emulate losing a concurrent tier claim: the row no longer matches the
    // conditional where clause because the winner already bumped the tier.
    simulateClaimConflict = false;
    return { count: 0 };
  }
  if (upgradeStore.get(args.where.component) !== args.where.tier) {
    return { count: 0 };
  }
  upgradeStore.set(args.where.component, args.data.tier);
  return { count: 1 };
};
prisma.playerShipUpgrade.create = async (args: any) => {
  if (!inTransaction) {
    throw new Error('playerShipUpgrade.create called outside a transaction');
  }
  if (simulateLostCreateRace) {
    // Emulate losing a concurrent create: the winner's tier-1 row commits and
    // this create fails on the unique constraint.
    simulateLostCreateRace = false;
    winnerRowAfterRace = { component: args.data.component, tier: 1 };
    const error = new Error('unique constraint') as Error & { code?: string };
    error.code = 'P2002';
    throw error;
  }
  if (upgradeStore.has(args.data.component)) {
    const error = new Error('unique constraint') as Error & { code?: string };
    error.code = 'P2002';
    throw error;
  }
  upgradeStore.set(args.data.component, args.data.tier);
  return args.data;
};
prisma.inventoryItem.updateMany = async (args: any) => {
  if (!inTransaction) {
    throw new Error('inventoryItem.updateMany called outside a transaction');
  }
  costUpdateArgs.push(args);
  const current = inventoryStore.get(args.where.itemKey) ?? 0;
  if (current < args.where.quantity.gte) {
    return { count: 0 };
  }
  inventoryStore.set(args.where.itemKey, current - args.data.quantity.decrement);
  return { count: 1 };
};
prisma.$transaction = async (arg: any) => {
  transactionCalls += 1;
  if (typeof arg === 'function') {
    inTransaction = true;
    // Emulate rollback: snapshot both stores and restore them on throw.
    const upgradeSnapshot = new Map(upgradeStore);
    const inventorySnapshot = new Map(inventoryStore);
    try {
      return await arg(prisma);
    } catch (error) {
      upgradeStore.clear();
      for (const [key, value] of upgradeSnapshot) upgradeStore.set(key, value);
      inventoryStore.clear();
      for (const [key, value] of inventorySnapshot) inventoryStore.set(key, value);
      if (winnerRowAfterRace) {
        upgradeStore.set(winnerRowAfterRace.component, winnerRowAfterRace.tier);
        winnerRowAfterRace = null;
      }
      throw error;
    } finally {
      inTransaction = false;
    }
  }
  return Promise.all(arg);
};
/* eslint-enable @typescript-eslint/no-explicit-any */

beforeAll(async () => {
  await app.ready();
});

afterAll(async () => {
  await app.close();
});

beforeEach(() => {
  upgradeStore.clear();
  inventoryStore.clear();
  transactionCalls = 0;
  claimUpdateArgs = [];
  costUpdateArgs = [];
  simulateClaimConflict = false;
  simulateLostCreateRace = false;
  winnerRowAfterRace = null;
});

const seedInventory = (items: Record<string, number>) => {
  for (const [itemKey, quantity] of Object.entries(items)) {
    inventoryStore.set(itemKey, quantity);
  }
};

const purchase = (overrides: Record<string, unknown> = {}) =>
  app.inject({
    method: 'POST',
    url: '/upgrades/purchase',
    payload: { playerId: PLAYER_ID, component: 'cannon', tier: 1, ...overrides }
  });

describe('upgrade catalog', () => {
  it('covers every component with tiers 1..max and known item costs', () => {
    expect(Object.keys(UPGRADE_COST_TABLE).sort()).toEqual([...UPGRADE_COMPONENTS].sort());
    for (const component of UPGRADE_COMPONENTS) {
      for (let tier = 1; tier <= MAX_UPGRADE_TIER; tier += 1) {
        const costs = upgradeCostsFor(component, tier);
        expect(costs.length).toBeGreaterThan(0);
        for (const cost of costs) {
          expect(REWARD_ITEM_KEYS).toContain(cost.itemKey);
          expect(Number.isInteger(cost.quantity)).toBe(true);
          expect(cost.quantity).toBeGreaterThan(0);
        }
      }
    }
  });

  it('lists the catalog and the caller-owned tiers, defaulting to zero', async () => {
    upgradeStore.set('sail', 2);

    const res = await app.inject({ method: 'GET', url: '/upgrades' });
    expect(res.statusCode).toBe(200);

    const body = res.json();
    expect(body.catalog).toHaveLength(UPGRADE_COMPONENTS.length);
    for (const entry of body.catalog) {
      expect(UPGRADE_COMPONENTS).toContain(entry.component);
      expect(entry.tiers.map((t: { tier: number }) => t.tier)).toEqual([1, 2, 3]);
    }
    expect(body.owned).toEqual([
      { component: 'cannon', tier: 0 },
      { component: 'sail', tier: 2 },
      { component: 'hull', tier: 0 }
    ]);
  });
});

describe('upgrade purchase', () => {
  it('buys the next tier, decrements inventory, and records the tier', async () => {
    seedInventory({ gold: 1000, ore: 500 });

    const first = await purchase();
    expect(first.statusCode).toBe(200);
    expect(first.json().upgrade).toEqual({ playerId: PLAYER_ID, component: 'cannon', tier: 1 });
    expect(first.json().spent).toEqual(upgradeCostsFor('cannon', 1));
    expect(upgradeStore.get('cannon')).toBe(1);
    expect(inventoryStore.get('gold')).toBe(900);
    expect(inventoryStore.get('ore')).toBe(480);

    const second = await purchase({ tier: 2 });
    expect(second.statusCode).toBe(200);
    expect(second.json().upgrade.tier).toBe(2);
    expect(second.json().spent).toEqual(upgradeCostsFor('cannon', 2));
    expect(upgradeStore.get('cannon')).toBe(2);
    expect(inventoryStore.get('gold')).toBe(650);
    expect(inventoryStore.get('ore')).toBe(430);
    expect(transactionCalls).toBe(2);
  });

  it('rejects insufficient funds without any partial mutation', async () => {
    seedInventory({ gold: 1000, ore: 5 });

    const res = await purchase();
    expect(res.statusCode).toBe(400);
    expect(res.json()).toEqual({ error: 'insufficient_funds', itemKey: 'ore' });
    // The tier claim and the gold decrement both rolled back with the failure.
    expect(upgradeStore.size).toBe(0);
    expect(inventoryStore.get('gold')).toBe(1000);
    expect(inventoryStore.get('ore')).toBe(5);
  });

  it('cannot skip tiers: the claim is conditional on the current tier and max tier is rejected', async () => {
    seedInventory({ gold: 10_000, ore: 10_000 });
    upgradeStore.set('cannon', 1);

    const skipped = await purchase({ tier: 3 });
    expect(skipped.statusCode).toBe(409);
    expect(skipped.json()).toEqual({ error: 'tier_conflict', component: 'cannon', currentTier: 1 });
    expect(upgradeStore.get('cannon')).toBe(1);
    expect(inventoryStore.get('gold')).toBe(10_000);

    const res = await purchase({ tier: 2 });
    expect(res.statusCode).toBe(200);
    expect(res.json().upgrade.tier).toBe(2);
    // The claim is a conditional tier transition, not a read-then-write.
    expect(claimUpdateArgs[0]).toMatchObject({
      where: { playerId: PLAYER_ID, component: 'cannon', tier: 1 },
      data: { tier: 2 }
    });
    // Cost decrements are conditional on sufficient quantity.
    expect(costUpdateArgs[0]).toMatchObject({
      where: { playerId: PLAYER_ID, itemKey: 'gold', quantity: { gte: 250 } },
      data: { quantity: { decrement: 250 } }
    });

    upgradeStore.set('cannon', MAX_UPGRADE_TIER);
    const capped = await purchase({ tier: MAX_UPGRADE_TIER });
    expect(capped.statusCode).toBe(400);
    expect(capped.json().error).toBe('max_tier_reached');
    expect(upgradeStore.get('cannon')).toBe(MAX_UPGRADE_TIER);
    expect(inventoryStore.get('gold')).toBe(9750);
  });

  it('rejects a replayed purchase without charging the next tier', async () => {
    seedInventory({ gold: 1000, ore: 500 });

    const first = await purchase({ tier: 1 });
    expect(first.statusCode).toBe(200);
    expect(inventoryStore.get('gold')).toBe(900);

    // A lost response or double-click replays the same request; the committed
    // tier no longer matches tier + 1, so nothing is charged again.
    const replay = await purchase({ tier: 1 });
    expect(replay.statusCode).toBe(409);
    expect(replay.json()).toEqual({ error: 'tier_conflict', component: 'cannon', currentTier: 1 });
    expect(upgradeStore.get('cannon')).toBe(1);
    expect(inventoryStore.get('gold')).toBe(900);
    expect(inventoryStore.get('ore')).toBe(480);
  });

  it('claims once when concurrent purchases race on an existing tier', async () => {
    seedInventory({ gold: 1000, ore: 500 });
    upgradeStore.set('cannon', 1);
    simulateClaimConflict = true;

    const res = await purchase({ tier: 2 });
    expect(res.statusCode).toBe(409);
    expect(res.json().error).toBe('upgrade_conflict');
    expect(upgradeStore.get('cannon')).toBe(1);
    expect(inventoryStore.get('gold')).toBe(1000);
    expect(inventoryStore.get('ore')).toBe(500);
    expect(transactionCalls).toBe(1);
  });

  it('claims once when concurrent first purchases race on create', async () => {
    seedInventory({ gold: 1000, ore: 500 });
    simulateLostCreateRace = true;

    const res = await purchase();
    expect(res.statusCode).toBe(409);
    expect(res.json().error).toBe('upgrade_conflict');
    // The winner's committed tier-1 row stands; the loser spent nothing.
    expect(upgradeStore.get('cannon')).toBe(1);
    expect(inventoryStore.get('gold')).toBe(1000);
    expect(inventoryStore.get('ore')).toBe(500);
    expect(transactionCalls).toBe(1);
  });

  it('rejects purchases for a player the caller does not own', async () => {
    const res = await purchase({ playerId: OTHER_PLAYER_ID });
    expect(res.statusCode).toBe(403);
    expect(res.json().error).toBe('forbidden');
    expect(transactionCalls).toBe(0);
  });

  it('rejects unknown components and out-of-range tiers without touching state', async () => {
    seedInventory({ gold: 1000 });

    const badComponent = await purchase({ component: 'mast' });
    expect(badComponent.statusCode).toBe(400);
    const badTier = await purchase({ tier: MAX_UPGRADE_TIER + 1 });
    expect(badTier.statusCode).toBe(400);
    expect(transactionCalls).toBe(0);
    expect(inventoryStore.get('gold')).toBe(1000);
    expect(upgradeStore.size).toBe(0);
  });
});

describe('inventory grant gating', () => {
  it('keeps grants behind the trusted-service flag, separate from inventory_api', async () => {
    // The mint route must not ride the player-facing inventory_api flag:
    // emulate production where inventory_api is on and inventory_grant_api
    // is seeded disabled.
    /* eslint-disable @typescript-eslint/no-explicit-any */
    const flags = app.flags as any;
    const originalIsEnabled = flags.isEnabled;
    const originalFlagLookup = prisma.featureFlag.findUnique;
    flags.isEnabled = (name: string) => name !== 'inventory_grant_api';
    prisma.featureFlag.findUnique = async (args: any) =>
      args?.where?.name === 'inventory_grant_api' ? { enabled: false } : { enabled: true };
    /* eslint-enable @typescript-eslint/no-explicit-any */

    try {
      const grant = await app.inject({
        method: 'POST',
        url: `/inventory/${PLAYER_ID}/grant`,
        payload: { itemKey: 'gold', quantity: 1_000_000 }
      });
      expect(grant.statusCode).toBe(403);
      expect(grant.json()).toEqual({ error: 'feature_disabled', flag: 'inventory_grant_api' });

      // Purchasing stays available under inventory_api alone.
      seedInventory({ gold: 1000, ore: 500 });
      const res = await purchase({ tier: 1 });
      expect(res.statusCode).toBe(200);
    } finally {
      flags.isEnabled = originalIsEnabled;
      prisma.featureFlag.findUnique = originalFlagLookup;
    }
  });
});
