import { describe, it, expect, beforeAll, afterAll } from 'vitest';
import { buildServer } from '../src/app.js';
import { resolveSimPreview } from '../src/sim/engine.js';
import {
  MISSION_02_CODE,
  MISSION_02_ENEMY_SHIP_IDS,
  MISSION_02_PLAYER_SHIP_IDS,
  MISSION_02_TURN_LIMIT,
  createMission02State,
  mission02Fingerprint,
  mission02StartResponse,
  runMission02
} from '../src/sim/mission02.js';
import type { SimOrder } from '../src/sim/types.js';

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

// Focus fire: both ships pound the aggressor for 4 turns, then the kite.
const focusOrders: SimOrder[][] = Array.from({ length: MISSION_02_TURN_LIMIT }, (_, i) => {
  const target = i < 4 ? MISSION_02_ENEMY_SHIP_IDS[0] : MISSION_02_ENEMY_SHIP_IDS[1];
  return [
    fire(MISSION_02_PLAYER_SHIP_IDS[0], target),
    fire(MISSION_02_PLAYER_SHIP_IDS[1], target)
  ];
});

// Split fire: ship A on the aggressor, ship B on the kite.
const splitOrders: SimOrder[][] = Array.from({ length: MISSION_02_TURN_LIMIT }, () => [
  fire(MISSION_02_PLAYER_SHIP_IDS[0], MISSION_02_ENEMY_SHIP_IDS[0]),
  fire(MISSION_02_PLAYER_SHIP_IDS[1], MISSION_02_ENEMY_SHIP_IDS[1])
]);

// Canonical fingerprint pinned on both sides; the Unity EditMode test
// (Mission02Scenario) asserts the identical string for scenario parity.
const EXPECTED_FINGERPRINT =
  'mission-02-weather-gage|turnLimit=9|bonusTurns=7|upwindTurns=3|enemyScale=1|wind=90:5|' +
  'island=100,40:r25|' +
  'enemy-aggressor:enemy:170,120:h215:v2:hp120:sl70:cw40|' +
  'enemy-kite:enemy:220,160:h215:v2:hp120:sl70:cw40|' +
  'player-sloop-a:player:0,30:h0:v3:hp120:sl80:cw50|' +
  'player-sloop-b:player:0,-30:h0:v3:hp120:sl80:cw50';

describe('engine obstacles', () => {
  const obstaclePreview = (obstacleX: number) => ({
    schemaVersion: 1 as const,
    seed: 3,
    turn: 1,
    state: {
      turn: 1,
      wind: { direction: 0, speed: 5 },
      ships: [
        {
          id: 'runner',
          side: 'player' as const,
          position: { x: 0, y: 0 },
          heading: 0,
          speed: 3,
          hp: 100,
          sail: 80,
          crew: 40
        }
      ],
      obstacles: [{ position: { x: obstacleX, y: 0 }, radius: 10 }]
    },
    orders: [],
    modifiers: { windMovement: true }
  });

  it('halts movement that would end inside an obstacle', () => {
    const result = resolveSimPreview(obstaclePreview(30));
    const movement = result.events.find((event) => event.type === 'movement');
    if (movement?.type !== 'movement') throw new Error('expected movement event');
    expect(movement.blocked).toBe(true);
    expect(result.nextState.ships[0].position).toEqual({ x: 0, y: 0 });
  });

  it('moves freely past distant obstacles', () => {
    const result = resolveSimPreview(obstaclePreview(200));
    const movement = result.events.find((event) => event.type === 'movement');
    if (movement?.type !== 'movement') throw new Error('expected movement event');
    expect(movement).not.toHaveProperty('blocked');
    expect(result.nextState.ships[0].position).toEqual({ x: 25, y: 0 });
  });
});

