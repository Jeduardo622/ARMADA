import { aiOrderFor, escortOrderFor } from './ai.js';
import { createDeterministicRng } from './engine.js';
import { classifyLoss } from './missionMetrics.js';
import { MissionTurnRecord, runMissionLoop } from './missionRunner.js';
import { SimOrder, SimState, Wind } from './types.js';

// Mission 05 "Line Break" — docs/content/missions/mission-05-line-break.md
export const MISSION_05_CODE = 'mission-05-line-break';
export const MISSION_05_TURN_LIMIT = 11;
export const MISSION_05_BONUS_TURN_TARGET = 9;
export const MISSION_05_FLAGSHIP_HP_SCALE = 1.1;
export const MISSION_05_DEFAULT_SEED = 505;

export const MISSION_05_PLAYER_SHIP_IDS = [
  'player-sloop-a',
  'player-sloop-b',
  'player-sloop-c'
] as const;
export const MISSION_05_FLAGSHIP_ID = 'enemy-flagship';
export const MISSION_05_ESCORT_SHIP_IDS = ['enemy-escort-a', 'enemy-escort-b'] as const;
export const MISSION_05_ENEMY_SHIP_IDS = [
  MISSION_05_FLAGSHIP_ID,
  ...MISSION_05_ESCORT_SHIP_IDS
] as const;

const PLAYER_BASE_HULL_HP = 120;
const FLAGSHIP_BASE_HULL_HP = 180;
const FLAGSHIP_HULL_HP = Math.floor(FLAGSHIP_BASE_HULL_HP * MISSION_05_FLAGSHIP_HP_SCALE);
const ESCORT_HULL_HP = 120;

// Steady wind: tailwind on the player's opening heading, low variance.
const WIND_DIRECTION = 0;
const WIND_BASE_SPEED = 5;

const FLANKED_SPREAD = 120;

export function createMission05State(): SimState {
  return {
    turn: 1,
    wind: { direction: WIND_DIRECTION, speed: WIND_BASE_SPEED },
    ships: [
      {
        id: MISSION_05_PLAYER_SHIP_IDS[0],
        side: 'player',
        position: { x: 0, y: 50 },
        heading: 0,
        speed: 3,
        hp: PLAYER_BASE_HULL_HP,
        sail: 80,
        crew: 50
      },
      {
        id: MISSION_05_PLAYER_SHIP_IDS[1],
        side: 'player',
        position: { x: 0, y: 0 },
        heading: 0,
        speed: 3,
        hp: PLAYER_BASE_HULL_HP,
        sail: 80,
        crew: 50
      },
      {
        id: MISSION_05_PLAYER_SHIP_IDS[2],
        side: 'player',
        position: { x: 0, y: -50 },
        heading: 0,
        speed: 3,
        hp: PLAYER_BASE_HULL_HP,
        sail: 80,
        crew: 50
      },
      {
        id: MISSION_05_FLAGSHIP_ID,
        side: 'enemy',
        position: { x: 260, y: 0 },
        heading: 180,
        speed: 2,
        hp: FLAGSHIP_HULL_HP,
        sail: 90,
        crew: 60
      },
      {
        id: MISSION_05_ESCORT_SHIP_IDS[0],
        side: 'enemy',
        position: { x: 240, y: 60 },
        heading: 180,
        speed: 2,
        hp: ESCORT_HULL_HP,
        sail: 70,
        crew: 40
      },
      {
        id: MISSION_05_ESCORT_SHIP_IDS[1],
        side: 'enemy',
        position: { x: 240, y: -60 },
        heading: 180,
        speed: 2,
        hp: ESCORT_HULL_HP,
        sail: 70,
        crew: 40
      }
    ],
    // Rocks forming a choke on the approach: a 70-unit channel on the axis.
    obstacles: [
      { position: { x: 120, y: 70 }, radius: 35 },
      { position: { x: 120, y: -70 }, radius: 35 }
    ]
  };
}

export interface Mission05Objectives {
  turnLimit: number;
  bonusTurnTarget: number;
  flagshipHpScale: number;
}

export interface Mission05StartResponse {
  missionCode: string;
  seed: number;
  turnLimit: number;
  objectives: Mission05Objectives;
  state: SimState;
}

export function mission05StartResponse(seed: number): Mission05StartResponse {
  return {
    missionCode: MISSION_05_CODE,
    seed,
    turnLimit: MISSION_05_TURN_LIMIT,
    objectives: {
      turnLimit: MISSION_05_TURN_LIMIT,
      bonusTurnTarget: MISSION_05_BONUS_TURN_TARGET,
      flagshipHpScale: MISSION_05_FLAGSHIP_HP_SCALE
    },
    state: createMission05State()
  };
}

