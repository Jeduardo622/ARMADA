import { aiOrderFor } from './ai.js';
import { createDeterministicRng, RAM_CONTACT_RANGE } from './engine.js';
import { classifyLoss, countRamProfile } from './missionMetrics.js';
import { MissionTurnRecord, runMissionLoop } from './missionRunner.js';
import { SimOrder, SimState, Wind } from './types.js';

// Mission 09 "Iron Bow" — docs/content/missions/mission-09-iron-bow.md
// First mission to adopt modifiers.ramming: movement-phase hull contact
// deals speed-scaled ram damage to both ships.
export const MISSION_09_CODE = 'mission-09-iron-bow';
export const MISSION_09_TURN_LIMIT = 10;
// Design-tunable placeholder pending the balance pass.
export const MISSION_09_RAM_TARGET = 2;
export const MISSION_09_DEFAULT_SEED = 909;

export const MISSION_09_PLAYER_SHIP_IDS = ['player-sloop-a', 'player-sloop-b'] as const;
export const MISSION_09_ENEMY_SHIP_IDS = ['enemy-brig-a', 'enemy-brig-b'] as const;

const PLAYER_HULL_HP = 120;
const BRIG_HULL_HP = 160;
const BRIG_SAIL = 85;
const BRIG_CREW = 55;

// Dead-astern tailwind for the player's opening heading: the fleets close
// bow-to-bow with the player running free before the wind, so the sloops
// carry ramming speed into the contact while the brigs claw upwind.
const WIND_BASE_DIRECTION = 0;
const WIND_BASE_SPEED = 4;

const FLANKED_SPREAD = 120;

export function createMission09State(): SimState {
  return {
    turn: 1,
    wind: { direction: WIND_BASE_DIRECTION, speed: WIND_BASE_SPEED },
    ships: [
      {
        id: MISSION_09_PLAYER_SHIP_IDS[0],
        side: 'player',
        position: { x: 0, y: 30 },
        heading: 0,
        speed: 3,
        hp: PLAYER_HULL_HP,
        sail: 80,
        crew: 50
      },
      {
        id: MISSION_09_PLAYER_SHIP_IDS[1],
        side: 'player',
        position: { x: 0, y: -30 },
        heading: 0,
        speed: 3,
        hp: PLAYER_HULL_HP,
        sail: 80,
        crew: 50
      },
      // Heavy-hulled brigs in line abreast beating into the wind at the
      // player: their approach is slow, so the closing speed — and the ram
      // exchange when the lines meet — belongs to the player.
      {
        id: MISSION_09_ENEMY_SHIP_IDS[0],
        side: 'enemy',
        position: { x: 220, y: 35 },
        heading: 180,
        speed: 3,
        hp: BRIG_HULL_HP,
        sail: BRIG_SAIL,
        crew: BRIG_CREW
      },
      {
        id: MISSION_09_ENEMY_SHIP_IDS[1],
        side: 'enemy',
        position: { x: 220, y: -35 },
        heading: 180,
        speed: 3,
        hp: BRIG_HULL_HP,
        sail: BRIG_SAIL,
        crew: BRIG_CREW
      }
    ]
  };
}

export interface Mission09Objectives {
  turnLimit: number;
  ramContactRange: number;
  ramTarget: number;
}

export interface Mission09StartResponse {
  missionCode: string;
  seed: number;
  turnLimit: number;
  objectives: Mission09Objectives;
  state: SimState;
}

export function mission09StartResponse(seed: number): Mission09StartResponse {
  return {
    missionCode: MISSION_09_CODE,
    seed,
    turnLimit: MISSION_09_TURN_LIMIT,
    objectives: {
      turnLimit: MISSION_09_TURN_LIMIT,
      ramContactRange: RAM_CONTACT_RANGE,
      ramTarget: MISSION_09_RAM_TARGET
    },
    state: createMission09State()
  };
}

// Canonical scenario fingerprint shared with the Unity client
// (Mission09Scenario.Fingerprint) to pin client-server scenario parity.
export function mission09Fingerprint(start: Mission09StartResponse): string {
  const ships = [...start.state.ships]
    .sort((a, b) => a.id.localeCompare(b.id))
    .map(
      (ship) =>
        `${ship.id}:${ship.side}:${ship.position.x},${ship.position.y}:h${ship.heading}:v${ship.speed}:hp${ship.hp}:sl${ship.sail}:cw${ship.crew}`
    );
  return [
    start.missionCode,
    `turnLimit=${start.turnLimit}`,
    `ramRange=${start.objectives.ramContactRange}`,
    `ramTarget=${start.objectives.ramTarget}`,
    `wind=${start.state.wind.direction}:${start.state.wind.speed}`,
    ...ships
  ].join('|');
}

