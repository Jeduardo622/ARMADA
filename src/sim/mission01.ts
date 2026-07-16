import { createDeterministicRng, resolveSimPreview } from './engine.js';
import { SimEvent, SimOrder, SimState, SimSummary } from './types.js';

// Mission 01 "Fair Wind" — docs/content/missions/mission-01-fair-wind.md
export const MISSION_01_CODE = 'mission-01-fair-wind';
export const MISSION_01_TURN_LIMIT = 8;
export const MISSION_01_BONUS_TURN_TARGET = 6;
export const MISSION_01_BONUS_HULL_DAMAGE_FRACTION = 0.2;
export const MISSION_01_ENEMY_DAMAGE_SCALE = 0.9;
export const MISSION_01_DEFAULT_SEED = 101;

export const MISSION_01_PLAYER_SHIP_ID = 'player-sloop';
export const MISSION_01_ENEMY_SHIP_ID = 'enemy-sloop';

const PLAYER_BASE_HULL_HP = 120;
const ENEMY_BASE_HULL_HP = 120;
const ENEMY_HULL_HP = Math.floor(ENEMY_BASE_HULL_HP * MISSION_01_ENEMY_DAMAGE_SCALE);

// Steady tailwind aligned with the player heading; variance stays low (±1).
const WIND_DIRECTION = 0;
const WIND_BASE_SPEED = 5;

// Line-advance profile: hold the line, advance steadily, then stay
// broadside-aligned (docs/content/ai-profiles.md).
const LINE_ADVANCE_TURNS = 2;

export function createMission01State(): SimState {
  return {
    turn: 1,
    wind: { direction: WIND_DIRECTION, speed: WIND_BASE_SPEED },
    ships: [
      {
        id: MISSION_01_PLAYER_SHIP_ID,
        side: 'player',
        position: { x: 0, y: 0 },
        heading: 0,
        speed: 3,
        hp: PLAYER_BASE_HULL_HP,
        sail: 80,
        crew: 50
      },
      {
        id: MISSION_01_ENEMY_SHIP_ID,
        side: 'enemy',
        position: { x: 150, y: 0 },
        heading: 180,
        speed: 2,
        hp: ENEMY_HULL_HP,
        sail: 70,
        crew: 40
      }
    ]
  };
}

export interface Mission01Objectives {
  turnLimit: number;
  bonusTurnTarget: number;
  bonusHullDamageFraction: number;
  enemyDamageScale: number;
}

export interface Mission01StartResponse {
  missionCode: string;
  seed: number;
  turnLimit: number;
  objectives: Mission01Objectives;
  state: SimState;
}

export function mission01StartResponse(seed: number): Mission01StartResponse {
  return {
    missionCode: MISSION_01_CODE,
    seed,
    turnLimit: MISSION_01_TURN_LIMIT,
    objectives: {
      turnLimit: MISSION_01_TURN_LIMIT,
      bonusTurnTarget: MISSION_01_BONUS_TURN_TARGET,
      bonusHullDamageFraction: MISSION_01_BONUS_HULL_DAMAGE_FRACTION,
      enemyDamageScale: MISSION_01_ENEMY_DAMAGE_SCALE
    },
    state: createMission01State()
  };
}

// Canonical scenario fingerprint shared with the Unity client
// (Mission01Scenario.Fingerprint) to pin client-server scenario parity.
export function mission01Fingerprint(start: Mission01StartResponse): string {
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
    `bonusHull=${start.objectives.bonusHullDamageFraction}`,
    `enemyScale=${start.objectives.enemyDamageScale}`,
    `wind=${start.state.wind.direction}:${start.state.wind.speed}`,
    ...ships
  ].join('|');
}

export function mission01EnemyOrder(state: SimState, turn: number): SimOrder {
  const enemy = state.ships.find((ship) => ship.id === MISSION_01_ENEMY_SHIP_ID);
  const player = state.ships.find((ship) => ship.id === MISSION_01_PLAYER_SHIP_ID);
  if (!enemy || enemy.hp <= 0 || !player || player.hp <= 0) {
    return { shipId: MISSION_01_ENEMY_SHIP_ID, action: 'pass', turnDelta: 0, speedDelta: 0 };
  }

  if (turn <= LINE_ADVANCE_TURNS) {
    return { shipId: enemy.id, action: 'maneuver', turnDelta: 0, speedDelta: 1 };
  }

  return {
    shipId: enemy.id,
    action: 'broadside',
    targetShipId: player.id,
    side: 'port',
    turnDelta: 0,
    speedDelta: 0
  };
}

