import { describe, it, expect, beforeAll, afterAll } from 'vitest';
import { buildServer } from '../src/app.js';
import { resolveSimPreview } from '../src/sim/engine.js';
import {
  PVP_DEFAULT_SEED,
  PVP_SCENARIO_CODE,
  PVP_SIDE_A_SHIP_IDS,
  PVP_SIDE_B_SHIP_IDS,
  PVP_TURN_LIMIT,
  createPvpModifiers,
  createPvpSkirmishState,
  pvpFingerprint,
  pvpResultForTurn,
  validateSideOrders
} from '../src/sim/pvpScenario.js';
import type { SimOrder, SimPreviewResult, SimState, SimSummary } from '../src/sim/types.js';

const app = buildServer({ testing: true });

beforeAll(async () => {
  await app.ready();
});

afterAll(async () => {
  await app.close();
});

// Canonical fingerprint pinned on both sides; the Unity EditMode test
// (PvpScenario) asserts the identical string for scenario parity.
const EXPECTED_FINGERPRINT =
  'pvp-skirmish-2v2|turnLimit=20|modifiers=chainShot|wind=90:0|' +
  'alpha-frigate-a:player:0,30:h0:v3:hp120:sl80:cw50|' +
  'alpha-frigate-b:player:0,-30:h0:v3:hp120:sl80:cw50|' +
  'bravo-frigate-a:enemy:220,30:h180:v3:hp120:sl80:cw50|' +
  'bravo-frigate-b:enemy:220,-30:h180:v3:hp120:sl80:cw50';

const fire = (shipId: string, target: string, ammo?: 'round' | 'chain'): SimOrder => ({
  shipId,
  action: 'broadside',
  targetShipId: target,
  side: 'starboard',
  turnDelta: 0,
  speedDelta: 0,
  ...(ammo ? { ammo } : {})
});

describe('pvp skirmish scenario', () => {
  it('pins the canonical scenario fingerprint', () => {
    expect(pvpFingerprint()).toBe(EXPECTED_FINGERPRINT);
  });

  it('is a symmetric mirror: identical stats, positions mirrored across the midline', () => {
    const state = createPvpSkirmishState();
    const byId = new Map(state.ships.map((ship) => [ship.id, ship]));
    const midlineX = 110;

    for (const [aId, bId] of [
      [PVP_SIDE_A_SHIP_IDS[0], PVP_SIDE_B_SHIP_IDS[0]],
      [PVP_SIDE_A_SHIP_IDS[1], PVP_SIDE_B_SHIP_IDS[1]]
    ]) {
      const a = byId.get(aId)!;
      const b = byId.get(bId)!;
      expect(a.side).toBe('player');
      expect(b.side).toBe('enemy');
      // Same hull, rig, crew, and canvas.
      expect({ hp: a.hp, sail: a.sail, crew: a.crew, speed: a.speed }).toEqual({
        hp: b.hp,
        sail: b.sail,
        crew: b.crew,
        speed: b.speed
      });
      // Mirrored across x = midline, same y, facing each other.
      expect(a.position.x + b.position.x).toBe(midlineX * 2);
      expect(a.position.y).toBe(b.position.y);
      expect(a.heading).toBe(0);
      expect(b.heading).toBe(180);
    }
  });

  it('pins the v1 modifier set to chain shot only', () => {
    expect(createPvpModifiers()).toEqual({ chainShot: true });
    // Fresh object per call so a caller mutation cannot leak into the pin.
    const mutated = createPvpModifiers();
    mutated.windMovement = true;
    expect(createPvpModifiers()).toEqual({ chainShot: true });
  });
});

