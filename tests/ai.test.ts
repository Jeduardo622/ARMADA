import { describe, it, expect } from 'vitest';
import { aiOrderFor, bossOrderFor, bossPhaseIndex, escortOrderFor } from '../src/sim/ai.js';
import type { BossParams } from '../src/sim/ai.js';
import type { ShipState, SimState, Wind } from '../src/sim/types.js';

function ship(overrides: Partial<ShipState> & Pick<ShipState, 'id' | 'side'>): ShipState {
  return {
    position: { x: 0, y: 0 },
    heading: 0,
    speed: 3,
    hp: 100,
    sail: 80,
    crew: 40,
    ...overrides
  };
}

function board(ships: ShipState[], wind: Wind = { direction: 0, speed: 5 }): SimState {
  return { turn: 1, wind, ships };
}

describe('boss template', () => {
  const params: BossParams = {
    baseHull: 200,
    phases: [
      { hullAbove: 0.6, profile: 'line-advance' },
      { hullAbove: 0, profile: 'aggressive', overrides: { rakeBias: 'high' } }
    ]
  };

  it('selects the phase from hull fraction', () => {
    expect(bossPhaseIndex(200, params)).toBe(0);
    expect(bossPhaseIndex(121, params)).toBe(0);
    expect(bossPhaseIndex(120, params)).toBe(1);
    expect(bossPhaseIndex(1, params)).toBe(1);
    expect(bossPhaseIndex(0, params)).toBe(1);
  });

  it('follows the phase-1 profile while healthy', () => {
    const boss = ship({ id: 'b', side: 'enemy', position: { x: 150, y: 0 }, heading: 180, hp: 200 });
    const player = ship({ id: 'p1', side: 'player' });
    const state = board([player, boss]);
    expect(bossOrderFor(boss, state, params)).toEqual(
      aiOrderFor(boss, state, 'line-advance')
    );
  });

  it('switches to the phase-2 profile once wounded', () => {
    const boss = ship({ id: 'b', side: 'enemy', position: { x: 150, y: 0 }, heading: 180, hp: 100 });
    const player = ship({ id: 'p1', side: 'player' });
    const state = board([player, boss]);
    expect(bossOrderFor(boss, state, params)).toEqual(
      aiOrderFor(boss, state, 'aggressive', { rakeBias: 'high' })
    );
  });

  it('passes when sunk', () => {
    const boss = ship({ id: 'b', side: 'enemy', hp: 0 });
    const player = ship({ id: 'p1', side: 'player' });
    expect(bossOrderFor(boss, board([player, boss]), params).action).toBe('pass');
  });
});

describe('line-advance profile', () => {
  it('advances without turning while outside preferred range', () => {
    const enemy = ship({ id: 'e1', side: 'enemy', position: { x: 150, y: 0 }, heading: 180 });
    const player = ship({ id: 'p1', side: 'player' });
    const order = aiOrderFor(enemy, board([player, enemy]), 'line-advance');
    expect(order).toEqual({ shipId: 'e1', action: 'maneuver', turnDelta: 0, speedDelta: 1 });
  });

  it('holds the line and fires the port battery once in range', () => {
    const enemy = ship({ id: 'e1', side: 'enemy', position: { x: 80, y: 0 }, heading: 180 });
    const player = ship({ id: 'p1', side: 'player' });
    const order = aiOrderFor(enemy, board([player, enemy]), 'line-advance');
    expect(order).toEqual({
      shipId: 'e1',
      action: 'broadside',
      targetShipId: 'p1',
      side: 'port',
      turnDelta: 0,
      speedDelta: 0
    });
  });

  it('is deterministic for identical input', () => {
    const enemy = ship({ id: 'e1', side: 'enemy', position: { x: 150, y: 0 }, heading: 180 });
    const player = ship({ id: 'p1', side: 'player' });
    expect(aiOrderFor(enemy, board([player, enemy]), 'line-advance')).toEqual(
      aiOrderFor(enemy, board([player, enemy]), 'line-advance')
    );
  });

  it('passes when dead or without living hostiles', () => {
    const enemy = ship({ id: 'e1', side: 'enemy' });
    const deadPlayer = ship({ id: 'p1', side: 'player', hp: 0 });
    expect(aiOrderFor(enemy, board([deadPlayer, enemy]), 'line-advance').action).toBe('pass');
    const deadEnemy = ship({ id: 'e1', side: 'enemy', hp: 0 });
    const player = ship({ id: 'p1', side: 'player' });
    expect(aiOrderFor(deadEnemy, board([player, deadEnemy]), 'line-advance').action).toBe('pass');
  });
});

