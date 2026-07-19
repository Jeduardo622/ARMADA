import { describe, it, expect, beforeAll, afterAll } from 'vitest';
import { buildServer } from '../src/app.js';
import {
  CHAIN_SHOT_CREW_PERCENT,
  CHAIN_SHOT_HULL_PERCENT,
  CHAIN_SHOT_SAIL_PERCENT,
  resolveSimPreview
} from '../src/sim/engine.js';
import {
  MISSION_10_CHAIN_SAIL_TARGET,
  MISSION_10_CODE,
  MISSION_10_ENEMY_SHIP_IDS,
  MISSION_10_PLAYER_SHIP_IDS,
  MISSION_10_TURN_LIMIT,
  createMission10State,
  mission10Fingerprint,
  mission10StartResponse,
  runMission10
} from '../src/sim/mission10.js';
import type { SimOrder, SimState } from '../src/sim/types.js';

const app = buildServer({ testing: true });

beforeAll(async () => {
  await app.ready();
});

afterAll(async () => {
  await app.close();
});

const fire = (shipId: string, target: string, ammo?: 'round' | 'chain'): SimOrder => ({
  shipId,
  action: 'broadside',
  targetShipId: target,
  side: 'starboard',
  turnDelta: 0,
  speedDelta: 0,
  ...(ammo ? { ammo } : {})
});

const buildOrders = (chainOnTurn: (i: number) => boolean): SimOrder[][] =>
  Array.from({ length: MISSION_10_TURN_LIMIT }, (_, i) => {
    const target = i < 5 ? MISSION_10_ENEMY_SHIP_IDS[0] : MISSION_10_ENEMY_SHIP_IDS[1];
    const ammo = chainOnTurn(i) ? ('chain' as const) : undefined;
    return [
      fire(MISSION_10_PLAYER_SHIP_IDS[0], target, ammo),
      fire(MISSION_10_PLAYER_SHIP_IDS[1], target, ammo)
    ];
  });

// Mixed battery: chain into the rigging for the first three turns while the
// lines close, then ball to sink, focusing clipper A then B.
const mixedOrders = buildOrders((i) => i < 3);
// A single opening chain volley, then ball the rest of the way.
const lightChainOrders = buildOrders((i) => i < 1);
// Pure chain never sinks inside the turn limit — the ammo choice matters.
const pureChainOrders = buildOrders(() => true);

// Canonical fingerprint pinned on both sides; the Unity EditMode test
// (Mission10Scenario) asserts the identical string for scenario parity.
const EXPECTED_FINGERPRINT =
  'mission-10-sail-cutter|turnLimit=10|chainHull=40|chainSail=120|chainCrew=20|sailTarget=60|wind=0:4|' +
  'enemy-clipper-a:enemy:220,35:h180:v3:hp140:sl110:cw50|' +
  'enemy-clipper-b:enemy:220,-35:h180:v3:hp140:sl110:cw50|' +
  'player-sloop-a:player:0,30:h0:v3:hp120:sl80:cw50|' +
  'player-sloop-b:player:0,-30:h0:v3:hp120:sl80:cw50';

