import { SimModifiers, SimState, SimSummary } from './types.js';

// PvP skirmish "2v2" — the pinned symmetric player-versus-player scenario.
// Side A ships ride the engine's 'player' side and side B the 'enemy' side;
// the engine has no AI hook here, so both sides' orders arrive in the same
// /sim/preview orders array (the mission runner already resolves enemy-side
// orders this way every turn). The fleet is defined once here and
// fingerprint-pinned in tests/pvpScenario.test.ts and the Unity EditMode
// suite (PvpScenario.Fingerprint) so client and server agree on the exact
// deterministic scenario.
export const PVP_SCENARIO_CODE = 'pvp-skirmish-2v2';
export const PVP_TURN_LIMIT = 20;
export const PVP_DEFAULT_SEED = 11;

export const PVP_SIDE_A_SHIP_IDS = ['alpha-frigate-a', 'alpha-frigate-b'] as const;
export const PVP_SIDE_B_SHIP_IDS = ['bravo-frigate-a', 'bravo-frigate-b'] as const;

// Identical frigates on both sides; stats are design-tunable placeholders
// pending the balance pass.
const FRIGATE_HP = 120;
const FRIGATE_SAIL = 80;
const FRIGATE_CREW = 50;
const FRIGATE_SPEED = 3;

// The lines face each other mirrored across x = LINE_SEPARATION / 2. Wind is
// a cross-breeze at zero speed: v1 PvP runs without modifiers.windMovement,
// so wind never touches resolution and the state field stays neutral.
const LINE_SEPARATION = 220;
const LINE_SPREAD = 30;
const WIND_DIRECTION = 90;
const WIND_SPEED = 0;

// v1 PvP modifier set (pinned): chain shot only, everything else off. A
// fresh object per call so callers can never mutate a shared instance.
export function createPvpModifiers(): SimModifiers {
  return { chainShot: true };
}

export function createPvpSkirmishState(): SimState {
  return {
    turn: 1,
    wind: { direction: WIND_DIRECTION, speed: WIND_SPEED },
    ships: [
      {
        id: PVP_SIDE_A_SHIP_IDS[0],
        side: 'player',
        position: { x: 0, y: LINE_SPREAD },
        heading: 0,
        speed: FRIGATE_SPEED,
        hp: FRIGATE_HP,
        sail: FRIGATE_SAIL,
        crew: FRIGATE_CREW
      },
      {
        id: PVP_SIDE_A_SHIP_IDS[1],
        side: 'player',
        position: { x: 0, y: -LINE_SPREAD },
        heading: 0,
        speed: FRIGATE_SPEED,
        hp: FRIGATE_HP,
        sail: FRIGATE_SAIL,
        crew: FRIGATE_CREW
      },
      {
        id: PVP_SIDE_B_SHIP_IDS[0],
        side: 'enemy',
        position: { x: LINE_SEPARATION, y: LINE_SPREAD },
        heading: 180,
        speed: FRIGATE_SPEED,
        hp: FRIGATE_HP,
        sail: FRIGATE_SAIL,
        crew: FRIGATE_CREW
      },
      {
        id: PVP_SIDE_B_SHIP_IDS[1],
        side: 'enemy',
        position: { x: LINE_SEPARATION, y: -LINE_SPREAD },
        heading: 180,
        speed: FRIGATE_SPEED,
        hp: FRIGATE_HP,
        sail: FRIGATE_SAIL,
        crew: FRIGATE_CREW
      }
    ]
  };
}

// Canonical scenario fingerprint shared with the Unity client
// (PvpScenario.Fingerprint) to pin client-server scenario parity. Follows
// the mission fingerprint format plus the pinned modifier set.
export function pvpFingerprint(state: SimState = createPvpSkirmishState()): string {
  const ships = [...state.ships]
    .sort((a, b) => a.id.localeCompare(b.id))
    .map(
      (ship) =>
        `${ship.id}:${ship.side}:${ship.position.x},${ship.position.y}:h${ship.heading}:v${ship.speed}:hp${ship.hp}:sl${ship.sail}:cw${ship.crew}`
    );
  return [
    PVP_SCENARIO_CODE,
    `turnLimit=${PVP_TURN_LIMIT}`,
    'modifiers=chainShot',
    `wind=${state.wind.direction}:${state.wind.speed}`,
    ...ships
  ].join('|');
}

export type PvpMatchResult = 'ongoing' | 'side_a' | 'side_b' | 'draw';

// Match result after a resolved turn. summary counts the engine sides:
// playerRemaining is side A, enemyRemaining is side B. Mutual annihilation
// and hitting the turn limit are both draws.
export function pvpResultForTurn(summary: SimSummary, resolvedTurn: number): PvpMatchResult {
  if (summary.playerRemaining === 0 && summary.enemyRemaining === 0) {
    return 'draw';
  }
  if (summary.enemyRemaining === 0) {
    return 'side_a';
  }
  if (summary.playerRemaining === 0) {
    return 'side_b';
  }
  return resolvedTurn >= PVP_TURN_LIMIT ? 'draw' : 'ongoing';
}
