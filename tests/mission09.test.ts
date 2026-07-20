import { describe, it, expect, beforeAll, afterAll } from 'vitest';
import { buildServer } from '../src/app.js';
import { RAM_CONTACT_RANGE, resolveSimPreview } from '../src/sim/engine.js';
import { countRamProfile } from '../src/sim/missionMetrics.js';
import {
  MISSION_09_CODE,
  MISSION_09_ENEMY_SHIP_IDS,
  MISSION_09_PLAYER_SHIP_IDS,
  MISSION_09_RAM_TARGET,
  MISSION_09_TURN_LIMIT,
  createMission09State,
  mission09Fingerprint,
  mission09StartResponse,
  runMission09
} from '../src/sim/mission09.js';
import type { SimOrder, SimState } from '../src/sim/types.js';

const app = buildServer({ testing: true });

beforeAll(async () => {
  await app.ready();
});

afterAll(async () => {
  await app.close();
});

const fire = (shipId: string, target: string, turnDelta = 0, speedDelta = 0): SimOrder => ({
  shipId,
  action: 'broadside',
  targetShipId: target,
  side: 'starboard',
  turnDelta,
  speedDelta
});

// Ramming run: both sloops crowd on sail for two turns (+2 speed) and drive
// straight downwind into the brig line, guns firing on brig A then B. The
// turn-4 contact rams both lanes at effective speed 9.
const chargeOrders: SimOrder[][] = Array.from({ length: MISSION_09_TURN_LIMIT }, (_, i) => {
  const target = i < 5 ? MISSION_09_ENEMY_SHIP_IDS[0] : MISSION_09_ENEMY_SHIP_IDS[1];
  const speedDelta = i < 2 ? 2 : 0;
  return [
    fire(MISSION_09_PLAYER_SHIP_IDS[0], target, 0, speedDelta),
    fire(MISSION_09_PLAYER_SHIP_IDS[1], target, 0, speedDelta)
  ];
});

// Gunnery hold: identical targeting at cruising speed, so the lines meet
// later and the brigs get the chance to drive their own bows home.
const holdOrders: SimOrder[][] = Array.from({ length: MISSION_09_TURN_LIMIT }, (_, i) => {
  const target = i < 5 ? MISSION_09_ENEMY_SHIP_IDS[0] : MISSION_09_ENEMY_SHIP_IDS[1];
  return [
    fire(MISSION_09_PLAYER_SHIP_IDS[0], target, 0, 0),
    fire(MISSION_09_PLAYER_SHIP_IDS[1], target, 0, 0)
  ];
});

// Canonical fingerprint pinned on both sides; the Unity EditMode test
// (Mission09Scenario) asserts the identical string for scenario parity.
const EXPECTED_FINGERPRINT =
  'mission-09-iron-bow|turnLimit=10|ramRange=25|ramTarget=2|wind=0:4|' +
  'enemy-brig-a:enemy:220,35:h180:v3:hp160:sl85:cw55|' +
  'enemy-brig-b:enemy:220,-35:h180:v3:hp160:sl85:cw55|' +
  'player-sloop-a:player:0,30:h0:v3:hp120:sl80:cw50|' +
  'player-sloop-b:player:0,-30:h0:v3:hp120:sl80:cw50';

