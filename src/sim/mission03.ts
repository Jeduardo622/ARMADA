import { aiOrderFor } from './ai.js';
import { createDeterministicRng } from './engine.js';
import { classifyLoss, countBoardings, countRakes } from './missionMetrics.js';
import { MissionTurnRecord, runMissionLoop } from './missionRunner.js';
import { SimOrder, SimState, Wind } from './types.js';

// Mission 03 "Raking Shot" — docs/content/missions/mission-03-raking-shot.md
export const MISSION_03_CODE = 'mission-03-raking-shot';
export const MISSION_03_TURN_LIMIT = 10;
export const MISSION_03_BONUS_TURN_TARGET = 8;
export const MISSION_03_RAKE_HIT_TARGET = 2;
export const MISSION_03_ENEMY_DAMAGE_SCALE = 1.05;
export const MISSION_03_DEFAULT_SEED = 303;

export const MISSION_03_PLAYER_SHIP_IDS = ['player-sloop-a', 'player-sloop-b'] as const;
export const MISSION_03_ENEMY_SHIP_IDS = ['enemy-frigate', 'enemy-sloop'] as const;

const PLAYER_BASE_HULL_HP = 120;
const FRIGATE_BASE_HULL_HP = 180;
const SLOOP_BASE_HULL_HP = 120;
const FRIGATE_HULL_HP = Math.floor(FRIGATE_BASE_HULL_HP * MISSION_03_ENEMY_DAMAGE_SCALE);
const SLOOP_HULL_HP = Math.floor(SLOOP_BASE_HULL_HP * MISSION_03_ENEMY_DAMAGE_SCALE);

// Gentle side wind across the west-east engagement axis: fixed direction,
// low speed variance (2..4).
const WIND_DIRECTION = 90;
const WIND_BASE_SPEED = 3;

const FLANKED_SPREAD = 120;

export function createMission03State(): SimState {
  return {
    turn: 1,
    wind: { direction: WIND_DIRECTION, speed: WIND_BASE_SPEED },
    ships: [
      {
        id: MISSION_03_PLAYER_SHIP_IDS[0],
        side: 'player',
        position: { x: 0, y: 30 },
        heading: 0,
        speed: 3,
        hp: PLAYER_BASE_HULL_HP,
        sail: 80,
        crew: 50
      },
      {
        id: MISSION_03_PLAYER_SHIP_IDS[1],
        side: 'player',
        position: { x: 0, y: -30 },
        heading: 0,
        speed: 3,
        hp: PLAYER_BASE_HULL_HP,
        sail: 80,
        crew: 50
      },
      // Pincer setup: the pair closes from opposite flanks ("attempts to
      // flank" per the spec) with the aggressive profile doing the closing.
      {
        id: MISSION_03_ENEMY_SHIP_IDS[0],
        side: 'enemy',
        position: { x: 200, y: 90 },
        heading: 205,
        speed: 2,
        hp: FRIGATE_HULL_HP,
        sail: 90,
        crew: 60
      },
      {
        id: MISSION_03_ENEMY_SHIP_IDS[1],
        side: 'enemy',
        position: { x: 200, y: -90 },
        heading: 155,
        speed: 3,
        hp: SLOOP_HULL_HP,
        sail: 70,
        crew: 40
      }
    ]
  };
}

export interface Mission03Objectives {
  turnLimit: number;
  bonusTurnTarget: number;
  rakeHitTarget: number;
  enemyDamageScale: number;
}

export interface Mission03StartResponse {
  missionCode: string;
  seed: number;
  turnLimit: number;
  objectives: Mission03Objectives;
  state: SimState;
}

export function mission03StartResponse(seed: number): Mission03StartResponse {
  return {
    missionCode: MISSION_03_CODE,
    seed,
    turnLimit: MISSION_03_TURN_LIMIT,
    objectives: {
      turnLimit: MISSION_03_TURN_LIMIT,
      bonusTurnTarget: MISSION_03_BONUS_TURN_TARGET,
      rakeHitTarget: MISSION_03_RAKE_HIT_TARGET,
      enemyDamageScale: MISSION_03_ENEMY_DAMAGE_SCALE
    },
    state: createMission03State()
  };
}

// Canonical scenario fingerprint shared with the Unity client
// (Mission03Scenario.Fingerprint) to pin client-server scenario parity.
export function mission03Fingerprint(start: Mission03StartResponse): string {
  const ships = [...start.state.ships]
    .sort((a, b) => a.id.localeCompare(b.id))
    .map(
      (ship) =>
        `${ship.id}:${ship.side}:${ship.position.x},${ship.position.y}:h${ship.heading}:v${ship.speed}:hp${ship.hp}:sl${ship.sail}:cw${ship.crew}`
    );
  return [
    start.missionCode,
    `turnLimit=${start.turnLimit}`,
    `bonusTurns=${start.objectives.bonusTurnTarget}`,
    `rakeTarget=${start.objectives.rakeHitTarget}`,
    `enemyScale=${start.objectives.enemyDamageScale}`,
    `wind=${start.state.wind.direction}:${start.state.wind.speed}`,
    ...ships
  ].join('|');
}