describe('both-sides order resolution (hot-seat contract)', () => {
  const bothSidesOrders = (): SimOrder[] => [
    { shipId: PVP_SIDE_A_SHIP_IDS[0], action: 'maneuver', turnDelta: 15, speedDelta: 1 },
    fire(PVP_SIDE_A_SHIP_IDS[1], PVP_SIDE_B_SHIP_IDS[0]),
    { shipId: PVP_SIDE_B_SHIP_IDS[0], action: 'maneuver', turnDelta: -30, speedDelta: -1 },
    fire(PVP_SIDE_B_SHIP_IDS[1], PVP_SIDE_A_SHIP_IDS[0], 'chain')
  ];

  it('applies enemy-side maneuvers and resolves enemy-side broadsides (legacy engine behavior pinned)', () => {
    const result = resolveSimPreview({
      schemaVersion: 1,
      seed: PVP_DEFAULT_SEED,
      turn: 1,
      state: createPvpSkirmishState(),
      orders: bothSidesOrders(),
      modifiers: createPvpModifiers()
    });

    // Side B's maneuver was honored: heading 180 - 30 = 150, speed 3 - 1 = 2.
    const bravoA = result.nextState.ships.find((ship) => ship.id === PVP_SIDE_B_SHIP_IDS[0])!;
    expect(bravoA.heading).toBe(150);
    expect(bravoA.speed).toBe(2);

    // Side B's chain-shot broadside was resolved against a side-A target.
    const bravoBroadside = result.events.find(
      (event) => event.type === 'broadside' && event.shipId === PVP_SIDE_B_SHIP_IDS[1]
    );
    expect(bravoBroadside).toBeDefined();
    expect(bravoBroadside).toMatchObject({ targetShipId: PVP_SIDE_A_SHIP_IDS[0] });

    // Every ordered ship produced a maneuver event (unified order surface).
    const maneuvered = result.events
      .filter((event) => event.type === 'maneuver')
      .map((event) => event.shipId)
      .sort();
    expect(maneuvered).toEqual(
      [...PVP_SIDE_A_SHIP_IDS, ...PVP_SIDE_B_SHIP_IDS].sort()
    );
  });

  it('resolves deterministically: identical input yields an identical hash', () => {
    const request = () => ({
      schemaVersion: 1 as const,
      seed: PVP_DEFAULT_SEED,
      turn: 1,
      state: createPvpSkirmishState(),
      orders: bothSidesOrders(),
      modifiers: createPvpModifiers()
    });
    expect(resolveSimPreview(request()).hash).toBe(resolveSimPreview(request()).hash);
  });

  it('accepts a full both-sides PvP turn through POST /sim/preview', async () => {
    const payload = {
      schemaVersion: 1,
      seed: PVP_DEFAULT_SEED,
      turn: 1,
      state: createPvpSkirmishState(),
      orders: bothSidesOrders(),
      modifiers: createPvpModifiers()
    };

    const res = await app.inject({ method: 'POST', url: '/sim/preview', payload });
    expect(res.statusCode).toBe(200);
    const result = res.json().result;
    expect(result.summary.playerRemaining).toBe(2);
    expect(result.summary.enemyRemaining).toBe(2);
    expect(result.nextState.turn).toBe(2);
  });
});

describe('pvp match loop (client-style state chaining)', () => {
  // Mirrors the hot-seat client loop: chain nextState back through
  // /sim/preview-style resolution until the match ends.
  const runMatch = (
    seed: number,
    ordersForTurn: (turn: number, state: SimState) => SimOrder[]
  ) => {
    let state = createPvpSkirmishState();
    const summaries: SimSummary[] = [];
    let result: ReturnType<typeof pvpResultForTurn> = 'ongoing';
    let turn = 1;
    for (; turn <= PVP_TURN_LIMIT; turn++) {
      const preview: SimPreviewResult = resolveSimPreview({
        schemaVersion: 1,
        seed,
        turn,
        state: { ...state, turn },
        orders: ordersForTurn(turn, state),
        modifiers: createPvpModifiers()
      });
      state = preview.nextState;
      summaries.push(preview.summary);
      result = pvpResultForTurn(preview.summary, turn);
      if (result !== 'ongoing') {
        break;
      }
    }
    return { result, turnCount: Math.min(turn, PVP_TURN_LIMIT), summaries, finalState: state };
  };

  const firstAfloat = (state: SimState, ids: readonly string[]) =>
    ids.find((id) => (state.ships.find((ship) => ship.id === id)?.hp ?? 0) > 0) ?? ids[0];

  it('a focus-fire side A beats a split-fire side B inside the turn limit (pinned fixture)', () => {
    const run = runMatch(PVP_DEFAULT_SEED, (_turn, state) => [
      // Side A concentrates on the first bravo ship still afloat.
      fire(PVP_SIDE_A_SHIP_IDS[0], firstAfloat(state, PVP_SIDE_B_SHIP_IDS)),
      fire(PVP_SIDE_A_SHIP_IDS[1], firstAfloat(state, PVP_SIDE_B_SHIP_IDS)),
      // Side B splits fire.
      fire(PVP_SIDE_B_SHIP_IDS[0], PVP_SIDE_A_SHIP_IDS[0]),
      fire(PVP_SIDE_B_SHIP_IDS[1], PVP_SIDE_A_SHIP_IDS[1])
    ]);
    expect(run.result).toBe('side_a');
    expect(run.turnCount).toBeLessThanOrEqual(PVP_TURN_LIMIT);
  });

  it('classifies results: mutual annihilation and timeout are draws', () => {
    expect(pvpResultForTurn({ playerRemaining: 0, enemyRemaining: 0, sunk: [] }, 3)).toBe('draw');
    expect(pvpResultForTurn({ playerRemaining: 2, enemyRemaining: 0, sunk: [] }, 3)).toBe('side_a');
    expect(pvpResultForTurn({ playerRemaining: 0, enemyRemaining: 1, sunk: [] }, 3)).toBe('side_b');
    expect(pvpResultForTurn({ playerRemaining: 1, enemyRemaining: 1, sunk: [] }, 3)).toBe('ongoing');
    expect(
      pvpResultForTurn({ playerRemaining: 1, enemyRemaining: 1, sunk: [] }, PVP_TURN_LIMIT)
    ).toBe('draw');
  });

  it('mirrored orders leave the scenario code stable (sanity)', () => {
    expect(PVP_SCENARIO_CODE).toBe('pvp-skirmish-2v2');
  });
});