describe('ramming modifier', () => {
  // Two ships closing bow-to-bow in a dead calm, so effective speeds are the
  // ordered speeds and the contact geometry is exact.
  const closingState = (gap: number, playerSpeed: number, enemySpeed: number): SimState => ({
    turn: 1,
    wind: { direction: 0, speed: 0 },
    ships: [
      {
        id: 'ship-a',
        side: 'player',
        position: { x: 0, y: 0 },
        heading: 0,
        speed: playerSpeed,
        hp: 100,
        sail: 80,
        crew: 40
      },
      {
        id: 'enemy-b',
        side: 'enemy',
        position: { x: gap, y: 0 },
        heading: 180,
        speed: enemySpeed,
        hp: 100,
        sail: 80,
        crew: 40
      }
    ]
  });

  const preview = (state: SimState, modifiers?: object) =>
    resolveSimPreview({
      schemaVersion: 1,
      seed: 1,
      turn: 1,
      state,
      orders: [],
      modifiers: { windMovement: true, ...(modifiers ?? {}) }
    });

  it('deals speed-scaled hull damage to both ships and rams each pair once', () => {
    // enemy-b resolves first (id order) and moves 30 -> 20, inside the
    // 25-unit contact range of ship-a: hull 10 + 2*4 = 18 to the target,
    // floor(18 * 0.5) = 9 recoil on the rammer's own bow. ship-a then moves
    // into contact too, but the pair already rammed this turn.
    const result = preview(closingState(30, 2, 2), { ramming: true });
    const rams = result.events.filter((event) => event.type === 'ram');
    expect(rams).toHaveLength(1);
    expect(rams[0]).toMatchObject({
      shipId: 'enemy-b',
      targetShipId: 'ship-a',
      effectiveSpeed: 2,
      hullDamage: 18,
      selfHullDamage: 9,
      targetRemaining: { hp: 82, sail: 80, crew: 40 },
      rammerRemaining: { hp: 91, sail: 80, crew: 40 }
    });
    const ships = new Map(result.nextState.ships.map((ship) => [ship.id, ship]));
    expect(ships.get('ship-a')?.hp).toBe(82);
    expect(ships.get('enemy-b')?.hp).toBe(91);
  });

  it('keeps flag-off behavior byte-identical whether absent or false', () => {
    const absent = preview(closingState(30, 2, 2));
    const explicitOff = preview(closingState(30, 2, 2), { ramming: false });
    expect(absent.events.some((event) => event.type === 'ram')).toBe(false);
    expect(explicitOff.hash).toBe(absent.hash);
    expect(explicitOff).toEqual(absent);
  });

  it('counts only the hull actually removed when a ram finishes a battered hull', () => {
    // ship-a is down to 10 hp, so enemy-b's nominal 18-point ram clamps at
    // zero: the event still reports the nominal roll, but the telemetry
    // must count the 10 hull actually removed (and the full 9 recoil
    // against enemy-b's healthy bow).
    const state = closingState(30, 2, 2);
    state.ships[0].hp = 10;
    const result = preview(state, { ramming: true });
    const rams = result.events.filter((event) => event.type === 'ram');
    expect(rams[0]).toMatchObject({ hullDamage: 18, targetRemaining: { hp: 0 } });

    const turns = [{ turn: 1, hash: result.hash, summary: result.summary, events: result.events }];
    expect(countRamProfile(turns, ['ship-a'], state)).toEqual({
      ramsInflicted: 0,
      ramsSuffered: 1,
      ramHullDamageDealt: 0,
      ramHullDamageTaken: 10
    });
    expect(countRamProfile(turns, ['enemy-b'], state)).toEqual({
      ramsInflicted: 1,
      ramsSuffered: 0,
      ramHullDamageDealt: 10,
      ramHullDamageTaken: 9
    });
  });

  it('mirrors fire-tick burns so an overkill ram after a tick is not overcounted', () => {
    // ship-a starts burning at 20 hp: the start-of-turn fire tick burns it
    // to 15 without any remaining block, then enemy-b's nominal 18-point
    // ram sinks it. The tracker must charge the ram only the 15 hull the
    // tick left behind — not the stale-tracked 18.
    const state = closingState(15, 0, 2);
    state.ships[0].hp = 20;
    state.ships[0].status = { onFire: true, fireTurnsRemaining: 2 };
    const result = preview(state, { ramming: true, statusEffects: true });
    const ram = result.events.find((event) => event.type === 'ram');
    expect(ram).toMatchObject({ hullDamage: 18, targetRemaining: { hp: 0 } });

    const turns = [{ turn: 1, hash: result.hash, summary: result.summary, events: result.events }];
    expect(countRamProfile(turns, ['ship-a'], state)).toEqual({
      ramsInflicted: 0,
      ramsSuffered: 1,
      ramHullDamageDealt: 0,
      ramHullDamageTaken: 15
    });
  });

  it('never rams from a standstill but a moving enemy can strike a stationary hull', () => {
    // Both becalmed inside contact range: proximity alone is not a ram.
    const becalmed = preview(closingState(10, 0, 0), { ramming: true });
    expect(becalmed.events.some((event) => event.type === 'ram')).toBe(false);

    // The enemy under way closes 15 -> 5 and strikes the stationary hull.
    const struck = preview(closingState(15, 0, 2), { ramming: true });
    const rams = struck.events.filter((event) => event.type === 'ram');
    expect(rams).toHaveLength(1);
    expect(rams[0]).toMatchObject({ shipId: 'enemy-b', targetShipId: 'ship-a' });
  });
});