export function mission09WindForTurn(seed: number, turn: number): Wind {
  const rng = createDeterministicRng((seed ^ 0x9c9) + turn * 149);
  const directionDrift = Math.floor(rng() * 31) - 15;
  const speed = WIND_BASE_SPEED - 1 + Math.floor(rng() * 3);
  return { direction: (WIND_BASE_DIRECTION + directionDrift + 360) % 360, speed };
}

export function mission09EnemyOrders(state: SimState): SimOrder[] {
  const orders: SimOrder[] = [];
  for (const enemyId of MISSION_09_ENEMY_SHIP_IDS) {
    const enemy = state.ships.find((ship) => ship.id === enemyId);
    if (!enemy) {
      continue;
    }
    orders.push(aiOrderFor(enemy, state, 'aggressive'));
  }
  return orders;
}

export interface Mission09Outcome {
  missionCode: string;
  seed: number;
  result: 'win' | 'loss';
  failReason: 'timeout' | 'sunk' | 'flanked' | null;
  turnCount: number;
  turnLimit: number;
  bonusObjectives: {
    hullBreaker: boolean;
    unrammed: boolean;
  };
  damageProfile: {
    playerHullDamage: number;
    playerHullDamageFraction: number;
    playerRemainingHp: number;
    enemyHullDamage: number;
    enemyRemainingHp: number;
  };
  telemetry: {
    ramsInflicted: number;
    ramsSuffered: number;
    ramHullDamageDealt: number;
    ramHullDamageTaken: number;
  };
  turns: MissionTurnRecord[];
}

export function runMission09(seed: number, playerTurnOrders: SimOrder[][]): Mission09Outcome {
  const run = runMissionLoop(seed, playerTurnOrders, {
    turnLimit: MISSION_09_TURN_LIMIT,
    createState: createMission09State,
    windForTurn: mission09WindForTurn,
    enemyOrders: mission09EnemyOrders,
    modifiers: {
      windMovement: true,
      rakingFire: true,
      ramming: true
    }
  });

  const { result, turns, turnCount } = run;
  const failReason =
    result === 'win' ? null : classifyLoss(run.finalState, run.playerSunk, FLANKED_SPREAD);

  const rams = countRamProfile(turns, MISSION_09_PLAYER_SHIP_IDS);

  const players = run.finalState.ships.filter((ship) => ship.side === 'player');
  const enemies = run.finalState.ships.filter((ship) => ship.side === 'enemy');
  const playerBaseHull = PLAYER_HULL_HP * MISSION_09_PLAYER_SHIP_IDS.length;
  const playerRemainingHp = players.reduce((sum, ship) => sum + ship.hp, 0);
  const enemyBaseHull = BRIG_HULL_HP * MISSION_09_ENEMY_SHIP_IDS.length;
  const enemyRemainingHp = enemies.reduce((sum, ship) => sum + ship.hp, 0);
  const playerHullDamage = playerBaseHull - playerRemainingHp;

  return {
    missionCode: MISSION_09_CODE,
    seed,
    result,
    failReason,
    turnCount,
    turnLimit: MISSION_09_TURN_LIMIT,
    bonusObjectives: {
      hullBreaker: result === 'win' && rams.ramsInflicted >= MISSION_09_RAM_TARGET,
      unrammed: result === 'win' && rams.ramsSuffered === 0
    },
    damageProfile: {
      playerHullDamage,
      playerHullDamageFraction: Math.round((playerHullDamage / playerBaseHull) * 100) / 100,
      playerRemainingHp,
      enemyHullDamage: enemyBaseHull - enemyRemainingHp,
      enemyRemainingHp
    },
    telemetry: {
      ramsInflicted: rams.ramsInflicted,
      ramsSuffered: rams.ramsSuffered,
      ramHullDamageDealt: rams.ramHullDamageDealt,
      ramHullDamageTaken: rams.ramHullDamageTaken
    },
    turns
  };
}
