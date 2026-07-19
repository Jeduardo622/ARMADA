import crypto from 'crypto';
import {
  SimEvent,
  SimOrder,
  SimPreviewRequest,
  SimPreviewResult,
  SimState,
  SimSummary,
  ShipState,
  ShipUpgradeTiers,
  Obstacle,
  SlowZone,
  Vector2,
  Wind
} from './types.js';
import {
  cannonDamageBonusPct,
  sailSpeedBonus,
  slowedTurnRateLimit,
  upgradedHullHp
} from './upgradeEffects.js';

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

// Status effects (modifiers.statusEffects): fire applies a per-turn hull DoT
// and a broadside accuracy penalty while burning; slow reduces speed and turn
// rate while sails are shredded. Counters tick down at the start of each turn
// and re-application refreshes the duration instead of stacking
// (docs/content/balance-tables.md, status effects). Values are design-tunable.
const FIRE_IGNITION_CHANCE = 25;
// The duration constants are exported so mission telemetry can tell an
// ignition/application event (counter at full duration) from a tick.
export const FIRE_DURATION_TURNS = 3;
const FIRE_HULL_DAMAGE_PER_TURN = 5;
const FIRE_ACCURACY_PENALTY = 15;
const SLOW_SAIL_FRACTION = 0.5;
export const SLOW_DURATION_TURNS = 2;
const SLOW_SPEED_PENALTY = 2;
const SLOW_TURN_RATE_LIMIT = 45;

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

// Start-of-turn tick for modifiers.statusEffects: fire burns hull and both
// counters count down; a counter already at zero clears its flag before any
// phase runs, so an effect stays active for exactly its remaining turns.
function tickShipStatus(ship: ShipState, events: SimEvent[]) {
  const status = ship.status;
  if (!status) return;
  let changed = false;
  if (status.onFire) {
    const remaining = status.fireTurnsRemaining ?? 0;
    if (remaining <= 0) {
      delete status.onFire;
      delete status.fireTurnsRemaining;
    } else {
      ship.hp = clamp(ship.hp - FIRE_HULL_DAMAGE_PER_TURN, 0, ship.hp);
      status.fireTurnsRemaining = remaining - 1;
    }
    changed = true;
  }
  if (status.slowed) {
    const remaining = status.slowTurnsRemaining ?? 0;
    if (remaining <= 0) {
      delete status.slowed;
      delete status.slowTurnsRemaining;
    } else {
      status.slowTurnsRemaining = remaining - 1;
    }
    changed = true;
  }
  if (changed) {
    events.push({ type: 'status', shipId: ship.id, status: { ...status } });
  }
}

function refreshFire(ship: ShipState): boolean {
  const status = (ship.status ??= {});
  if (status.onFire === true && status.fireTurnsRemaining === FIRE_DURATION_TURNS) {
    return false;
  }
  status.onFire = true;
  status.fireTurnsRemaining = FIRE_DURATION_TURNS;
  return true;
}

function refreshSlow(ship: ShipState): boolean {
  const status = (ship.status ??= {});
  if (status.slowed === true && status.slowTurnsRemaining === SLOW_DURATION_TURNS) {
    return false;
  }
  status.slowed = true;
  status.slowTurnsRemaining = SLOW_DURATION_TURNS;
  return true;
}