describe('mission 09 scenario', () => {
  it('pins the scenario fingerprint shared with the Unity client', () => {
    expect(mission09Fingerprint(mission09StartResponse(909))).toBe(EXPECTED_FINGERPRINT);
  });

  it('opens with the player running free at full brig strength', () => {
    const state = createMission09State();
    expect(state.wind).toEqual({ direction: 0, speed: 4 });
    const brigs = state.ships.filter((ship) => ship.side === 'enemy');
    expect(brigs.map((ship) => ship.hp)).toEqual([160, 160]);
    expect(brigs.map((ship) => ship.sail)).toEqual([85, 85]);
  });

  it('is deterministic for the same seed and orders', () => {
    const first = runMission09(87, chargeOrders);
    const second = runMission09(87, chargeOrders);
    expect(second).toEqual(first);
    expect(first.turns.map((turn) => turn.hash)).toEqual(second.turns.map((turn) => turn.hash));
  });

  it('reports a double-ram win with full ram telemetry for seed 87', () => {
    const outcome = runMission09(87, chargeOrders);
    expect(outcome.result).toBe('win');
    expect(outcome.failReason).toBeNull();
    expect(outcome.turnCount).toBe(7);
    // The turn-4 contact rams both lanes at effective speed 9 (speed 7 plus
    // the tailwind bonus): nominal 46 hull each, but brig B is down to 23 hp
    // at contact, so the applied telemetry counts 46 + 23; recoil is 23 each
    // against full sloop hulls.
    expect(outcome.telemetry).toEqual({
      ramsInflicted: 2,
      ramsSuffered: 0,
      ramHullDamageDealt: 69,
      ramHullDamageTaken: 46
    });
    const rams = outcome.turns.flatMap((turn) =>
      turn.events.filter((event) => event.type === 'ram')
    );
    expect(rams.map((ram) => ram.type === 'ram' && ram.shipId)).toEqual([
      MISSION_09_PLAYER_SHIP_IDS[0],
      MISSION_09_PLAYER_SHIP_IDS[1]
    ]);
    expect(outcome.bonusObjectives).toEqual({ hullBreaker: true, unrammed: true });
    expect(outcome.damageProfile.enemyRemainingHp).toBe(0);
  });

  it('awards unrammed but not hullBreaker when the lines never touch', () => {
    const outcome = runMission09(1, chargeOrders);
    expect(outcome.result).toBe('win');
    expect(outcome.turnCount).toBe(8);
    expect(outcome.telemetry.ramsInflicted).toBe(0);
    expect(outcome.telemetry.ramsSuffered).toBe(0);
    expect(outcome.bonusObjectives).toEqual({ hullBreaker: false, unrammed: true });
  });

  it('denies unrammed when an enemy bow strikes home and sinks itself on the recoil', () => {
    const outcome = runMission09(41, holdOrders);
    expect(outcome.result).toBe('win');
    expect(outcome.telemetry).toEqual({
      ramsInflicted: 0,
      ramsSuffered: 1,
      ramHullDamageDealt: 0,
      ramHullDamageTaken: 34
    });
    expect(outcome.bonusObjectives).toEqual({ hullBreaker: false, unrammed: false });
    const ram = outcome.turns
      .flatMap((turn) => turn.events)
      .find((event) => event.type === 'ram');
    // The battered brig drives its bow home and the recoil finishes its own
    // hull — both ships taking ram damage is the heart of the modifier.
    expect(ram).toMatchObject({
      shipId: MISSION_09_ENEMY_SHIP_IDS[0],
      targetShipId: MISSION_09_PLAYER_SHIP_IDS[0],
      rammerRemaining: { hp: 0 }
    });
  });

  it('denies both bonuses on a timeout loss', () => {
    const outcome = runMission09(63, chargeOrders);
    expect(outcome.result).toBe('loss');
    expect(outcome.failReason).toBe('timeout');
    expect(outcome.bonusObjectives).toEqual({ hullBreaker: false, unrammed: false });
    expect(outcome.turns).toHaveLength(MISSION_09_TURN_LIMIT);
  });
});

describe('mission 09 routes', () => {
  it('starts deterministically with the scenario payload', async () => {
    const res1 = await app.inject({ method: 'POST', url: `/missions/${MISSION_09_CODE}/start` });
    const res2 = await app.inject({ method: 'POST', url: `/missions/${MISSION_09_CODE}/start` });
    expect(res1.statusCode).toBe(200);
    expect(res1.json()).toEqual(res2.json());

    const body = res1.json();
    expect(body.missionCode).toBe(MISSION_09_CODE);
    expect(body.objectives).toEqual({
      turnLimit: MISSION_09_TURN_LIMIT,
      ramContactRange: RAM_CONTACT_RANGE,
      ramTarget: MISSION_09_RAM_TARGET
    });
    expect(body.state.ships).toHaveLength(4);
  });

  it('resolves a double-ram win with ram telemetry', async () => {
    const res = await app.inject({
      method: 'POST',
      url: `/missions/${MISSION_09_CODE}/resolve`,
      payload: { schemaVersion: 1, seed: 87, turns: chargeOrders }
    });
    expect(res.statusCode).toBe(200);

    const outcome = res.json().outcome;
    expect(outcome.result).toBe('win');
    expect(outcome.telemetry.ramsInflicted).toBe(2);
    expect(outcome.telemetry.ramHullDamageDealt).toBe(69);
  });

  it('rejects orders for ships the player does not control', async () => {
    const res = await app.inject({
      method: 'POST',
      url: `/missions/${MISSION_09_CODE}/resolve`,
      payload: {
        schemaVersion: 1,
        seed: 87,
        turns: [[{ shipId: MISSION_09_ENEMY_SHIP_IDS[0], action: 'pass' }]]
      }
    });
    expect(res.statusCode).toBe(400);
    expect(res.json().error).toBe('invalid_order_ship');
  });

  it('rejects upgrade tiers because the mission does not support them', async () => {
    const res = await app.inject({
      method: 'POST',
      url: `/missions/${MISSION_09_CODE}/resolve`,
      payload: {
        schemaVersion: 1,
        seed: 87,
        turns: chargeOrders,
        upgrades: { cannon: 1 }
      }
    });
    expect(res.statusCode).toBe(400);
  });
});
