import { aiOrderFor } from './ai.js';
import {
  CHAIN_SHOT_CREW_PERCENT,
  CHAIN_SHOT_HULL_PERCENT,
  CHAIN_SHOT_SAIL_PERCENT,
  createDeterministicRng
} from './engine.js';
import { classifyLoss, countAmmoProfile } from './missionMetrics.js';
import { MissionTurnRecord, runMissionLoop } from './missionRunner.js';
import { SimOrder, SimState, Wind } from './types.js';

// Mission 10 "Sail-Cutter" — docs/content/missions/mission-10-sail-cutter.md
// First mission to adopt modifiers.chainShot: broadside orders may select
// ammo 'chain' to trade hull damage for heavy sail/rigging damage.
export const MISSION_10_CODE = 'mission-10-sail-cutter';
export const MISSION_10_TURN_LIMIT = 10;
// Design-tunable placeholder pending the balance pass.
export const MISSION_10_CHAIN_SAIL_TARGET = 60;
export const MISSION_10_DEFAULT_SEED = 1010;

export const MISSION_10_PLAYER_SHIP_IDS = ['player-sloop-a', 'player-sloop-b'] as const;
export const MISSION_10_ENEMY_SHIP_IDS = ['enemy-clipper-a', 'enemy-clipper-b'] as const;

const PLAYER_HULL_HP = 120;
const CLIPPER_HULL_HP = 140;
// Tall-rigged clippers: their broadside weight rides on that sail area, so
// chain shot into the rigging blunts their guns while round shot sinks them.
const CLIPPER_SAIL = 110;
const CLIPPER_CREW = 50;

// Dead-astern tailwind for the player's opening heading: the sloops run
// free at the clipper line while the clippers beat upwind, keeping the
// early gunnery exchange at bow-rake geometry for both ammo loads.
const WIND_BASE_DIRECTION = 0;
const WIND_BASE_SPEED = 4;

const FLANKED_SPREAD = 120;

export function createMission10State(): SimState {
  return {
    turn: 1,
    wind: { direction: WIND_BASE_DIRECTION, speed: WIND_BASE_SPEED },
    ships: [
      {
        id: MISSION_10_PLAYER_SHIP_IDS[0],
        side: 'player',
        position: { x: 0, y: 30 },
        heading: 0,
        speed: 3,
        hp: PLAYER_HULL_HP,
        sail: 80,
        crew: 50
      },
      {
        id: MISSION_10_PLAYER_SHIP_IDS[1],
        side: 'player',
        position: { x: 0, y: -30 },
        heading: 0,
        speed: 3,
        hp: PLAYER_HULL_HP,
        sail: 80,
        crew: 50
      },
      {
        id: MISSION_10_ENEMY_SHIP_IDS[0],
        side: 'enemy',
        position: { x: 220, y: 35 },
        heading: 180,
        speed: 3,
        hp: CLIPPER_HULL_HP,
        sail: CLIPPER_SAIL,
        crew: CLIPPER_CREW
      },
      {
        id: MISSION_10_ENEMY_SHIP_IDS[1],
        side: 'enemy',
        position: { x: 220, y: -35 },
        heading: 180,
        speed: 3,
        hp: CLIPPER_HULL_HP,
        sail: CLIPPER_SAIL,
        crew: CLIPPER_CREW
      }
    ]
  };
}

export interface Mission10Objectives {
  turnLimit: number;
  chainHullPercent: number;
  chainSailPercent: number;
  chainCrewPercent: number;
  chainSailTarget: number;
}

export interface Mission10StartResponse {
  missionCode: string;
  seed: number;
  turnLimit: number;
  objectives: Mission10Objectives;
  state: SimState;
}

export function mission10StartResponse(seed: number): Mission10StartResponse {
  return {
    missionCode: MISSION_10_CODE,
    seed,
    turnLimit: MISSION_10_TURN_LIMIT,
    objectives: {
      turnLimit: MISSION_10_TURN_LIMIT,
      chainHullPercent: CHAIN_SHOT_HULL_PERCENT,
      chainSailPercent: CHAIN_SHOT_SAIL_PERCENT,
      chainCrewPercent: CHAIN_SHOT_CREW_PERCENT,
      chainSailTarget: MISSION_10_CHAIN_SAIL_TARGET
    },
    state: createMission10State()
  };
}

