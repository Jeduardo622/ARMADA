import { describe, it, expect, beforeAll, afterAll, beforeEach } from 'vitest';
import { buildServer } from '../src/app.js';
import {
  MISSION_REWARD_TABLE,
  missionRewardsForCode,
  type RewardGrant
} from '../src/economy/missionRewards.js';
import {
  MISSION_01_CODE,
  MISSION_01_ENEMY_SHIP_ID,
  MISSION_01_PLAYER_SHIP_ID,
  MISSION_01_TURN_LIMIT
} from '../src/sim/mission01.js';
import { MISSION_02_CODE } from '../src/sim/mission02.js';
import { MISSION_03_CODE } from '../src/sim/mission03.js';
import { MISSION_04_CODE } from '../src/sim/mission04.js';
import { MISSION_05_CODE } from '../src/sim/mission05.js';
import { MISSION_06_CODE } from '../src/sim/mission06.js';
import {
  MISSION_07_CODE,
  MISSION_07_ENEMY_SHIP_IDS,
  MISSION_07_PLAYER_SHIP_IDS,
  MISSION_07_TURN_LIMIT
} from '../src/sim/mission07.js';
import { MISSION_08_CODE } from '../src/sim/mission08.js';
import { MISSION_09_CODE } from '../src/sim/mission09.js';
import { MISSION_10_CODE } from '../src/sim/mission10.js';

const PLAYER_ID = '11111111-1111-1111-1111-111111111111';
const ALL_MISSION_CODES = [
  MISSION_01_CODE,
  MISSION_02_CODE,
  MISSION_03_CODE,
  MISSION_04_CODE,
  MISSION_05_CODE,
  MISSION_06_CODE,
  MISSION_07_CODE,
  MISSION_08_CODE,
  MISSION_09_CODE,
  MISSION_10_CODE
];
const REWARD_ITEM_KEYS = ['gold', 'timber', 'ore', 'captain_shard', 'cosmetic_token'];

// Winning fixture shared with tests/mission01.test.ts: seed 16, all broadsides.
const WINNING_SEED = 16;
const winningTurns = Array.from({ length: MISSION_01_TURN_LIMIT }, () => [
  {
    shipId: MISSION_01_PLAYER_SHIP_ID,
    action: 'broadside',
    targetShipId: MISSION_01_ENEMY_SHIP_ID,
    side: 'starboard',
    turnDelta: 0,
    speedDelta: 0
  }
]);

// Winning mission 07 fixture shared with tests/mission07.test.ts: seed 21,
// pure gunnery focusing frigate A then B, heaving to from turn 4.
const MISSION_07_WINNING_SEED = 21;
const mission07WinningTurns = Array.from({ length: MISSION_07_TURN_LIMIT }, (_, i) => {
  const target = i < 5 ? MISSION_07_ENEMY_SHIP_IDS[0] : MISSION_07_ENEMY_SHIP_IDS[1];
  const speedDelta = i >= 3 ? -2 : 0;
  return MISSION_07_PLAYER_SHIP_IDS.map((shipId) => ({
    shipId,
    action: 'broadside',
    targetShipId: target,
    side: 'starboard',
    turnDelta: 0,
    speedDelta
  }));
});

const app = buildServer({ testing: true });

// The testing preHandler in buildServer stamps a placeholder user id; this
// later hook wins so ownership checks pass for PLAYER_ID.
app.addHook('preHandler', async (request) => {
  request.user = { id: PLAYER_ID };
});

// In-memory stand-ins for the prisma stub so completion state and inventory
// quantities persist across requests within a test.
type ProgressRow = {
  playerId: string;
  missionId: string;
  status: string;
  bestScore: number | null;
};
const progressStore = new Map<string, ProgressRow>();
const inventoryStore = new Map<string, number>();
let transactionCalls = 0;
let inTransaction = false;
let inventoryUpsertArgs: Array<Record<string, unknown>> = [];
let updateManyArgs: Array<Record<string, unknown>> = [];
let simulateLostCreateRace = false;

