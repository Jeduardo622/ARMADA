import { describe, it, expect, beforeAll, afterAll } from 'vitest';
import { buildServer } from '../src/app.js';
import {
  MISSION_05_CODE,
  MISSION_05_ESCORT_SHIP_IDS,
  MISSION_05_FLAGSHIP_ID,
  MISSION_05_PLAYER_SHIP_IDS,
  MISSION_05_TURN_LIMIT,
  createMission05State,
  firstSunkShip,
  mission05Fingerprint,
  mission05StartResponse,
  runMission05
} from '../src/sim/mission05.js';
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

// Concentrated line-breaking: flagship until turn 4, escorts split after.
const lineBreakOrders: SimOrder[][] = Array.from({ length: MISSION_05_TURN_LIMIT }, (_, i) => {
  const slow = i >= 3 ? -2 : 0;
  const target =
    i < 4
      ? MISSION_05_FLAGSHIP_ID
      : i < 6
        ? MISSION_05_ESCORT_SHIP_IDS[0]
        : MISSION_05_ESCORT_SHIP_IDS[1];
  return MISSION_05_PLAYER_SHIP_IDS.map((id) => fire(id, target, slow));
});

// Slow flagship-first variant: wins but blows the turn bonus.
const slowFlagshipOrders: SimOrder[][] = Array.from({ length: MISSION_05_TURN_LIMIT }, (_, i) => {
  const slow = i >= 3 ? -2 : 0;
  const target =
    i < 5
      ? MISSION_05_FLAGSHIP_ID
      : i < 8
        ? MISSION_05_ESCORT_SHIP_IDS[0]
        : MISSION_05_ESCORT_SHIP_IDS[1];
  return MISSION_05_PLAYER_SHIP_IDS.map((id) => fire(id, target, slow));
});

// Escorts first: wins without the flagship-first bonus.
const escortsFirstOrders: SimOrder[][] = Array.from({ length: MISSION_05_TURN_LIMIT }, (_, i) => {
  const slow = i >= 3 ? -2 : 0;
  const target =
    i < 3
      ? MISSION_05_ESCORT_SHIP_IDS[0]
      : i < 6
        ? MISSION_05_ESCORT_SHIP_IDS[1]
        : MISSION_05_FLAGSHIP_ID;
  return MISSION_05_PLAYER_SHIP_IDS.map((id) => fire(id, target, slow));
});

// Canonical fingerprint pinned on both sides; the Unity EditMode test
// (Mission05Scenario) asserts the identical string for scenario parity.
const EXPECTED_FINGERPRINT =
  'mission-05-line-break|turnLimit=11|bonusTurns=9|flagshipScale=1.1|wind=0:5|' +
  'rock=120,70:r35|rock=120,-70:r35|' +
  'enemy-escort-a:enemy:240,60:h180:v2:hp120:sl70:cw40|' +
  'enemy-escort-b:enemy:240,-60:h180:v2:hp120:sl70:cw40|' +
  'enemy-flagship:enemy:260,0:h180:v2:hp198:sl90:cw60|' +
  'player-sloop-a:player:0,50:h0:v3:hp120:sl80:cw50|' +
  'player-sloop-b:player:0,0:h0:v3:hp120:sl80:cw50|' +
  'player-sloop-c:player:0,-50:h0:v3:hp120:sl80:cw50';

