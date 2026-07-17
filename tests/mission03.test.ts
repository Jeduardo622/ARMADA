import { describe, it, expect, beforeAll, afterAll } from 'vitest';
import { buildServer } from '../src/app.js';
import { classifyLoss, countRakes } from '../src/sim/missionMetrics.js';
import {
  MISSION_03_CODE,
  MISSION_03_ENEMY_SHIP_IDS,
  MISSION_03_PLAYER_SHIP_IDS,
  MISSION_03_TURN_LIMIT,
  createMission03State,
  mission03Fingerprint,
  mission03StartResponse,
  runMission03
} from '../src/sim/mission03.js';
import type { ShipState, SimOrder, SimState } from '../src/sim/types.js';

const app = buildServer({ testing: true });

beforeAll(async () => {
  await app.ready();
});

afterAll(async () => {
  await app.close();
});

const fire = (shipId: string, target: string): SimOrder => ({
  shipId,
  action: 'broadside',
  targetShipId: target,
  side: 'starboard',
  turnDelta: 0,
  speedDelta: 0
});
const board = (shipId: string, target: string): SimOrder => ({
  shipId,
  action: 'boarding',
  targetShipId: target,
  turnDelta: 0,
  speedDelta: 0
});

// Sloop first (softer target), then the frigate.
const sloopFirstOrders: SimOrder[][] = Array.from({ length: MISSION_03_TURN_LIMIT }, (_, i) => {
  const target = i < 4 ? MISSION_03_ENEMY_SHIP_IDS[1] : MISSION_03_ENEMY_SHIP_IDS[0];
  return [
    fire(MISSION_03_PLAYER_SHIP_IDS[0], target),
    fire(MISSION_03_PLAYER_SHIP_IDS[1], target)
  ];
});

// Gunnery early, boarding the frigate once ranges have closed.
const boardingOrders: SimOrder[][] = Array.from({ length: MISSION_03_TURN_LIMIT }, (_, i) => {
  if (i < 4) {
    return [
      fire(MISSION_03_PLAYER_SHIP_IDS[0], MISSION_03_ENEMY_SHIP_IDS[1]),
      fire(MISSION_03_PLAYER_SHIP_IDS[1], MISSION_03_ENEMY_SHIP_IDS[1])
    ];
  }
  return [
    board(MISSION_03_PLAYER_SHIP_IDS[0], MISSION_03_ENEMY_SHIP_IDS[0]),
    board(MISSION_03_PLAYER_SHIP_IDS[1], MISSION_03_ENEMY_SHIP_IDS[0])
  ];
});

// Canonical fingerprint pinned on both sides; the Unity EditMode test
// (Mission03Scenario) asserts the identical string for scenario parity.
const EXPECTED_FINGERPRINT =
  'mission-03-raking-shot|turnLimit=10|bonusTurns=8|rakeTarget=2|enemyScale=1.05|wind=90:3|' +
  'enemy-frigate:enemy:200,90:h205:v2:hp189:sl90:cw60|' +
  'enemy-sloop:enemy:200,-90:h155:v3:hp126:sl70:cw40|' +
  'player-sloop-a:player:0,30:h0:v3:hp120:sl80:cw50|' +
  'player-sloop-b:player:0,-30:h0:v3:hp120:sl80:cw50';

