import { describe, it, expect, beforeAll, afterAll } from 'vitest';
import { buildServer } from '../src/app.js';
import { resolveSimPreview } from '../src/sim/engine.js';
import {
  MISSION_01_CODE,
  MISSION_01_ENEMY_SHIP_ID,
  MISSION_01_PLAYER_SHIP_ID,
  MISSION_01_TURN_LIMIT,
  createMission01State,
  mission01Fingerprint,
  mission01StartResponse,
  runMission01
} from '../src/sim/mission01.js';
import type { SimOrder, SimPreviewRequest } from '../src/sim/types.js';

const app = buildServer({ testing: true });

beforeAll(async () => {
  await app.ready();
});

afterAll(async () => {
  await app.close();
});

const playerBroadside: SimOrder = {
  shipId: MISSION_01_PLAYER_SHIP_ID,
  action: 'broadside',
  targetShipId: MISSION_01_ENEMY_SHIP_ID,
  side: 'starboard',
  turnDelta: 0,
  speedDelta: 0
};

const allBroadsides: SimOrder[][] = Array.from({ length: MISSION_01_TURN_LIMIT }, () => [
  playerBroadside
]);

// Canonical fingerprint pinned on both sides; the Unity EditMode test
// (Mission01Scenario) asserts the identical string for scenario parity.
const EXPECTED_FINGERPRINT =
  'mission-01-fair-wind|turnLimit=8|bonusTurns=6|bonusHull=0.2|enemyScale=0.9|wind=0:5|' +
  'enemy-sloop:enemy:150,0:h180:v2:hp108:sl70:cw40|player-sloop:player:0,0:h0:v3:hp120:sl80:cw50';

describe('mission 01 scenario', () => {
  it('applies the 0.9x enemy hull tuning knob', () => {
    const state = createMission01State();
    const enemy = state.ships.find((ship) => ship.id === MISSION_01_ENEMY_SHIP_ID);
    expect(enemy?.hp).toBe(Math.floor(120 * 0.9));
  });

  it('pins the scenario fingerprint shared with the Unity client', () => {
    expect(mission01Fingerprint(mission01StartResponse(101))).toBe(EXPECTED_FINGERPRINT);
  });

  it('is deterministic for the same seed and orders', () => {
    const first = runMission01(44, allBroadsides);
    const second = runMission01(44, allBroadsides);
    expect(second).toEqual(first);
    expect(first.turns.map((turn) => turn.hash)).toEqual(second.turns.map((turn) => turn.hash));
  });

  it('reports a win with both bonus objectives for seed 44', () => {
    const outcome = runMission01(44, allBroadsides);
    expect(outcome.result).toBe('win');
    expect(outcome.failReason).toBeNull();
    expect(outcome.turnCount).toBeLessThanOrEqual(6);
    expect(outcome.bonusObjectives).toEqual({
      underHullDamageThreshold: true,
      withinTurnTarget: true
    });
    expect(outcome.damageProfile.enemyRemainingHp).toBe(0);
    expect(outcome.damageProfile.playerHullDamage).toBeLessThan(120 * 0.2);
  });

  it('reports a win without the hull bonus when the player takes heavy fire', () => {
    const outcome = runMission01(4, allBroadsides);
    expect(outcome.result).toBe('win');
    expect(outcome.bonusObjectives.underHullDamageThreshold).toBe(false);
    expect(outcome.bonusObjectives.withinTurnTarget).toBe(true);
  });

  it('fails with timeout when the player never engages', () => {
    const outcome = runMission01(30, []);
    expect(outcome.result).toBe('loss');
    expect(outcome.failReason).toBe('timeout');
    expect(outcome.turnCount).toBe(MISSION_01_TURN_LIMIT);
    expect(outcome.turns).toHaveLength(MISSION_01_TURN_LIMIT);
  });

  it('fails with sunk when the enemy destroys the player', () => {
    const outcome = runMission01(1, []);
    expect(outcome.result).toBe('loss');
    expect(outcome.failReason).toBe('sunk');
    expect(outcome.damageProfile.playerRemainingHp).toBe(0);
  });
});