describe('mission 05 scenario', () => {
  it('pins the scenario fingerprint shared with the Unity client', () => {
    expect(mission05Fingerprint(mission05StartResponse(505))).toBe(EXPECTED_FINGERPRINT);
  });

  it('applies the 1.1x flagship hull knob with 1.0x escorts and a rock choke', () => {
    const state = createMission05State();
    const flagship = state.ships.find((ship) => ship.id === MISSION_05_FLAGSHIP_ID);
    const escorts = state.ships.filter((ship) =>
      (MISSION_05_ESCORT_SHIP_IDS as readonly string[]).includes(ship.id)
    );
    expect(flagship?.hp).toBe(Math.floor(180 * 1.1));
    expect(escorts.map((ship) => ship.hp)).toEqual([120, 120]);
    expect(state.obstacles).toHaveLength(2);
  });

  it('is deterministic for the same seed and orders', () => {
    const first = runMission05(1, lineBreakOrders);
    const second = runMission05(1, lineBreakOrders);
    expect(second).toEqual(first);
  });

  it('reports a win with both bonuses for seed 1 line-breaking play', () => {
    const outcome = runMission05(1, lineBreakOrders);
    expect(outcome.result).toBe('win');
    expect(outcome.failReason).toBeNull();
    expect(outcome.turnCount).toBeLessThanOrEqual(9);
    expect(outcome.bonusObjectives).toEqual({ sankFlagshipFirst: true, withinTurnTarget: true });
    expect(outcome.telemetry.firstSinkTarget).toBe(MISSION_05_FLAGSHIP_ID);
    expect(outcome.damageProfile.enemyRemainingHp).toBe(0);
  });

  it('reports a flagship-first win that misses the turn bonus for seed 1 slow play', () => {
    const outcome = runMission05(1, slowFlagshipOrders);
    expect(outcome.result).toBe('win');
    expect(outcome.bonusObjectives.sankFlagshipFirst).toBe(true);
    expect(outcome.bonusObjectives.withinTurnTarget).toBe(false);
  });

  it('reports a win without the flagship bonus when escorts die first', () => {
    const outcome = runMission05(14, escortsFirstOrders);
    expect(outcome.result).toBe('win');
    expect(outcome.bonusObjectives.sankFlagshipFirst).toBe(false);
    expect(outcome.telemetry.firstSinkTarget).not.toBe(MISSION_05_FLAGSHIP_ID);
  });

  it('fails with timeout when the line holds', () => {
    const outcome = runMission05(9, slowFlagshipOrders);
    expect(outcome.result).toBe('loss');
    expect(outcome.failReason).toBe('timeout');
    expect(outcome.turns).toHaveLength(MISSION_05_TURN_LIMIT);
  });

  it('records choke navigation telemetry', () => {
    const outcome = runMission05(1, lineBreakOrders);
    expect(outcome.telemetry.chokeBlockedMoves).toBeGreaterThanOrEqual(0);
    expect(typeof outcome.telemetry.chokeBlockedMoves).toBe('number');
  });

  it('extracts the first sunk ship from turn summaries', () => {
    const record = (turn: number, sunk: string[]) => ({
      turn,
      hash: 'x',
      summary: { playerRemaining: 3, enemyRemaining: 2, sunk },
      events: []
    });
    expect(firstSunkShip([record(1, []), record(2, ['a']), record(3, ['a', 'b'])])).toBe('a');
    expect(firstSunkShip([record(1, [])])).toBeNull();
  });
});

describe('mission 05 routes', () => {
  it('starts deterministically with the scenario payload', async () => {
    const res1 = await app.inject({ method: 'POST', url: `/missions/${MISSION_05_CODE}/start` });
    const res2 = await app.inject({ method: 'POST', url: `/missions/${MISSION_05_CODE}/start` });
    expect(res1.statusCode).toBe(200);
    expect(res1.json()).toEqual(res2.json());

    const body = res1.json();
    expect(body.missionCode).toBe(MISSION_05_CODE);
    expect(body.objectives).toEqual({ turnLimit: 11, bonusTurnTarget: 9, flagshipHpScale: 1.1 });
    expect(body.state.ships).toHaveLength(6);
    expect(body.state.obstacles).toHaveLength(2);
  });

  it('resolves a winning run with first-sink telemetry', async () => {
    const res = await app.inject({
      method: 'POST',
      url: `/missions/${MISSION_05_CODE}/resolve`,
      payload: { schemaVersion: 1, seed: 1, turns: lineBreakOrders }
    });
    expect(res.statusCode).toBe(200);

    const outcome = res.json().outcome;
    expect(outcome.result).toBe('win');
    expect(outcome.telemetry.firstSinkTarget).toBe(MISSION_05_FLAGSHIP_ID);
    expect(outcome.telemetry).toHaveProperty('chokeBlockedMoves');
  });

  it('rejects orders for ships the player does not control', async () => {
    const res = await app.inject({
      method: 'POST',
      url: `/missions/${MISSION_05_CODE}/resolve`,
      payload: {
        schemaVersion: 1,
        seed: 1,
        turns: [[{ shipId: MISSION_05_FLAGSHIP_ID, action: 'pass' }]]
      }
    });
    expect(res.statusCode).toBe(400);
    expect(res.json().error).toBe('invalid_order_ship');
  });
});
