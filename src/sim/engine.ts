import crypto from 'crypto';
import {
  SimEvent,
  SimOrder,
  SimPreviewRequest,
  SimPreviewResult,
  SimState,
  SimSummary,
  ShipState,
  Obstacle,
  SlowZone,
  Vector2,
  Wind
} from './types.js';

const MAX_SPEED = 10;

// Wind impact curve (modifiers.windMovement): sailing within TAILWIND_ARC of
// the wind direction grants a bonus, within HEADWIND_ARC of dead upwind a
// penalty of half the wind speed; beam reaches are neutral
// (docs/content/balance-tables.md).
const WIND_TAILWIND_ARC = 45;
const WIND_HEADWIND_ARC = 135;
const MOVEMENT_SCALE = 5;

// Raking fire (modifiers.rakingFire): a broadside whose shot line runs along
// the target's keel — bearing within RAKE_ARC of the target heading (stern
// rake) or its reverse (bow rake) — deals multiplied damage
// (docs/content/balance-tables.md, raking multiplier baseline).
const RAKE_ARC = 20;
const RAKE_MULTIPLIER = 1.5;

function clamp(value: number, min: number, max: number) {
  return Math.min(max, Math.max(min, value));
}

function normalizeHeading(degrees: number) {
  const normalized = ((degrees % 360) + 360) % 360;
  return Math.round(normalized);
}

function angleBetween(from: ShipState, to: ShipState) {
  const dx = to.position.x - from.position.x;
  const dy = to.position.y - from.position.y;
  const radians = Math.atan2(dy, dx);
  const degrees = (radians * 180) / Math.PI;
  return normalizeHeading(degrees);
}

function distance(from: ShipState, to: ShipState) {
  const dx = to.position.x - from.position.x;
  const dy = to.position.y - from.position.y;
  return Math.sqrt(dx * dx + dy * dy);
}

export function createDeterministicRng(seed: number) {
  let t = seed >>> 0;
  return () => {
    t += 0x6d2b79f5;
    let r = Math.imul(t ^ (t >>> 15), 1 | t);
    r ^= r + Math.imul(r ^ (r >>> 7), 61 | r);
    return ((r ^ (r >>> 14)) >>> 0) / 4294967296;
  };
}

export function effectiveSpeed(ship: ShipState, wind: Wind): number {
  if (ship.speed === 0) {
    return 0;
  }
  const rawDiff = Math.abs(normalizeHeading(ship.heading - wind.direction));
  const windAngle = Math.min(rawDiff, 360 - rawDiff);
  let delta = 0;
  if (windAngle <= WIND_TAILWIND_ARC) {
    delta = Math.floor(wind.speed / 2);
  } else if (windAngle >= WIND_HEADWIND_ARC) {
    delta = -Math.floor(wind.speed / 2);
  }
  // A ship under sail keeps steerage way even dead upwind.
  return clamp(ship.speed + delta, 1, MAX_SPEED);
}

function insideObstacle(point: Vector2, obstacles: Obstacle[]) {
  return obstacles.some((obstacle) => {
    const dx = point.x - obstacle.position.x;
    const dy = point.y - obstacle.position.y;
    return Math.sqrt(dx * dx + dy * dy) < obstacle.radius;
  });
}

function insideSlowZone(point: Vector2, slowZones: SlowZone[]): SlowZone | undefined {
  return slowZones.find((zone) => {
    const dx = point.x - zone.position.x;
    const dy = point.y - zone.position.y;
    return Math.sqrt(dx * dx + dy * dy) < zone.radius;
  });
}

function applyMovement(
  ship: ShipState,
  wind: Wind,
  obstacles: Obstacle[],
  slowZones: SlowZone[]
): SimEvent {
  let speed = effectiveSpeed(ship, wind);
  const hazard = speed > 0 ? insideSlowZone(ship.position, slowZones) : undefined;
  if (hazard) {
    // Ships under way keep steerage even inside debris.
    speed = Math.max(1, speed - hazard.speedPenalty);
  }
  const radians = (ship.heading * Math.PI) / 180;
  const destination = {
    x: ship.position.x + Math.round(Math.cos(radians) * speed * MOVEMENT_SCALE),
    y: ship.position.y + Math.round(Math.sin(radians) * speed * MOVEMENT_SCALE)
  };
  const blocked = insideObstacle(destination, obstacles);
  if (!blocked) {
    ship.position = destination;
  }
  return {
    type: 'movement',
    shipId: ship.id,
    effectiveSpeed: speed,
    position: { ...ship.position },
    ...(blocked ? { blocked: true } : {}),
    ...(hazard ? { slowedByHazard: true } : {})
  };
}

