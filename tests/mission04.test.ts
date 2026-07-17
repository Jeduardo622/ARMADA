import { describe, it, expect, beforeAll, afterAll } from 'vitest';
import { buildServer } from '../src/app.js';
import { resolveSimPreview } from '../src/sim/engine.js';
import {
  MISSION_04_CODE,
  MISSION_04_ENEMY_SHIP_IDS,
  MISSION_04_PLAYER_SHIP_IDS,
  MISSION_04_TURN_LIMIT,
  createMission04State,
  mission04Fingerprint,
  mission04StartResponse,
  runMission04
} from '../src/sim/mission04.js';
import type { SimOrder, SimPreviewRequest } from '../src/sim/types.js';

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
  speedDelta: -2
});

// Parallel boarding: each sloop softens then grapples its own frigate.
const parallelOrders: SimOrder[][] = Array.from({ length: MISSION_04_TURN_LIMIT }, (_, i) => {
  if (i < 3) {
    return [
      fire(MISSION_04_PLAYER_SHIP_IDS[0], MISSION_04_ENEMY_SHIP_IDS[0]),
      fire(MISSION_04_PLAYER_SHIP_IDS[1], MISSION_04_ENEMY_SHIP_IDS[1])
    ];
  }
  return [
    board(MISSION_04_PLAYER_SHIP_IDS[0], MISSION_04_ENEMY_SHIP_IDS[0]),
    board(MISSION_04_PLAYER_SHIP_IDS[1], MISSION_04_ENEMY_SHIP_IDS[1])
  ];
});

// Pure gunnery: focus frigate A then B, stopping alongside from turn 4.
const gunneryOrders: SimOrder[][] = Array.from({ length: MISSION_04_TURN_LIMIT }, (_, i) => {
  const target = i < 5 ? MISSION_04_ENEMY_SHIP_IDS[0] : MISSION_04_ENEMY_SHIP_IDS[1];
  const slow = i >= 3 ? -2 : 0;
  return [
    { ...fire(MISSION_04_PLAYER_SHIP_IDS[0], target), speedDelta: slow },
    { ...fire(MISSION_04_PLAYER_SHIP_IDS[1], target), speedDelta: slow }
  ];
});

// Canonical fingerprint pinned on both sides; the Unity EditMode test
// (Mission04Scenario) asserts the identical string for scenario parity.
const EXPECTED_FINGERPRINT =
  'mission-04-boarding-party|turnLimit=10|crewScale=0.9|boardBonus=0.1|wind=180:3|' +
  'debris=130,0:r45:p2|' +
  'enemy-frigate-a:enemy:220,40:h180:v2:hp180:sl90:cw54|' +
  'enemy-frigate-b:enemy:260,-40:h180:v2:hp180:sl90:cw54|' +
  'player-sloop-a:player:0,30:h0:v3:hp120:sl80:cw50|' +
  'player-sloop-b:player:0,-30:h0:v3:hp120:sl80:cw50';

describe('engine slow zones', () => {
  const zonePreview = (withZone: boolean): SimPreviewRequest => ({
    schemaVersion: 1,
    seed: 3,
    turn: 1,
    state: {
      turn: 1,
      wind: { direction: 0, speed: 5 },
      ships: [
        {
          id: 'runner',
          side: 'player',
          position: { x: 130, y: 0 },
          heading: 0,
          speed: 3,
          hp: 100,
          sail: 80,
          crew: 40
        }
      ],
      ...(withZone
        ? { slowZones: [{ position: { x: 130, y: 0 }, radius: 45, speedPenalty: 2 }] }
        : {})
    },
    orders: [],
    modifiers: { windMovement: true }
  });

  it('slows ships moving inside a debris field', () => {
    const result = resolveSimPreview(zonePreview(true));
    const movement = result.events.find((event) => event.type === 'movement');
    if (movement?.type !== 'movement') throw new Error('expected movement event');
    // Tailwind effective speed 5 loses the 2-point debris penalty.
    expect(movement.effectiveSpeed).toBe(3);
    expect(movement.slowedByHazard).toBe(true);
    expect(result.nextState.ships[0].position).toEqual({ x: 145, y: 0 });
  });

  it('leaves ships outside the field at full speed', () => {
    const result = resolveSimPreview(zonePreview(false));
    const movement = result.events.find((event) => event.type === 'movement');
    if (movement?.type !== 'movement') throw new Error('expected movement event');
    expect(movement.effectiveSpeed).toBe(5);
    expect(movement).not.toHaveProperty('slowedByHazard');
    expect(result.nextState.ships[0].position).toEqual({ x: 155, y: 0 });
  });
});