describe('chain shot modifier', () => {
  // A becalmed broadside duel so the only rolls are the hit and variance
  // rolls and the damage numbers are exact.
  const dueling = (): SimState => ({
    turn: 1,
    wind: { direction: 0, speed: 0 },
    ships: [
      {
        id: 'ship-a',
        side: 'player',
        position: { x: 0, y: 0 },
        heading: 0,
        speed: 2,
        hp: 100,
        sail: 80,
        crew: 40
      },
      {
        id: 'enemy-b',
        side: 'enemy',
        position: { x: 60, y: 0 },
        heading: 90,
        speed: 2,
        hp: 100,
        sail: 80,
        crew: 40
      }
    ]
  });

  const preview = (ammo?: 'round' | 'chain', modifiers?: object) =>
    resolveSimPreview({
      schemaVersion: 1,
      seed: 4,
      turn: 1,
      state: dueling(),
      orders: [{ ...fire('ship-a', 'enemy-b'), ...(ammo ? { ammo } : {}) }],
      modifiers
    });

  it('redistributes the same shot weight toward the rigging and marks the event', () => {
    // Seed 4 lands with scaled damage 28: round splits 28/16/9, chain splits
    // floor(28 * 40/120/20 / 100) = 11/33/5 — heavy sail, reduced hull.
    const result = preview('chain', { chainShot: true });
    const broadsides = result.events.filter((event) => event.type === 'broadside');
    expect(broadsides).toHaveLength(1);
    expect(broadsides[0]).toMatchObject({
      shipId: 'ship-a',
      targetShipId: 'enemy-b',
      hit: true,
      roll: 68,
      hitChance: 72,
      damage: { hull: 11, sail: 33, crew: 5 },
      targetRemaining: { hp: 89, sail: 47, crew: 35 },
      ammo: 'chain'
    });
    expect(CHAIN_SHOT_HULL_PERCENT).toBe(40);
    expect(CHAIN_SHOT_SAIL_PERCENT).toBe(120);
    expect(CHAIN_SHOT_CREW_PERCENT).toBe(20);
  });

  it('keeps round shot under the flag byte-identical to the legacy split', () => {
    const legacy = preview();
    const roundExplicit = preview('round', { chainShot: true });
    const ammoAbsent = preview(undefined, { chainShot: true });
    const legacyBroadside = legacy.events.find((event) => event.type === 'broadside');
    expect(legacyBroadside).toMatchObject({ damage: { hull: 28, sail: 16, crew: 9 } });
    expect(legacy.events.some((event) => event.type === 'broadside' && event.ammo)).toBe(false);
    expect(roundExplicit.hash).toBe(legacy.hash);
    expect(roundExplicit).toEqual(legacy);
    expect(ammoAbsent.hash).toBe(legacy.hash);
  });

  it('ignores the ammo key entirely whether the flag is absent or false', () => {
    const legacy = preview();
    const flagAbsent = preview('chain');
    const flagOff = preview('chain', { chainShot: false });
    expect(flagAbsent.hash).toBe(legacy.hash);
    expect(flagAbsent).toEqual(legacy);
    expect(flagOff.hash).toBe(legacy.hash);
    expect(flagOff).toEqual(legacy);
  });
});

describe('mission 10 scenario', () => {
  it('pins the scenario fingerprint shared with the Unity client', () => {
    expect(mission10Fingerprint(mission10StartResponse(1010))).toBe(EXPECTED_FINGERPRINT);
  });

  it('opens with tall-rigged clippers at full strength', () => {
    const state = createMission10State();
    expect(state.wind).toEqual({ direction: 0, speed: 4 });
    const clippers = state.ships.filter((ship) => ship.side === 'enemy');
    expect(clippers.map((ship) => ship.hp)).toEqual([140, 140]);
    expect(clippers.map((ship) => ship.sail)).toEqual([110, 110]);
  });

  it('is deterministic for the same seed and orders', () => {
    const first = runMission10(2, mixedOrders);
    const second = runMission10(2, mixedOrders);
    expect(second).toEqual(first);
    expect(first.turns.map((turn) => turn.hash)).toEqual(second.turns.map((turn) => turn.hash));
  });

  it('reports a mixed-battery win with full ammo telemetry for seed 2', () => {
    const outcome = runMission10(2, mixedOrders);
    expect(outcome.result).toBe('win');
    expect(outcome.failReason).toBeNull();
    expect(outcome.turnCount).toBe(8);
    // The four chain hits strip clipper A's entire 110 sail; the telemetry
    // counts the applied loss, not the larger nominal rolls the engine
    // clamped at zero.
    expect(outcome.telemetry).toEqual({
      chainShotOrders: 6,
      chainShotHits: 4,
      roundShotHits: 7,
      chainSailDamageDealt: 110
    });
    expect(outcome.bonusObjectives).toEqual({ sailShredder: true, mixedBattery: true });
    expect(outcome.damageProfile.enemyRemainingHp).toBe(0);
    const chainEvents = outcome.turns.flatMap((turn) =>
      turn.events.filter((event) => event.type === 'broadside' && event.ammo === 'chain')
    );
    expect(chainEvents).toHaveLength(6);
  });

  it('awards mixedBattery but not sailShredder when a lone chain hit falls short', () => {
    const outcome = runMission10(1, lightChainOrders);
    expect(outcome.result).toBe('win');
    expect(outcome.turnCount).toBe(8);
    // One chain hit at 55 sail sits just under the 60-point target.
    expect(outcome.telemetry).toEqual({
      chainShotOrders: 2,
      chainShotHits: 1,
      roundShotHits: 8,
      chainSailDamageDealt: 55
    });
    expect(outcome.telemetry.chainSailDamageDealt).toBeLessThan(MISSION_10_CHAIN_SAIL_TARGET);
    expect(outcome.bonusObjectives).toEqual({ sailShredder: false, mixedBattery: true });
  });

  it('denies both bonuses when the opening chain volley misses outright', () => {
    const outcome = runMission10(88, lightChainOrders);
    expect(outcome.result).toBe('win');
    expect(outcome.telemetry).toEqual({
      chainShotOrders: 2,
      chainShotHits: 0,
      roundShotHits: 9,
      chainSailDamageDealt: 0
    });
    expect(outcome.bonusObjectives).toEqual({ sailShredder: false, mixedBattery: false });
  });

  it('times out on pure chain — shredded rigging alone sinks nothing', () => {
    const outcome = runMission10(63, pureChainOrders);
    expect(outcome.result).toBe('loss');
    expect(outcome.failReason).toBe('timeout');
    expect(outcome.bonusObjectives).toEqual({ sailShredder: false, mixedBattery: false });
    expect(outcome.telemetry.roundShotHits).toBe(0);
    // Applied sail damage saturates at the fleet's total canvas (2 x 110);
    // twelve chain hits cannot shred more sail than the clippers carry.
    expect(outcome.telemetry.chainSailDamageDealt).toBe(220);
    expect(outcome.turns).toHaveLength(MISSION_10_TURN_LIMIT);
  });
});

