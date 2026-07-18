import { FIRE_DURATION_TURNS, SLOW_DURATION_TURNS } from './engine.js';
import { MissionTurnRecord } from './missionRunner.js';
import { SimEvent, SimState, Vector2, Wind } from './types.js';

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
