import { aiOrderFor } from './ai.js';
import { createDeterministicRng } from './engine.js';
import { classifyLoss, countRakes, heldWeatherGageOnTurn } from './missionMetrics.js';
import { MissionTurnRecord, runMissionLoop } from './missionRunner.js';
import { SimOrder, SimState, Wind } from './types.js';

// Mission 02 "Weather Gage" — docs/content/missions/mission-02-weather-gage.md
export const MISSION_02_CODE = 'mission-02-weather-gage';
export const MISSION_02_TURN_LIMIT = 9;
export const MISSION_02_BONUS_TURN_TARGET = 7;
export const MISSION_02_UPWIND_BONUS_TURNS = 3;
// Enemy tuning knobs are 1.0x for this mission.
export const MISSION_02_ENEMY_DAMAGE_SCALE = 1;
export const MISSION_02_DEFAULT_SEED = 202;

export const MISSION_02_PLAYER_SHIP_IDS = ['player-sloop-a', 'player-sloop-b'] as const;
export const MISSION_02_ENEMY_SHIP_IDS = ['enemy-aggressor', 'enemy-kite'] as const;

const PLAYER_BASE_HULL_HP = 120;
const ENEMY_BASE_HULL_HP = 120;

// Variable crosswind: blows across the west-east engagement axis with medium
// variance (direction ±15, speed 4..7).
const WIND_BASE_DIRECTION = 90;
const WIND_BASE_SPEED = 5;

// A player is "upwind" (holds the weather gage) on a turn when the wind
// direction points from the player centroid toward the enemy centroid within
// this arc.
const UPWIND_ARC = 60;
// The player counts as flanked when sunk with both enemies alive and their
// bearings from the last player position at least this far apart.
const FLANKED_SPREAD = 120;

export function createMission02State(): SimState {
  return {
    turn: 1,
    wind: { direction: WIND_BASE_DIRECTION, speed: WIND_BASE_SPEED },
    ships: [
      {
        id: MISSION_02_PLAYER_SHIP_IDS[0],
        side: 'player',
        position: { x: 0, y: 30 },
        heading: 0,
        speed: 3,
        hp: PLAYER_BASE_HULL_HP,
        sail: 80,
        crew: 50
      },
      {
        id: MISSION_02_PLAYER_SHIP_IDS[1],
        side: 'player',
        position: { x: 0, y: -30 },
        heading: 0,
        speed: 3,
        hp: PLAYER_BASE_HULL_HP,
        sail: 80,
        crew: 50
      },
      {
        id: MISSION_02_ENEMY_SHIP_IDS[0],
        side: 'enemy',
        position: { x: 170, y: 120 },
        heading: 215,
        speed: 2,
        hp: ENEMY_BASE_HULL_HP,
        sail: 70,
        crew: 40
      },
      {
        id: MISSION_02_ENEMY_SHIP_IDS[1],
        side: 'enemy',
        position: { x: 220, y: 160 },
        heading: 215,
        speed: 2,
        hp: ENEMY_BASE_HULL_HP,
        sail: 70,
        crew: 40
      }
    ],
    obstacles: [{ position: { x: 100, y: 40 }, radius: 25 }]
  };
}

export interface Mission02Objectives {
  turnLimit: number;
  bonusTurnTarget: number;
  upwindBonusTurns: number;
  enemyDamageScale: number;
}

export interface Mission02StartResponse {
  missionCode: string;
  seed: number;
  turnLimit: number;
  objectives: Mission02Objectives;
  state: SimState;
}

export function mission02StartResponse(seed: number): Mission02StartResponse {
  return {
    missionCode: MISSION_02_CODE,
    seed,
    turnLimit: MISSION_02_TURN_LIMIT,
    objectives: {
      turnLimit: MISSION_02_TURN_LIMIT,
      bonusTurnTarget: MISSION_02_BONUS_TURN_TARGET,
      upwindBonusTurns: MISSION_02_UPWIND_BONUS_TURNS,
      enemyDamageScale: MISSION_02_ENEMY_DAMAGE_SCALE
    },
    state: createMission02State()
  };
}

// Canonical scenario fingerprint shared with the Unity client
// (Mission02Scenario.Fingerprint) to pin client-server scenario parity.
export function mission02Fingerprint(start: Mission02StartResponse): string {
  const ships = [...start.state.ships]
    .sort((a, b) => a.id.localeCompare(b.id))
    .map(
      (ship) =>
        `${ship.id}:${ship.side}:${ship.position.x},${ship.position.y}:h${ship.heading}:v${ship.speed}:hp${ship.hp}:sl${ship.sail}:cw${ship.crew}`
    );
  const obstacles = (start.state.obstacles ?? []).map(
    (obstacle) => `island=${obstacle.position.x},${obstacle.position.y}:r${obstacle.radius}`
  );
  return [
    start.missionCode,
    `turnLimit=${start.turnLimit}`,
    `bonusTurns=${start.objectives.bonusTurnTarget}`,
    `upwindTurns=${start.objectives.upwindBonusTurns}`,
    `enemyScale=${start.objectives.enemyDamageScale}`,
    `wind=${start.state.wind.direction}:${start.state.wind.speed}`,
    ...obstacles,
    ...ships
  ].join('|');
}