function applyManeuver(ship: ShipState, order: SimOrder): SimEvent {
  const nextHeading = normalizeHeading(ship.heading + (order.turnDelta ?? 0));
  const nextSpeed = clamp(ship.speed + (order.speedDelta ?? 0), 0, MAX_SPEED);
  ship.heading = nextHeading;
  ship.speed = nextSpeed;
  return {
    type: 'maneuver',
    shipId: ship.id,
    heading: nextHeading,
    speed: nextSpeed,
    turnDelta: order.turnDelta ?? 0,
    speedDelta: order.speedDelta ?? 0
  };
}

function resolveBroadside(
  rng: () => number,
  attacker: ShipState,
  target: ShipState,
  order: SimOrder,
  damageScale: number,
  attackerSpeed: number,
  rakingEnabled: boolean,
  accuracyBonus: number
): SimEvent {
  const range = distance(attacker, target);
  const bearingToTarget = angleBetween(attacker, target);

  let rake: 'bow' | 'stern' | undefined;
  if (rakingEnabled) {
    const rawKeelDiff = Math.abs(normalizeHeading(bearingToTarget - target.heading));
    const keelDiff = Math.min(rawKeelDiff, 360 - rawKeelDiff);
    if (keelDiff <= RAKE_ARC) {
      rake = 'stern';
    } else if (keelDiff >= 180 - RAKE_ARC) {
      rake = 'bow';
    }
  }
  const angleDiff = Math.abs(normalizeHeading(attacker.heading - bearingToTarget));
  const normalizedDiff = Math.min(angleDiff, 360 - angleDiff);

  const rangePenalty = Math.floor(range / 50);
  const anglePenalty = Math.floor(normalizedDiff / 15);

  const baseChance =
    72 - rangePenalty - anglePenalty + Math.floor(attackerSpeed / 2) + accuracyBonus;
  const hitChance = clamp(baseChance, 15, 95);
  const roll = Math.floor(rng() * 100);
  const hit = roll < hitChance;

  const baseDamage = 18 + Math.floor(attacker.sail / 25) + Math.floor(attackerSpeed * 1.5);
  const variance = Math.floor(rng() * 6);
  let scaledDamage = Math.floor((baseDamage + variance) * damageScale);
  if (rake) {
    scaledDamage = Math.floor(scaledDamage * RAKE_MULTIPLIER);
  }
  const hullDamage = hit ? scaledDamage : 0;
  const sailDamage = hit ? Math.floor(scaledDamage * 0.6) : 0;
  const crewDamage = hit ? Math.floor(scaledDamage * 0.35) : 0;

  target.hp = clamp(target.hp - hullDamage, 0, target.hp);
  target.sail = clamp(target.sail - sailDamage, 0, target.sail);
  target.crew = clamp(target.crew - crewDamage, 0, target.crew);

  return {
    type: 'broadside',
    shipId: attacker.id,
    targetShipId: target.id,
    side: order.side ?? 'port',
    hit,
    roll,
    hitChance,
    damage: {
      hull: hullDamage,
      sail: sailDamage,
      crew: crewDamage
    },
    targetRemaining: {
      hp: target.hp,
      sail: target.sail,
      crew: target.crew
    },
    ...(rake ? { rake } : {})
  };
}

