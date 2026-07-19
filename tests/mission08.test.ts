import { describe, it, expect, beforeAll, afterAll } from 'vitest';
import { buildServer } from '../src/app.js';
import {
  WIND_TURN_BEAM_LIMIT,
  WIND_TURN_DOWNWIND_LIMIT,
  WIND_TURN_UPWIND_LIMIT,
  resolveSimPreview
} from '../src/sim/engine.js';
import {
  MISSION_08_CODE,
  MISSION_08_ENEMY_SHIP_IDS,
  MISSION_08_PLAYER_SHIP_IDS,
  MISSION_08_TURN_LIMIT,
  createMission08State,
  mission08Fingerprint,
  mission08StartResponse,
  runMission08
} from '../src/sim/mission08.js';
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

// Tacking gunnery: both sloops focus corvette A then B, heaving to from turn
// 4. On turns 2-3 they order a hard 60° weave that the upwind clamp cuts to
// 30°, so the run showcases clamped-maneuver telemetry while still winning.
const tackingOrders: SimOrder[][] = Array.from({ length: MISSION_08_TURN_LIMIT }, (_, i) => {
  const target = i < 5 ? MISSION_08_ENEMY_SHIP_IDS[0] : MISSION_08_ENEMY_SHIP_IDS[1];
  const turnDelta = i === 1 ? 60 : i === 2 ? -60 : 0;
  const speedDelta = i >= 3 ? -2 : 0;
  return [
    fire(MISSION_08_PLAYER_SHIP_IDS[0], target, turnDelta, speedDelta),
    fire(MISSION_08_PLAYER_SHIP_IDS[1], target, turnDelta, speedDelta)
  ];
});

// Clean gunnery: identical targeting with the helm amidships throughout, so
// no maneuver is ever clamped and the cleanTack bonus stays reachable.
const cleanOrders: SimOrder[][] = Array.from({ length: MISSION_08_TURN_LIMIT }, (_, i) => {
  const target = i < 5 ? MISSION_08_ENEMY_SHIP_IDS[0] : MISSION_08_ENEMY_SHIP_IDS[1];
  const speedDelta = i >= 3 ? -2 : 0;
  return [
    fire(MISSION_08_PLAYER_SHIP_IDS[0], target, 0, speedDelta),
    fire(MISSION_08_PLAYER_SHIP_IDS[1], target, 0, speedDelta)
  ];
});

// Canonical fingerprint pinned on both sides; the Unity EditMode test
// (Mission08Scenario) asserts the identical string for scenario parity.
const EXPECTED_FINGERPRINT =
  'mission-08-eye-of-the-wind|turnLimit=10|upwindLimit=30|downwindLimit=90|swiftTarget=8|wind=180:4|' +
  'enemy-corvette-a:enemy:240,35:h180:v3:hp150:sl85:cw55|' +
  'enemy-corvette-b:enemy:240,-35:h180:v3:hp150:sl85:cw55|' +
  'player-sloop-a:player:0,30:h0:v3:hp120:sl80:cw50|' +
  'player-sloop-b:player:0,-30:h0:v3:hp120:sl80:cw50';

