import { FIRE_DURATION_TURNS, SLOW_DURATION_TURNS, pointOfSail } from './engine.js';
import { MissionTurnRecord } from './missionRunner.js';
import { SimEvent, SimOrder, SimState, Vector2, Wind } from './types.js';

// Mission-generic outcome metrics shared by mission scenarios: positional
// geometry, weather-gage detection, flanked classification, and rake counts.

export function signedAngle(from: number, to: number) {
  return ((to - from + 540) % 360) - 180;
}

export function bearingBetween(from: Vector2, to: Vector2) {
  const radians = Math.atan2(to.y - from.y, to.x - from.x);
  const degrees = (radians * 180) / Math.PI;
  return Math.round(((degrees % 360) + 360) % 360);
}

export function centroid(points: Vector2[]): Vector2 | undefined {
  if (points.length === 0) {
    return undefined;
  }
  const sum = points.reduce((acc, point) => ({ x: acc.x + point.x, y: acc.y + point.y }), {
    x: 0,
    y: 0
  });
  return { x: sum.x / points.length, y: sum.y / points.length };
}

// Positions after a turn's movement phase, read back from movement events.
export function positionsFromEvents(events: SimEvent[], shipIds: readonly string[]): Vector2[] {
  const positions: Vector2[] = [];
  for (const event of events) {
    if (event.type === 'movement' && shipIds.includes(event.shipId)) {
      positions.push(event.position);
    }
  }
  return positions;
}

export function heldWeatherGageOnTurn(
  events: SimEvent[],
  wind: Wind,
  playerShipIds: readonly string[],
  enemyShipIds: readonly string[],
  upwindArc: number
): boolean {
  const playerCentroid = centroid(positionsFromEvents(events, playerShipIds));
  const enemyCentroid = centroid(positionsFromEvents(events, enemyShipIds));
  if (!playerCentroid || !enemyCentroid) {
    return false;
  }
  const bearing = bearingBetween(playerCentroid, enemyCentroid);
  return Math.abs(signedAngle(bearing, wind.direction)) <= upwindArc;
}

// The player counts as flanked when sunk with at least two enemies alive and
// the widest pair of enemy bearings from the last player position at least
// flankedSpread degrees apart.
export function classifyLoss(
  finalState: SimState,
  playerSunk: boolean,
  flankedSpread: number
): 'timeout' | 'sunk' | 'flanked' {
  if (!playerSunk) {
    return 'timeout';
  }
  const enemies = finalState.ships.filter((ship) => ship.side === 'enemy' && ship.hp > 0);
  if (enemies.length < 2) {
    return 'sunk';
  }
  const players = finalState.ships.filter((ship) => ship.side === 'player');
  const lastPlayer = centroid(players.map((ship) => ship.position));
  if (!lastPlayer) {
    return 'sunk';
  }
  const bearings = enemies.map((ship) => bearingBetween(lastPlayer, ship.position));
  let spread = 0;
  for (let i = 0; i < bearings.length; i++) {
    for (let j = i + 1; j < bearings.length; j++) {
      spread = Math.max(spread, Math.abs(signedAngle(bearings[i], bearings[j])));
    }
  }
  return spread >= flankedSpread ? 'flanked' : 'sunk';
}

export interface RakeCounts {
  rakeAttempts: number;
  rakeHits: number;
}

export interface BoardingCounts {
  boardingAttempts: number;
  boardingSuccesses: number;
}

export function countBoardings(
  turns: MissionTurnRecord[],
  shipIds: readonly string[]
): BoardingCounts {
  let boardingAttempts = 0;
  let boardingSuccesses = 0;
  for (const turn of turns) {
    for (const event of turn.events) {
      if (event.type === 'boarding' && shipIds.includes(event.shipId)) {
        boardingAttempts += 1;
        if (event.success) {
          boardingSuccesses += 1;
        }
      }
    }
  }
  return { boardingAttempts, boardingSuccesses };
}

export interface StatusApplicationCounts {
  ignitions: number;
  slows: number;
}