export function mission02WindForTurn(seed: number, turn: number): Wind {
  const rng = createDeterministicRng((seed ^ 0x2ea) + turn * 131);
  const directionDrift = Math.floor(rng() * 31) - 15;
  const speed = WIND_BASE_SPEED - 1 + Math.floor(rng() * 4);
  return { direction: (WIND_BASE_DIRECTION + directionDrift + 360) % 360, speed };
}

export function mission02EnemyOrders(state: SimState): SimOrder[] {
  const orders: SimOrder[] = [];
  const aggressor = state.ships.find((ship) => ship.id === MISSION_02_ENEMY_SHIP_IDS[0]);
  if (aggressor) {
    orders.push(aiOrderFor(aggressor, state, 'aggressive'));
  }
  const kite = state.ships.find((ship) => ship.id === MISSION_02_ENEMY_SHIP_IDS[1]);
  if (kite) {
    orders.push(aiOrderFor(kite, state, 'kiting'));
  }
  return orders;
}

export interface Mission02Outcome {
  missionCode: string;
  seed: number;
  result: 'win' | 'loss';
  failReason: 'timeout' | 'sunk' | 'flanked' | null;
  turnCount: number;
  turnLimit: number;
  bonusObjectives: {
    heldWeatherGage: boolean;
    withinTurnTarget: boolean;
  };
  damageProfile: {
    playerHullDamage: number;
    playerHullDamageFraction: number;
    playerRemainingHp: number;
    enemyHullDamage: number;
    enemyRemainingHp: number;
  };
  telemetry: {
    rakeAttempts: number;
    rakeHits: number;
    upwindTurns: number;
    upwindByTurn: boolean[];
  };
  turns: MissionTurnRecord[];
}

export function runMission02(seed: number, playerTurnOrders: SimOrder[][]): Mission02Outcome {
  const run = runMissionLoop(seed, playerTurnOrders, {
    turnLimit: MISSION_02_TURN_LIMIT,
    createState: createMission02State,
    windForTurn: mission02WindForTurn,
    enemyOrders: mission02EnemyOrders,
    modifiers: { windMovement: true, rakingFire: true }
  });

  const { result, turns, turnCount } = run;
  const failReason =
    result === 'win' ? null : classifyLoss(run.finalState, run.playerSunk, FLANKED_SPREAD);

  const upwindByTurn = turns.map((turn) =>
    heldWeatherGageOnTurn(
      turn.events,
      mission02WindForTurn(seed, turn.turn),
      MISSION_02_PLAYER_SHIP_IDS,
      MISSION_02_ENEMY_SHIP_IDS,
      UPWIND_ARC
    )
  );
  const upwindTurns = upwindByTurn.filter(Boolean).length;

  const { rakeAttempts, rakeHits } = countRakes(turns, MISSION_02_PLAYER_SHIP_IDS);

  const players = run.finalState.ships.filter((ship) => ship.side === 'player');
  const enemies = run.finalState.ships.filter((ship) => ship.side === 'enemy');
  const playerBaseHull = PLAYER_BASE_HULL_HP * MISSION_02_PLAYER_SHIP_IDS.length;
  const playerRemainingHp = players.reduce((sum, ship) => sum + ship.hp, 0);
  const enemyBaseHull = ENEMY_BASE_HULL_HP * MISSION_02_ENEMY_SHIP_IDS.length;
  const enemyRemainingHp = enemies.reduce((sum, ship) => sum + ship.hp, 0);
  const playerHullDamage = playerBaseHull - playerRemainingHp;

  return {
    missionCode: MISSION_02_CODE,
    seed,
    result,
    failReason,
    turnCount,
    turnLimit: MISSION_02_TURN_LIMIT,
    bonusObjectives: {
      heldWeatherGage: result === 'win' && upwindTurns >= MISSION_02_UPWIND_BONUS_TURNS,
      withinTurnTarget: result === 'win' && turnCount <= MISSION_02_BONUS_TURN_TARGET
    },
    damageProfile: {
      playerHullDamage,
      playerHullDamageFraction: Math.round((playerHullDamage / playerBaseHull) * 100) / 100,
      playerRemainingHp,
      enemyHullDamage: enemyBaseHull - enemyRemainingHp,
      enemyRemainingHp
    },
    telemetry: { rakeAttempts, rakeHits, upwindTurns, upwindByTurn },
    turns
  };
}
