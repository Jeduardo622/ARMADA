import { describe, it, expect, beforeAll, afterAll } from 'vitest';
import { readFileSync } from 'node:fs';
import { buildServer } from '../src/app.js';
import {
  MISSION_06_BOSS_ID,
  MISSION_06_CODE,
  MISSION_06_PLAYER_SHIP_IDS,
  MISSION_06_REINFORCEMENT_ID,
  MISSION_06_REINFORCEMENT_TURN,
  MISSION_06_TURN_LIMIT,
  createMission06State,
  mission06Fingerprint,
  mission06Modifiers,
  mission06OnTurnStart,
  mission06StartResponse,
  mission06WindForTurn,
  runMission06
} from '../src/sim/mission06.js';
import type { SimOrder } from '../src/sim/types.js';

const app = buildServer({ testing: true });

beforeAll(async () => {
  await app.ready();
});

afterAll(async () => {
  await app.close();
});

const fire = (shipId: string, target: string, slow = 0): SimOrder => ({
  shipId,
  action: 'broadside',
  targetShipId: target,
  side: 'starboard',
  turnDelta: 0,
  speedDelta: slow
});

// Sustained siege on the boss, breaking off to swat the reinforcement in the
// given turn window.
function siegeOrders(reinforceFrom: number, reinforceUntil: number): SimOrder[][] {
  return Array.from({ length: MISSION_06_TURN_LIMIT }, (_, i) => {
    const slow = i >= 3 ? -2 : 0;
    const target =
      i >= reinforceFrom && i < reinforceUntil
        ? MISSION_06_REINFORCEMENT_ID
        : MISSION_06_BOSS_ID;
    return MISSION_06_PLAYER_SHIP_IDS.map((id) => fire(id, target, slow));
  });
}

const swatMidOrders = siegeOrders(5, 7);
const swatLateOrders = siegeOrders(8, 10);
const bossOnlyOrders = siegeOrders(99, 99);

// Canonical fingerprint pinned on both sides; the Unity EditMode test
// (Mission06Scenario) asserts the identical string for scenario parity.
const EXPECTED_FINGERPRINT =
  'mission-06-dreadnought-siege|turnLimit=14|bonusTurns=12|bossScale=1.3|bossDmg=1.5|' +
  'enrage=0.3|reinforce=5:0.9|wind=0:5|debris=150,0:r50:p2|' +
  'enemy-dreadnought:enemy:280,0:h180:v2:hp468:sl100:cw80|' +
  'player-sloop-a:player:0,50:h0:v3:hp120:sl80:cw50|' +
  'player-sloop-b:player:0,0:h0:v3:hp120:sl80:cw50|' +
  'player-sloop-c:player:0,-50:h0:v3:hp120:sl80:cw50';

describe('mission 06 scenario', () => {
  it('pins the scenario fingerprint shared with the Unity client', () => {
    expect(mission06Fingerprint(mission06StartResponse(606))).toBe(EXPECTED_FINGERPRINT);
  });

  it('fields the 1.3x dreadnought and a central debris field', () => {
    const state = createMission06State();
    const boss = state.ships.find((ship) => ship.id === MISSION_06_BOSS_ID);
    expect(boss?.hp).toBe(Math.floor(360 * 1.3));
    expect(state.ships).toHaveLength(4);
    expect(state.slowZones).toHaveLength(1);
  });

  it('spawns the 0.9x reinforcement exactly once on its scripted turn', () => {
    const state = createMission06State();
    expect(mission06OnTurnStart(state, 1)).toBe(state);
    const spawned = mission06OnTurnStart(state, MISSION_06_REINFORCEMENT_TURN);
    const reinforcement = spawned.ships.find(
      (ship) => ship.id === MISSION_06_REINFORCEMENT_ID
    );
    expect(reinforcement?.hp).toBe(Math.floor(120 * 0.9));
    expect(mission06OnTurnStart(spawned, MISSION_06_REINFORCEMENT_TURN)).toBe(spawned);
  });

  it('enrages the boss with an accuracy bonus below 30% hull', () => {
    const healthy = createMission06State();
    expect(mission06Modifiers(healthy).accuracyBonus).toBeUndefined();

    const wounded = createMission06State();
    const boss = wounded.ships.find((ship) => ship.id === MISSION_06_BOSS_ID);
    if (!boss) throw new Error('boss missing');
    boss.hp = 100;
    expect(mission06Modifiers(wounded).accuracyBonus).toEqual({
      [MISSION_06_BOSS_ID]: 25
    });
  });

  it('shifts the wind mid-fight', () => {
    expect(mission06WindForTurn(1, 6).direction).toBe(0);
    expect(mission06WindForTurn(1, 7).direction).toBe(90);
  });

  it('is deterministic for the same seed and orders', () => {
    const first = runMission06(1, swatMidOrders);
    const second = runMission06(1, swatMidOrders);
    expect(second).toEqual(first);
  });

  it('reports a both-bonus win with phase and enrage telemetry for seed 1', () => {
    const outcome = runMission06(1, swatMidOrders);
    expect(outcome.result).toBe('win');
    expect(outcome.failReason).toBeNull();
    expect(outcome.turnCount).toBe(9);
    expect(outcome.bonusObjectives).toEqual({ noShipLost: true, withinTurnTarget: true });
    expect(outcome.telemetry.phaseTransitions).toEqual([
      { turn: 1, phase: 1 },
      { turn: 5, phase: 2 }
    ]);
    expect(outcome.telemetry.enragedOnTurn).toBe(6);
    expect(outcome.telemetry.reinforcementTurn).toBe(MISSION_06_REINFORCEMENT_TURN);
    expect(outcome.damageProfile.bossRemainingHp).toBe(0);
    // The boss damage scale's most direct signature: what the siege pays for the
    // win. Pinned so a change to the scale cannot pass silently, and so the
    // PlayMode fake client's synthetic copy of this run has a source of truth.
    expect(outcome.damageProfile.playerHullDamage).toBe(94);
    expect(outcome.damageProfile.playerHullDamageFraction).toBe(0.26);
    expect(outcome.damageProfile.playerRemainingHp).toBe(266);
  });

  it('reports a win that loses a ship for seed 106', () => {
    const outcome = runMission06(106, swatMidOrders);
    expect(outcome.result).toBe('win');
    expect(outcome.turnCount).toBe(12);
    expect(outcome.bonusObjectives.noShipLost).toBe(false);
    expect(outcome.bonusObjectives.withinTurnTarget).toBe(true);
  });

  // Under the retuned constants a no-ship-lost slow win no longer exists on this
  // line (0 of 3000 searched seeds), so this fixture pins the surviving
  // late-swat failure mode: the siege drags to the limit and pays a ship for it.
  // The enrage accuracy bonus alone is what flips this seed; the boss damage
  // scale deepens the damage but does not by itself cost the ship.
  it('reports a slow win that misses the turn bonus for seed 68', () => {
    const outcome = runMission06(68, swatLateOrders);
    expect(outcome.result).toBe('win');
    expect(outcome.turnCount).toBe(14);
    expect(outcome.bonusObjectives.noShipLost).toBe(false);
    expect(outcome.bonusObjectives.withinTurnTarget).toBe(false);
  });

  // The fourth bonus-flag combination: the siege stays clean but overruns the
  // turn target. Rare under the 1.5x boss damage (2 of 3000 seeds), so it takes
  // a far seed rather than the swat-late line, which no longer produces it.
  it('reports a clean win that still misses the turn bonus for seed 2706', () => {
    const outcome = runMission06(2706, swatMidOrders);
    expect(outcome.result).toBe('win');
    expect(outcome.turnCount).toBe(13);
    expect(outcome.bonusObjectives.noShipLost).toBe(true);
    expect(outcome.bonusObjectives.withinTurnTarget).toBe(false);
  });

  it('fails with timeout while recording reinforcement impact', () => {
    const outcome = runMission06(1, bossOnlyOrders);
    expect(outcome.result).toBe('loss');
    expect(outcome.failReason).toBe('timeout');
    expect(outcome.turns).toHaveLength(MISSION_06_TURN_LIMIT);
    expect(outcome.telemetry.reinforcementDamageDealt).toBe(101);
  });
});