// Fresh fire/slow applications on the given ships. Application (and refresh)
// events carry the counter at full duration; per-turn ticks and expiries
// always carry less, so they are not counted. Deduplicated per ship and turn
// because a later same-turn status snapshot repeats the full counter.
export function countStatusApplications(
  turns: MissionTurnRecord[],
  shipIds: readonly string[]
): StatusApplicationCounts {
  const ignitions = new Set<string>();
  const slows = new Set<string>();
  for (const turn of turns) {
    for (const event of turn.events) {
      if (event.type !== 'status' || !shipIds.includes(event.shipId)) {
        continue;
      }
      const key = `${turn.turn}:${event.shipId}`;
      if (event.status.onFire === true && event.status.fireTurnsRemaining === FIRE_DURATION_TURNS) {
        ignitions.add(key);
      }
      if (event.status.slowed === true && event.status.slowTurnsRemaining === SLOW_DURATION_TURNS) {
        slows.add(key);
      }
    }
  }
  return { ignitions: ignitions.size, slows: slows.size };
}

export interface ManeuverWindCounts {
  clampedManeuvers: number;
  upwindManeuvers: number;
  downwindManeuvers: number;
}

// Wind profile of the given ships' maneuvers: how many were clamped below
// the ordered turn (a turn-rate limit bit) and how many executed on an
// upwind versus downwind point of sail. The pre-maneuver heading is
// reconstructed from the event (heading minus applied turnDelta) and judged
// against the wind the loop supplied for that turn.
export function countManeuverWindProfile(
  turns: MissionTurnRecord[],
  playerTurnOrders: SimOrder[][],
  shipIds: readonly string[],
  windForTurn: (turn: number) => Wind
): ManeuverWindCounts {
  let clampedManeuvers = 0;
  let upwindManeuvers = 0;
  let downwindManeuvers = 0;
  for (const turn of turns) {
    // Last order per ship wins, mirroring how resolveSimPreview builds its
    // orderByShip map, so duplicate-order turns compare against the order
    // that actually executed.
    const requestedByShip = new Map<string, number>();
    for (const order of playerTurnOrders[turn.turn - 1] ?? []) {
      requestedByShip.set(order.shipId, order.turnDelta ?? 0);
    }
    const wind = windForTurn(turn.turn);
    for (const event of turn.events) {
      if (event.type !== 'maneuver' || !shipIds.includes(event.shipId)) {
        continue;
      }
      const requested = requestedByShip.get(event.shipId) ?? 0;
      if (event.turnDelta !== requested) {
        clampedManeuvers += 1;
      }
      const headingBefore = (((event.heading - event.turnDelta) % 360) + 360) % 360;
      const sail = pointOfSail(headingBefore, wind);
      if (sail === 'upwind') {
        upwindManeuvers += 1;
      } else if (sail === 'downwind') {
        downwindManeuvers += 1;
      }
    }
  }
  return { clampedManeuvers, upwindManeuvers, downwindManeuvers };
}

export interface RamProfileCounts {
  ramsInflicted: number;
  ramsSuffered: number;
  ramHullDamageDealt: number;
  ramHullDamageTaken: number;
}

// Ram profile of the given ships: rams they initiated (hull damage dealt to
// the enemy plus the recoil their own bows absorbed) versus rams an enemy
// drove into them. Enemy recoil is self-inflicted and never counts as
// damage dealt.
export function countRamProfile(
  turns: MissionTurnRecord[],
  shipIds: readonly string[]
): RamProfileCounts {
  let ramsInflicted = 0;
  let ramsSuffered = 0;
  let ramHullDamageDealt = 0;
  let ramHullDamageTaken = 0;
  for (const turn of turns) {
    for (const event of turn.events) {
      if (event.type !== 'ram') {
        continue;
      }
      if (shipIds.includes(event.shipId)) {
        ramsInflicted += 1;
        ramHullDamageDealt += event.hullDamage;
        ramHullDamageTaken += event.selfHullDamage;
      } else if (shipIds.includes(event.targetShipId)) {
        ramsSuffered += 1;
        ramHullDamageTaken += event.hullDamage;
      }
    }
  }
  return { ramsInflicted, ramsSuffered, ramHullDamageDealt, ramHullDamageTaken };
}

export function countRakes(turns: MissionTurnRecord[], shipIds: readonly string[]): RakeCounts {
  let rakeAttempts = 0;
  let rakeHits = 0;
  for (const turn of turns) {
    for (const event of turn.events) {
      if (event.type === 'broadside' && shipIds.includes(event.shipId) && event.rake) {
        rakeAttempts += 1;
        if (event.hit) {
          rakeHits += 1;
        }
      }
    }
  }
  return { rakeAttempts, rakeHits };
}
