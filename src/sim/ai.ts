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

// Obstacle avoidance is a deterministic deflection, not pathfinding: when the
// desired heading would reach an obstacle within one move, bear away by a
// fixed angle on the side pointing away from the obstacle's center.
const OBSTACLE_PROBE = 50;
const OBSTACLE_DEFLECTION = 30;

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

function avoidObstacles(ship: ShipState, desiredBearing: number, state: SimState) {
  for (const obstacle of state.obstacles ?? []) {
    const radians = (desiredBearing * Math.PI) / 180;
    const probe = {
      x: ship.position.x + Math.round(Math.cos(radians) * OBSTACLE_PROBE),
      y: ship.position.y + Math.round(Math.sin(radians) * OBSTACLE_PROBE)
    };
    if (distanceBetween(probe, obstacle.position) < obstacle.radius) {
      const offset = signedAngle(desiredBearing, bearingBetween(ship.position, obstacle.position));
      const away = offset >= 0 ? -OBSTACLE_DEFLECTION : OBSTACLE_DEFLECTION;
      return normalizeHeading(desiredBearing + away);
    }
  }
  return desiredBearing;
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

function reposition(ship: ShipState, threat: ShipState, state: SimState): SimOrder {
  const awayBearing = normalizeHeading(bearingBetween(threat.position, ship.position));
  return {
    shipId: ship.id,
    action: 'maneuver',
    turnDelta: turnToward(ship, avoidObstacles(ship, awayBearing, state)),
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

function aggressiveOrder(
  ship: ShipState,
  target: ShipState,
  params: AiProfileParams,
  state: SimState
): SimOrder {
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
    const approachBearing = avoidObstacles(ship, bearingBetween(ship.position, aimPoint), state);
    return {
      shipId: ship.id,
      action: 'maneuver',
      turnDelta: turnToward(ship, approachBearing),
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
      turnDelta: turnToward(ship, avoidObstacles(ship, escapeBearing, state)),
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

export interface EscortParams {
  // Station offset in the leader's frame: forward along its heading, lateral
  // to port (positive) or starboard (negative).
  stationForward: number;
  stationLateral: number;
  stationTolerance: number;
  preferredRange: number;
}

export const ESCORT_DEFAULTS: EscortParams = {
  stationForward: -20,
  stationLateral: 60,
  stationTolerance: 40,
  preferredRange: 80
};

// Flank-assist escort (formation_keep high, docs/content/ai-profiles.md):
// hold station on the leader's flank, engage the hostile nearest the leader,
// and fall back to the aggressive profile once the leader is lost — the line
// breaks. Pure function of its inputs; no RNG.
export function escortOrderFor(
  ship: ShipState,
  state: SimState,
  leaderId: string,
  overrides?: Partial<EscortParams>
): SimOrder {
  const params = { ...ESCORT_DEFAULTS, ...overrides };
  if (ship.hp <= 0) {
    return pass(ship);
  }

  const leader = state.ships.find(
    (candidate) => candidate.id === leaderId && candidate.hp > 0
  );
  if (!leader) {
    return aiOrderFor(ship, state, 'aggressive');
  }

  const radians = (leader.heading * Math.PI) / 180;
  const station = {
    x:
      leader.position.x +
      Math.round(Math.cos(radians) * params.stationForward - Math.sin(radians) * params.stationLateral),
    y:
      leader.position.y +
      Math.round(Math.sin(radians) * params.stationForward + Math.cos(radians) * params.stationLateral)
  };

  const stationDistance = distanceBetween(ship.position, station);
  if (stationDistance > params.stationTolerance) {
    const bearing = avoidObstacles(ship, bearingBetween(ship.position, station), state);
    return {
      shipId: ship.id,
      action: 'maneuver',
      turnDelta: turnToward(ship, bearing),
      speedDelta: stationDistance > 80 ? 1 : 0
    };
  }

  const threat = pickTarget(leader, state, 'medium');
  if (threat && distanceBetween(ship.position, threat.position) <= params.preferredRange) {
    return {
      shipId: ship.id,
      action: 'broadside',
      targetShipId: threat.id,
      side: broadsideSide(ship, threat),
      turnDelta: 0,
      speedDelta: 0
    };
  }

  // Hold formation: match the leader's heading and keep pace.
  return {
    shipId: ship.id,
    action: 'maneuver',
    turnDelta: turnToward(ship, leader.heading),
    speedDelta: 0
  };
}

// Boss template (docs/content/ai-profiles.md): scripted phases keyed to hull
// fraction, each delegating to a reusable profile. Phase selection is shared
// with mission telemetry via bossPhaseIndex.
export interface BossPhase {
  // Active while hull fraction is strictly above this threshold.
  hullAbove: number;
  profile: AiProfileName;
  overrides?: Partial<AiProfileParams>;
}

export interface BossParams {
  baseHull: number;
  phases: BossPhase[];
}

export function bossPhaseIndex(hull: number, params: BossParams): number {
  const fraction = params.baseHull > 0 ? hull / params.baseHull : 0;
  for (let i = 0; i < params.phases.length; i++) {
    if (fraction > params.phases[i].hullAbove) {
      return i;
    }
  }
  return Math.max(0, params.phases.length - 1);
}

export function bossOrderFor(ship: ShipState, state: SimState, params: BossParams): SimOrder {
  if (ship.hp <= 0) {
    return pass(ship);
  }
  const phase = params.phases[bossPhaseIndex(ship.hp, params)];
  return aiOrderFor(ship, state, phase.profile, phase.overrides);
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
    return reposition(ship, target, state);
  }

  if (profile === 'aggressive') {
    return aggressiveOrder(ship, target, params, state);
  }
  if (profile === 'kiting') {
    return kitingOrder(ship, target, params, state);
  }
  return lineAdvanceOrder(ship, target, params);
}
