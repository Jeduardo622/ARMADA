import { aiOrderFor, BossParams, bossOrderFor, bossPhaseIndex } from './ai.js';
import { createDeterministicRng } from './engine.js';
import { classifyLoss } from './missionMetrics.js';
import { MissionTurnRecord, runMissionLoop } from './missionRunner.js';
import { ShipState, SimModifiers, SimOrder, SimState, Wind } from './types.js';

// Mission 06 "Dreadnought Siege" — docs/content/missions/mission-06-dreadnought-siege.md
export const MISSION_06_CODE = 'mission-06-dreadnought-siege';
export const MISSION_06_TURN_LIMIT = 14;
export const MISSION_06_BONUS_TURN_TARGET = 12;
export const MISSION_06_BOSS_HP_SCALE = 1.3;
export const MISSION_06_BOSS_DAMAGE_SCALE = 1.1;
export const MISSION_06_ENRAGE_HULL_FRACTION = 0.3;
export const MISSION_06_ENRAGE_ACCURACY_BONUS = 10;
export const MISSION_06_REINFORCEMENT_TURN = 5;
export const MISSION_06_REINFORCEMENT_HP_SCALE = 0.9;
export const MISSION_06_DEFAULT_SEED = 606;

export const MISSION_06_PLAYER_SHIP_IDS = [
  'player-sloop-a',
  'player-sloop-b',
  'player-sloop-c'
] as const;
export const MISSION_06_BOSS_ID = 'enemy-dreadnought';
export const MISSION_06_REINFORCEMENT_ID = 'enemy-reinforcement';
export const MISSION_06_ENEMY_SHIP_IDS = [
  MISSION_06_BOSS_ID,
  MISSION_06_REINFORCEMENT_ID
] as const;

const PLAYER_BASE_HULL_HP = 120;
const BOSS_BASE_HULL_HP = 360;
const BOSS_HULL_HP = Math.floor(BOSS_BASE_HULL_HP * MISSION_06_BOSS_HP_SCALE);
const REINFORCEMENT_HULL_HP = Math.floor(120 * MISSION_06_REINFORCEMENT_HP_SCALE);

// Shifting wind: opens as a tailwind on the player axis, veers to a
// crosswind mid-fight.
const WIND_OPENING_DIRECTION = 0;
const WIND_SHIFTED_DIRECTION = 90;
const WIND_SHIFT_TURN = 7;
const WIND_BASE_SPEED = 5;

// Boss phases: hold the line while healthy, turn aggressive with a high rake
// bias once wounded.
const BOSS_PHASE2_HULL_ABOVE = 0.6;

const FLANKED_SPREAD = 120;

export function createMission06State(): SimState {
  return {
    turn: 1,
    wind: { direction: WIND_OPENING_DIRECTION, speed: WIND_BASE_SPEED },
    ships: [
      {
        id: MISSION_06_PLAYER_SHIP_IDS[0],
        side: 'player',
        position: { x: 0, y: 50 },
        heading: 0,
        speed: 3,
        hp: PLAYER_BASE_HULL_HP,
        sail: 80,
        crew: 50
      },
      {
        id: MISSION_06_PLAYER_SHIP_IDS[1],
        side: 'player',
        position: { x: 0, y: 0 },
        heading: 0,
        speed: 3,
        hp: PLAYER_BASE_HULL_HP,
        sail: 80,
        crew: 50
      },
      {
        id: MISSION_06_PLAYER_SHIP_IDS[2],
        side: 'player',
        position: { x: 0, y: -50 },
        heading: 0,
        speed: 3,
        hp: PLAYER_BASE_HULL_HP,
        sail: 80,
        crew: 50
      },
      {
        id: MISSION_06_BOSS_ID,
        side: 'enemy',
        position: { x: 280, y: 0 },
        heading: 180,
        speed: 2,
        hp: BOSS_HULL_HP,
        sail: 100,
        crew: 80
      }
    ],
    // Floating debris slows the center of the field.
    slowZones: [{ position: { x: 150, y: 0 }, radius: 50, speedPenalty: 2 }]
  };
}

function reinforcementShip(): ShipState {
  return {
    id: MISSION_06_REINFORCEMENT_ID,
    side: 'enemy',
    position: { x: 300, y: 80 },
    heading: 200,
    speed: 3,
    hp: REINFORCEMENT_HULL_HP,
    sail: 70,
    crew: 40
  };
}

const BOSS_PARAMS: BossParams = {
  baseHull: BOSS_HULL_HP,
  phases: [
    { hullAbove: BOSS_PHASE2_HULL_ABOVE, profile: 'line-advance' },
    { hullAbove: 0, profile: 'aggressive', overrides: { rakeBias: 'high' } }
  ]
};

export interface Mission06Objectives {
  turnLimit: number;
  bonusTurnTarget: number;
  bossHpScale: number;
  bossDamageScale: number;
  enrageHullFraction: number;
  reinforcementTurn: number;
  reinforcementHpScale: number;
}