describe('mission 02 scenario', () => {
  it('pins the scenario fingerprint shared with the Unity client', () => {
    expect(mission02Fingerprint(mission02StartResponse(202))).toBe(EXPECTED_FINGERPRINT);
  });

  it('fields two full-strength enemies and the island obstacle', () => {
    const state = createMission02State();
    const enemies = state.ships.filter((ship) => ship.side === 'enemy');
    expect(enemies.map((ship) => ship.hp)).toEqual([120, 120]);
    expect(state.obstacles).toHaveLength(1);
  });

  it('is deterministic for the same seed and orders', () => {
    const first = runMission02(1, focusOrders);
    const second = runMission02(1, focusOrders);
    expect(second).toEqual(first);
  });

  it('reports a win with both bonuses and rake telemetry for seed 1 focus fire', () => {
    const outcome = runMission02(1, focusOrders);
    expect(outcome.result).toBe('win');
    expect(outcome.failReason).toBeNull();
    expect(outcome.turnCount).toBeLessThanOrEqual(7);
    expect(outcome.bonusObjectives).toEqual({ heldWeatherGage: true, withinTurnTarget: true });
    expect(outcome.telemetry.upwindTurns).toBeGreaterThanOrEqual(3);
    expect(outcome.telemetry.rakeAttempts).toBeGreaterThan(0);
    expect(outcome.telemetry.rakeHits).toBeLessThanOrEqual(outcome.telemetry.rakeAttempts);
    expect(outcome.damageProfile.enemyRemainingHp).toBe(0);
  });

  it('reports a win that misses the turn bonus for seed 3 focus fire', () => {
    const outcome = runMission02(3, focusOrders);
    expect(outcome.result).toBe('win');
    expect(outcome.bonusObjectives.withinTurnTarget).toBe(false);
    expect(outcome.bonusObjectives.heldWeatherGage).toBe(true);
  });

  it('reports a win that misses the weather-gage bonus for seed 18 split fire', () => {
    const outcome = runMission02(18, splitOrders);
    expect(outcome.result).toBe('win');
    expect(outcome.bonusObjectives.heldWeatherGage).toBe(false);
    expect(outcome.telemetry.upwindTurns).toBeLessThan(3);
  });

  it('fails with timeout when the enemies survive the turn limit', () => {
    const outcome = runMission02(4, focusOrders);
    expect(outcome.result).toBe('loss');
    expect(outcome.failReason).toBe('timeout');
    expect(outcome.turns).toHaveLength(MISSION_02_TURN_LIMIT);
  });

  it('fails as flanked when both enemies catch the idle player between them', () => {
    const outcome = runMission02(8, []);
    expect(outcome.result).toBe('loss');
    expect(outcome.failReason).toBe('flanked');
    expect(outcome.damageProfile.playerRemainingHp).toBe(0);
  });
});

describe('mission 02 routes', () => {
  it('starts deterministically with the scenario payload', async () => {
    const res1 = await app.inject({ method: 'POST', url: `/missions/${MISSION_02_CODE}/start` });
    const res2 = await app.inject({ method: 'POST', url: `/missions/${MISSION_02_CODE}/start` });
    expect(res1.statusCode).toBe(200);
    expect(res1.json()).toEqual(res2.json());

    const body = res1.json();
    expect(body.missionCode).toBe(MISSION_02_CODE);
    expect(body.objectives).toEqual({
      turnLimit: 9,
      bonusTurnTarget: 7,
      upwindBonusTurns: 3,
      enemyDamageScale: 1
    });
    expect(body.state.ships).toHaveLength(4);
    expect(body.state.obstacles).toHaveLength(1);
  });

  it('resolves a winning run with weather-gage telemetry', async () => {
    const res = await app.inject({
      method: 'POST',
      url: `/missions/${MISSION_02_CODE}/resolve`,
      payload: { schemaVersion: 1, seed: 1, turns: focusOrders }
    });
    expect(res.statusCode).toBe(200);

    const outcome = res.json().outcome;
    expect(outcome.result).toBe('win');
    expect(outcome.telemetry).toHaveProperty('rakeAttempts');
    expect(outcome.telemetry).toHaveProperty('upwindTurns');
    expect(outcome.telemetry.upwindByTurn.length).toBe(outcome.turnCount);
    expect(outcome.damageProfile).toHaveProperty('playerHullDamageFraction');
  });

  it('resolves a flanked loss with the fail reason', async () => {
    const res = await app.inject({
      method: 'POST',
      url: `/missions/${MISSION_02_CODE}/resolve`,
      payload: { schemaVersion: 1, seed: 8, turns: [] }
    });
    expect(res.statusCode).toBe(200);
    expect(res.json().outcome.failReason).toBe('flanked');
  });

  it('rejects boarding orders while boarding is locked', async () => {
    const res = await app.inject({
      method: 'POST',
      url: `/missions/${MISSION_02_CODE}/resolve`,
      payload: {
        schemaVersion: 1,
        seed: 1,
        turns: [
          [
            {
              shipId: MISSION_02_PLAYER_SHIP_IDS[0],
              action: 'boarding',
              targetShipId: MISSION_02_ENEMY_SHIP_IDS[0]
            }
          ]
        ]
      }
    });
    expect(res.statusCode).toBe(400);
    expect(res.json().error).toBe('boarding_disabled');
  });

  it('rejects orders for ships the player does not control', async () => {
    const res = await app.inject({
      method: 'POST',
      url: `/missions/${MISSION_02_CODE}/resolve`,
      payload: {
        schemaVersion: 1,
        seed: 1,
        turns: [[{ shipId: MISSION_02_ENEMY_SHIP_IDS[0], action: 'pass' }]]
      }
    });
    expect(res.statusCode).toBe(400);
    expect(res.json().error).toBe('invalid_order_ship');
  });
});