describe('engine damage scale modifier', () => {
  const basePreview = (): SimPreviewRequest => ({
    schemaVersion: 1,
    seed: 7,
    turn: 1,
    state: createMission01State(),
    orders: [
      {
        shipId: MISSION_01_ENEMY_SHIP_ID,
        action: 'broadside',
        targetShipId: MISSION_01_PLAYER_SHIP_ID,
        side: 'port',
        turnDelta: 0,
        speedDelta: 0
      }
    ]
  });

  it('scales broadside damage for the configured ship only', () => {
    const unscaled = resolveSimPreview(basePreview());
    const scaled = resolveSimPreview({
      ...basePreview(),
      modifiers: { damageScale: { [MISSION_01_ENEMY_SHIP_ID]: 0.9 } }
    });

    const unscaledHit = unscaled.events.find((event) => event.type === 'broadside');
    const scaledHit = scaled.events.find((event) => event.type === 'broadside');
    if (unscaledHit?.type !== 'broadside' || scaledHit?.type !== 'broadside') {
      throw new Error('expected broadside events');
    }
    expect(unscaledHit.hit).toBe(true);
    expect(scaledHit.damage.hull).toBe(Math.floor(unscaledHit.damage.hull * 0.9));
  });
});

describe('mission 01 routes', () => {
  it('starts deterministically with default seed and scenario payload', async () => {
    const res1 = await app.inject({ method: 'POST', url: `/missions/${MISSION_01_CODE}/start` });
    const res2 = await app.inject({ method: 'POST', url: `/missions/${MISSION_01_CODE}/start` });
    expect(res1.statusCode).toBe(200);
    expect(res1.json()).toEqual(res2.json());

    const body = res1.json();
    expect(body.missionCode).toBe(MISSION_01_CODE);
    expect(body.turnLimit).toBe(8);
    expect(body.objectives).toEqual({
      turnLimit: 8,
      bonusTurnTarget: 6,
      bonusHullDamageFraction: 0.2,
      enemyDamageScale: 0.9
    });
    expect(body.state.ships).toHaveLength(2);
  });

  it('honors an explicit seed on start', async () => {
    const res = await app.inject({
      method: 'POST',
      url: `/missions/${MISSION_01_CODE}/start`,
      payload: { seed: 44 }
    });
    expect(res.statusCode).toBe(200);
    expect(res.json().seed).toBe(44);
  });

  it('resolves a winning run with telemetry fields', async () => {
    const res = await app.inject({
      method: 'POST',
      url: `/missions/${MISSION_01_CODE}/resolve`,
      payload: { schemaVersion: 1, seed: 44, turns: allBroadsides }
    });
    expect(res.statusCode).toBe(200);

    const outcome = res.json().outcome;
    expect(outcome.result).toBe('win');
    expect(outcome.failReason).toBeNull();
    expect(outcome.turnCount).toBeLessThanOrEqual(6);
    expect(outcome.damageProfile).toHaveProperty('playerHullDamage');
    expect(outcome.damageProfile).toHaveProperty('playerHullDamageFraction');
    expect(outcome.damageProfile).toHaveProperty('enemyRemainingHp');
    expect(outcome.turns.every((turn: { hash: string }) => turn.hash.length === 64)).toBe(true);
  });

  it('resolves a timeout loss with the fail reason', async () => {
    const res = await app.inject({
      method: 'POST',
      url: `/missions/${MISSION_01_CODE}/resolve`,
      payload: { schemaVersion: 1, seed: 30, turns: [] }
    });
    expect(res.statusCode).toBe(200);
    expect(res.json().outcome.result).toBe('loss');
    expect(res.json().outcome.failReason).toBe('timeout');
  });

  it('rejects boarding orders per mission constraints', async () => {
    const res = await app.inject({
      method: 'POST',
      url: `/missions/${MISSION_01_CODE}/resolve`,
      payload: {
        schemaVersion: 1,
        seed: 44,
        turns: [
          [
            {
              shipId: MISSION_01_PLAYER_SHIP_ID,
              action: 'boarding',
              targetShipId: MISSION_01_ENEMY_SHIP_ID
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
      url: `/missions/${MISSION_01_CODE}/resolve`,
      payload: {
        schemaVersion: 1,
        seed: 44,
        turns: [[{ shipId: MISSION_01_ENEMY_SHIP_ID, action: 'pass' }]]
      }
    });
    expect(res.statusCode).toBe(400);
    expect(res.json().error).toBe('invalid_order_ship');
  });
});