describe('mission 06 routes', () => {
  it('starts deterministically with the scenario payload', async () => {
    const res1 = await app.inject({ method: 'POST', url: `/missions/${MISSION_06_CODE}/start` });
    const res2 = await app.inject({ method: 'POST', url: `/missions/${MISSION_06_CODE}/start` });
    expect(res1.statusCode).toBe(200);
    expect(res1.json()).toEqual(res2.json());

    const body = res1.json();
    expect(body.missionCode).toBe(MISSION_06_CODE);
    expect(body.objectives).toEqual({
      turnLimit: 14,
      bonusTurnTarget: 12,
      bossHpScale: 1.3,
      bossDamageScale: 1.5,
      enrageHullFraction: 0.3,
      reinforcementTurn: 5,
      reinforcementHpScale: 0.9
    });
    expect(body.state.ships).toHaveLength(4);
  });

  // The route caps turns at MISSION_06_TURN_LIMIT, so the published bound has to
  // stay equal to it (this slice keeps the limit at 14; the pin is what makes a
  // future change to it fail loudly). verify-contracts compares operations and
  // schemaVersion only, so nothing else catches this drift.
  it('documents the same resolve turn cap that the route enforces', () => {
    const spec = readFileSync(new URL('../docs/api/openapi.yaml', import.meta.url), 'utf8');
    const block = spec.split('Mission06ResolveRequest:')[1]?.split('Mission06BonusObjectives:')[0];
    const documented = block?.match(/turns:\s*\n\s*type: array\s*\n\s*maxItems: (\d+)/)?.[1];
    expect(documented).toBe(String(MISSION_06_TURN_LIMIT));
  });

  it('resolves a winning run with boss telemetry', async () => {
    const res = await app.inject({
      method: 'POST',
      url: `/missions/${MISSION_06_CODE}/resolve`,
      payload: { schemaVersion: 1, seed: 1, turns: swatMidOrders }
    });
    expect(res.statusCode).toBe(200);

    const outcome = res.json().outcome;
    expect(outcome.result).toBe('win');
    expect(outcome.telemetry.phaseTransitions.length).toBeGreaterThanOrEqual(2);
    expect(outcome.damageProfile).toHaveProperty('bossHullDamage');
  });

  it('accepts orders targeting the reinforcement before it spawns', async () => {
    const res = await app.inject({
      method: 'POST',
      url: `/missions/${MISSION_06_CODE}/resolve`,
      payload: {
        schemaVersion: 1,
        seed: 1,
        turns: [
          [
            fire(MISSION_06_PLAYER_SHIP_IDS[0], MISSION_06_REINFORCEMENT_ID)
          ]
        ]
      }
    });
    expect(res.statusCode).toBe(200);
  });

  it('rejects orders for ships the player does not control', async () => {
    const res = await app.inject({
      method: 'POST',
      url: `/missions/${MISSION_06_CODE}/resolve`,
      payload: {
        schemaVersion: 1,
        seed: 1,
        turns: [[{ shipId: MISSION_06_BOSS_ID, action: 'pass' }]]
      }
    });
    expect(res.statusCode).toBe(400);
    expect(res.json().error).toBe('invalid_order_ship');
  });
});
