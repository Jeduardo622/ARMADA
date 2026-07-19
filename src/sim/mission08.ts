import { aiOrderFor } from './ai.js';
import {
  createDeterministicRng,
  WIND_TURN_DOWNWIND_LIMIT,
  WIND_TURN_UPWIND_LIMIT
} from './engine.js';
import { classifyLoss, countManeuverWindProfile } from './missionMetrics.js';
import { MissionTurnRecord, runMissionLoop } from './missionRunner.js';
import { SimOrder, SimState, Wind } from './types.js';

// Mission 08 "Eye of the Wind" — docs/content/missions/mission-08-eye-of-the-wind.md
// First mission to adopt modifiers.windTurnRate: turning is harder upwind,
// easier downwind.
export const MISSION_08_CODE = 'mission-08-eye-of-the-wind';
export const MISSION_08_TURN_LIMIT = 10;
// Design-tunable placeholder pending the balance pass.
export const MISSION_08_SWIFT_TURN_TARGET = 8;
export const MISSION_08_DEFAULT_SEED = 808;

export const MISSION_08_PLAYER_SHIP_IDS = ['player-sloop-a', 'player-sloop-b'] as const;
export const MISSION_08_ENEMY_SHIP_IDS = ['enemy-corvette-a', 'enemy-corvette-b'] as const;

const PLAYER_HULL_HP = 120;
const CORVETTE_HULL_HP = 150;
const CORVETTE_SAIL = 85;
const CORVETTE_CREW = 55;

// Dead-ahead headwind for the player's opening heading: the fleets close
// bow-to-bow, so the player beats into the wind's eye all the way in while
// the corvettes run free before it.
const WIND_BASE_DIRECTION = 180;
const WIND_BASE_SPEED = 4;

const FLANKED_SPREAD = 120;

export function createMission08State(): SimState {
  return {
    turn: 1,
    wind: { direction: WIND_BASE_DIRECTION, speed: WIND_BASE_SPEED },
    ships: [
      {
        id: MISSION_08_PLAYER_SHIP_IDS[0],
        side: 'player',
        position: { x: 0, y: 30 },
        heading: 0,
        speed: 3,
        hp: PLAYER_HULL_HP,
        sail: 80,
        crew: 50
      },
      {
        id: MISSION_08_PLAYER_SHIP_IDS[1],
        side: 'player',
        position: { x: 0, y: -30 },
        heading: 0,
        speed: 3,
        hp: PLAYER_HULL_HP,
        sail: 80,
        crew: 50
      },
      // Line abreast running downwind at the player: closing is fast for
      // them and slow for the player, so helm discipline in the clamped
      // upwind arc decides the exchange.
      {
        id: MISSION_08_ENEMY_SHIP_IDS[0],
        side: 'enemy',
        position: { x: 240, y: 35 },
        heading: 180,
        speed: 3,
        hp: CORVETTE_HULL_HP,
        sail: CORVETTE_SAIL,
        crew: CORVETTE_CREW
      },
      {
        id: MISSION_08_ENEMY_SHIP_IDS[1],
        side: 'enemy',
        position: { x: 240, y: -35 },
        heading: 180,
        speed: 3,
        hp: CORVETTE_HULL_HP,
        sail: CORVETTE_SAIL,
        crew: CORVETTE_CREW
      }
    ]
  };
}

export interface Mission08Objectives {
  turnLimit: number;
  upwindTurnLimit: number;
  downwindTurnLimit: number;
  swiftTurnTarget: number;
}

export interface Mission08StartResponse {
  missionCode: string;
  seed: number;
  turnLimit: number;
  objectives: Mission08Objectives;
  state: SimState;
}

export function mission08StartResponse(seed: number): Mission08StartResponse {
  return {
    missionCode: MISSION_08_CODE,
    seed,
    turnLimit: MISSION_08_TURN_LIMIT,
    objectives: {
      turnLimit: MISSION_08_TURN_LIMIT,
      upwindTurnLimit: WIND_TURN_UPWIND_LIMIT,
      downwindTurnLimit: WIND_TURN_DOWNWIND_LIMIT,
      swiftTurnTarget: MISSION_08_SWIFT_TURN_TARGET
    },
    state: createMission08State()
  };
}