describe('wind turn-rate modifier', () => {
  const singleShipState = (heading: number): SimState => ({
    turn: 1,
    wind: { direction: 180, speed: 4 },
    ships: [
      {
        id: 'ship-a',
        side: 'player',
        position: { x: 0, y: 0 },
        heading,
        speed: 2,
        hp: 100,
        sail: 80,
        crew: 40
      }
    ]
  });

  const helm = (turnDelta: number): SimOrder => ({
    shipId: 'ship-a',
    action: 'maneuver',
    turnDelta,
    speedDelta: 0
  });

  const maneuverDelta = (heading: number, turnDelta: number, modifiers?: object) => {
    const preview = resolveSimPreview({
      schemaVersion: 1,
      seed: 1,
      turn: 1,
      state: singleShipState(heading),
      orders: [helm(turnDelta)],
      ...(modifiers ? { modifiers } : {})
    });
    const maneuver = preview.events.find((event) => event.type === 'maneuver');
    return maneuver && maneuver.type === 'maneuver' ? maneuver.turnDelta : undefined;
  };

  it('clamps upwind turns hardest, beam moderately, and downwind barely', () => {
    // Wind 180: heading 0 is dead upwind, 90 a beam reach, 180 running free.
    expect(maneuverDelta(0, 60, { windTurnRate: true })).toBe(WIND_TURN_UPWIND_LIMIT);
    expect(maneuverDelta(90, 75, { windTurnRate: true })).toBe(WIND_TURN_BEAM_LIMIT);
    expect(maneuverDelta(180, 75, { windTurnRate: true })).toBe(75);
    expect(WIND_TURN_DOWNWIND_LIMIT).toBeGreaterThanOrEqual(75);
  });

  it('keeps flag-off behavior byte-identical whether absent or false', () => {
    const request = (modifiers?: { windTurnRate: boolean }) =>
      resolveSimPreview({
        schemaVersion: 1,
        seed: 1,
        turn: 1,
        state: singleShipState(0),
        orders: [helm(60)],
        ...(modifiers ? { modifiers } : {})
      });
    const absent = request();
    const explicitOff = request({ windTurnRate: false });
    expect(maneuverDelta(0, 60)).toBe(60);
    expect(explicitOff.hash).toBe(absent.hash);
    expect(explicitOff).toEqual(absent);
  });

  it('takes the tighter limit when a slowed ship also fights the wind', () => {
    const slowedPreview = (heading: number) => {
      const state = singleShipState(heading);
      state.ships[0].status = { slowed: true, slowTurnsRemaining: 2 };
      const preview = resolveSimPreview({
        schemaVersion: 1,
        seed: 1,
        turn: 1,
        state,
        orders: [helm(80)],
        modifiers: { statusEffects: true, windTurnRate: true }
      });
      const maneuver = preview.events.find((event) => event.type === 'maneuver');
      return maneuver && maneuver.type === 'maneuver' ? maneuver.turnDelta : undefined;
    };
    // Upwind the wind clamp (30) undercuts the slow clamp (45); running free
    // the slow clamp is the binding one.
    expect(slowedPreview(0)).toBe(WIND_TURN_UPWIND_LIMIT);
    expect(slowedPreview(180)).toBe(45);
  });
});