export interface Mission06StartResponse {
  missionCode: string;
  seed: number;
  turnLimit: number;
  objectives: Mission06Objectives;
  state: SimState;
}

export function mission06StartResponse(seed: number): Mission06StartResponse {
  return {
    missionCode: MISSION_06_CODE,
    seed,
    turnLimit: MISSION_06_TURN_LIMIT,
    objectives: {
      turnLimit: MISSION_06_TURN_LIMIT,
      bonusTurnTarget: MISSION_06_BONUS_TURN_TARGET,
      bossHpScale: MISSION_06_BOSS_HP_SCALE,
      bossDamageScale: MISSION_06_BOSS_DAMAGE_SCALE,
      enrageHullFraction: MISSION_06_ENRAGE_HULL_FRACTION,
      reinforcementTurn: MISSION_06_REINFORCEMENT_TURN,
      reinforcementHpScale: MISSION_06_REINFORCEMENT_HP_SCALE
    },
    state: createMission06State()
  };
}

// Canonical scenario fingerprint shared with the Unity client
// (Mission06Scenario.Fingerprint) to pin client-server scenario parity.
export function mission06Fingerprint(start: Mission06StartResponse): string {
  const ships = [...start.state.ships]
    .sort((a, b) => a.id.localeCompare(b.id))
    .map(
      (ship) =>
        `${ship.id}:${ship.side}:${ship.position.x},${ship.position.y}:h${ship.heading}:v${ship.speed}:hp${ship.hp}:sl${ship.sail}:cw${ship.crew}`
    );
  const debris = (start.state.slowZones ?? []).map(
    (zone) => `debris=${zone.position.x},${zone.position.y}:r${zone.radius}:p${zone.speedPenalty}`
  );
  return [
    start.missionCode,
    `turnLimit=${start.turnLimit}`,
    `bonusTurns=${start.objectives.bonusTurnTarget}`,
    `bossScale=${start.objectives.bossHpScale}`,
    `bossDmg=${start.objectives.bossDamageScale}`,
    `enrage=${start.objectives.enrageHullFraction}`,
    `reinforce=${start.objectives.reinforcementTurn}:${start.objectives.reinforcementHpScale}`,
    `wind=${start.state.wind.direction}:${start.state.wind.speed}`,
    ...debris,
    ...ships
  ].join('|');
}

export function mission06WindForTurn(seed: number, turn: number): Wind {
  const rng = createDeterministicRng((seed ^ 0x60d) + turn * 149);
  const direction = turn < WIND_SHIFT_TURN ? WIND_OPENING_DIRECTION : WIND_SHIFTED_DIRECTION;
  return { direction, speed: WIND_BASE_SPEED - 1 + Math.floor(rng() * 3) };
}

export function mission06OnTurnStart(state: SimState, turn: number): SimState {
  if (
    turn === MISSION_06_REINFORCEMENT_TURN &&
    !state.ships.some((ship) => ship.id === MISSION_06_REINFORCEMENT_ID)
  ) {
    return { ...state, ships: [...state.ships, reinforcementShip()] };
  }
  return state;
}

export function mission06EnemyOrders(state: SimState): SimOrder[] {
  const orders: SimOrder[] = [];
  const boss = state.ships.find((ship) => ship.id === MISSION_06_BOSS_ID);
  if (boss) {
    const order = bossOrderFor(boss, state, BOSS_PARAMS);
    // The dreadnought holds station once its guns bear.
    orders.push(order.action === 'broadside' ? { ...order, speedDelta: -2 } : order);
  }
  const reinforcement = state.ships.find((ship) => ship.id === MISSION_06_REINFORCEMENT_ID);
  if (reinforcement) {
    orders.push(aiOrderFor(reinforcement, state, 'aggressive'));
  }
  return orders;
}

export function mission06Modifiers(state: SimState): SimModifiers {
  const boss = state.ships.find((ship) => ship.id === MISSION_06_BOSS_ID);
  const enraged =
    boss !== undefined &&
    boss.hp > 0 &&
    boss.hp / BOSS_HULL_HP < MISSION_06_ENRAGE_HULL_FRACTION;
  return {
    damageScale: { [MISSION_06_BOSS_ID]: MISSION_06_BOSS_DAMAGE_SCALE },
    windMovement: true,
    rakingFire: true,
    ...(enraged
      ? { accuracyBonus: { [MISSION_06_BOSS_ID]: MISSION_06_ENRAGE_ACCURACY_BONUS } }
      : {})
  };
}

interface BossTurnStatus {
  turn: number;
  phase: number;
  enraged: boolean;
}