describe('side-order fairness guard (lifted by the slice-2 server routes)', () => {
  const state = () => createPvpSkirmishState();

  it('accepts a side ordering its own living ships at opposing targets', () => {
    expect(
      validateSideOrders(
        [
          fire(PVP_SIDE_A_SHIP_IDS[0], PVP_SIDE_B_SHIP_IDS[0]),
          { shipId: PVP_SIDE_A_SHIP_IDS[1], action: 'maneuver', turnDelta: 15, speedDelta: 0 }
        ],
        state(),
        'player'
      )
    ).toBeNull();
  });

  it('rejects ordering the opposing side, unknown, or sunk ships', () => {
    expect(
      validateSideOrders([fire(PVP_SIDE_B_SHIP_IDS[0], PVP_SIDE_A_SHIP_IDS[0])], state(), 'player')
    ).toBe('order_side_mismatch');
    expect(
      validateSideOrders(
        [{ shipId: 'ghost-ship', action: 'pass', turnDelta: 0, speedDelta: 0 }],
        state(),
        'player'
      )
    ).toBe('order_side_mismatch');

    const sunkOwn = state();
    sunkOwn.ships[0].hp = 0;
    expect(
      validateSideOrders([fire(PVP_SIDE_A_SHIP_IDS[0], PVP_SIDE_B_SHIP_IDS[0])], sunkOwn, 'player')
    ).toBe('order_side_mismatch');
  });

  it('rejects friendly-fire, unknown, and sunk targets', () => {
    expect(
      validateSideOrders([fire(PVP_SIDE_A_SHIP_IDS[0], PVP_SIDE_A_SHIP_IDS[1])], state(), 'player')
    ).toBe('target_side_mismatch');
    expect(
      validateSideOrders([fire(PVP_SIDE_A_SHIP_IDS[0], 'ghost-ship')], state(), 'player')
    ).toBe('target_side_mismatch');

    const sunkTarget = state();
    sunkTarget.ships[2].hp = 0;
    expect(
      validateSideOrders(
        [fire(PVP_SIDE_A_SHIP_IDS[0], PVP_SIDE_B_SHIP_IDS[0])],
        sunkTarget,
        'player'
      )
    ).toBe('target_side_mismatch');

    // The same rules hold from side B's perspective.
    expect(
      validateSideOrders([fire(PVP_SIDE_B_SHIP_IDS[0], PVP_SIDE_A_SHIP_IDS[0])], state(), 'enemy')
    ).toBeNull();
    expect(
      validateSideOrders([fire(PVP_SIDE_B_SHIP_IDS[0], PVP_SIDE_B_SHIP_IDS[1])], state(), 'enemy')
    ).toBe('target_side_mismatch');
  });
});
