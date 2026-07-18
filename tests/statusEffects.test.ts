import { describe, it, expect } from 'vitest';
import { resolveSimPreview } from '../src/sim/engine.js';
import type { ShipState, SimPreviewRequest, SimState } from '../src/sim/types.js';

// Engine status-effect slice (modifiers.statusEffects): fire = per-turn hull
// DoT + accuracy penalty, slow = speed and turn-rate penalty. Everything is
// inert unless the flag is set; the mission fixture pins in mission01.test.ts
// guard the flag-off hash chains.
const ATTACKER_ID = 'attacker';
const TARGET_ID = 'target';

const attackerShip = (patch: Partial<ShipState> = {}): ShipState => ({
  id: ATTACKER_ID,
  side: 'player',
  position: { x: 0, y: 0 },
  heading: 0,
  speed: 3,
  hp: 120,
  sail: 80,
  crew: 50,
  ...patch
});

const targetShip = (patch: Partial<ShipState> = {}): ShipState => ({
  id: TARGET_ID,
  side: 'enemy',
  position: { x: 100, y: 0 },
  heading: 90,
  speed: 2,
  hp: 200,
  sail: 70,
  crew: 40,
  ...patch
});

const duelState = (attacker: ShipState, target: ShipState): SimState => ({
  turn: 1,
  wind: { direction: 0, speed: 5 },
  ships: [attacker, target]
});

const broadsideOrder = {
  shipId: ATTACKER_ID,
  action: 'broadside' as const,
  targetShipId: TARGET_ID,
  side: 'starboard' as const,
  turnDelta: 0,
  speedDelta: 0
};

const duelPreview = (
  seed: number,
  attacker: ShipState,
  target: ShipState,
  overrides: Partial<SimPreviewRequest> = {}
): SimPreviewRequest => ({
  schemaVersion: 1,
  seed,
  turn: 1,
  state: duelState(attacker, target),
  orders: [broadsideOrder],
  ...overrides
});

const shipIn = (result: ReturnType<typeof resolveSimPreview>, id: string) => {
  const ship = result.nextState.ships.find((candidate) => candidate.id === id);
  if (!ship) throw new Error(`expected ship ${id}`);
  return ship;
};

const broadsideEvent = (result: ReturnType<typeof resolveSimPreview>) => {
  const event = result.events.find((candidate) => candidate.type === 'broadside');
  if (event?.type !== 'broadside') throw new Error('expected broadside event');
  return event;
};

const statusEvents = (result: ReturnType<typeof resolveSimPreview>) =>
  result.events.filter((event) => event.type === 'status');

describe('engine status effects flag-off inertness', () => {
  it('resolves identically with the flag absent, false, or alongside status state', () => {
    const burning = () =>
      duelPreview(7, attackerShip({ status: { onFire: true, fireTurnsRemaining: 2 } }), targetShip());
    const absent = resolveSimPreview(burning());
    const explicitOff = resolveSimPreview({ ...burning(), modifiers: { statusEffects: false } });

    expect(explicitOff.hash).toBe(absent.hash);
    expect(explicitOff).toEqual(absent);
    // No DoT, no accuracy penalty, no status events without the flag.
    expect(statusEvents(absent)).toHaveLength(0);
    expect(shipIn(absent, ATTACKER_ID).hp).toBe(120);
    expect(shipIn(absent, ATTACKER_ID).status).toEqual({ onFire: true, fireTurnsRemaining: 2 });
    const clean = resolveSimPreview(duelPreview(7, attackerShip(), targetShip()));
    expect(broadsideEvent(absent).hitChance).toBe(broadsideEvent(clean).hitChance);
  });
});