// Canonical scenario fingerprint shared with the Unity client
// (Mission05Scenario.Fingerprint) to pin client-server scenario parity.
export function mission05Fingerprint(start: Mission05StartResponse): string {
  const ships = [...start.state.ships]
    .sort((a, b) => a.id.localeCompare(b.id))
    .map(
      (ship) =>
        `${ship.id}:${ship.side}:${ship.position.x},${ship.position.y}:h${ship.heading}:v${ship.speed}:hp${ship.hp}:sl${ship.sail}:cw${ship.crew}`
    );
  const obstacles = (start.state.obstacles ?? []).map(
    (obstacle) => `rock=${obstacle.position.x},${obstacle.position.y}:r${obstacle.radius}`
  );
  return [
    start.missionCode,
    `turnLimit=${start.turnLimit}`,
    `bonusTurns=${start.objectives.bonusTurnTarget}`,
    `flagshipScale=${start.objectives.flagshipHpScale}`,
    `wind=${start.state.wind.direction}:${start.state.wind.speed}`,
    ...obstacles,
    ...ships
  ].join('|');
}

export function mission05WindForTurn(seed: number, turn: number): Wind {
  const rng = createDeterministicRng((seed ^ 0x51b) + turn * 127);
  return { direction: WIND_DIRECTION, speed: WIND_BASE_SPEED - 1 + Math.floor(rng() * 3) };
}

export function mission05EnemyOrders(state: SimState): SimOrder[] {
  const orders: SimOrder[] = [];
  const flagship = state.ships.find((ship) => ship.id === MISSION_05_FLAGSHIP_ID);
  if (flagship) {
    const order = aiOrderFor(flagship, state, 'line-advance');
    // The line holds station once engaged rather than sailing through.
    orders.push(order.action === 'broadside' ? { ...order, speedDelta: -2 } : order);
  }
  for (const escortId of MISSION_05_ESCORT_SHIP_IDS) {
    const escort = state.ships.find((ship) => ship.id === escortId);
    if (escort) {
      const lateral = escortId === MISSION_05_ESCORT_SHIP_IDS[0] ? 60 : -60;
      orders.push(escortOrderFor(escort, state, MISSION_05_FLAGSHIP_ID, { stationLateral: lateral }));
    }
  }
  return orders;
}

// First ship to appear in the cumulative per-turn sunk lists.
export function firstSunkShip(turns: MissionTurnRecord[]): string | null {
  const seen = new Set<string>();
  for (const turn of turns) {
    for (const shipId of turn.summary.sunk) {
      if (!seen.has(shipId)) {
        return shipId;
      }
    }
    for (const shipId of turn.summary.sunk) {
      seen.add(shipId);
    }
  }
  return null;
}

export interface Mission05Outcome {
  missionCode: string;
  seed: number;
  result: 'win' | 'loss';
  failReason: 'timeout' | 'sunk' | 'flanked' | null;
  turnCount: number;
  turnLimit: number;
  bonusObjectives: {
    sankFlagshipFirst: boolean;
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
    firstSinkTarget: string | null;
    chokeBlockedMoves: number;
  };
  turns: MissionTurnRecord[];
}

export function runMission05(seed: number, playerTurnOrders: SimOrder[][]): Mission05Outcome {
  const run = runMissionLoop(seed, playerTurnOrders, {
    turnLimit: MISSION_05_TURN_LIMIT,
    createState: createMission05State,
    windForTurn: mission05WindForTurn,
    enemyOrders: mission05EnemyOrders,
    modifiers: { windMovement: true, rakingFire: true }
  });

  const { result, turns, turnCount } = run;
  const failReason =
    result === 'win' ? null : classifyLoss(run.finalState, run.playerSunk, FLANKED_SPREAD);

  const firstSinkTarget = firstSunkShip(turns);
  let chokeBlockedMoves = 0;
  for (const turn of turns) {
    for (const event of turn.events) {
      if (event.type === 'movement' && event.blocked) {
        chokeBlockedMoves += 1;
      }
    }
  }

  const players = run.finalState.ships.filter((ship) => ship.side === 'player');
  const enemies = run.finalState.ships.filter((ship) => ship.side === 'enemy');
  const playerBaseHull = PLAYER_BASE_HULL_HP * MISSION_05_PLAYER_SHIP_IDS.length;
  const playerRemainingHp = players.reduce((sum, ship) => sum + ship.hp, 0);
  const enemyBaseHull = FLAGSHIP_HULL_HP + ESCORT_HULL_HP * MISSION_05_ESCORT_SHIP_IDS.length;
  const enemyRemainingHp = enemies.reduce((sum, ship) => sum + ship.hp, 0);
  const playerHullDamage = playerBaseHull - playerRemainingHp;

  return {
    missionCode: MISSION_05_CODE,
    seed,
    result,
    failReason,
    turnCount,
    turnLimit: MISSION_05_TURN_LIMIT,
    bonusObjectives: {
      sankFlagshipFirst: result === 'win' && firstSinkTarget === MISSION_05_FLAGSHIP_ID,
      withinTurnTarget: result === 'win' && turnCount <= MISSION_05_BONUS_TURN_TARGET
    },
    damageProfile: {
      playerHullDamage,
      playerHullDamageFraction: Math.round((playerHullDamage / playerBaseHull) * 100) / 100,
      playerRemainingHp,
      enemyHullDamage: enemyBaseHull - enemyRemainingHp,
      enemyRemainingHp
    },
    telemetry: { firstSinkTarget, chokeBlockedMoves },
    turns
  };
}
