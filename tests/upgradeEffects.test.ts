import { describe, it, expect } from 'vitest';
import { resolveSimPreview } from '../src/sim/engine.js';
import { simPreviewSchema } from '../src/sim/types.js';
import type { ShipState, SimPreviewRequest, SimState } from '../src/sim/types.js';

// Engine ship-upgrade slice (modifiers.shipUpgrades): request-level owned
// tiers scale player-side ships only — cannon → broadside/rake damage,
// sail → speed and slowed turn recovery, hull → hull hp at state build.
// Everything is inert unless the flag is set; the flag-off runs must stay
// byte-identical to the legacy hash chains.
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

const tiers = (patch: Partial<{ cannon: number; sail: number; hull: number }> = {}) => ({
  cannon: 0,
  sail: 0,
  hull: 0,
  ...patch
});

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

const movementEvent = (result: ReturnType<typeof resolveSimPreview>, shipId: string) => {
  const event = result.events.find(
    (candidate) => candidate.type === 'movement' && candidate.shipId === shipId
  );
  if (event?.type !== 'movement') throw new Error(`expected movement event for ${shipId}`);
  return event;
};

describe('engine ship upgrades flag-off inertness', () => {
  it('ignores supplied tiers with the flag absent or false', () => {
    const baseline = resolveSimPreview(duelPreview(7, attackerShip(), targetShip()));
    const flagAbsent = resolveSimPreview(
      duelPreview(7, attackerShip(), targetShip(), {
        upgrades: tiers({ cannon: 3, sail: 3, hull: 3 })
      })
    );
    const flagFalse = resolveSimPreview(
      duelPreview(7, attackerShip(), targetShip(), {
        modifiers: { shipUpgrades: false },
        upgrades: tiers({ cannon: 3, sail: 3, hull: 3 })
      })
    );

    expect(flagAbsent).toEqual(baseline);
    expect(flagAbsent.hash).toBe(baseline.hash);
    expect(flagFalse).toEqual(baseline);
    expect(flagFalse.hash).toBe(baseline.hash);
  });

  it('resolves identically with the flag on but every tier at zero or omitted', () => {
    const baseline = resolveSimPreview(duelPreview(7, attackerShip(), targetShip()));
    const zeroTiers = resolveSimPreview(
      duelPreview(7, attackerShip(), targetShip(), {
        modifiers: { shipUpgrades: true },
        upgrades: tiers()
      })
    );
    const noBlock = resolveSimPreview(
      duelPreview(7, attackerShip(), targetShip(), { modifiers: { shipUpgrades: true } })
    );

    expect(zeroTiers).toEqual(baseline);
    expect(zeroTiers.hash).toBe(baseline.hash);
    expect(noBlock).toEqual(baseline);
    expect(noBlock.hash).toBe(baseline.hash);
  });
});

describe('engine cannon upgrade (modifiers.shipUpgrades)', () => {
  it('scales player broadside damage at tier 3 without changing hit resolution', () => {
    const base = resolveSimPreview(
      duelPreview(7, attackerShip(), targetShip(), {
        modifiers: { shipUpgrades: true },
        upgrades: tiers()
      })
    );
    const upgraded = resolveSimPreview(
      duelPreview(7, attackerShip(), targetShip(), {
        modifiers: { shipUpgrades: true },
        upgrades: tiers({ cannon: 3 })
      })
    );

    const baseEvent = broadsideEvent(base);
    const upgradedEvent = broadsideEvent(upgraded);
    expect(baseEvent.hit).toBe(true);
    expect(upgradedEvent.hit).toBe(true);
    // Cannon tiers scale damage only; the roll and hit chance are untouched.
    expect(upgradedEvent.roll).toBe(baseEvent.roll);
    expect(upgradedEvent.hitChance).toBe(baseEvent.hitChance);
    // Integer scale-then-floor at +10% per tier.
    expect(upgradedEvent.damage.hull).toBe(Math.floor((baseEvent.damage.hull * 130) / 100));
    expect(upgradedEvent.damage.sail).toBeGreaterThan(baseEvent.damage.sail);
    expect(upgradedEvent.damage.crew).toBeGreaterThan(baseEvent.damage.crew);
    expect(upgraded.hash).not.toBe(base.hash);
  });

  it('never scales enemy-side attackers', () => {
    const enemyOrder = {
      shipId: TARGET_ID,
      action: 'broadside' as const,
      targetShipId: ATTACKER_ID,
      side: 'port' as const,
      turnDelta: 0,
      speedDelta: 0
    };
    const baseline = resolveSimPreview(
      duelPreview(7, attackerShip(), targetShip(), { orders: [enemyOrder] })
    );
    const upgraded = resolveSimPreview(
      duelPreview(7, attackerShip(), targetShip(), {
        orders: [enemyOrder],
        modifiers: { shipUpgrades: true },
        upgrades: tiers({ cannon: 3 })
      })
    );

    // Only the enemy acts and enemy ships receive no scaling, so the entire
    // resolution is identical to the flag-off run.
    expect(upgraded).toEqual(baseline);
    expect(upgraded.hash).toBe(baseline.hash);
  });
});