// Boss hull entering each turn, reconstructed from the event stream, drives
// the phase/enrage telemetry exactly as it drove order generation.
function bossStatusByTurn(turns: MissionTurnRecord[]): BossTurnStatus[] {
  const statuses: BossTurnStatus[] = [];
  let hull = BOSS_HULL_HP;
  for (const turn of turns) {
    statuses.push({
      turn: turn.turn,
      phase: bossPhaseIndex(hull, BOSS_PARAMS) + 1,
      enraged: hull > 0 && hull / BOSS_HULL_HP < MISSION_06_ENRAGE_HULL_FRACTION
    });
    for (const event of turn.events) {
      if (
        (event.type === 'broadside' || event.type === 'boarding') &&
        event.targetShipId === MISSION_06_BOSS_ID
      ) {
        hull = event.targetRemaining.hp;
      }
    }
  }
  return statuses;
}

export interface Mission06Outcome {
  missionCode: string;
  seed: number;
  result: 'win' | 'loss';
  failReason: 'timeout' | 'sunk' | 'flanked' | null;
  turnCount: number;
  turnLimit: number;
  bonusObjectives: {
    noShipLost: boolean;
    withinTurnTarget: boolean;
  };
  damageProfile: {
    playerHullDamage: number;
    playerHullDamageFraction: number;
    playerRemainingHp: number;
    enemyHullDamage: number;
    enemyRemainingHp: number;
    bossHullDamage: number;
    bossRemainingHp: number;
  };
  telemetry: {
    phaseTransitions: { turn: number; phase: number }[];
    enragedOnTurn: number | null;
    reinforcementTurn: number | null;
    reinforcementDamageDealt: number;
  };
  turns: MissionTurnRecord[];
}

export function runMission06(seed: number, playerTurnOrders: SimOrder[][]): Mission06Outcome {
  const run = runMissionLoop(seed, playerTurnOrders, {
    turnLimit: MISSION_06_TURN_LIMIT,
    createState: createMission06State,
    windForTurn: mission06WindForTurn,
    enemyOrders: mission06EnemyOrders,
    modifiers: mission06Modifiers,
    onTurnStart: mission06OnTurnStart
  });

  const { result, turns, turnCount } = run;
  const failReason =
    result === 'win' ? null : classifyLoss(run.finalState, run.playerSunk, FLANKED_SPREAD);

  const statuses = bossStatusByTurn(turns);
  const phaseTransitions: { turn: number; phase: number }[] = [];
  let lastPhase = 0;
  for (const status of statuses) {
    if (status.phase !== lastPhase) {
      phaseTransitions.push({ turn: status.turn, phase: status.phase });
      lastPhase = status.phase;
    }
  }
  const enragedOnTurn = statuses.find((status) => status.enraged)?.turn ?? null;

  const spawned = turns.length >= MISSION_06_REINFORCEMENT_TURN;
  let reinforcementDamageDealt = 0;
  for (const turn of turns) {
    for (const event of turn.events) {
      if (event.type === 'broadside' && event.shipId === MISSION_06_REINFORCEMENT_ID) {
        reinforcementDamageDealt += event.damage.hull;
      }
    }
  }

  const players = run.finalState.ships.filter((ship) => ship.side === 'player');
  const enemies = run.finalState.ships.filter((ship) => ship.side === 'enemy');
  const boss = run.finalState.ships.find((ship) => ship.id === MISSION_06_BOSS_ID);
  const playerBaseHull = PLAYER_BASE_HULL_HP * MISSION_06_PLAYER_SHIP_IDS.length;
  const playerRemainingHp = players.reduce((sum, ship) => sum + ship.hp, 0);
  const enemyBaseHull =
    BOSS_HULL_HP + (spawned ? REINFORCEMENT_HULL_HP : 0);
  const enemyRemainingHp = enemies.reduce((sum, ship) => sum + ship.hp, 0);
  const playerHullDamage = playerBaseHull - playerRemainingHp;
  const bossRemainingHp = boss?.hp ?? 0;

  return {
    missionCode: MISSION_06_CODE,
    seed,
    result,
    failReason,
    turnCount,
    turnLimit: MISSION_06_TURN_LIMIT,
    bonusObjectives: {
      noShipLost: result === 'win' && players.every((ship) => ship.hp > 0),
      withinTurnTarget: result === 'win' && turnCount <= MISSION_06_BONUS_TURN_TARGET
    },
    damageProfile: {
      playerHullDamage,
      playerHullDamageFraction: Math.round((playerHullDamage / playerBaseHull) * 100) / 100,
      playerRemainingHp,
      enemyHullDamage: enemyBaseHull - enemyRemainingHp,
      enemyRemainingHp,
      bossHullDamage: BOSS_HULL_HP - bossRemainingHp,
      bossRemainingHp
    },
    telemetry: {
      phaseTransitions,
      enragedOnTurn,
      reinforcementTurn: spawned ? MISSION_06_REINFORCEMENT_TURN : null,
      reinforcementDamageDealt
    },
    turns
  };
}