describe('engine fire effect (modifiers.statusEffects)', () => {
  it('burns hull each turn and decrements the remaining-turn counter', () => {
    const result = resolveSimPreview(
      duelPreview(7, attackerShip({ status: { onFire: true, fireTurnsRemaining: 2 } }), targetShip(), {
        orders: [],
        modifiers: { statusEffects: true }
      })
    );

    const attacker = shipIn(result, ATTACKER_ID);
    expect(attacker.hp).toBe(120 - 5);
    expect(attacker.status).toEqual({ onFire: true, fireTurnsRemaining: 1 });
    expect(statusEvents(result)).toEqual([
      { type: 'status', shipId: ATTACKER_ID, status: { onFire: true, fireTurnsRemaining: 1 } }
    ]);
  });

  it('applies the accuracy penalty to burning attackers', () => {
    const burning = resolveSimPreview(
      duelPreview(7, attackerShip({ status: { onFire: true, fireTurnsRemaining: 2 } }), targetShip(), {
        modifiers: { statusEffects: true }
      })
    );
    const healthy = resolveSimPreview(
      duelPreview(7, attackerShip(), targetShip(), { modifiers: { statusEffects: true } })
    );

    expect(broadsideEvent(burning).hitChance).toBe(broadsideEvent(healthy).hitChance - 15);
  });

  it('can ignite the target on a landed broadside hull hit', () => {
    const ignited = resolveSimPreview(
      duelPreview(4, attackerShip(), targetShip(), { modifiers: { statusEffects: true } })
    );
    expect(broadsideEvent(ignited).hit).toBe(true);
    expect(shipIn(ignited, TARGET_ID).status).toEqual({ onFire: true, fireTurnsRemaining: 3 });
    expect(statusEvents(ignited)).toEqual([
      { type: 'status', shipId: TARGET_ID, status: { onFire: true, fireTurnsRemaining: 3 } }
    ]);

    const spared = resolveSimPreview(
      duelPreview(7, attackerShip(), targetShip(), { modifiers: { statusEffects: true } })
    );
    expect(broadsideEvent(spared).hit).toBe(true);
    expect(shipIn(spared, TARGET_ID).status).toEqual({});
    expect(statusEvents(spared)).toHaveLength(0);
  });

  it('refreshes the burn duration instead of stacking on re-ignition', () => {
    const result = resolveSimPreview(
      duelPreview(4, attackerShip(), targetShip({ status: { onFire: true, fireTurnsRemaining: 1 } }), {
        modifiers: { statusEffects: true }
      })
    );

    const target = shipIn(result, TARGET_ID);
    // Exactly one DoT tick this turn, then the landed hit refreshes the timer.
    expect(target.status).toEqual({ onFire: true, fireTurnsRemaining: 3 });
    const broadside = broadsideEvent(result);
    expect(target.hp).toBe(200 - 5 - broadside.damage.hull);
  });
});

describe('engine slow effect (modifiers.statusEffects)', () => {
  it('slows the target when sail falls below half its turn-start value', () => {
    const result = resolveSimPreview(
      duelPreview(7, attackerShip(), targetShip({ sail: 20 }), { modifiers: { statusEffects: true } })
    );

    const broadside = broadsideEvent(result);
    expect(broadside.hit).toBe(true);
    expect(broadside.targetRemaining.sail).toBeLessThan(10);
    expect(shipIn(result, TARGET_ID).status).toEqual({ slowed: true, slowTurnsRemaining: 2 });
    expect(statusEvents(result)).toEqual([
      { type: 'status', shipId: TARGET_ID, status: { slowed: true, slowTurnsRemaining: 2 } }
    ]);
  });

  it('clamps the turn rate of slowed ships', () => {
    const maneuver = { shipId: ATTACKER_ID, action: 'maneuver' as const, turnDelta: 90, speedDelta: 0 };
    const slowed = resolveSimPreview(
      duelPreview(5, attackerShip({ status: { slowed: true, slowTurnsRemaining: 2 } }), targetShip(), {
        orders: [maneuver],
        modifiers: { statusEffects: true }
      })
    );

    const event = slowed.events.find((candidate) => candidate.type === 'maneuver');
    if (event?.type !== 'maneuver') throw new Error('expected maneuver event');
    expect(event.turnDelta).toBe(45);
    expect(shipIn(slowed, ATTACKER_ID).heading).toBe(45);
  });

  it('reduces movement and broadside speed while slowed', () => {
    const slowedRun = resolveSimPreview(
      duelPreview(5, attackerShip({ status: { slowed: true, slowTurnsRemaining: 2 } }), targetShip(), {
        modifiers: { statusEffects: true, windMovement: true }
      })
    );
    const cleanRun = resolveSimPreview(
      duelPreview(5, attackerShip(), targetShip(), {
        modifiers: { statusEffects: true, windMovement: true }
      })
    );

    // Tailwind effective speed 5 drops to 3: 15 units instead of 25.
    expect(shipIn(cleanRun, ATTACKER_ID).position).toEqual({ x: 25, y: 0 });
    expect(shipIn(slowedRun, ATTACKER_ID).position).toEqual({ x: 15, y: 0 });
    // Attacker speed feeds hit chance: floor(5 / 2) becomes floor(3 / 2).
    expect(broadsideEvent(slowedRun).hitChance).toBe(broadsideEvent(cleanRun).hitChance - 1);
  });
});

