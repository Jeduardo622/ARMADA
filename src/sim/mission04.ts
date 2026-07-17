import { aiOrderFor } from './ai.js';
import { createDeterministicRng } from './engine.js';
import { classifyLoss, countBoardings } from './missionMetrics.js';
import { MissionTurnRecord, runMissionLoop } from './missionRunner.js';
import { SimOrder, SimState, Wind } from './types.js';

// Mission 04 "Boarding Party" — docs/content/missions/mission-04-boarding-party.md
export const MISSION_04_CODE = 'mission-04-boarding-party';
export const MISSION_04_TURN_LIMIT = 10;
export const MISSION_04_ENEMY_CREW_SCALE = 0.9;
export const MISSION_04_PLAYER_BOARDING_BONUS = 0.1;
export const MISSION_04_DEFAULT_SEED = 404;

export const MISSION_04_PLAYER_SHIP_IDS = ['player-sloop-a', 'player-sloop-b'] as const;
export const MISSION_04_ENEMY_SHIP_IDS = ['enemy-frigate-a', 'enemy-frigate-b'] as const;

const PLAYER_BASE_HULL_HP = 120;
// Enemy hull is 1.0x; the crew tuning knob is 0.9x.
const FRIGATE_HULL_HP = 180;
const FRIGATE_BASE_CREW = 60;
const FRIGATE_CREW = Math.floor(FRIGATE_BASE_CREW * MISSION_04_ENEMY_CREW_SCALE);

// Light headwind against the player's opening heading, medium variance.
const WIND_BASE_DIRECTION = 180;
const WIND_BASE_SPEED = 3;

const FLANKED_SPREAD = 120;

export function createMission04State(): SimState {
  return {
    turn: 1,
    wind: { direction: WIND_BASE_DIRECTION, speed: WIND_BASE_SPEED },
    ships: [
      {
        id: MISSION_04_PLAYER_SHIP_IDS[0],
        side: 'player',
        position: { x: 0, y: 30 },
        heading: 0,
        speed: 3,
        hp: PLAYER_BASE_HULL_HP,
        sail: 80,
        crew: 50
      },
      {
        id: MISSION_04_PLAYER_SHIP_IDS[1],
        side: 'player',
        position: { x: 0, y: -30 },
        heading: 0,
        speed: 3,
        hp: PLAYER_BASE_HULL_HP,
        sail: 80,
        crew: 50
      },
      // Line-advance pair closing in column; holding the line leaves their
      // flanks exposed as they pass ("will expose flanks" per the spec).
      {
        id: MISSION_04_ENEMY_SHIP_IDS[0],
        side: 'enemy',
        position: { x: 220, y: 40 },
        heading: 180,
        speed: 2,
        hp: FRIGATE_HULL_HP,
        sail: 90,
        crew: FRIGATE_CREW
      },
      {
        id: MISSION_04_ENEMY_SHIP_IDS[1],
        side: 'enemy',
        position: { x: 260, y: -40 },
        heading: 180,
        speed: 2,
        hp: FRIGATE_HULL_HP,
        sail: 90,
        crew: FRIGATE_CREW
      }
    ],
    // Debris field mid-approach: ships crossing it around turns 3-5 lose way.
    slowZones: [{ position: { x: 130, y: 0 }, radius: 45, speedPenalty: 2 }]
  };
}

export interface Mission04Objectives {
  turnLimit: number;
  enemyCrewScale: number;
  playerBoardingBonus: number;
}

export interface Mission04StartResponse {
  missionCode: string;
  seed: number;
  turnLimit: number;
  objectives: Mission04Objectives;
  state: SimState;
}

export function mission04StartResponse(seed: number): Mission04StartResponse {
  return {
    missionCode: MISSION_04_CODE,
    seed,
    turnLimit: MISSION_04_TURN_LIMIT,
    objectives: {
      turnLimit: MISSION_04_TURN_LIMIT,
      enemyCrewScale: MISSION_04_ENEMY_CREW_SCALE,
      playerBoardingBonus: MISSION_04_PLAYER_BOARDING_BONUS
    },
    state: createMission04State()
  };
}

// Canonical scenario fingerprint shared with the Unity client
// (Mission04Scenario.Fingerprint) to pin client-server scenario parity.
export function mission04Fingerprint(start: Mission04StartResponse): string {
  const ships = [...start.state.ships]
    .sort((a, b) => a.id.localeCompare(b.id))
    .map(
      (ship) =>
        `${ship.id}:${ship.side}:${ship.position.x},${ship.position.y}:h${ship.heading}:v${ship.speed}:hp${ship.hp}:sl${ship.sail}:cw${ship.crew}`
    );
  const slowZones = (start.state.slowZones ?? []).map(
    (zone) => `debris=${zone.position.x},${zone.position.y}:r${zone.radius}:p${zone.speedPenalty}`
  );
  return [
    start.missionCode,
    `turnLimit=${start.turnLimit}`,
    `crewScale=${start.objectives.enemyCrewScale}`,
    `boardBonus=${start.objectives.playerBoardingBonus}`,
    `wind=${start.state.wind.direction}:${start.state.wind.speed}`,
    ...slowZones,
    ...ships
  ].join('|');
}