describe('mission metrics', () => {
  const shipAt = (
    id: string,
    side: 'player' | 'enemy',
    x: number,
    y: number,
    hp: number
  ): ShipState => ({
    id,
    side,
    position: { x, y },
    heading: 0,
    speed: 0,
    hp,
    sail: 50,
    crew: 30
  });
  const finalState = (ships: ShipState[]): SimState => ({
    turn: 5,
    wind: { direction: 0, speed: 3 },
    ships
  });

  it('classifies survival as timeout', () => {
    const state = finalState([shipAt('p1', 'player', 0, 0, 50)]);
    expect(classifyLoss(state, false, 120)).toBe('timeout');
  });

  it('classifies flanked when live enemies bracket the sunk player', () => {
    const state = finalState([
      shipAt('p1', 'player', 0, 0, 0),
      shipAt('e1', 'enemy', 100, 0, 80),
      shipAt('e2', 'enemy', -100, 0, 80)
    ]);
    expect(classifyLoss(state, true, 120)).toBe('flanked');
  });

  it('classifies plain sunk when enemies attack from the same side', () => {
    const state = finalState([
      shipAt('p1', 'player', 0, 0, 0),
      shipAt('e1', 'enemy', 100, 20, 80),
      shipAt('e2', 'enemy', 100, -20, 80)
    ]);
    expect(classifyLoss(state, true, 120)).toBe('sunk');
  });

  it('classifies plain sunk when only one enemy survives', () => {
    const state = finalState([
      shipAt('p1', 'player', 0, 0, 0),
      shipAt('e1', 'enemy', 100, 0, 80),
      shipAt('e2', 'enemy', -100, 0, 0)
    ]);
    expect(classifyLoss(state, true, 120)).toBe('sunk');
  });

  it('counts rake attempts and hits for the given ships only', () => {
    const turns = [
      {
        turn: 1,
        hash: 'x',
        summary: { playerRemaining: 1, enemyRemaining: 1, sunk: [] },
        events: [
          {
            type: 'broadside' as const,
            shipId: 'p1',
            targetShipId: 'e1',
            side: 'port' as const,
            hit: true,
            roll: 1,
            hitChance: 90,
            damage: { hull: 30, sail: 18, crew: 10 },
            targetRemaining: { hp: 50, sail: 30, crew: 20 },
            rake: 'bow' as const
          },
          {
            type: 'broadside' as const,
            shipId: 'e1',
            targetShipId: 'p1',
            side: 'port' as const,
            hit: false,
            roll: 99,
            hitChance: 60,
            damage: { hull: 0, sail: 0, crew: 0 },
            targetRemaining: { hp: 100, sail: 80, crew: 50 },
            rake: 'stern' as const
          }
        ]
      }
    ];
    expect(countRakes(turns, ['p1'])).toEqual({ rakeAttempts: 1, rakeHits: 1 });
    expect(countRakes(turns, ['e1'])).toEqual({ rakeAttempts: 1, rakeHits: 0 });
  });
});

describe('mission 03 scenario', () => {
  it('pins the scenario fingerprint shared with the Unity client', () => {
    expect(mission03Fingerprint(mission03StartResponse(303))).toBe(EXPECTED_FINGERPRINT);
  });

  it('applies the 1.05x enemy hull tuning knob to both enemy classes', () => {
    const state = createMission03State();
    const frigate = state.ships.find((ship) => ship.id === MISSION_03_ENEMY_SHIP_IDS[0]);
    const sloop = state.ships.find((ship) => ship.id === MISSION_03_ENEMY_SHIP_IDS[1]);
    expect(frigate?.hp).toBe(Math.floor(180 * 1.05));
    expect(sloop?.hp).toBe(Math.floor(120 * 1.05));
  });

  it('is deterministic for the same seed and orders', () => {
    const first = runMission03(1, sloopFirstOrders);
    const second = runMission03(1, sloopFirstOrders);
    expect(second).toEqual(first);
  });

  it('reports a win with both bonuses and rake telemetry for seed 1', () => {
    const outcome = runMission03(1, sloopFirstOrders);
    expect(outcome.result).toBe('win');
    expect(outcome.failReason).toBeNull();
    expect(outcome.turnCount).toBeLessThanOrEqual(8);
    expect(outcome.bonusObjectives).toEqual({ landedRakingHits: true, withinTurnTarget: true });
    expect(outcome.telemetry.rakeHits).toBeGreaterThanOrEqual(2);
    expect(outcome.damageProfile.enemyRemainingHp).toBe(0);
  });

  it('reports a win that misses the turn bonus for seed 2', () => {
    const outcome = runMission03(2, sloopFirstOrders);
    expect(outcome.result).toBe('win');
    expect(outcome.bonusObjectives.withinTurnTarget).toBe(false);
    expect(outcome.bonusObjectives.landedRakingHits).toBe(true);
  });

  it('fails with timeout when the enemies survive the turn limit', () => {
    const outcome = runMission03(21, sloopFirstOrders);
    expect(outcome.result).toBe('loss');
    expect(outcome.failReason).toBe('timeout');
    expect(outcome.turns).toHaveLength(MISSION_03_TURN_LIMIT);
  });

  it('supports boarding as an optional path to victory', () => {
    const outcome = runMission03(5, boardingOrders);
    expect(outcome.result).toBe('win');
    expect(outcome.telemetry.boardingAttempts).toBeGreaterThan(0);
    expect(outcome.telemetry.boardingSuccesses).toBeGreaterThan(0);
    expect(outcome.telemetry.boardingSuccesses).toBeLessThanOrEqual(
      outcome.telemetry.boardingAttempts
    );
  });

  it('reports the per-ship damage distribution', () => {
    const outcome = runMission03(1, sloopFirstOrders);
    expect(outcome.damageProfile.perShip).toHaveLength(4);
    const frigate = outcome.damageProfile.perShip.find(
      (entry) => entry.shipId === MISSION_03_ENEMY_SHIP_IDS[0]
    );
    expect(frigate?.remainingHp).toBe(0);
    expect(frigate?.hullDamage).toBe(Math.floor(180 * 1.05));
  });
});

