import { describe, it, expect, beforeAll, afterAll } from 'vitest';
import { buildServer } from '../src/app.js';
import {
  MISSION_07_CODE,
  MISSION_07_ENEMY_SHIP_IDS,
  MISSION_07_PLAYER_SHIP_IDS,
  MISSION_07_TURN_LIMIT,
  createMission07State,
  mission07Fingerprint,
  mission07StartResponse,
  runMission07
} from '../src/sim/mission07.js';
import type { ShipUpgradeTiers, SimOrder } from '../src/sim/types.js';

const app = buildServer({ testing: true });

beforeAll(async () => {
  await app.ready();
});

afterAll(async () => {
  await app.close();
});

const fire = (shipId: string, target: string, speedDelta = 0): SimOrder => ({
  shipId,
  action: 'broadside',
  targetShipId: target,
  side: 'starboard',
  turnDelta: 0,
  speedDelta
});

// Pure gunnery: focus frigate A then B, heaving to from turn 4. The sustained
// exchange is what lets fire and shredded-sail slows decide the duel.
const gunneryOrders: SimOrder[][] = Array.from({ length: MISSION_07_TURN_LIMIT }, (_, i) => {
  const target = i < 5 ? MISSION_07_ENEMY_SHIP_IDS[0] : MISSION_07_ENEMY_SHIP_IDS[1];
  const slow = i >= 3 ? -2 : 0;
  return [
    fire(MISSION_07_PLAYER_SHIP_IDS[0], target, slow),
    fire(MISSION_07_PLAYER_SHIP_IDS[1], target, slow)
  ];
});

// Canonical fingerprint pinned on both sides; the Unity EditMode test
// (Mission07Scenario) asserts the identical string for scenario parity.
const EXPECTED_FINGERPRINT =
  'mission-07-burning-seas|turnLimit=10|sailScale=0.85|ignitionTarget=1|wind=0:4|' +
  'enemy-frigate-a:enemy:220,40:h180:v2:hp180:sl76:cw60|' +
  'enemy-frigate-b:enemy:220,-40:h180:v2:hp180:sl76:cw60|' +
  'player-sloop-a:player:0,30:h0:v3:hp120:sl80:cw50|' +
  'player-sloop-b:player:0,-30:h0:v3:hp120:sl80:cw50';

describe('mission 07 scenario', () => {
  it('pins the scenario fingerprint shared with the Unity client', () => {
    expect(mission07Fingerprint(mission07StartResponse(707))).toBe(EXPECTED_FINGERPRINT);
  });

  it('applies the 0.85x enemy sail tuning knob at full hull strength', () => {
    const state = createMission07State();
    const frigates = state.ships.filter((ship) => ship.side === 'enemy');
    expect(frigates.map((ship) => ship.sail)).toEqual([76, 76]);
    expect(frigates.map((ship) => ship.hp)).toEqual([180, 180]);
  });

  it('is deterministic for the same seed and orders', () => {
    const first = runMission07(21, gunneryOrders);
    const second = runMission07(21, gunneryOrders);
    expect(second).toEqual(first);
    expect(first.turns.map((turn) => turn.hash)).toEqual(second.turns.map((turn) => turn.hash));
  });

  it('reports a gunnery win with both status bonuses for seed 21', () => {
    const outcome = runMission07(21, gunneryOrders);
    expect(outcome.result).toBe('win');
    expect(outcome.failReason).toBeNull();
    expect(outcome.turnCount).toBe(9);
    expect(outcome.bonusObjectives).toEqual({ enemyIgnited: true, unscorched: true });
    expect(outcome.telemetry).toEqual({
      ignitionsInflicted: 6,
      ignitionsSuffered: 0,
      slowsInflicted: 4
    });
    expect(outcome.damageProfile.enemyRemainingHp).toBe(0);
  });

  it('emits status events clients can render during the winning run', () => {
    const outcome = runMission07(21, gunneryOrders);
    const enemyStatusEvents = outcome.turns.flatMap((turn) =>
      turn.events.filter(
        (event) =>
          event.type === 'status' &&
          (MISSION_07_ENEMY_SHIP_IDS as readonly string[]).includes(event.shipId)
      )
    );
    expect(enemyStatusEvents.length).toBeGreaterThan(0);
  });

  it('withholds the ignition bonus on a win without a single fire', () => {
    const outcome = runMission07(49, gunneryOrders);
    expect(outcome.result).toBe('win');
    expect(outcome.telemetry.ignitionsInflicted).toBe(0);
    expect(outcome.bonusObjectives).toEqual({ enemyIgnited: false, unscorched: true });
  });

  it('withholds the unscorched bonus when the player catches fire', () => {
    const outcome = runMission07(4, gunneryOrders);
    expect(outcome.result).toBe('win');
    expect(outcome.telemetry.ignitionsSuffered).toBe(1);
    expect(outcome.bonusObjectives.enemyIgnited).toBe(true);
    expect(outcome.bonusObjectives.unscorched).toBe(false);
  });

  it('denies both bonuses on a loss even when fires were started', () => {
    const outcome = runMission07(5, gunneryOrders);
    expect(outcome.result).toBe('loss');
    expect(outcome.failReason).toBe('timeout');
    expect(outcome.telemetry.ignitionsInflicted).toBeGreaterThan(0);
    expect(outcome.bonusObjectives).toEqual({ enemyIgnited: false, unscorched: false });
    expect(outcome.turns).toHaveLength(MISSION_07_TURN_LIMIT);
  });
});