describe('engine boarding bonus', () => {
  const boardingPreview = (bonus?: number): SimPreviewRequest => ({
    schemaVersion: 1,
    seed: 5,
    turn: 1,
    state: {
      turn: 1,
      wind: { direction: 0, speed: 0 },
      ships: [
        {
          id: 'boarder',
          side: 'player',
          position: { x: 0, y: 0 },
          heading: 0,
          speed: 0,
          hp: 100,
          sail: 50,
          crew: 20
        },
        {
          id: 'defender',
          side: 'enemy',
          position: { x: 0, y: 0 },
          heading: 0,
          speed: 0,
          hp: 100,
          sail: 50,
          crew: 60
        }
      ]
    },
    orders: [
      { shipId: 'boarder', action: 'boarding', targetShipId: 'defender', turnDelta: 0, speedDelta: 0 }
    ],
    ...(bonus === undefined ? {} : { modifiers: { boardingBonus: { boarder: bonus } } })
  });

  it('turns a failed boarding into a success at the same roll', () => {
    // Seed 5 rolls 52: base chance 50 fails, +0.1 bonus (60) succeeds.
    const base = resolveSimPreview(boardingPreview());
    const boosted = resolveSimPreview(boardingPreview(0.1));
    const baseEvent = base.events.find((event) => event.type === 'boarding');
    const boostedEvent = boosted.events.find((event) => event.type === 'boarding');
    if (baseEvent?.type !== 'boarding' || boostedEvent?.type !== 'boarding') {
      throw new Error('expected boarding events');
    }
    expect(baseEvent.roll).toBe(52);
    expect(boostedEvent.roll).toBe(52);
    expect(baseEvent.success).toBe(false);
    expect(boostedEvent.success).toBe(true);
  });
});

describe('mission 04 scenario', () => {
  it('pins the scenario fingerprint shared with the Unity client', () => {
    expect(mission04Fingerprint(mission04StartResponse(404))).toBe(EXPECTED_FINGERPRINT);
  });

  it('applies the 0.9x enemy crew tuning knob at full hull strength', () => {
    const state = createMission04State();
    const frigates = state.ships.filter((ship) => ship.side === 'enemy');
    expect(frigates.map((ship) => ship.crew)).toEqual([54, 54]);
    expect(frigates.map((ship) => ship.hp)).toEqual([180, 180]);
    expect(state.slowZones).toHaveLength(1);
  });

  it('is deterministic for the same seed and orders', () => {
    const first = runMission04(4, parallelOrders);
    const second = runMission04(4, parallelOrders);
    expect(second).toEqual(first);
  });

  it('reports a boarding win with both bonuses for seed 4 parallel boarding', () => {
    const outcome = runMission04(4, parallelOrders);
    expect(outcome.result).toBe('win');
    expect(outcome.failReason).toBeNull();
    expect(outcome.bonusObjectives).toEqual({ successfulBoarding: true, noShipLost: true });
    expect(outcome.telemetry.boardingSuccesses).toBeGreaterThan(0);
    expect(outcome.telemetry.boardingSuccesses).toBeLessThanOrEqual(
      outcome.telemetry.boardingAttempts
    );
    expect(outcome.damageProfile.enemyRemainingHp).toBe(0);
  });

  it('reports a gunnery win without the boarding bonus for seed 13', () => {
    const outcome = runMission04(13, gunneryOrders);
    expect(outcome.result).toBe('win');
    expect(outcome.bonusObjectives.successfulBoarding).toBe(false);
    expect(outcome.bonusObjectives.noShipLost).toBe(true);
    expect(outcome.telemetry.boardingAttempts).toBe(0);
  });

  it('fails with timeout when the frigates hold out', () => {
    const outcome = runMission04(1, parallelOrders);
    expect(outcome.result).toBe('loss');
    expect(outcome.failReason).toBe('timeout');
    expect(outcome.turns).toHaveLength(MISSION_04_TURN_LIMIT);
  });

  it('fails as flanked when the idle player is caught between the frigates', () => {
    const outcome = runMission04(1, []);
    expect(outcome.result).toBe('loss');
    expect(outcome.failReason).toBe('flanked');
  });

  it('fails as sunk when boarding actions go wrong up close', () => {
    const outcome = runMission04(3, parallelOrders);
    expect(outcome.result).toBe('loss');
    expect(outcome.failReason).toBe('sunk');
    expect(outcome.damageProfile.playerRemainingHp).toBe(0);
  });
});

describe('mission 04 routes', () => {
  it('starts deterministically with the scenario payload', async () => {
    const res1 = await app.inject({ method: 'POST', url: `/missions/${MISSION_04_CODE}/start` });
    const res2 = await app.inject({ method: 'POST', url: `/missions/${MISSION_04_CODE}/start` });
    expect(res1.statusCode).toBe(200);
    expect(res1.json()).toEqual(res2.json());

    const body = res1.json();
    expect(body.missionCode).toBe(MISSION_04_CODE);
    expect(body.objectives).toEqual({
      turnLimit: 10,
      enemyCrewScale: 0.9,
      playerBoardingBonus: 0.1
    });
    expect(body.state.ships).toHaveLength(4);
    expect(body.state.slowZones).toHaveLength(1);
  });

  it('resolves a boarding win with boarding telemetry', async () => {
    const res = await app.inject({
      method: 'POST',
      url: `/missions/${MISSION_04_CODE}/resolve`,
      payload: { schemaVersion: 1, seed: 4, turns: parallelOrders }
    });
    expect(res.statusCode).toBe(200);

    const outcome = res.json().outcome;
    expect(outcome.result).toBe('win');
    expect(outcome.bonusObjectives.successfulBoarding).toBe(true);
    expect(outcome.telemetry.boardingSuccesses).toBeGreaterThan(0);
  });

  it('rejects orders for ships the player does not control', async () => {
    const res = await app.inject({
      method: 'POST',
      url: `/missions/${MISSION_04_CODE}/resolve`,
      payload: {
        schemaVersion: 1,
        seed: 4,
        turns: [[{ shipId: MISSION_04_ENEMY_SHIP_IDS[0], action: 'pass' }]]
      }
    });
    expect(res.statusCode).toBe(400);
    expect(res.json().error).toBe('invalid_order_ship');
  });
});