describe('mission 03 routes', () => {
  it('starts deterministically with the scenario payload', async () => {
    const res1 = await app.inject({ method: 'POST', url: `/missions/${MISSION_03_CODE}/start` });
    const res2 = await app.inject({ method: 'POST', url: `/missions/${MISSION_03_CODE}/start` });
    expect(res1.statusCode).toBe(200);
    expect(res1.json()).toEqual(res2.json());

    const body = res1.json();
    expect(body.missionCode).toBe(MISSION_03_CODE);
    expect(body.objectives).toEqual({
      turnLimit: 10,
      bonusTurnTarget: 8,
      rakeHitTarget: 2,
      enemyDamageScale: 1.05
    });
    expect(body.state.ships).toHaveLength(4);
  });

  it('resolves a winning run with rake telemetry', async () => {
    const res = await app.inject({
      method: 'POST',
      url: `/missions/${MISSION_03_CODE}/resolve`,
      payload: { schemaVersion: 1, seed: 1, turns: sloopFirstOrders }
    });
    expect(res.statusCode).toBe(200);

    const outcome = res.json().outcome;
    expect(outcome.result).toBe('win');
    expect(outcome.telemetry.rakeHits).toBeGreaterThanOrEqual(2);
    expect(outcome.damageProfile.perShip).toHaveLength(4);
  });

  it('accepts boarding orders now that boarding is unlocked', async () => {
    const res = await app.inject({
      method: 'POST',
      url: `/missions/${MISSION_03_CODE}/resolve`,
      payload: { schemaVersion: 1, seed: 5, turns: boardingOrders }
    });
    expect(res.statusCode).toBe(200);
    expect(res.json().outcome.telemetry.boardingAttempts).toBeGreaterThan(0);
  });

  it('rejects orders for ships the player does not control', async () => {
    const res = await app.inject({
      method: 'POST',
      url: `/missions/${MISSION_03_CODE}/resolve`,
      payload: {
        schemaVersion: 1,
        seed: 1,
        turns: [[{ shipId: MISSION_03_ENEMY_SHIP_IDS[0], action: 'pass' }]]
      }
    });
    expect(res.statusCode).toBe(400);
    expect(res.json().error).toBe('invalid_order_ship');
  });

  it('rejects orders that target friendly ships', async () => {
    const res = await app.inject({
      method: 'POST',
      url: `/missions/${MISSION_03_CODE}/resolve`,
      payload: {
        schemaVersion: 1,
        seed: 1,
        turns: [
          [
            {
              shipId: MISSION_03_PLAYER_SHIP_IDS[0],
              action: 'broadside',
              targetShipId: MISSION_03_PLAYER_SHIP_IDS[1],
              side: 'port'
            }
          ]
        ]
      }
    });
    expect(res.statusCode).toBe(400);
    expect(res.json().error).toBe('unknown_target_in_order');
  });
});