export function mission03WindForTurn(seed: number, turn: number): Wind {
  const rng = createDeterministicRng((seed ^ 0x3a7) + turn * 97);
  return { direction: WIND_DIRECTION, speed: WIND_BASE_SPEED - 1 + Math.floor(rng() * 3) };
}

export function mission03EnemyOrders(state: SimState): SimOrder[] {
  const orders: SimOrder[] = [];
  for (const enemyId of MISSION_03_ENEMY_SHIP_IDS) {
    const enemy = state.ships.find((ship) => ship.id === enemyId);
    if (enemy) {
      orders.push(aiOrderFor(enemy, state, 'aggressive'));
    }
  }
  return orders;
}

export interface Mission03ShipDamage {
  shipId: string;
  hullDamage: number;
  remainingHp: number;
}

export interface Mission03Outcome {
  missionCode: string;
  seed: number;
  result: 'win' | 'loss';
  failReason: 'timeout' | 'sunk' | 'flanked' | null;
  turnCount: number;
  turnLimit: number;
  bonusObjectives: {
    landedRakingHits: boolean;
    withinTurnTarget: boolean;
  };
  damageProfile: {
    playerHullDamage: number;
    playerHullDamageFraction: number;
    playerRemainingHp: number;
    enemyHullDamage: number;
    enemyRemainingHp: number;
    perShip: Mission03ShipDamage[];
  };
  telemetry: {
    rakeAttempts: number;
    rakeHits: number;
    boardingAttempts: number;
    boardingSuccesses: number;
  };
  turns: MissionTurnRecord[];
}

export function runMission03(seed: number, playerTurnOrders: SimOrder[][]): Mission03Outcome {
  const run = runMissionLoop(seed, playerTurnOrders, {
    turnLimit: MISSION_03_TURN_LIMIT,
    createState: createMission03State,
    windForTurn: mission03WindForTurn,
    enemyOrders: mission03EnemyOrders,
    modifiers: {
      damageScale: {
        [MISSION_03_ENEMY_SHIP_IDS[0]]: MISSION_03_ENEMY_DAMAGE_SCALE,
        [MISSION_03_ENEMY_SHIP_IDS[1]]: MISSION_03_ENEMY_DAMAGE_SCALE
      },
      windMovement: true,
      rakingFire: true
    }
  });

  const { result, turns, turnCount } = run;
  const failReason =
    result === 'win' ? null : classifyLoss(run.finalState, run.playerSunk, FLANKED_SPREAD);

  const { rakeAttempts, rakeHits } = countRakes(turns, MISSION_03_PLAYER_SHIP_IDS);

  const { boardingAttempts, boardingSuccesses } = countBoardings(
    turns,
    MISSION_03_PLAYER_SHIP_IDS
  );

  const initialByShip = new Map(
    createMission03State().ships.map((ship) => [ship.id, ship.hp] as const)
  );
  const perShip: Mission03ShipDamage[] = run.finalState.ships.map((ship) => ({
    shipId: ship.id,
    hullDamage: (initialByShip.get(ship.id) ?? 0) - ship.hp,
    remainingHp: ship.hp
  }));

  const players = run.finalState.ships.filter((ship) => ship.side === 'player');
  const enemies = run.finalState.ships.filter((ship) => ship.side === 'enemy');
  const playerBaseHull = PLAYER_BASE_HULL_HP * MISSION_03_PLAYER_SHIP_IDS.length;
  const playerRemainingHp = players.reduce((sum, ship) => sum + ship.hp, 0);
  const enemyBaseHull = FRIGATE_HULL_HP + SLOOP_HULL_HP;
  const enemyRemainingHp = enemies.reduce((sum, ship) => sum + ship.hp, 0);
  const playerHullDamage = playerBaseHull - playerRemainingHp;

  return {
    missionCode: MISSION_03_CODE,
    seed,
    result,
    failReason,
    turnCount,
    turnLimit: MISSION_03_TURN_LIMIT,
    bonusObjectives: {
      landedRakingHits: result === 'win' && rakeHits >= MISSION_03_RAKE_HIT_TARGET,
      withinTurnTarget: result === 'win' && turnCount <= MISSION_03_BONUS_TURN_TARGET
    },
    damageProfile: {
      playerHullDamage,
      playerHullDamageFraction: Math.round((playerHullDamage / playerBaseHull) * 100) / 100,
      playerRemainingHp,
      enemyHullDamage: enemyBaseHull - enemyRemainingHp,
      enemyRemainingHp,
      perShip
    },
    telemetry: { rakeAttempts, rakeHits, boardingAttempts, boardingSuccesses },
    turns
  };
}
