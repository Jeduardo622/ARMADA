import { describe, it, expect, beforeAll, afterAll } from 'vitest';
import { buildServer } from '../src/app.js';
import {
  MISSION_01_CODE,
  MISSION_01_DEFAULT_SEED,
  MISSION_01_ENEMY_SHIP_ID,
  MISSION_01_PLAYER_SHIP_ID,
  MISSION_01_TURN_LIMIT
} from '../src/sim/mission01.js';
import {
  MISSION_02_CODE,
  MISSION_02_DEFAULT_SEED,
  MISSION_02_ENEMY_SHIP_IDS,
  MISSION_02_PLAYER_SHIP_IDS,
  MISSION_02_TURN_LIMIT
} from '../src/sim/mission02.js';
import {
  MISSION_03_CODE,
  MISSION_03_DEFAULT_SEED,
  MISSION_03_ENEMY_SHIP_IDS,
  MISSION_03_PLAYER_SHIP_IDS,
  MISSION_03_TURN_LIMIT
} from '../src/sim/mission03.js';
import {
  MISSION_04_CODE,
  MISSION_04_DEFAULT_SEED,
  MISSION_04_ENEMY_SHIP_IDS,
  MISSION_04_PLAYER_SHIP_IDS,
  MISSION_04_TURN_LIMIT
} from '../src/sim/mission04.js';
import {
  MISSION_05_CODE,
  MISSION_05_DEFAULT_SEED,
  MISSION_05_ENEMY_SHIP_IDS,
  MISSION_05_PLAYER_SHIP_IDS,
  MISSION_05_TURN_LIMIT
} from '../src/sim/mission05.js';
import {
  MISSION_06_CODE,
  MISSION_06_DEFAULT_SEED,
  MISSION_06_ENEMY_SHIP_IDS,
  MISSION_06_PLAYER_SHIP_IDS,
  MISSION_06_TURN_LIMIT
} from '../src/sim/mission06.js';
import {
  MISSION_07_CODE,
  MISSION_07_DEFAULT_SEED,
  MISSION_07_ENEMY_SHIP_IDS,
  MISSION_07_PLAYER_SHIP_IDS,
  MISSION_07_TURN_LIMIT
} from '../src/sim/mission07.js';
import {
  MISSION_08_CODE,
  MISSION_08_DEFAULT_SEED,
  MISSION_08_ENEMY_SHIP_IDS,
  MISSION_08_PLAYER_SHIP_IDS,
  MISSION_08_TURN_LIMIT
} from '../src/sim/mission08.js';
import {
  MISSION_09_CODE,
  MISSION_09_DEFAULT_SEED,
  MISSION_09_ENEMY_SHIP_IDS,
  MISSION_09_PLAYER_SHIP_IDS,
  MISSION_09_TURN_LIMIT
} from '../src/sim/mission09.js';
import {
  MISSION_10_CODE,
  MISSION_10_ENEMY_SHIP_IDS,
  MISSION_10_PLAYER_SHIP_IDS,
  MISSION_10_TURN_LIMIT
} from '../src/sim/mission10.js';
import type { SimOrder } from '../src/sim/types.js';

// Network payload budget: docs/perf-budgets.md pins mission start/end API
// payloads at < 200KB; docs/design/render-pipeline.md §5 names this backend
// byte-size assertion as the measurable follow-up for that budget line.
const PAYLOAD_BUDGET_BYTES = 200 * 1024;

const PLAYER_ID = '11111111-1111-1111-1111-111111111111';

const app = buildServer({ testing: true });

// The testing preHandler stamps a placeholder user id; this later hook wins
// so the /complete ownership check passes for PLAYER_ID.
app.addHook('preHandler', async (request) => {
  request.user = { id: PLAYER_ID };
});

// Minimal in-memory prisma stand-ins so /complete can persist progress and
// grant rewards without a database (same pattern as tests/missionRewards.test.ts).
type ProgressRow = { playerId: string; missionId: string; status: string; bestScore: number | null };
const progressStore = new Map<string, ProgressRow>();

/* eslint-disable @typescript-eslint/no-explicit-any */
const prisma = app.prisma as any;
prisma.mission.findFirst = async (args: any) => {
  const code = args?.where?.code as string;
  return code === MISSION_01_CODE ? { id: `mission-id:${code}`, code } : null;
};
prisma.player.findUnique = async (args: any) =>
  args?.where?.id === PLAYER_ID ? { id: PLAYER_ID } : null;