describe('aggressive profile', () => {
  it('closes toward a point astern of the target when rake bias is high', () => {
    const enemy = ship({ id: 'e1', side: 'enemy', heading: 0 });
    // Target beam-on at (100, 0) heading north: astern lies at (100, -40).
    const player = ship({ id: 'p1', side: 'player', position: { x: 100, y: 0 }, heading: 90 });
    const order = aiOrderFor(enemy, board([player, enemy]), 'aggressive');
    expect(order.action).toBe('maneuver');
    expect(order.turnDelta).toBe(-22);
    expect(order.speedDelta).toBe(1);
  });

  it('aims straight at the target when rake bias is lowered', () => {
    const enemy = ship({ id: 'e1', side: 'enemy', heading: 0 });
    const player = ship({ id: 'p1', side: 'player', position: { x: 100, y: 0 }, heading: 90 });
    const order = aiOrderFor(enemy, board([player, enemy]), 'aggressive', { rakeBias: 'low' });
    expect(order).toMatchObject({ action: 'maneuver', turnDelta: 0, speedDelta: 1 });
  });

  it('fires the battery facing the target once inside preferred range', () => {
    const enemy = ship({ id: 'e1', side: 'enemy', heading: 0 });
    const above = ship({ id: 'p1', side: 'player', position: { x: 0, y: 40 } });
    expect(aiOrderFor(enemy, board([above, enemy]), 'aggressive')).toMatchObject({
      action: 'broadside',
      side: 'port'
    });
    const below = ship({ id: 'p1', side: 'player', position: { x: 0, y: -40 } });
    expect(aiOrderFor(enemy, board([below, enemy]), 'aggressive')).toMatchObject({
      action: 'broadside',
      side: 'starboard'
    });
  });

  it('focuses fire on the weakest hostile', () => {
    const enemy = ship({ id: 'e1', side: 'enemy' });
    const strong = ship({ id: 'p1', side: 'player', position: { x: 40, y: 0 }, hp: 80 });
    const weak = ship({ id: 'p2', side: 'player', position: { x: 50, y: 0 }, hp: 20 });
    const order = aiOrderFor(enemy, board([strong, weak, enemy]), 'aggressive');
    expect(order).toMatchObject({ action: 'broadside', targetShipId: 'p2' });
  });
});

describe('flank-assist escort', () => {
  // Leader heading west at (200,0); lateral +60 puts the station at (220,-60).
  const leader = () => ship({ id: 'flag', side: 'enemy', position: { x: 200, y: 0 }, heading: 180 });

  it('steers back toward station when out of position', () => {
    const escort = ship({ id: 'esc', side: 'enemy', position: { x: 300, y: 100 }, heading: 180 });
    const player = ship({ id: 'p1', side: 'player', position: { x: 0, y: 0 } });
    const order = escortOrderFor(escort, board([player, leader(), escort]), 'flag');
    expect(order.action).toBe('maneuver');
    expect(order.speedDelta).toBe(1);
  });

  it('holds formation on station when no threat is in range', () => {
    const escort = ship({ id: 'esc', side: 'enemy', position: { x: 220, y: -60 }, heading: 180 });
    const player = ship({ id: 'p1', side: 'player', position: { x: 0, y: 0 } });
    const order = escortOrderFor(escort, board([player, leader(), escort]), 'flag');
    expect(order).toEqual({ shipId: 'esc', action: 'maneuver', turnDelta: 0, speedDelta: 0 });
  });

  it('engages the hostile nearest the leader once in range', () => {
    const escort = ship({ id: 'esc', side: 'enemy', position: { x: 220, y: -60 }, heading: 180 });
    const near = ship({ id: 'p1', side: 'player', position: { x: 180, y: -20 } });
    const far = ship({ id: 'p2', side: 'player', position: { x: 0, y: 0 }, hp: 10 });
    const order = escortOrderFor(escort, board([near, far, leader(), escort]), 'flag');
    expect(order).toMatchObject({ action: 'broadside', targetShipId: 'p1', side: 'starboard' });
  });

  it('falls back to the aggressive profile when the leader is lost', () => {
    const escort = ship({ id: 'esc', side: 'enemy', position: { x: 220, y: -60 }, heading: 180 });
    const deadLeader = ship({ id: 'flag', side: 'enemy', position: { x: 200, y: 0 }, hp: 0 });
    const player = ship({ id: 'p1', side: 'player', position: { x: 0, y: 0 } });
    const order = escortOrderFor(escort, board([player, deadLeader, escort]), 'flag');
    expect(order.action).toBe('maneuver');
    expect(order.speedDelta).toBe(1);
    expect(escortOrderFor(escort, board([player, deadLeader, escort]), 'flag')).toEqual(
      aiOrderFor(escort, board([player, deadLeader, escort]), 'aggressive')
    );
  });

  it('passes when the escort itself is sunk', () => {
    const escort = ship({ id: 'esc', side: 'enemy', hp: 0 });
    const player = ship({ id: 'p1', side: 'player' });
    expect(escortOrderFor(escort, board([player, leader(), escort]), 'flag').action).toBe('pass');
  });
});