function resolveBoarding(
  rng: () => number,
  attacker: ShipState,
  target: ShipState,
  boardingBonus: number
): SimEvent {
  const range = distance(attacker, target);
  const roll = Math.floor(rng() * 100);
  const proximityPenalty = Math.floor(Math.max(0, range - 30) / 10);
  const power = Math.max(5, attacker.crew - proximityPenalty);
  const defense = Math.max(5, target.crew);

  const bonusPoints = Math.round(boardingBonus * 100);
  const successChance = clamp(
    60 + power - defense / 2 - proximityPenalty * 2 + bonusPoints,
    10,
    90
  );
  const success = roll < successChance;

  const attackerLoss = Math.floor((defense / 8 + rng() * 3) * (success ? 0.5 : 1.2));
  const targetLoss = success ? Math.floor((power / 6 + rng() * 4)) : Math.floor(power / 10);

  attacker.crew = clamp(attacker.crew - attackerLoss, 0, attacker.crew);
  target.crew = clamp(target.crew - targetLoss, 0, target.crew);
  if (success && target.crew === 0) {
    target.hp = 0;
  }

  return {
    type: 'boarding',
    shipId: attacker.id,
    targetShipId: target.id,
    success,
    roll,
    crewLoss: attackerLoss,
    targetCrewLoss: targetLoss,
    targetRemaining: {
      hp: target.hp,
      sail: target.sail,
      crew: target.crew
    }
  };
}

function buildSummary(ships: ShipState[]): SimSummary {
  return {
    playerRemaining: ships.filter((s) => s.side === 'player' && s.hp > 0).length,
    enemyRemaining: ships.filter((s) => s.side === 'enemy' && s.hp > 0).length,
    sunk: ships.filter((s) => s.hp <= 0).map((s) => s.id)
  };
}

export function resolveSimPreview(input: SimPreviewRequest): SimPreviewResult {
  const rng = createDeterministicRng(input.seed + input.turn);
  const ships: ShipState[] = input.state.ships.map((ship) => ({
    ...ship,
    status: ship.status ?? {},
    cooldowns: ship.cooldowns ?? {}
  }));

  const shipById = new Map<string, ShipState>();
  for (const ship of ships) {
    shipById.set(ship.id, ship);
  }

  const orderByShip = new Map<string, SimOrder>();
  for (const order of input.orders) {
    orderByShip.set(order.shipId, order);
  }

  const events: SimEvent[] = [];
  const resolutionOrder = [...ships].sort((a, b) => a.id.localeCompare(b.id));

  for (const ship of resolutionOrder) {
    const order = orderByShip.get(ship.id);
    if (order) {
      events.push(applyManeuver(ship, order));
    }
  }

  const windAware = input.modifiers?.windMovement === true;
  if (windAware) {
    const obstacles = input.state.obstacles ?? [];
    const slowZones = input.state.slowZones ?? [];
    for (const ship of resolutionOrder) {
      if (ship.hp <= 0) continue;
      events.push(applyMovement(ship, input.state.wind, obstacles, slowZones));
    }
  }

  for (const ship of resolutionOrder) {
    if (ship.hp <= 0) continue;
    const order = orderByShip.get(ship.id);
    if (!order || order.action === 'pass') {
      continue;
    }

    if (order.action === 'broadside' && order.targetShipId) {
      const target = shipById.get(order.targetShipId);
      if (target && target.hp > 0) {
        const damageScale = input.modifiers?.damageScale?.[ship.id] ?? 1;
        const attackerSpeed = windAware ? effectiveSpeed(ship, input.state.wind) : ship.speed;
        const rakingEnabled = input.modifiers?.rakingFire === true;
        const accuracyBonus = input.modifiers?.accuracyBonus?.[ship.id] ?? 0;
        events.push(
          resolveBroadside(rng, ship, target, order, damageScale, attackerSpeed, rakingEnabled, accuracyBonus)
        );
      }
    } else if (order.action === 'boarding' && order.targetShipId) {
      const target = shipById.get(order.targetShipId);
      if (target && target.hp > 0) {
        const boardingBonus = input.modifiers?.boardingBonus?.[ship.id] ?? 0;
        events.push(resolveBoarding(rng, ship, target, boardingBonus));
      }
    }
  }

  const nextState: SimState = {
    ...input.state,
    turn: input.turn + 1,
    ships
  };

  const summary = buildSummary(ships);
  const payloadForHash = {
    turn: input.turn,
    nextState,
    events,
    summary
  };

  const hash = crypto.createHash('sha256').update(JSON.stringify(payloadForHash)).digest('hex');

  return {
    ...payloadForHash,
    hash
  };
}