// Canonical scenario fingerprint shared with the Unity client
// (Mission10Scenario.Fingerprint) to pin client-server scenario parity.
export function mission10Fingerprint(start: Mission10StartResponse): string {
  const ships = [...start.state.ships]
    .sort((a, b) => a.id.localeCompare(b.id))
    .map(
      (ship) =>
        `${ship.id}:${ship.side}:${ship.position.x},${ship.position.y}:h${ship.heading}:v${ship.speed}:hp${ship.hp}:sl${ship.sail}:cw${ship.crew}`
    );
  return [
    start.missionCode,
    `turnLimit=${start.turnLimit}`,
    `chainHull=${start.objectives.chainHullPercent}`,
    `chainSail=${start.objectives.chainSailPercent}`,
    `chainCrew=${start.objectives.chainCrewPercent}`,
    `sailTarget=${start.objectives.chainSailTarget}`,
    `wind=${start.state.wind.direction}:${start.state.wind.speed}`,
    ...ships
  ].join('|');
}

export function mission10WindForTurn(seed: number, turn: number): Wind {
  const rng = createDeterministicRng((seed ^ 0xa2a) + turn * 151);
  const directionDrift = Math.floor(rng() * 31) - 15;
  const speed = WIND_BASE_SPEED - 1 + Math.floor(rng() * 3);
  return { direction: (WIND_BASE_DIRECTION + directionDrift + 360) % 360, speed };
}

export function mission10EnemyOrders(state: SimState): SimOrder[] {
  const orders: SimOrder[] = [];
  for (const enemyId of MISSION_10_ENEMY_SHIP_IDS) {
    const enemy = state.ships.find((ship) => ship.id === enemyId);
    if (!enemy) {
      continue;
    }
    // AI clippers carry only round shot; the ammo choice is the player's.
    orders.push(aiOrderFor(enemy, state, 'aggressive'));
  }
  return orders;
}

export interface Mission10Outcome {
  missionCode: string;
  seed: number;
  result: 'win' | 'loss';
  failReason: 'timeout' | 'sunk' | 'flanked' | null;
  turnCount: number;
  turnLimit: number;
  bonusObjectives: {
    sailShredder: boolean;
    mixedBattery: boolean;
  };
  damageProfile: {
    playerHullDamage: number;
    playerHullDamageFraction: number;
    playerRemainingHp: number;
    enemyHullDamage: number;
    enemyRemainingHp: number;
  };
  telemetry: {
    chainShotOrders: number;
    chainShotHits: number;
    roundShotHits: number;
    chainSailDamageDealt: number;
  };
  turns: MissionTurnRecord[];
}

export function runMission10(seed: number, playerTurnOrders: SimOrder[][]): Mission10Outcome {
  const run = runMissionLoop(seed, playerTurnOrders, {
    turnLimit: MISSION_10_TURN_LIMIT,
    createState: createMission10State,
    windForTurn: mission10WindForTurn,
    enemyOrders: mission10EnemyOrders,
    modifiers: {
      windMovement: true,
      rakingFire: true,
      chainShot: true
    }
  });

  const { result, turns, turnCount } = run;
  const failReason =
    result === 'win' ? null : classifyLoss(run.finalState, run.playerSunk, FLANKED_SPREAD);

  const ammo = countAmmoProfile(turns, playerTurnOrders, MISSION_10_PLAYER_SHIP_IDS);

  const players = run.finalState.ships.filter((ship) => ship.side === 'player');
  const enemies = run.finalState.ships.filter((ship) => ship.side === 'enemy');
  const playerBaseHull = PLAYER_HULL_HP * MISSION_10_PLAYER_SHIP_IDS.length;
  const playerRemainingHp = players.reduce((sum, ship) => sum + ship.hp, 0);
  const enemyBaseHull = CLIPPER_HULL_HP * MISSION_10_ENEMY_SHIP_IDS.length;
  const enemyRemainingHp = enemies.reduce((sum, ship) => sum + ship.hp, 0);
  const playerHullDamage = playerBaseHull - playerRemainingHp;

  return {
    missionCode: MISSION_10_CODE,
    seed,
    result,
    failReason,
    turnCount,
    turnLimit: MISSION_10_TURN_LIMIT,
    bonusObjectives: {
      sailShredder: result === 'win' && ammo.chainSailDamageDealt >= MISSION_10_CHAIN_SAIL_TARGET,
      mixedBattery: result === 'win' && ammo.chainShotHits >= 1 && ammo.roundShotHits >= 1
    },
    damageProfile: {
      playerHullDamage,
      playerHullDamageFraction: Math.round((playerHullDamage / playerBaseHull) * 100) / 100,
      playerRemainingHp,
      enemyHullDamage: enemyBaseHull - enemyRemainingHp,
      enemyRemainingHp
    },
    telemetry: {
      chainShotOrders: ammo.chainShotOrders,
      chainShotHits: ammo.chainShotHits,
      roundShotHits: ammo.roundShotHits,
      chainSailDamageDealt: ammo.chainSailDamageDealt
    },
    turns
  };
}