prisma.missionProgress.updateMany = async (args: any) => {
  const key = `${args.where.playerId}|${args.where.missionId}`;
  const row = progressStore.get(key);
  if (!row || row.status === 'COMPLETED') {
    return { count: 0 };
  }
  row.status = 'COMPLETED';
  return { count: 1 };
};
prisma.missionProgress.findUnique = async (args: any) => {
  const key = `${args.where.playerId_missionId.playerId}|${args.where.playerId_missionId.missionId}`;
  return progressStore.get(key) ?? null;
};
prisma.missionProgress.create = async (args: any) => {
  const key = `${args.data.playerId}|${args.data.missionId}`;
  const row: ProgressRow = {
    playerId: args.data.playerId,
    missionId: args.data.missionId,
    status: args.data.status,
    bestScore: args.data.bestScore ?? null
  };
  progressStore.set(key, row);
  return row;
};
prisma.inventoryItem.upsert = async (args: any) => ({
  playerId: PLAYER_ID,
  itemKey: args.where.playerId_itemKey.itemKey,
  quantity: args.update.quantity.increment
});
prisma.$transaction = async (arg: any) =>
  typeof arg === 'function' ? arg(prisma) : Promise.all(arg);
/* eslint-enable @typescript-eslint/no-explicit-any */

beforeAll(async () => {
  await app.ready();
});

afterAll(async () => {
  await app.close();
});

// The actual serialized wire payload in bytes, not an object count.
const byteSize = (payload: string) => Buffer.byteLength(payload, 'utf8');

const fire = (shipId: string, target: string, ammo?: 'round' | 'chain'): SimOrder => ({
  shipId,
  action: 'broadside',
  targetShipId: target,
  side: 'starboard',
  turnDelta: 0,
  speedDelta: 0,
  ...(ammo ? { ammo } : {})
});

// Worst-case-shaped resolve fixture: every player ship fires every turn for
// the full turn limit, so the outcome carries the densest event stream the
// mission can produce over its longest possible run.
const fullBroadsideTurns = (
  playerShipIds: readonly string[],
  targetShipId: string,
  turnLimit: number,
  ammo?: 'round' | 'chain'
): SimOrder[][] =>
  Array.from({ length: turnLimit }, () =>
    playerShipIds.map((shipId) => fire(shipId, targetShipId, ammo))
  );

type MissionFixture = {
  code: string;
  seed: number;
  turns: SimOrder[][];
};

