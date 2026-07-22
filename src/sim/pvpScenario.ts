import { SimModifiers, SimOrder, SimState, SimSummary } from './types.js';

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

// The lines face each other mirrored across x = LINE_SEPARATION / 2. The
// wind blows across the battle axis (direction 90), which keeps the mirror
// perfectly fair: a maneuver and its mirrored counterpart sit at the same
// point of sail. Speed 4 matches the mission convention (±2 effective
// speed on the tailwind/headwind arcs; both fleets open at a neutral beam
// reach).
const LINE_SEPARATION = 220;
const LINE_SPREAD = 30;
const WIND_DIRECTION = 90;
const WIND_SPEED = 4;

// Scenario v2 modifier set (pinned; v1 was chain shot only): windMovement
// turns heading and speed into real position, and ramming makes contact
// dangerous — movement-phase contact within RAM_CONTACT_RANGE deals
// speed-scaled hull damage. mutualRamming (the ram balance pass) makes a
// head-on exchange cost both sides equally regardless of resolution
// order: a target under way strikes back with counter-momentum damage
// instead of leaving the rammer with fractional recoil, which removes the
// ship-id first-mover edge in symmetric collisions. Ramming flags are
// only meaningful with windMovement, so the three travel together. A
// fresh object per call so callers can never mutate a shared instance.
export function createPvpModifiers(): SimModifiers {
  return { chainShot: true, mutualRamming: true, ramming: true, windMovement: true };
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
    'modifiers=chainShot,mutualRamming,ramming,windMovement',
    `wind=${state.wind.direction}:${state.wind.speed}`,
    ...ships
  ].join('|');
}

export type PvpMatchResult = 'ongoing' | 'side_a' | 'side_b' | 'draw';

export type PvpEngineSide = 'player' | 'enemy';

// Hot-seat/server fairness guard, defined once here so the slice-2 server
// routes lift a tested helper instead of re-deriving the C# client mirror
// (PvpHotseatFlow.ValidateSideOrders): a side may only order its own living
// ships, and attacks may only target living ships on the opposing side.
export function validateSideOrders(
  orders: SimOrder[],
  state: SimState,
  engineSide: PvpEngineSide
): 'order_side_mismatch' | 'target_side_mismatch' | null {
  const shipById = new Map(state.ships.map((ship) => [ship.id, ship]));
  for (const order of orders) {
    const ship = shipById.get(order.shipId);
    if (!ship || ship.side !== engineSide || ship.hp <= 0) {
      return 'order_side_mismatch';
    }
    if (order.targetShipId !== undefined) {
      const target = shipById.get(order.targetShipId);
      if (!target || target.side === engineSide || target.hp <= 0) {
        return 'target_side_mismatch';
      }
    }
  }
  return null;
}

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