describe('mission 07 ship upgrades', () => {
  const fullTiers: ShipUpgradeTiers = { cannon: 3, sail: 3, hull: 3 };
  const zeroTiers: ShipUpgradeTiers = { cannon: 0, sail: 0, hull: 0 };

  it('keeps all-zero tiers hash-identical to a run without upgrades', () => {
    const base = runMission07(21, gunneryOrders);
    const zero = runMission07(21, gunneryOrders, zeroTiers);
    expect(zero.turns.map((turn) => turn.hash)).toEqual(base.turns.map((turn) => turn.hash));
    expect(zero).toEqual(base);
  });

  it('changes the hash chain deterministically under full tiers', () => {
    const base = runMission07(21, gunneryOrders);
    const first = runMission07(21, gunneryOrders, fullTiers);
    const second = runMission07(21, gunneryOrders, fullTiers);
    expect(first.turns[0].hash).not.toBe(base.turns[0].hash);
    expect(second).toEqual(first);
  });

  it('scales the damage baseline with hull tiers so upgraded runs stay non-negative', () => {
    const outcome = runMission07(21, gunneryOrders, { cannon: 0, sail: 0, hull: 3 });
    // Two sloops at floor(120 * 1.3) = 156 battle-start hull each.
    expect(outcome.damageProfile.playerHullDamage).toBeGreaterThanOrEqual(0);
    expect(outcome.damageProfile.playerHullDamageFraction).toBeGreaterThanOrEqual(0);
    expect(outcome.damageProfile.playerRemainingHp).toBeLessThanOrEqual(312);
  });

  it('turns the seed 5 timeout loss into a win with full tiers', () => {
    expect(runMission07(5, gunneryOrders).result).toBe('loss');
    const upgraded = runMission07(5, gunneryOrders, fullTiers);
    expect(upgraded.result).toBe('win');
  });
});

describe('mission 07 routes', () => {
  it('starts deterministically with the scenario payload', async () => {
    const res1 = await app.inject({ method: 'POST', url: `/missions/${MISSION_07_CODE}/start` });
    const res2 = await app.inject({ method: 'POST', url: `/missions/${MISSION_07_CODE}/start` });
    expect(res1.statusCode).toBe(200);
    expect(res1.json()).toEqual(res2.json());

    const body = res1.json();
    expect(body.missionCode).toBe(MISSION_07_CODE);
    expect(body.objectives).toEqual({
      turnLimit: 10,
      enemySailScale: 0.85,
      ignitionTarget: 1
    });
    expect(body.state.ships).toHaveLength(4);
  });

  it('resolves a status-effect win with ignition telemetry', async () => {
    const res = await app.inject({
      method: 'POST',
      url: `/missions/${MISSION_07_CODE}/resolve`,
      payload: { schemaVersion: 1, seed: 21, turns: gunneryOrders }
    });
    expect(res.statusCode).toBe(200);

    const outcome = res.json().outcome;
    expect(outcome.result).toBe('win');
    expect(outcome.bonusObjectives.enemyIgnited).toBe(true);
    expect(outcome.telemetry.ignitionsInflicted).toBeGreaterThan(0);
  });

  it('rejects orders for ships the player does not control', async () => {
    const res = await app.inject({
      method: 'POST',
      url: `/missions/${MISSION_07_CODE}/resolve`,
      payload: {
        schemaVersion: 1,
        seed: 21,
        turns: [[{ shipId: MISSION_07_ENEMY_SHIP_IDS[0], action: 'pass' }]]
      }
    });
    expect(res.statusCode).toBe(400);
    expect(res.json().error).toBe('invalid_order_ship');
  });

  it('resolves with upgrade tiers and reflects them in the outcome', async () => {
    const withUpgrades = await app.inject({
      method: 'POST',
      url: `/missions/${MISSION_07_CODE}/resolve`,
      payload: {
        schemaVersion: 1,
        seed: 21,
        turns: gunneryOrders,
        upgrades: { cannon: 3, sail: 3, hull: 3 }
      }
    });
    expect(withUpgrades.statusCode).toBe(200);
    const outcome = withUpgrades.json().outcome;
    expect(outcome.result).toBe('win');

    const baseline = await app.inject({
      method: 'POST',
      url: `/missions/${MISSION_07_CODE}/resolve`,
      payload: { schemaVersion: 1, seed: 21, turns: gunneryOrders }
    });
    expect(outcome.turns[0].hash).not.toBe(baseline.json().outcome.turns[0].hash);
  });

  it('rejects upgrade tiers outside the owned range', async () => {
    const res = await app.inject({
      method: 'POST',
      url: `/missions/${MISSION_07_CODE}/resolve`,
      payload: {
        schemaVersion: 1,
        seed: 21,
        turns: gunneryOrders,
        upgrades: { cannon: 4 }
      }
    });
    expect(res.statusCode).toBe(400);
  });
});
