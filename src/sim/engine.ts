import crypto from 'crypto';
import {
  SimEvent,
  SimOrder,
  SimPreviewRequest,
  SimPreviewResult,
  SimState,
  SimSummary,
  ShipState
} from './types.js';

const MAX_SPEED = 10;

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

function createDeterministicRng(seed: number) {
  let t = seed >>> 0;
  return () => {
    t += 0x6d2b79f5;
    let r = Math.imul(t ^ (t >>> 15), 1 | t);
    r ^= r + Math.imul(r ^ (r >>> 7), 61 | r);
    return ((r ^ (r >>> 14)) >>> 0) / 4294967296;
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
  order: SimOrder
): SimEvent {
  const range = distance(attacker, target);
  const bearingToTarget = angleBetween(attacker, target);
  const angleDiff = Math.abs(normalizeHeading(attacker.heading - bearingToTarget));
  const normalizedDiff = Math.min(angleDiff, 360 - angleDiff);

  const rangePenalty = Math.floor(range / 50);
  const anglePenalty = Math.floor(normalizedDiff / 15);

  const baseChance = 72 - rangePenalty - anglePenalty + Math.floor(attacker.speed / 2);
  const hitChance = clamp(baseChance, 15, 95);
  const roll = Math.floor(rng() * 100);
  const hit = roll < hitChance;

  const baseDamage = 18 + Math.floor(attacker.sail / 25) + Math.floor(attacker.speed * 1.5);
  const variance = Math.floor(rng() * 6);
  const hullDamage = hit ? baseDamage + variance : 0;
  const sailDamage = hit ? Math.floor((baseDamage + variance) * 0.6) : 0;
  const crewDamage = hit ? Math.floor((baseDamage + variance) * 0.35) : 0;

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
    }
  };
}

function resolveBoarding(
  rng: () => number,
  attacker: ShipState,
  target: ShipState
): SimEvent {
  const range = distance(attacker, target);
  const roll = Math.floor(rng() * 100);
  const proximityPenalty = Math.floor(Math.max(0, range - 30) / 10);
  const power = Math.max(5, attacker.crew - proximityPenalty);
  const defense = Math.max(5, target.crew);

  const successChance = clamp(60 + power - defense / 2 - proximityPenalty * 2, 10, 90);
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

  for (const ship of resolutionOrder) {
    if (ship.hp <= 0) continue;
    const order = orderByShip.get(ship.id);
    if (!order || order.action === 'pass') {
      continue;
    }

    if (order.action === 'broadside' && order.targetShipId) {
      const target = shipById.get(order.targetShipId);
      if (target && target.hp > 0) {
        events.push(resolveBroadside(rng, ship, target, order));
      }
    } else if (order.action === 'boarding' && order.targetShipId) {
      const target = shipById.get(order.targetShipId);
      if (target && target.hp > 0) {
        events.push(resolveBoarding(rng, ship, target));
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