// Canonical scenario fingerprint shared with the Unity client
// (Mission08Scenario.Fingerprint) to pin client-server scenario parity.
export function mission08Fingerprint(start: Mission08StartResponse): string {
  const ships = [...start.state.ships]
    .sort((a, b) => a.id.localeCompare(b.id))
    .map(
      (ship) =>
        `${ship.id}:${ship.side}:${ship.position.x},${ship.position.y}:h${ship.heading}:v${ship.speed}:hp${ship.hp}:sl${ship.sail}:cw${ship.crew}`
    );
  return [
    start.missionCode,
    `turnLimit=${start.turnLimit}`,
    `upwindLimit=${start.objectives.upwindTurnLimit}`,
    `downwindLimit=${start.objectives.downwindTurnLimit}`,
    `swiftTarget=${start.objectives.swiftTurnTarget}`,
    `wind=${start.state.wind.direction}:${start.state.wind.speed}`,
    ...ships
  ].join('|');
}

export function mission08WindForTurn(seed: number, turn: number): Wind {
  const rng = createDeterministicRng((seed ^ 0x8c8) + turn * 137);
  const directionDrift = Math.floor(rng() * 31) - 15;
  const speed = WIND_BASE_SPEED - 1 + Math.floor(rng() * 3);
  return { direction: (WIND_BASE_DIRECTION + directionDrift + 360) % 360, speed };
}

export function mission08EnemyOrders(state: SimState): SimOrder[] {
  const orders: SimOrder[] = [];
  for (const enemyId of MISSION_08_ENEMY_SHIP_IDS) {
    const enemy = state.ships.find((ship) => ship.id === enemyId);
    if (!enemy) {
      continue;
    }
    orders.push(aiOrderFor(enemy, state, 'aggressive'));
  }
  return orders;
}

export interface Mission08Outcome {
  missionCode: string;
  seed: number;
  result: 'win' | 'loss';
  failReason: 'timeout' | 'sunk' | 'flanked' | null;
  turnCount: number;
  turnLimit: number;
  bonusObjectives: {
    cleanTack: boolean;
    swiftVictory: boolean;
  };
  damageProfile: {
    playerHullDamage: number;
    playerHullDamageFraction: number;
    playerRemainingHp: number;
    enemyHullDamage: number;
    enemyRemainingHp: number;
  };
  telemetry: {
    clampedManeuvers: number;
    upwindManeuvers: number;
    downwindManeuvers: number;
  };
  turns: MissionTurnRecord[];
}

export function runMission08(seed: number, playerTurnOrders: SimOrder[][]): Mission08Outcome {
  const run = runMissionLoop(seed, playerTurnOrders, {
    turnLimit: MISSION_08_TURN_LIMIT,
    createState: createMission08State,
    windForTurn: mission08WindForTurn,
    enemyOrders: mission08EnemyOrders,
    modifiers: {
      windMovement: true,
      rakingFire: true,
      windTurnRate: true
    }
  });

  const { result, turns, turnCount } = run;
  const failReason =
    result === 'win' ? null : classifyLoss(run.finalState, run.playerSunk, FLANKED_SPREAD);

  const maneuvers = countManeuverWindProfile(
    turns,
    playerTurnOrders,
    MISSION_08_PLAYER_SHIP_IDS,
    (turn) => mission08WindForTurn(seed, turn)
  );

  const players = run.finalState.ships.filter((ship) => ship.side === 'player');
  const enemies = run.finalState.ships.filter((ship) => ship.side === 'enemy');
  const playerBaseHull = PLAYER_HULL_HP * MISSION_08_PLAYER_SHIP_IDS.length;
  const playerRemainingHp = players.reduce((sum, ship) => sum + ship.hp, 0);
  const enemyBaseHull = CORVETTE_HULL_HP * MISSION_08_ENEMY_SHIP_IDS.length;
  const enemyRemainingHp = enemies.reduce((sum, ship) => sum + ship.hp, 0);
  const playerHullDamage = playerBaseHull - playerRemainingHp;

  return {
    missionCode: MISSION_08_CODE,
    seed,
    result,
    failReason,
    turnCount,
    turnLimit: MISSION_08_TURN_LIMIT,
    bonusObjectives: {
      cleanTack: result === 'win' && maneuvers.clampedManeuvers === 0,
      swiftVictory: result === 'win' && turnCount <= MISSION_08_SWIFT_TURN_TARGET
    },
    damageProfile: {
      playerHullDamage,
      playerHullDamageFraction: Math.round((playerHullDamage / playerBaseHull) * 100) / 100,
      playerRemainingHp,
      enemyHullDamage: enemyBaseHull - enemyRemainingHp,
      enemyRemainingHp
    },
    telemetry: {
      clampedManeuvers: maneuvers.clampedManeuvers,
      upwindManeuvers: maneuvers.upwindManeuvers,
      downwindManeuvers: maneuvers.downwindManeuvers
    },
    turns
  };
}