describe('engine sail upgrade (modifiers.shipUpgrades)', () => {
  it('raises effective speed for movement and broadside accuracy at tier 3', () => {
    const clean = resolveSimPreview(
      duelPreview(5, attackerShip(), targetShip(), {
        modifiers: { shipUpgrades: true, windMovement: true },
        upgrades: tiers()
      })
    );
    const upgraded = resolveSimPreview(
      duelPreview(5, attackerShip(), targetShip(), {
        modifiers: { shipUpgrades: true, windMovement: true },
        upgrades: tiers({ sail: 3 })
      })
    );

    // Tailwind effective speed 5 gains +1 per sail tier: 40 units instead of 25.
    expect(movementEvent(clean, ATTACKER_ID).effectiveSpeed).toBe(5);
    expect(shipIn(clean, ATTACKER_ID).position).toEqual({ x: 25, y: 0 });
    expect(movementEvent(upgraded, ATTACKER_ID).effectiveSpeed).toBe(8);
    expect(shipIn(upgraded, ATTACKER_ID).position).toEqual({ x: 40, y: 0 });
    // Attacker speed feeds hit chance: floor(8 / 2) versus floor(5 / 2).
    expect(broadsideEvent(upgraded).hitChance).toBe(broadsideEvent(clean).hitChance + 2);
  });

  it('eases the slowed turn clamp at tier 3', () => {
    const maneuver = { shipId: ATTACKER_ID, action: 'maneuver' as const, turnDelta: 90, speedDelta: 0 };
    const run = (sail: number) =>
      resolveSimPreview(
        duelPreview(5, attackerShip({ status: { slowed: true, slowTurnsRemaining: 2 } }), targetShip(), {
          orders: [maneuver],
          modifiers: { statusEffects: true, shipUpgrades: true },
          upgrades: tiers({ sail })
        })
      );

    // Slow clamps turns to 45; each sail tier recovers 15 degrees.
    expect(shipIn(run(0), ATTACKER_ID).heading).toBe(45);
    expect(shipIn(run(3), ATTACKER_ID).heading).toBe(90);
  });
});

describe('engine hull upgrade (modifiers.shipUpgrades)', () => {
  it('scales player hull hp at state build and leaves enemies untouched', () => {
    const result = resolveSimPreview(
      duelPreview(7, attackerShip(), targetShip(), {
        orders: [],
        modifiers: { shipUpgrades: true },
        upgrades: tiers({ hull: 3 })
      })
    );

    // Integer scale-then-floor at +10% per tier: floor(120 * 1.3).
    expect(shipIn(result, ATTACKER_ID).hp).toBe(156);
    expect(shipIn(result, TARGET_ID).hp).toBe(200);
  });

  it('does not reapply the hull bonus when chaining previews across turns', () => {
    const upgradeRequest = {
      modifiers: { shipUpgrades: true },
      upgrades: tiers({ hull: 3 })
    };
    const first = resolveSimPreview(
      duelPreview(7, attackerShip(), targetShip(), { orders: [], ...upgradeRequest })
    );
    expect(shipIn(first, ATTACKER_ID).hp).toBe(156);

    // Feeding nextState back with the flag and tiers still set must carry the
    // upgraded hp forward unchanged instead of compounding it (156, not 202).
    const second = resolveSimPreview({
      schemaVersion: 1,
      seed: 7,
      turn: 2,
      state: first.nextState,
      orders: [],
      ...upgradeRequest
    });
    expect(shipIn(second, ATTACKER_ID).hp).toBe(156);

    const third = resolveSimPreview({
      schemaVersion: 1,
      seed: 7,
      turn: 3,
      state: second.nextState,
      orders: [],
      ...upgradeRequest
    });
    expect(shipIn(third, ATTACKER_ID).hp).toBe(156);
  });

  it('caps scaled hull hp at the schema maximum', () => {
    const result = resolveSimPreview(
      duelPreview(7, attackerShip({ hp: 1000 }), targetShip(), {
        orders: [],
        modifiers: { shipUpgrades: true },
        upgrades: tiers({ hull: 3 })
      })
    );

    expect(shipIn(result, ATTACKER_ID).hp).toBe(1000);
  });
});

describe('sim preview upgrade tier validation', () => {
  const requestWith = (upgrades: unknown) => ({
    schemaVersion: 1,
    seed: 7,
    turn: 1,
    state: duelState(attackerShip(), targetShip()),
    orders: [],
    modifiers: { shipUpgrades: true },
    upgrades
  });

  it('defaults omitted components to tier zero', () => {
    const parsed = simPreviewSchema.parse(requestWith({ cannon: 1 }));
    expect(parsed.upgrades).toEqual({ cannon: 1, sail: 0, hull: 0 });
  });

  it('rejects tiers outside the owned range', () => {
    expect(simPreviewSchema.safeParse(requestWith({ cannon: 4 })).success).toBe(false);
    expect(simPreviewSchema.safeParse(requestWith({ sail: -1 })).success).toBe(false);
    expect(simPreviewSchema.safeParse(requestWith({ hull: 1.5 })).success).toBe(false);
  });
});

describe('engine ship upgrade determinism', () => {
  it('produces identical results and hashes for identical seed and tiers', () => {
    const run = () =>
      resolveSimPreview(
        duelPreview(4, attackerShip(), targetShip(), {
          modifiers: { shipUpgrades: true, windMovement: true },
          upgrades: tiers({ cannon: 3, sail: 3, hull: 3 })
        })
      );

    const first = run();
    const second = run();
    expect(second).toEqual(first);
    expect(second.hash).toBe(first.hash);
    expect(first.hash).toHaveLength(64);
  });

  it('does not mutate the caller input state when scaling hull hp', () => {
    const request = duelPreview(4, attackerShip(), targetShip(), {
      orders: [],
      modifiers: { shipUpgrades: true },
      upgrades: tiers({ hull: 3 })
    });

    const first = resolveSimPreview(request);
    const second = resolveSimPreview(request);
    expect(request.state.ships[0].hp).toBe(120);
    expect(second.hash).toBe(first.hash);
  });
});
