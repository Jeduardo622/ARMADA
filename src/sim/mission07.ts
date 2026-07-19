import { aiOrderFor } from './ai.js';
import { createDeterministicRng } from './engine.js';
import { classifyLoss, countStatusApplications } from './missionMetrics.js';
import { MissionTurnRecord, runMissionLoop } from './missionRunner.js';
import { SimOrder, SimState, ShipUpgradeTiers, Wind } from './types.js';
import { upgradedHullHp } from './upgradeEffects.js';

// Mission 07 "Burning Seas" — docs/content/missions/mission-07-burning-seas.md
// First mission to adopt modifiers.statusEffects (fire and slow).
export const MISSION_07_CODE = 'mission-07-burning-seas';
export const MISSION_07_TURN_LIMIT = 10;
export const MISSION_07_ENEMY_SAIL_SCALE = 0.85;
export const MISSION_07_IGNITION_TARGET = 1;
export const MISSION_07_DEFAULT_SEED = 707;

export const MISSION_07_PLAYER_SHIP_IDS = ['player-sloop-a', 'player-sloop-b'] as const;
export const MISSION_07_ENEMY_SHIP_IDS = ['enemy-frigate-a', 'enemy-frigate-b'] as const;

const PLAYER_BASE_HULL_HP = 120;
const FRIGATE_HULL_HP = 180;
const FRIGATE_BASE_SAIL = 90;
// Weathered rigging: the sail tuning knob keeps the slow effect in play.
const FRIGATE_SAIL = Math.floor(FRIGATE_BASE_SAIL * MISSION_07_ENEMY_SAIL_SCALE);
const FRIGATE_CREW = 60;

// Steady tailwind for the player's opening heading, medium variance.
const WIND_BASE_DIRECTION = 0;
const WIND_BASE_SPEED = 4;

const FLANKED_SPREAD = 120;

export function createMission07State(): SimState {
  return {
    turn: 1,
    wind: { direction: WIND_BASE_DIRECTION, speed: WIND_BASE_SPEED },
    ships: [
      {
        id: MISSION_07_PLAYER_SHIP_IDS[0],
        side: 'player',
        position: { x: 0, y: 30 },
        heading: 0,
        speed: 3,
        hp: PLAYER_BASE_HULL_HP,
        sail: 80,
        crew: 50
      },
      {
        id: MISSION_07_PLAYER_SHIP_IDS[1],
        side: 'player',
        position: { x: 0, y: -30 },
        heading: 0,
        speed: 3,
        hp: PLAYER_BASE_HULL_HP,
        sail: 80,
        crew: 50
      },
      // Line abreast bearing down on the player: a sustained gun duel where
      // fire and shredded sails decide the exchange.
      {
        id: MISSION_07_ENEMY_SHIP_IDS[0],
        side: 'enemy',
        position: { x: 220, y: 40 },
        heading: 180,
        speed: 2,
        hp: FRIGATE_HULL_HP,
        sail: FRIGATE_SAIL,
        crew: FRIGATE_CREW
      },
      {
        id: MISSION_07_ENEMY_SHIP_IDS[1],
        side: 'enemy',
        position: { x: 220, y: -40 },
        heading: 180,
        speed: 2,
        hp: FRIGATE_HULL_HP,
        sail: FRIGATE_SAIL,
        crew: FRIGATE_CREW
      }
    ]
  };
}

export interface Mission07Objectives {
  turnLimit: number;
  enemySailScale: number;
  ignitionTarget: number;
}

export interface Mission07StartResponse {
  missionCode: string;
  seed: number;
  turnLimit: number;
  objectives: Mission07Objectives;
  state: SimState;
}

export function mission07StartResponse(seed: number): Mission07StartResponse {
  return {
    missionCode: MISSION_07_CODE,
    seed,
    turnLimit: MISSION_07_TURN_LIMIT,
    objectives: {
      turnLimit: MISSION_07_TURN_LIMIT,
      enemySailScale: MISSION_07_ENEMY_SAIL_SCALE,
      ignitionTarget: MISSION_07_IGNITION_TARGET
    },
    state: createMission07State()
  };
}

// Canonical scenario fingerprint shared with the Unity client
// (Mission07Scenario.Fingerprint) to pin client-server scenario parity.
export function mission07Fingerprint(start: Mission07StartResponse): string {
  const ships = [...start.state.ships]
    .sort((a, b) => a.id.localeCompare(b.id))
    .map(
      (ship) =>
        `${ship.id}:${ship.side}:${ship.position.x},${ship.position.y}:h${ship.heading}:v${ship.speed}:hp${ship.hp}:sl${ship.sail}:cw${ship.crew}`
    );
  return [
    start.missionCode,
    `turnLimit=${start.turnLimit}`,
    `sailScale=${start.objectives.enemySailScale}`,
    `ignitionTarget=${start.objectives.ignitionTarget}`,
    `wind=${start.state.wind.direction}:${start.state.wind.speed}`,
    ...ships
  ].join('|');
}