describe('engine status effects on sunk ships', () => {
  it('sinks a burning ship at the tick and skips its orders', () => {
    const result = resolveSimPreview(
      duelPreview(7, attackerShip({ hp: 5, status: { onFire: true, fireTurnsRemaining: 1 } }), targetShip(), {
        modifiers: { statusEffects: true }
      })
    );

    expect(shipIn(result, ATTACKER_ID).hp).toBe(0);
    expect(result.summary.sunk).toContain(ATTACKER_ID);
    // The sunk ship's broadside order produces neither a maneuver nor an attack.
    expect(result.events.filter((event) => event.type === 'maneuver')).toHaveLength(0);
    expect(result.events.filter((event) => event.type === 'broadside')).toHaveLength(0);
  });

  it('does not refresh fire or slow on a target sunk by the broadside', () => {
    // Seed 4 ignites a surviving target (see the ignition test); a killing
    // hit must leave the sunk target's status untouched instead.
    const result = resolveSimPreview(
      duelPreview(4, attackerShip(), targetShip({ hp: 10, sail: 20 }), {
        modifiers: { statusEffects: true }
      })
    );

    const broadside = broadsideEvent(result);
    expect(broadside.hit).toBe(true);
    expect(broadside.targetRemaining.hp).toBe(0);
    expect(result.summary.sunk).toContain(TARGET_ID);
    expect(shipIn(result, TARGET_ID).status).toEqual({});
    expect(statusEvents(result)).toHaveLength(0);
  });
});

describe('engine status effect durations', () => {
  it('expires effects after their remaining turns and emits the cleared status', () => {
    const first = resolveSimPreview(
      duelPreview(
        9,
        attackerShip({ status: { onFire: true, fireTurnsRemaining: 1, slowed: true, slowTurnsRemaining: 1 } }),
        targetShip(),
        { orders: [], modifiers: { statusEffects: true } }
      )
    );
    const afterFirst = shipIn(first, ATTACKER_ID);
    expect(afterFirst.hp).toBe(120 - 5);
    expect(afterFirst.status).toEqual({
      onFire: true,
      fireTurnsRemaining: 0,
      slowed: true,
      slowTurnsRemaining: 0
    });

    const second = resolveSimPreview({
      schemaVersion: 1,
      seed: 9,
      turn: 2,
      state: first.nextState,
      orders: [],
      modifiers: { statusEffects: true }
    });
    const afterSecond = shipIn(second, ATTACKER_ID);
    // Expiry turn: flags clear before any phase runs, and no further DoT lands.
    expect(afterSecond.hp).toBe(120 - 5);
    expect(afterSecond.status).toEqual({});
    expect(statusEvents(second)).toEqual([{ type: 'status', shipId: ATTACKER_ID, status: {} }]);

    const third = resolveSimPreview({
      schemaVersion: 1,
      seed: 9,
      turn: 3,
      state: second.nextState,
      orders: [],
      modifiers: { statusEffects: true }
    });
    expect(statusEvents(third)).toHaveLength(0);
    expect(shipIn(third, ATTACKER_ID).hp).toBe(120 - 5);
  });
});

describe('engine status effect determinism', () => {
  it('produces identical results and hashes for identical seed and orders', () => {
    const run = () =>
      resolveSimPreview(
        duelPreview(4, attackerShip({ status: { slowed: true, slowTurnsRemaining: 2 } }), targetShip({ sail: 20 }), {
          modifiers: { statusEffects: true, windMovement: true }
        })
      );

    const first = run();
    const second = run();
    expect(second).toEqual(first);
    expect(second.hash).toBe(first.hash);
    expect(first.hash).toHaveLength(64);
  });

  it('does not mutate the caller input state across repeated resolutions', () => {
    const request = duelPreview(
      9,
      attackerShip({ status: { onFire: true, fireTurnsRemaining: 2 } }),
      targetShip(),
      { orders: [], modifiers: { statusEffects: true } }
    );

    const first = resolveSimPreview(request);
    const second = resolveSimPreview(request);
    expect(request.state.ships[0].status).toEqual({ onFire: true, fireTurnsRemaining: 2 });
    expect(second.hash).toBe(first.hash);
  });
});