const missionIdFor = (code: string) => `mission-id:${code}`;
let ownedUpgrades: Array<{ component: string; tier: number }> = [];

/* eslint-disable @typescript-eslint/no-explicit-any */
const prisma = app.prisma as any;
prisma.mission.findFirst = async (args: any) => {
  const code = args?.where?.code as string;
  return ALL_MISSION_CODES.includes(code) ? { id: missionIdFor(code), code } : null;
};
prisma.player.findUnique = async (args: any) =>
  args?.where?.id === PLAYER_ID ? { id: PLAYER_ID } : null;
prisma.playerShipUpgrade.findMany = async (args: any) =>
  args?.where?.playerId === PLAYER_ID
    ? ownedUpgrades.map((row) => ({ playerId: PLAYER_ID, ...row }))
    : [];
prisma.missionProgress.updateMany = async (args: any) => {
  updateManyArgs.push(args);
  const key = `${args.where.playerId}|${args.where.missionId}`;
  const row = progressStore.get(key);
  if (!row || row.status === 'COMPLETED') {
    return { count: 0 };
  }
  row.status = 'COMPLETED';
  row.bestScore = args.data.bestScore ?? row.bestScore;
  return { count: 1 };
};
prisma.missionProgress.findUnique = async (args: any) => {
  const key = `${args.where.playerId_missionId.playerId}|${args.where.playerId_missionId.missionId}`;
  return progressStore.get(key) ?? null;
};
prisma.missionProgress.update = async (args: any) => {
  const key = `${args.where.playerId_missionId.playerId}|${args.where.playerId_missionId.missionId}`;
  const row = progressStore.get(key);
  if (!row) throw new Error('missionProgress.update: row missing');
  row.bestScore = args.data.bestScore ?? row.bestScore;
  return row;
};
prisma.missionProgress.create = async (args: any) => {
  const key = `${args.data.playerId}|${args.data.missionId}`;
  if (simulateLostCreateRace) {
    // Emulate losing a concurrent create: the winner's COMPLETED row commits
    // and this create fails on the unique constraint.
    simulateLostCreateRace = false;
    progressStore.set(key, {
      playerId: args.data.playerId,
      missionId: args.data.missionId,
      status: 'COMPLETED',
      bestScore: null
    });
    const error = new Error('unique constraint') as Error & { code?: string };
    error.code = 'P2002';
    throw error;
  }
  if (progressStore.has(key)) {
    const error = new Error('unique constraint') as Error & { code?: string };
    error.code = 'P2002';
    throw error;
  }
  const row: ProgressRow = {
    playerId: args.data.playerId,
    missionId: args.data.missionId,
    status: args.data.status,
    bestScore: args.data.bestScore ?? null
  };
  progressStore.set(key, row);
  return row;
};
prisma.inventoryItem.upsert = async (args: any) => {
  if (!inTransaction) {
    throw new Error('inventoryItem.upsert called outside a transaction');
  }
  inventoryUpsertArgs.push(args);
  const itemKey = args.where.playerId_itemKey.itemKey as string;
  const quantity = (inventoryStore.get(itemKey) ?? 0) + args.update.quantity.increment;
  inventoryStore.set(itemKey, quantity);
  return { playerId: PLAYER_ID, itemKey, quantity };
};
prisma.$transaction = async (arg: any) => {
  transactionCalls += 1;
  if (typeof arg === 'function') {
    inTransaction = true;
    try {
      return await arg(prisma);
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
  progressStore.clear();
  inventoryStore.clear();
  transactionCalls = 0;
  inventoryUpsertArgs = [];
  updateManyArgs = [];
  simulateLostCreateRace = false;
  ownedUpgrades = [];
});

const complete = (code: string, overrides: Record<string, unknown> = {}) =>
  app.inject({
    method: 'POST',
    url: `/missions/${code}/complete`,
    payload: { playerId: PLAYER_ID, seed: WINNING_SEED, turns: winningTurns, ...overrides }
  });

describe('mission reward table', () => {
  it('covers exactly the ten runtime mission codes', () => {
    expect(Object.keys(MISSION_REWARD_TABLE).sort()).toEqual([...ALL_MISSION_CODES].sort());
  });

  it('grants only known item keys with positive integer quantities', () => {
    for (const code of ALL_MISSION_CODES) {
      const rewards = missionRewardsForCode(code);
      expect(rewards.length).toBeGreaterThan(0);
      for (const reward of rewards) {
        expect(REWARD_ITEM_KEYS).toContain(reward.itemKey);
        expect(Number.isInteger(reward.quantity)).toBe(true);
        expect(reward.quantity).toBeGreaterThan(0);
      }
    }
  });

  it('returns an empty grant list for unknown codes', () => {
    expect(missionRewardsForCode('mission-99-unknown')).toEqual([]);
  });
});

describe('mission completion win proof', () => {
  it('rejects completion without seed and turns', async () => {
    const res = await complete(MISSION_01_CODE, { seed: undefined, turns: undefined });
    expect(res.statusCode).toBe(400);
    expect(res.json().error).toBe('win_proof_required');
    expect(transactionCalls).toBe(0);
    expect(progressStore.size).toBe(0);
  });

  it('rejects a losing run without saving progress or granting rewards', async () => {
    const res = await complete(MISSION_01_CODE, { seed: 8, turns: [] });
    expect(res.statusCode).toBe(400);
    expect(res.json().error).toBe('mission_not_won');
    expect(transactionCalls).toBe(0);
    expect(progressStore.size).toBe(0);
    expect(inventoryStore.size).toBe(0);
  });

  it('rejects proofs that issue orders for enemy ships', async () => {
    const res = await complete(MISSION_01_CODE, {
      turns: [
        [
          {
            shipId: MISSION_01_ENEMY_SHIP_ID,
            action: 'pass'
          }
        ]
      ]
    });
    expect(res.statusCode).toBe(400);
    expect(res.json().error).toBe('invalid_order_ship');
    expect(transactionCalls).toBe(0);
  });

  it('rejects boarding orders in proofs for missions that forbid boarding', async () => {
    const res = await complete(MISSION_01_CODE, {
      turns: [
        [
          {
            shipId: MISSION_01_PLAYER_SHIP_ID,
            action: 'boarding',
            targetShipId: MISSION_01_ENEMY_SHIP_ID
          }
        ]
      ]
    });
    expect(res.statusCode).toBe(400);
    expect(res.json().error).toBe('boarding_disabled');
  });
});

describe('mission completion upgrade tiers', () => {
  const mission07Complete = (overrides: Record<string, unknown> = {}) =>
    complete(MISSION_07_CODE, {
      seed: MISSION_07_WINNING_SEED,
      turns: mission07WinningTurns,
      ...overrides
    });

  it('rejects upgrade tiers in proofs for missions without upgrade support', async () => {
    const res = await complete(MISSION_01_CODE, { upgrades: { cannon: 1, sail: 0, hull: 0 } });
    expect(res.statusCode).toBe(400);
    expect(res.json().error).toBe('upgrades_not_supported');
    expect(transactionCalls).toBe(0);
    expect(inventoryStore.size).toBe(0);
  });

  it('rejects proofs claiming tiers the player does not own', async () => {
    ownedUpgrades = [{ component: 'cannon', tier: 2 }];

    const res = await mission07Complete({ upgrades: { cannon: 3, sail: 0, hull: 0 } });
    expect(res.statusCode).toBe(409);
    expect(res.json()).toEqual({
      error: 'upgrade_tiers_exceed_owned',
      component: 'cannon',
      claimed: 3,
      owned: 2
    });
    expect(transactionCalls).toBe(0);
    expect(progressStore.size).toBe(0);
    expect(inventoryStore.size).toBe(0);
  });

  it('treats missing upgrade rows as tier zero', async () => {
    const res = await mission07Complete({ upgrades: { cannon: 0, sail: 1, hull: 0 } });
    expect(res.statusCode).toBe(409);
    expect(res.json().component).toBe('sail');
    expect(res.json().owned).toBe(0);
  });

  it('completes and grants when the proof tiers are owned and the run wins', async () => {
    ownedUpgrades = [
      { component: 'cannon', tier: 3 },
      { component: 'sail', tier: 3 },
      { component: 'hull', tier: 3 }
    ];

    const res = await mission07Complete({ upgrades: { cannon: 3, sail: 3, hull: 3 } });
    expect(res.statusCode).toBe(200);
    expect(res.json().progress.status).toBe('COMPLETED');
    expect(res.json().rewardsGranted).toEqual(missionRewardsForCode(MISSION_07_CODE));
  });

  it('accepts all-zero tiers without owning any upgrades', async () => {
    const res = await mission07Complete({ upgrades: { cannon: 0, sail: 0, hull: 0 } });
    expect(res.statusCode).toBe(200);
    expect(res.json().rewardsGranted).toEqual(missionRewardsForCode(MISSION_07_CODE));
  });
});

describe('mission completion rewards', () => {
  it('grants the reward table entry on first verified completion', async () => {
    const res = await complete(MISSION_01_CODE);
    expect(res.statusCode).toBe(200);

    const body = res.json();
    expect(body.progress.status).toBe('COMPLETED');
    expect(body.rewardsGranted).toEqual(missionRewardsForCode(MISSION_01_CODE));
    for (const reward of missionRewardsForCode(MISSION_01_CODE)) {
      expect(inventoryStore.get(reward.itemKey)).toBe(reward.quantity);
    }
  });

  it('grants nothing on repeat completion and keeps inventory unchanged', async () => {
    const first = await complete(MISSION_01_CODE);
    expect(first.statusCode).toBe(200);
    expect(first.json().rewardsGranted).toEqual(missionRewardsForCode(MISSION_01_CODE));
    const snapshot = new Map(inventoryStore);

    const second = await complete(MISSION_01_CODE);
    expect(second.statusCode).toBe(200);
    expect(second.json().progress.status).toBe('COMPLETED');
    expect(second.json().rewardsGranted).toEqual([]);
    expect(inventoryStore).toEqual(snapshot);
  });

  it('claims first completion atomically inside a single transaction', async () => {
    const rewards = missionRewardsForCode(MISSION_01_CODE);
    await complete(MISSION_01_CODE);

    expect(transactionCalls).toBe(1);
    // The claim is a conditional status transition, not a read-then-write.
    expect(updateManyArgs[0]).toMatchObject({
      where: {
        playerId: PLAYER_ID,
        missionId: missionIdFor(MISSION_01_CODE),
        status: { not: 'COMPLETED' }
      }
    });
    expect(inventoryUpsertArgs).toHaveLength(rewards.length);
    for (const [index, reward] of rewards.entries()) {
      expect(inventoryUpsertArgs[index]).toMatchObject({
        where: { playerId_itemKey: { playerId: PLAYER_ID, itemKey: reward.itemKey } },
        update: { quantity: { increment: reward.quantity } },
        create: { playerId: PLAYER_ID, itemKey: reward.itemKey, quantity: reward.quantity }
      });
    }
  });

  it('grants nothing after losing a concurrent create race', async () => {
    simulateLostCreateRace = true;

    const res = await complete(MISSION_01_CODE);
    expect(res.statusCode).toBe(200);
    expect(res.json().progress.status).toBe('COMPLETED');
    expect(res.json().rewardsGranted).toEqual([]);
    expect(inventoryStore.size).toBe(0);
    // First transaction aborts on P2002, the retry lands on the winner's row.
    expect(transactionCalls).toBe(2);
  });

  it('keeps unknown mission codes a 404 without touching inventory', async () => {
    const res = await complete('mission-99-unknown');
    expect(res.statusCode).toBe(404);
    expect(res.json().error).toBe('mission_not_found');
    expect(transactionCalls).toBe(0);
    expect(inventoryStore.size).toBe(0);
  });
});

describe('reward grant shape', () => {
  it('matches the RewardGrant contract used by the client', () => {
    const grant: RewardGrant = { itemKey: 'gold', quantity: 1 };
    expect(grant).toEqual({ itemKey: 'gold', quantity: 1 });
  });
});