describe('mission 08 scenario', () => {
  it('pins the scenario fingerprint shared with the Unity client', () => {
    expect(mission08Fingerprint(mission08StartResponse(808))).toBe(EXPECTED_FINGERPRINT);
  });

  it('opens with the player beating dead upwind at full corvette strength', () => {
    const state = createMission08State();
    expect(state.wind).toEqual({ direction: 180, speed: 4 });
    const corvettes = state.ships.filter((ship) => ship.side === 'enemy');
    expect(corvettes.map((ship) => ship.hp)).toEqual([150, 150]);
    expect(corvettes.map((ship) => ship.sail)).toEqual([85, 85]);
  });

  it('is deterministic for the same seed and orders', () => {
    const first = runMission08(9, tackingOrders);
    const second = runMission08(9, tackingOrders);
    expect(second).toEqual(first);
    expect(first.turns.map((turn) => turn.hash)).toEqual(second.turns.map((turn) => turn.hash));
  });

  it('reports a tacking win with clamped-maneuver telemetry for seed 9', () => {
    const outcome = runMission08(9, tackingOrders);
    expect(outcome.result).toBe('win');
    expect(outcome.failReason).toBeNull();
    expect(outcome.turnCount).toBe(9);
    // The 60° weave on turns 2-3 is cut to 30° per ship, and every maneuver
    // happens on an upwind point of sail in the dead-ahead headwind.
    expect(outcome.telemetry).toEqual({
      clampedManeuvers: 4,
      upwindManeuvers: 18,
      downwindManeuvers: 0
    });
    expect(outcome.bonusObjectives).toEqual({ cleanTack: false, swiftVictory: false });
    expect(outcome.damageProfile.enemyRemainingHp).toBe(0);
  });

  it('awards both bonuses to a clean-helm win inside the swift target', () => {
    const outcome = runMission08(46, cleanOrders);
    expect(outcome.result).toBe('win');
    expect(outcome.turnCount).toBe(8);
    expect(outcome.telemetry.clampedManeuvers).toBe(0);
    expect(outcome.bonusObjectives).toEqual({ cleanTack: true, swiftVictory: true });
  });

  it('awards the swift bonus but not cleanTack to a fast tacking win', () => {
    const outcome = runMission08(60, tackingOrders);
    expect(outcome.result).toBe('win');
    expect(outcome.turnCount).toBe(8);
    expect(outcome.telemetry.clampedManeuvers).toBe(4);
    expect(outcome.bonusObjectives).toEqual({ cleanTack: false, swiftVictory: true });
  });

  it('judges clamping against the last duplicate order, matching engine resolution', () => {
    // Two orders for the same sloop in one turn: the engine's orderByShip
    // map executes the last one (60°, clamped upwind to 30°), so telemetry
    // must count the clamp even though the first order (20°) was legal.
    const duplicateOrders: SimOrder[][] = [
      [
        fire(MISSION_08_PLAYER_SHIP_IDS[0], MISSION_08_ENEMY_SHIP_IDS[0], 20),
        fire(MISSION_08_PLAYER_SHIP_IDS[0], MISSION_08_ENEMY_SHIP_IDS[0], 60)
      ]
    ];
    const outcome = runMission08(9, duplicateOrders);
    expect(outcome.telemetry.clampedManeuvers).toBe(1);
  });

  it('denies both bonuses on a timeout loss', () => {
    const outcome = runMission08(3, tackingOrders);
    expect(outcome.result).toBe('loss');
    expect(outcome.failReason).toBe('timeout');
    expect(outcome.bonusObjectives).toEqual({ cleanTack: false, swiftVictory: false });
    expect(outcome.turns).toHaveLength(MISSION_08_TURN_LIMIT);
  });
});

describe('mission 08 routes', () => {
  it('starts deterministically with the scenario payload', async () => {
    const res1 = await app.inject({ method: 'POST', url: `/missions/${MISSION_08_CODE}/start` });
    const res2 = await app.inject({ method: 'POST', url: `/missions/${MISSION_08_CODE}/start` });
    expect(res1.statusCode).toBe(200);
    expect(res1.json()).toEqual(res2.json());

    const body = res1.json();
    expect(body.missionCode).toBe(MISSION_08_CODE);
    expect(body.objectives).toEqual({
      turnLimit: 10,
      upwindTurnLimit: 30,
      downwindTurnLimit: 90,
      swiftTurnTarget: 8
    });
    expect(body.state.ships).toHaveLength(4);
  });

  it('resolves a tacking win with clamped-maneuver telemetry', async () => {
    const res = await app.inject({
      method: 'POST',
      url: `/missions/${MISSION_08_CODE}/resolve`,
      payload: { schemaVersion: 1, seed: 9, turns: tackingOrders }
    });
    expect(res.statusCode).toBe(200);

    const outcome = res.json().outcome;
    expect(outcome.result).toBe('win');
    expect(outcome.telemetry.clampedManeuvers).toBe(4);
    expect(outcome.telemetry.upwindManeuvers).toBeGreaterThan(0);
  });

  it('rejects orders for ships the player does not control', async () => {
    const res = await app.inject({
      method: 'POST',
      url: `/missions/${MISSION_08_CODE}/resolve`,
      payload: {
        schemaVersion: 1,
        seed: 9,
        turns: [[{ shipId: MISSION_08_ENEMY_SHIP_IDS[0], action: 'pass' }]]
      }
    });
    expect(res.statusCode).toBe(400);
    expect(res.json().error).toBe('invalid_order_ship');
  });

  it('rejects upgrade tiers because the mission does not support them', async () => {
    const res = await app.inject({
      method: 'POST',
      url: `/missions/${MISSION_08_CODE}/resolve`,
      payload: {
        schemaVersion: 1,
        seed: 9,
        turns: tackingOrders,
        upgrades: { cannon: 1 }
      }
    });
    expect(res.statusCode).toBe(400);
  });
});