function applyMovement(
  ship: ShipState,
  wind: Wind,
  obstacles: Obstacle[],
  slowZones: SlowZone[],
  statusSpeedPenalty: number,
  upgradeSpeedBonus: number
): SimEvent {
  let speed = effectiveSpeed(ship, wind);
  if (upgradeSpeedBonus > 0 && speed > 0) {
    // Upgraded rigging: the bonus applies before hazard and status penalties.
    speed = Math.min(MAX_SPEED, speed + upgradeSpeedBonus);
  }
  const hazard = speed > 0 ? insideSlowZone(ship.position, slowZones) : undefined;
  if (hazard) {
    // Ships under way keep steerage even inside debris.
    speed = Math.max(1, speed - hazard.speedPenalty);
  }
  if (statusSpeedPenalty > 0 && speed > 0) {
    speed = Math.max(1, speed - statusSpeedPenalty);
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

function applyManeuver(ship: ShipState, order: SimOrder, turnRateLimit?: number): SimEvent {
  const requestedTurn = order.turnDelta ?? 0;
  const turnDelta =
    turnRateLimit === undefined
      ? requestedTurn
      : clamp(requestedTurn, -turnRateLimit, turnRateLimit);
  const nextHeading = normalizeHeading(ship.heading + turnDelta);
  const nextSpeed = clamp(ship.speed + (order.speedDelta ?? 0), 0, MAX_SPEED);
  ship.heading = nextHeading;
  ship.speed = nextSpeed;
  return {
    type: 'maneuver',
    shipId: ship.id,
    heading: nextHeading,
    speed: nextSpeed,
    turnDelta,
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
  accuracyBonus: number,
  cannonBonusPct: number
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
  if (cannonBonusPct > 0) {
    // Integer scale-then-floor; applies to raked damage too.
    scaledDamage = Math.floor((scaledDamage * (100 + cannonBonusPct)) / 100);
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

  // Ship upgrades (modifiers.shipUpgrades): request-level tiers scale
  // player-side ships only. Tier authenticity against owned upgrades is
  // enforced at mission win-proof time, not here.
  const upgrades =
    input.modifiers?.shipUpgrades === true ? input.upgrades : undefined;
  const playerTier = (ship: ShipState, component: keyof ShipUpgradeTiers) =>
    upgrades && ship.side === 'player' ? upgrades[component] : 0;

  const ships: ShipState[] = input.state.ships.map((ship) => {
    const next: ShipState = {
      ...ship,
      // Copy rather than alias: status effects mutate this object and must
      // never write back into the caller's input state.
      status: { ...ship.status },
      cooldowns: ship.cooldowns ?? {}
    };
    // Hull tiers raise battle-start hp: applied only on the opening turn's
    // state build so chained previews (nextState fed back with the flag
    // still set) never rescale already-upgraded hp.
    const hullTier = input.turn === 1 ? playerTier(next, 'hull') : 0;
    if (hullTier > 0) {
      next.hp = upgradedHullHp(next.hp, hullTier);
    }
    return next;
  });

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

  const statusEffects = input.modifiers?.statusEffects === true;
  const sailAtTurnStart = statusEffects
    ? new Map(ships.map((ship) => [ship.id, ship.sail]))
    : undefined;
  if (statusEffects) {
    for (const ship of resolutionOrder) {
      if (ship.hp <= 0) continue;
      tickShipStatus(ship, events);
    }
  }
  const isSlowed = (ship: ShipState) => statusEffects && ship.status?.slowed === true;
  // Slow clamps turns to SLOW_TURN_RATE_LIMIT; sail tiers ease the clamp.
  const turnRateLimitFor = (ship: ShipState) => {
    if (!isSlowed(ship)) return undefined;
    const sailTier = playerTier(ship, 'sail');
    return sailTier > 0
      ? slowedTurnRateLimit(SLOW_TURN_RATE_LIMIT, sailTier)
      : SLOW_TURN_RATE_LIMIT;
  };

  for (const ship of resolutionOrder) {
    // A ship the fire tick just sank takes no further actions. Gated on the
    // flag: legacy resolution never hp-checked maneuvers and the flag-off
    // hash chains must stay byte-identical.
    if (statusEffects && ship.hp <= 0) continue;
    const order = orderByShip.get(ship.id);
    if (order) {
      events.push(applyManeuver(ship, order, turnRateLimitFor(ship)));
    }
  }

  const windAware = input.modifiers?.windMovement === true;
  if (windAware) {
    const obstacles = input.state.obstacles ?? [];
    const slowZones = input.state.slowZones ?? [];
    for (const ship of resolutionOrder) {
      if (ship.hp <= 0) continue;
      events.push(
        applyMovement(
          ship,
          input.state.wind,
          obstacles,
          slowZones,
          isSlowed(ship) ? SLOW_SPEED_PENALTY : 0,
          sailSpeedBonus(playerTier(ship, 'sail'))
        )
      );
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
        let attackerSpeed = windAware ? effectiveSpeed(ship, input.state.wind) : ship.speed;
        const sailBonus = sailSpeedBonus(playerTier(ship, 'sail'));
        if (sailBonus > 0 && attackerSpeed > 0) {
          attackerSpeed = Math.min(MAX_SPEED, attackerSpeed + sailBonus);
        }
        if (isSlowed(ship)) {
          attackerSpeed = Math.max(0, attackerSpeed - SLOW_SPEED_PENALTY);
        }
        const rakingEnabled = input.modifiers?.rakingFire === true;
        const firePenalty =
          statusEffects && ship.status?.onFire === true ? FIRE_ACCURACY_PENALTY : 0;
        const accuracyBonus = (input.modifiers?.accuracyBonus?.[ship.id] ?? 0) - firePenalty;
        const event = resolveBroadside(
          rng,
          ship,
          target,
          order,
          damageScale,
          attackerSpeed,
          rakingEnabled,
          accuracyBonus,
          cannonDamageBonusPct(playerTier(ship, 'cannon'))
        );
        events.push(event);
        if (statusEffects && event.type === 'broadside' && event.hit) {
          // The ignition roll is consumed on every landed hit so the rng
          // stream does not depend on the target's current status. A killing
          // hit leaves the sunk target's status untouched.
          const ignitionRoll = Math.floor(rng() * 100);
          const targetAfloat = target.hp > 0;
          if (
            targetAfloat &&
            event.damage.hull > 0 &&
            ignitionRoll < FIRE_IGNITION_CHANCE &&
            refreshFire(target)
          ) {
            events.push({ type: 'status', shipId: target.id, status: { ...target.status } });
          }
          const startSail = sailAtTurnStart?.get(target.id) ?? target.sail;
          if (
            targetAfloat &&
            event.damage.sail > 0 &&
            target.sail < startSail * SLOW_SAIL_FRACTION &&
            refreshSlow(target)
          ) {
            events.push({ type: 'status', shipId: target.id, status: { ...target.status } });
          }
        }
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