function windSpeedForTurn(seed: number, turn: number): number {
  const rng = createDeterministicRng((seed ^ 0x5eed) + turn * 101);
  return WIND_BASE_SPEED - 1 + Math.floor(rng() * 3);
}

export interface Mission01TurnRecord {
  turn: number;
  hash: string;
  summary: SimSummary;
  events: SimEvent[];
}

export interface Mission01Outcome {
  missionCode: string;
  seed: number;
  result: 'win' | 'loss';
  failReason: 'timeout' | 'sunk' | null;
  turnCount: number;
  turnLimit: number;
  bonusObjectives: {
    underHullDamageThreshold: boolean;
    withinTurnTarget: boolean;
  };
  damageProfile: {
    playerHullDamage: number;
    playerHullDamageFraction: number;
    playerRemainingHp: number;
    enemyHullDamage: number;
    enemyRemainingHp: number;
  };
  turns: Mission01TurnRecord[];
}

export function runMission01(seed: number, playerTurnOrders: SimOrder[][]): Mission01Outcome {
  let state = createMission01State();
  const turns: Mission01TurnRecord[] = [];
  let result: 'win' | 'loss' = 'loss';
  let failReason: 'timeout' | 'sunk' | null = 'timeout';
  let turnCount = MISSION_01_TURN_LIMIT;

  for (let turn = 1; turn <= MISSION_01_TURN_LIMIT; turn++) {
    const turnState: SimState = {
      ...state,
      turn,
      wind: { direction: WIND_DIRECTION, speed: windSpeedForTurn(seed, turn) }
    };
    const orders = [...(playerTurnOrders[turn - 1] ?? []), mission01EnemyOrder(turnState, turn)];
    const preview = resolveSimPreview({
      schemaVersion: 1,
      seed,
      turn,
      state: turnState,
      orders,
      modifiers: { damageScale: { [MISSION_01_ENEMY_SHIP_ID]: MISSION_01_ENEMY_DAMAGE_SCALE } }
    });

    turns.push({ turn, hash: preview.hash, summary: preview.summary, events: preview.events });
    state = preview.nextState;

    if (preview.summary.enemyRemaining === 0) {
      result = 'win';
      failReason = null;
      turnCount = turn;
      break;
    }
    if (preview.summary.playerRemaining === 0) {
      result = 'loss';
      failReason = 'sunk';
      turnCount = turn;
      break;
    }
  }

  const player = state.ships.find((ship) => ship.id === MISSION_01_PLAYER_SHIP_ID);
  const enemy = state.ships.find((ship) => ship.id === MISSION_01_ENEMY_SHIP_ID);
  const playerHullDamage = PLAYER_BASE_HULL_HP - (player?.hp ?? 0);
  const playerHullDamageFraction = Math.round((playerHullDamage / PLAYER_BASE_HULL_HP) * 100) / 100;

  return {
    missionCode: MISSION_01_CODE,
    seed,
    result,
    failReason,
    turnCount,
    turnLimit: MISSION_01_TURN_LIMIT,
    bonusObjectives: {
      underHullDamageThreshold:
        result === 'win' &&
        playerHullDamage < PLAYER_BASE_HULL_HP * MISSION_01_BONUS_HULL_DAMAGE_FRACTION,
      withinTurnTarget: result === 'win' && turnCount <= MISSION_01_BONUS_TURN_TARGET
    },
    damageProfile: {
      playerHullDamage,
      playerHullDamageFraction,
      playerRemainingHp: player?.hp ?? 0,
      enemyHullDamage: ENEMY_HULL_HP - (enemy?.hp ?? 0),
      enemyRemainingHp: enemy?.hp ?? 0
    },
    turns
  };
}
