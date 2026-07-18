import { describe, it, expect, beforeAll, afterAll, beforeEach } from 'vitest';
import { buildServer } from '../src/app.js';
import {
  MISSION_REWARD_TABLE,
  missionRewardsForCode,
  type RewardGrant
} from '../src/economy/missionRewards.js';
import { MISSION_01_CODE } from '../src/sim/mission01.js';
import { MISSION_02_CODE } from '../src/sim/mission02.js';
import { MISSION_03_CODE } from '../src/sim/mission03.js';
import { MISSION_04_CODE } from '../src/sim/mission04.js';
import { MISSION_05_CODE } from '../src/sim/mission05.js';
import { MISSION_06_CODE } from '../src/sim/mission06.js';

const PLAYER_ID = '11111111-1111-1111-1111-111111111111';
const ALL_MISSION_CODES = [
  MISSION_01_CODE,
  MISSION_02_CODE,
  MISSION_03_CODE,
  MISSION_04_CODE,
  MISSION_05_CODE,
  MISSION_06_CODE
];
const REWARD_ITEM_KEYS = ['gold', 'timber', 'ore', 'captain_shard', 'cosmetic_token'];

const app = buildServer({ testing: true });

// The testing preHandler in buildServer stamps a placeholder user id; this
// later hook wins so ownership checks pass for PLAYER_ID.
app.addHook('preHandler', async (request) => {
  request.user = { id: PLAYER_ID };
});

// In-memory stand-ins for the prisma stub so completion state and inventory
// quantities persist across requests within a test.
const progressStore = new Map<string, { status: string }>();
const inventoryStore = new Map<string, number>();
let transactionOps: number[] = [];
let inventoryUpsertArgs: Array<Record<string, unknown>> = [];

/* eslint-disable @typescript-eslint/no-explicit-any */
const prisma = app.prisma as any;
prisma.mission.findFirst = async (args: any) => {
  const code = args?.where?.code as string;
  return ALL_MISSION_CODES.includes(code) ? { id: `mission-id:${code}`, code } : null;
};
prisma.player.findUnique = async (args: any) =>
  args?.where?.id === PLAYER_ID ? { id: PLAYER_ID } : null;
prisma.missionProgress.findUnique = async (args: any) => {
  const key = `${args.where.playerId_missionId.playerId}|${args.where.playerId_missionId.missionId}`;
  return progressStore.get(key) ?? null;
};
prisma.missionProgress.upsert = async (args: any) => {
  const key = `${args.where.playerId_missionId.playerId}|${args.where.playerId_missionId.missionId}`;
  const progress = {
    playerId: args.where.playerId_missionId.playerId,
    missionId: args.where.playerId_missionId.missionId,
    status: 'COMPLETED',
    bestScore: args.update.bestScore ?? null
  };
  progressStore.set(key, progress);
  return progress;
};
prisma.inventoryItem.upsert = async (args: any) => {
  inventoryUpsertArgs.push(args);
  const itemKey = args.where.playerId_itemKey.itemKey as string;
  const quantity = (inventoryStore.get(itemKey) ?? 0) + args.update.quantity.increment;
  inventoryStore.set(itemKey, quantity);
  return { playerId: PLAYER_ID, itemKey, quantity };
};
prisma.$transaction = async (ops: Promise<unknown>[]) => {
  transactionOps.push(ops.length);
  return Promise.all(ops);
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
  transactionOps = [];
  inventoryUpsertArgs = [];
});

const complete = (code: string) =>
  app.inject({
    method: 'POST',
    url: `/missions/${code}/complete`,
    payload: { playerId: PLAYER_ID }
  });

describe('mission reward table', () => {
  it('covers exactly the six runtime mission codes', () => {
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

describe('mission completion rewards', () => {
  it('grants the reward table entry on first completion', async () => {
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
    const first = await complete(MISSION_02_CODE);
    expect(first.statusCode).toBe(200);
    expect(first.json().rewardsGranted).toEqual(missionRewardsForCode(MISSION_02_CODE));
    const snapshot = new Map(inventoryStore);

    const second = await complete(MISSION_02_CODE);
    expect(second.statusCode).toBe(200);
    expect(second.json().progress.status).toBe('COMPLETED');
    expect(second.json().rewardsGranted).toEqual([]);
    expect(inventoryStore).toEqual(snapshot);
  });

  it('runs the progress upsert and reward grants in a single transaction', async () => {
    const rewards = missionRewardsForCode(MISSION_06_CODE);
    await complete(MISSION_06_CODE);

    expect(transactionOps).toEqual([1 + rewards.length]);
    expect(inventoryUpsertArgs).toHaveLength(rewards.length);
    for (const [index, reward] of rewards.entries()) {
      expect(inventoryUpsertArgs[index]).toMatchObject({
        where: { playerId_itemKey: { playerId: PLAYER_ID, itemKey: reward.itemKey } },
        update: { quantity: { increment: reward.quantity } },
        create: { playerId: PLAYER_ID, itemKey: reward.itemKey, quantity: reward.quantity }
      });
    }

    // Repeat completion still updates progress but grants no inventory rows.
    await complete(MISSION_06_CODE);
    expect(transactionOps).toEqual([1 + rewards.length, 1]);
  });

  it('keeps unknown mission codes a 404 without touching inventory', async () => {
    const res = await complete('mission-99-unknown');
    expect(res.statusCode).toBe(404);
    expect(res.json().error).toBe('mission_not_found');
    expect(transactionOps).toEqual([]);
    expect(inventoryStore.size).toBe(0);
  });
});

describe('reward grant shape', () => {
  it('matches the RewardGrant contract used by the client', () => {
    const grant: RewardGrant = { itemKey: 'gold', quantity: 1 };
    expect(grant).toEqual({ itemKey: 'gold', quantity: 1 });
  });
});