// Every runtime mission, resolved with its deterministic default seed and a
// full-length broadside barrage. Mission 10 additionally uses pure chain shot:
// chain never sinks anything, so the run is guaranteed to span the entire
// turn limit — the largest resolve payload the mission can serve (the same
// timeout fixture tests/mission10.test.ts pins at seed 63).
const missionFixtures: MissionFixture[] = [
  {
    code: MISSION_01_CODE,
    seed: MISSION_01_DEFAULT_SEED,
    turns: fullBroadsideTurns(
      [MISSION_01_PLAYER_SHIP_ID],
      MISSION_01_ENEMY_SHIP_ID,
      MISSION_01_TURN_LIMIT
    )
  },
  {
    code: MISSION_02_CODE,
    seed: MISSION_02_DEFAULT_SEED,
    turns: fullBroadsideTurns(
      MISSION_02_PLAYER_SHIP_IDS,
      MISSION_02_ENEMY_SHIP_IDS[0],
      MISSION_02_TURN_LIMIT
    )
  },
  {
    code: MISSION_03_CODE,
    seed: MISSION_03_DEFAULT_SEED,
    turns: fullBroadsideTurns(
      MISSION_03_PLAYER_SHIP_IDS,
      MISSION_03_ENEMY_SHIP_IDS[0],
      MISSION_03_TURN_LIMIT
    )
  },
  {
    code: MISSION_04_CODE,
    seed: MISSION_04_DEFAULT_SEED,
    turns: fullBroadsideTurns(
      MISSION_04_PLAYER_SHIP_IDS,
      MISSION_04_ENEMY_SHIP_IDS[0],
      MISSION_04_TURN_LIMIT
    )
  },
  {
    code: MISSION_05_CODE,
    seed: MISSION_05_DEFAULT_SEED,
    turns: fullBroadsideTurns(
      MISSION_05_PLAYER_SHIP_IDS,
      MISSION_05_ENEMY_SHIP_IDS[0],
      MISSION_05_TURN_LIMIT
    )
  },
  {
    code: MISSION_06_CODE,
    seed: MISSION_06_DEFAULT_SEED,
    turns: fullBroadsideTurns(
      MISSION_06_PLAYER_SHIP_IDS,
      MISSION_06_ENEMY_SHIP_IDS[0],
      MISSION_06_TURN_LIMIT
    )
  },
  {
    code: MISSION_07_CODE,
    seed: MISSION_07_DEFAULT_SEED,
    turns: fullBroadsideTurns(
      MISSION_07_PLAYER_SHIP_IDS,
      MISSION_07_ENEMY_SHIP_IDS[0],
      MISSION_07_TURN_LIMIT
    )
  },
  {
    code: MISSION_08_CODE,
    seed: MISSION_08_DEFAULT_SEED,
    turns: fullBroadsideTurns(
      MISSION_08_PLAYER_SHIP_IDS,
      MISSION_08_ENEMY_SHIP_IDS[0],
      MISSION_08_TURN_LIMIT
    )
  },
  {
    code: MISSION_09_CODE,
    seed: MISSION_09_DEFAULT_SEED,
    turns: fullBroadsideTurns(
      MISSION_09_PLAYER_SHIP_IDS,
      MISSION_09_ENEMY_SHIP_IDS[0],
      MISSION_09_TURN_LIMIT
    )
  },
  {
    code: MISSION_10_CODE,
    // tests/mission10.test.ts pins seed 63 pure chain as the full-turn-limit
    // timeout run; reusing it here maximizes the number of turn records.
    seed: 63,
    turns: fullBroadsideTurns(
      MISSION_10_PLAYER_SHIP_IDS,
      MISSION_10_ENEMY_SHIP_IDS[0],
      MISSION_10_TURN_LIMIT,
      'chain'
    )
  }
];

// Winning mission 01 fixture shared with tests/missionRewards.test.ts:
// seed 16, all broadsides.
const MISSION_01_WINNING_SEED = 16;
const mission01WinningTurns = fullBroadsideTurns(
  [MISSION_01_PLAYER_SHIP_ID],
  MISSION_01_ENEMY_SHIP_ID,
  MISSION_01_TURN_LIMIT
);

describe('mission API payload budget (< 200KB, docs/perf-budgets.md)', () => {
  it('serves every mission start payload under budget', async () => {
    for (const { code } of missionFixtures) {
      const res = await app.inject({ method: 'POST', url: `/missions/${code}/start` });
      expect(res.statusCode, `${code} start`).toBe(200);
      const size = byteSize(res.payload);
      expect(size, `${code} start payload is ${size} bytes`).toBeLessThan(PAYLOAD_BUDGET_BYTES);
    }
  });

  it('serves every full-length mission resolve payload under budget', async () => {
    for (const { code, seed, turns } of missionFixtures) {
      const res = await app.inject({
        method: 'POST',
        url: `/missions/${code}/resolve`,
        payload: { schemaVersion: 1, seed, turns }
      });
      expect(res.statusCode, `${code} resolve`).toBe(200);
      // The fixture really exercised the mission: the outcome must contain at
      // least one resolved turn record, so a schema rejection or empty run
      // can never masquerade as an in-budget payload.
      expect(res.json().outcome.turns.length, `${code} resolve turns`).toBeGreaterThan(0);
      const size = byteSize(res.payload);
      expect(size, `${code} resolve payload is ${size} bytes`).toBeLessThan(PAYLOAD_BUDGET_BYTES);
    }
  });

  it('serves the mission complete payload under budget', async () => {
    const res = await app.inject({
      method: 'POST',
      url: `/missions/${MISSION_01_CODE}/complete`,
      payload: {
        playerId: PLAYER_ID,
        seed: MISSION_01_WINNING_SEED,
        turns: mission01WinningTurns
      }
    });
    expect(res.statusCode).toBe(200);
    expect(res.json().rewardsGranted.length).toBeGreaterThan(0);
    const size = byteSize(res.payload);
    expect(size, `complete payload is ${size} bytes`).toBeLessThan(PAYLOAD_BUDGET_BYTES);
  });
});