export function mission04WindForTurn(seed: number, turn: number): Wind {
  const rng = createDeterministicRng((seed ^ 0x4b0) + turn * 113);
  const directionDrift = Math.floor(rng() * 31) - 15;
  const speed = WIND_BASE_SPEED - 1 + Math.floor(rng() * 3);
  return { direction: (WIND_BASE_DIRECTION + directionDrift + 360) % 360, speed };
}

export function mission04EnemyOrders(state: SimState): SimOrder[] {
  const orders: SimOrder[] = [];
  for (const enemyId of MISSION_04_ENEMY_SHIP_IDS) {
    const enemy = state.ships.find((ship) => ship.id === enemyId);
    if (!enemy) {
      continue;
    }
    const order = aiOrderFor(enemy, state, 'line-advance');
    if (order.action === 'broadside') {
      // Heave to for the gun duel: the line holds station once engaged,
      // which is what leaves its flanks exposed to boarders.
      orders.push({ ...order, speedDelta: -2 });
    } else {
      orders.push(order);
    }
  }
  return orders;
}

export interface Mission04Outcome {
  missionCode: string;
  seed: number;
  result: 'win' | 'loss';
  failReason: 'timeout' | 'sunk' | 'flanked' | null;
  turnCount: number;
  turnLimit: number;
  bonusObjectives: {
    successfulBoarding: boolean;
    noShipLost: boolean;
  };
  damageProfile: {
    playerHullDamage: number;
    playerHullDamageFraction: number;
    playerRemainingHp: number;
    enemyHullDamage: number;
    enemyRemainingHp: number;
  };
  telemetry: {
    boardingAttempts: number;
    boardingSuccesses: number;
  };
  turns: MissionTurnRecord[];
}

export function runMission04(seed: number, playerTurnOrders: SimOrder[][]): Mission04Outcome {
  const run = runMissionLoop(seed, playerTurnOrders, {
    turnLimit: MISSION_04_TURN_LIMIT,
    createState: createMission04State,
    windForTurn: mission04WindForTurn,
    enemyOrders: mission04EnemyOrders,
    modifiers: {
      windMovement: true,
      rakingFire: true,
      boardingBonus: {
        [MISSION_04_PLAYER_SHIP_IDS[0]]: MISSION_04_PLAYER_BOARDING_BONUS,
        [MISSION_04_PLAYER_SHIP_IDS[1]]: MISSION_04_PLAYER_BOARDING_BONUS
      }
    }
  });

  const { result, turns, turnCount } = run;
  const failReason =
    result === 'win' ? null : classifyLoss(run.finalState, run.playerSunk, FLANKED_SPREAD);

  const { boardingAttempts, boardingSuccesses } = countBoardings(
    turns,
    MISSION_04_PLAYER_SHIP_IDS
  );

  const players = run.finalState.ships.filter((ship) => ship.side === 'player');
  const enemies = run.finalState.ships.filter((ship) => ship.side === 'enemy');
  const playerBaseHull = PLAYER_BASE_HULL_HP * MISSION_04_PLAYER_SHIP_IDS.length;
  const playerRemainingHp = players.reduce((sum, ship) => sum + ship.hp, 0);
  const enemyBaseHull = FRIGATE_HULL_HP * MISSION_04_ENEMY_SHIP_IDS.length;
  const enemyRemainingHp = enemies.reduce((sum, ship) => sum + ship.hp, 0);
  const playerHullDamage = playerBaseHull - playerRemainingHp;
  const noShipLost = players.every((ship) => ship.hp > 0);

  return {
    missionCode: MISSION_04_CODE,
    seed,
    result,
    failReason,
    turnCount,
    turnLimit: MISSION_04_TURN_LIMIT,
    bonusObjectives: {
      successfulBoarding: result === 'win' && boardingSuccesses > 0,
      noShipLost: result === 'win' && noShipLost
    },
    damageProfile: {
      playerHullDamage,
      playerHullDamageFraction: Math.round((playerHullDamage / playerBaseHull) * 100) / 100,
      playerRemainingHp,
      enemyHullDamage: enemyBaseHull - enemyRemainingHp,
      enemyRemainingHp
    },
    telemetry: { boardingAttempts, boardingSuccesses },
    turns
  };
}