describe('obstacle avoidance', () => {
  it('deflects an approach heading that would run into an obstacle', () => {
    const enemy = ship({ id: 'e1', side: 'enemy', heading: 0 });
    const player = ship({ id: 'p1', side: 'player', position: { x: 200, y: 0 }, heading: 0 });
    const open = board([player, enemy]);
    const blocked = {
      ...board([player, enemy]),
      obstacles: [{ position: { x: 50, y: 5 }, radius: 20 }]
    };

    const direct = aiOrderFor(enemy, open, 'aggressive', { rakeBias: 'low' });
    const deflected = aiOrderFor(enemy, blocked, 'aggressive', { rakeBias: 'low' });
    expect(direct).toMatchObject({ action: 'maneuver', turnDelta: 0 });
    expect(deflected).toMatchObject({ action: 'maneuver', turnDelta: -30 });
  });
});

describe('kiting profile', () => {
  it('disengages downwind when the target closes inside standoff', () => {
    const kiter = ship({ id: 'e1', side: 'enemy', heading: 90 });
    const player = ship({ id: 'p1', side: 'player', position: { x: 50, y: 0 } });
    // Escape bearing away from the target is 180; wind at 135 is within a
    // quarter turn of that, so the kiter runs with the wind instead.
    const order = aiOrderFor(kiter, board([player, kiter], { direction: 135, speed: 5 }), 'kiting');
    expect(order).toEqual({ shipId: 'e1', action: 'maneuver', turnDelta: 45, speedDelta: 2 });
  });

  it('disengages directly away when downwind would run into the threat', () => {
    const kiter = ship({ id: 'e1', side: 'enemy', heading: 90 });
    const player = ship({ id: 'p1', side: 'player', position: { x: 50, y: 0 } });
    const order = aiOrderFor(kiter, board([player, kiter], { direction: 350, speed: 5 }), 'kiting');
    expect(order).toEqual({ shipId: 'e1', action: 'maneuver', turnDelta: 90, speedDelta: 2 });
  });

  it('fires from standoff range', () => {
    const kiter = ship({ id: 'e1', side: 'enemy', heading: 90 });
    const player = ship({ id: 'p1', side: 'player', position: { x: 150, y: 0 } });
    const order = aiOrderFor(kiter, board([player, kiter]), 'kiting');
    expect(order).toMatchObject({ action: 'broadside', targetShipId: 'p1' });
  });

  it('repositions instead of firing once hull drops below the retreat threshold', () => {
    const kiter = ship({ id: 'e1', side: 'enemy', heading: 90, hp: 20 });
    const player = ship({ id: 'p1', side: 'player', position: { x: 150, y: 0 } });
    const order = aiOrderFor(kiter, board([player, kiter]), 'kiting');
    expect(order).toMatchObject({ action: 'maneuver', speedDelta: 1 });
  });

  it('targets the nearest hostile with medium focus fire', () => {
    const kiter = ship({ id: 'e1', side: 'enemy', heading: 90 });
    const near = ship({ id: 'p2', side: 'player', position: { x: 150, y: 0 }, hp: 100 });
    const far = ship({ id: 'p1', side: 'player', position: { x: 200, y: 0 }, hp: 10 });
    const order = aiOrderFor(kiter, board([near, far, kiter]), 'kiting');
    expect(order).toMatchObject({ action: 'broadside', targetShipId: 'p2' });
  });
});