describe('mission 10 routes', () => {
  it('starts deterministically with the scenario payload', async () => {
    const res1 = await app.inject({ method: 'POST', url: `/missions/${MISSION_10_CODE}/start` });
    const res2 = await app.inject({ method: 'POST', url: `/missions/${MISSION_10_CODE}/start` });
    expect(res1.statusCode).toBe(200);
    expect(res1.json()).toEqual(res2.json());

    const body = res1.json();
    expect(body.missionCode).toBe(MISSION_10_CODE);
    expect(body.objectives).toEqual({
      turnLimit: MISSION_10_TURN_LIMIT,
      chainHullPercent: CHAIN_SHOT_HULL_PERCENT,
      chainSailPercent: CHAIN_SHOT_SAIL_PERCENT,
      chainCrewPercent: CHAIN_SHOT_CREW_PERCENT,
      chainSailTarget: MISSION_10_CHAIN_SAIL_TARGET
    });
    expect(body.state.ships).toHaveLength(4);
  });

  it('resolves a mixed-battery win with ammo telemetry', async () => {
    const res = await app.inject({
      method: 'POST',
      url: `/missions/${MISSION_10_CODE}/resolve`,
      payload: { schemaVersion: 1, seed: 2, turns: mixedOrders }
    });
    expect(res.statusCode).toBe(200);

    const outcome = res.json().outcome;
    expect(outcome.result).toBe('win');
    expect(outcome.telemetry.chainShotHits).toBe(4);
    expect(outcome.telemetry.chainSailDamageDealt).toBe(110);
  });

  it('rejects orders for ships the player does not control', async () => {
    const res = await app.inject({
      method: 'POST',
      url: `/missions/${MISSION_10_CODE}/resolve`,
      payload: {
        schemaVersion: 1,
        seed: 2,
        turns: [[{ shipId: MISSION_10_ENEMY_SHIP_IDS[0], action: 'pass' }]]
      }
    });
    expect(res.statusCode).toBe(400);
    expect(res.json().error).toBe('invalid_order_ship');
  });

  it('rejects upgrade tiers because the mission does not support them', async () => {
    const res = await app.inject({
      method: 'POST',
      url: `/missions/${MISSION_10_CODE}/resolve`,
      payload: {
        schemaVersion: 1,
        seed: 2,
        turns: mixedOrders,
        upgrades: { cannon: 1 }
      }
    });
    expect(res.statusCode).toBe(400);
  });
});