export function mission07WindForTurn(seed: number, turn: number): Wind {
  const rng = createDeterministicRng((seed ^ 0x7b0) + turn * 131);
  const directionDrift = Math.floor(rng() * 31) - 15;
  const speed = WIND_BASE_SPEED - 1 + Math.floor(rng() * 3);
  return { direction: (WIND_BASE_DIRECTION + directionDrift + 360) % 360, speed };
}

export function mission07EnemyOrders(state: SimState): SimOrder[] {
  const orders: SimOrder[] = [];
  for (const enemyId of MISSION_07_ENEMY_SHIP_IDS) {
    const enemy = state.ships.find((ship) => ship.id === enemyId);
    if (!enemy) {
      continue;
    }
    orders.push(aiOrderFor(enemy, state, 'aggressive'));
  }
  return orders;
}

export interface Mission07Outcome {
  missionCode: string;
  seed: number;
  result: 'win' | 'loss';
  failReason: 'timeout' | 'sunk' | 'flanked' | null;
  turnCount: number;
  turnLimit: number;
  bonusObjectives: {
    enemyIgnited: boolean;
    unscorched: boolean;
  };
  damageProfile: {
    playerHullDamage: number;
    playerHullDamageFraction: number;
    playerRemainingHp: number;
    enemyHullDamage: number;
    enemyRemainingHp: number;
  };
  telemetry: {
    ignitionsInflicted: number;
    ignitionsSuffered: number;
    slowsInflicted: number;
  };
  turns: MissionTurnRecord[];
}

// First mission to accept modifiers.shipUpgrades: optional owned tiers scale
// the player sloops. Resolve trusts the caller; completion validates the
// claimed tiers against the player's owned upgrades before rewards.
export function runMission07(
  seed: number,
  playerTurnOrders: SimOrder[][],
  upgrades?: ShipUpgradeTiers
): Mission07Outcome {
  const run = runMissionLoop(seed, playerTurnOrders, {
    turnLimit: MISSION_07_TURN_LIMIT,
    createState: createMission07State,
    windForTurn: mission07WindForTurn,
    enemyOrders: mission07EnemyOrders,
    modifiers: {
      windMovement: true,
      rakingFire: true,
      statusEffects: true
    },
    ...(upgrades ? { upgrades } : {})
  });

  const { result, turns, turnCount } = run;
  const failReason =
    result === 'win' ? null : classifyLoss(run.finalState, run.playerSunk, FLANKED_SPREAD);

  const enemyStatus = countStatusApplications(turns, MISSION_07_ENEMY_SHIP_IDS);
  const playerStatus = countStatusApplications(turns, MISSION_07_PLAYER_SHIP_IDS);

  const players = run.finalState.ships.filter((ship) => ship.side === 'player');
  const enemies = run.finalState.ships.filter((ship) => ship.side === 'enemy');
  // Hull tiers raise battle-start hp, so the damage baseline must scale with
  // them or upgraded runs would report negative hull damage.
  const playerStartHull = upgrades
    ? upgradedHullHp(PLAYER_BASE_HULL_HP, upgrades.hull)
    : PLAYER_BASE_HULL_HP;
  const playerBaseHull = playerStartHull * MISSION_07_PLAYER_SHIP_IDS.length;
  const playerRemainingHp = players.reduce((sum, ship) => sum + ship.hp, 0);
  const enemyBaseHull = FRIGATE_HULL_HP * MISSION_07_ENEMY_SHIP_IDS.length;
  const enemyRemainingHp = enemies.reduce((sum, ship) => sum + ship.hp, 0);
  const playerHullDamage = playerBaseHull - playerRemainingHp;

  return {
    missionCode: MISSION_07_CODE,
    seed,
    result,
    failReason,
    turnCount,
    turnLimit: MISSION_07_TURN_LIMIT,
    bonusObjectives: {
      enemyIgnited: result === 'win' && enemyStatus.ignitions >= MISSION_07_IGNITION_TARGET,
      unscorched: result === 'win' && playerStatus.ignitions === 0
    },
    damageProfile: {
      playerHullDamage,
      playerHullDamageFraction: Math.round((playerHullDamage / playerBaseHull) * 100) / 100,
      playerRemainingHp,
      enemyHullDamage: enemyBaseHull - enemyRemainingHp,
      enemyRemainingHp
    },
    telemetry: {
      ignitionsInflicted: enemyStatus.ignitions,
      ignitionsSuffered: playerStatus.ignitions,
      slowsInflicted: enemyStatus.slows
    },
    turns
  };
}
