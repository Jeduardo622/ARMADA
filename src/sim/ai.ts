import { ShipState, SimOrder, SimState, Vector2 } from './types.js';

// Reusable AI profiles (docs/content/ai-profiles.md). Order generation is a
// pure function of (ship, state, params) — no RNG — so behavior stays
// deterministic under the sim seed.
export type AiProfileName = 'line-advance' | 'aggressive' | 'kiting';

export interface AiProfileParams {
  preferredRange: number;
  rakeBias: 'low' | 'medium' | 'high';
  // Absolute hull HP below which the ship repositions (0 = never retreats).
  // Retreat is repositioning, not flight: one turn-away maneuver per turn.
  retreatHullBelow: number;
  focusFire: 'low' | 'medium' | 'high';
}

export const AI_PROFILE_DEFAULTS: Record<AiProfileName, AiProfileParams> = {
  'line-advance': { preferredRange: 100, rakeBias: 'low', retreatHullBelow: 0, focusFire: 'medium' },
  aggressive: { preferredRange: 60, rakeBias: 'high', retreatHullBelow: 0, focusFire: 'high' },
  kiting: { preferredRange: 140, rakeBias: 'medium', retreatHullBelow: 25, focusFire: 'medium' }
};

// Aggressive ships with high rake bias steer for a point astern of the target
// so the approach naturally lines up stern-rake geometry.
const RAKE_AIM_OFFSET = 40;
const MAX_TURN = 90;

function normalizeHeading(degrees: number) {
  const normalized = ((degrees % 360) + 360) % 360;
  return Math.round(normalized);
}

function signedAngle(from: number, to: number) {
  return ((to - from + 540) % 360) - 180;
}

function bearingBetween(from: Vector2, to: Vector2) {
  const radians = Math.atan2(to.y - from.y, to.x - from.x);
  return normalizeHeading((radians * 180) / Math.PI);
}

function distanceBetween(from: Vector2, to: Vector2) {
  const dx = to.x - from.x;
  const dy = to.y - from.y;
  return Math.sqrt(dx * dx + dy * dy);
}

function clampTurn(delta: number) {
  return Math.max(-MAX_TURN, Math.min(MAX_TURN, Math.round(delta)));
}

function turnToward(ship: ShipState, targetBearing: number) {
  return clampTurn(signedAngle(ship.heading, targetBearing));
}

// Port guns face the ship's left (counterclockwise) side in the sim's
// math-coordinate frame; pick whichever battery faces the target.
function broadsideSide(ship: ShipState, target: ShipState): 'port' | 'starboard' {
  const bearing = bearingBetween(ship.position, target.position);
  return signedAngle(ship.heading, bearing) >= 0 ? 'port' : 'starboard';
}

function pickTarget(ship: ShipState, state: SimState, focusFire: AiProfileParams['focusFire']) {
  const hostiles = state.ships
    .filter((candidate) => candidate.side !== ship.side && candidate.hp > 0)
    .sort((a, b) => a.id.localeCompare(b.id));
  if (hostiles.length === 0) {
    return undefined;
  }
  if (focusFire === 'high') {
    return hostiles.reduce((best, candidate) => (candidate.hp < best.hp ? candidate : best));
  }
  if (focusFire === 'medium') {
    return hostiles.reduce((best, candidate) =>
      distanceBetween(ship.position, candidate.position) <
      distanceBetween(ship.position, best.position)
        ? candidate
        : best
    );
  }
  return hostiles[0];
}

function pass(ship: ShipState): SimOrder {
  return { shipId: ship.id, action: 'pass', turnDelta: 0, speedDelta: 0 };
}

function reposition(ship: ShipState, threat: ShipState): SimOrder {
  const awayBearing = normalizeHeading(bearingBetween(threat.position, ship.position));
  return {
    shipId: ship.id,
    action: 'maneuver',
    turnDelta: turnToward(ship, awayBearing),
    speedDelta: 1
  };
}

function lineAdvanceOrder(ship: ShipState, target: ShipState, params: AiProfileParams): SimOrder {
  const range = distanceBetween(ship.position, target.position);
  if (range > params.preferredRange) {
    // Hold the line: advance steadily without turning.
    return { shipId: ship.id, action: 'maneuver', turnDelta: 0, speedDelta: 1 };
  }
  return {
    shipId: ship.id,
    action: 'broadside',
    targetShipId: target.id,
    side: 'port',
    turnDelta: 0,
    speedDelta: 0
  };
}

function aggressiveOrder(ship: ShipState, target: ShipState, params: AiProfileParams): SimOrder {
  const range = distanceBetween(ship.position, target.position);
  if (range > params.preferredRange) {
    let aimPoint = target.position;
    if (params.rakeBias === 'high') {
      const radians = (target.heading * Math.PI) / 180;
      aimPoint = {
        x: target.position.x - Math.round(Math.cos(radians) * RAKE_AIM_OFFSET),
        y: target.position.y - Math.round(Math.sin(radians) * RAKE_AIM_OFFSET)
      };
    }
    return {
      shipId: ship.id,
      action: 'maneuver',
      turnDelta: turnToward(ship, bearingBetween(ship.position, aimPoint)),
      speedDelta: 1
    };
  }
  return {
    shipId: ship.id,
    action: 'broadside',
    targetShipId: target.id,
    side: broadsideSide(ship, target),
    turnDelta: 0,
    speedDelta: 0
  };
}

function kitingOrder(
  ship: ShipState,
  target: ShipState,
  params: AiProfileParams,
  state: SimState
): SimOrder {
  const range = distanceBetween(ship.position, target.position);
  if (range < params.preferredRange) {
    // Disengage away from the target, preferring a downwind escape so the
    // wind curve keeps the standoff open.
    const awayBearing = normalizeHeading(bearingBetween(target.position, ship.position));
    const downwindDiff = Math.abs(signedAngle(awayBearing, state.wind.direction));
    const escapeBearing = downwindDiff <= 90 ? state.wind.direction : awayBearing;
    return {
      shipId: ship.id,
      action: 'maneuver',
      turnDelta: turnToward(ship, escapeBearing),
      speedDelta: 2
    };
  }
  return {
    shipId: ship.id,
    action: 'broadside',
    targetShipId: target.id,
    side: broadsideSide(ship, target),
    turnDelta: 0,
    speedDelta: 0
  };
}

export function aiOrderFor(
  ship: ShipState,
  state: SimState,
  profile: AiProfileName,
  overrides?: Partial<AiProfileParams>
): SimOrder {
  const params = { ...AI_PROFILE_DEFAULTS[profile], ...overrides };
  if (ship.hp <= 0) {
    return pass(ship);
  }

  const target = pickTarget(ship, state, params.focusFire);
  if (!target) {
    return pass(ship);
  }

  if (params.retreatHullBelow > 0 && ship.hp < params.retreatHullBelow) {
    return reposition(ship, target);
  }

  if (profile === 'aggressive') {
    return aggressiveOrder(ship, target, params);
  }
  if (profile === 'kiting') {
    return kitingOrder(ship, target, params, state);
  }
  return lineAdvanceOrder(ship, target, params);
}
