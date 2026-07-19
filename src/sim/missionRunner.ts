import { resolveSimPreview } from './engine.js';
import {
  SimEvent,
  SimModifiers,
  SimOrder,
  SimState,
  SimSummary,
  ShipUpgradeTiers,
  Wind
} from './types.js';

// Mission-generic turn loop shared by mission scenarios. Scenarios supply the
// initial state, per-turn wind, enemy order generation, and modifiers;
// fail-reason classification, bonuses, and telemetry stay mission-specific.
export interface MissionTurnRecord {
  turn: number;
  hash: string;
  summary: SimSummary;
  events: SimEvent[];
}

export interface MissionRunConfig {
  turnLimit: number;
  createState: () => SimState;
  windForTurn: (seed: number, turn: number) => Wind;
  enemyOrders: (state: SimState) => SimOrder[];
  // Static modifiers, or a per-turn function for state-dependent effects
  // (e.g. a boss enrage below a hull threshold).
  modifiers: SimModifiers | ((state: SimState, turn: number) => SimModifiers);
  // Optional pre-turn hook for scripted events such as reinforcement spawns;
  // returns the (possibly extended) state used for the turn.
  onTurnStart?: (state: SimState, turn: number) => SimState;
  // Optional player upgrade tiers; when present the loop opts in to
  // modifiers.shipUpgrades so the engine scales player-side ships (the hull
  // bonus applies on turn 1 only and carries forward through the chain).
  upgrades?: ShipUpgradeTiers;
}

export interface MissionRunResult {
  result: 'win' | 'loss';
  playerSunk: boolean;
  turnCount: number;
  turns: MissionTurnRecord[];
  finalState: SimState;
}

export function runMissionLoop(
  seed: number,
  playerTurnOrders: SimOrder[][],
  config: MissionRunConfig
): MissionRunResult {
  let state = config.createState();
  const turns: MissionTurnRecord[] = [];
  let result: 'win' | 'loss' = 'loss';
  let playerSunk = false;
  let turnCount = config.turnLimit;

  for (let turn = 1; turn <= config.turnLimit; turn++) {
    if (config.onTurnStart) {
      state = config.onTurnStart(state, turn);
    }
    const turnState: SimState = {
      ...state,
      turn,
      wind: config.windForTurn(seed, turn)
    };
    const orders = [...(playerTurnOrders[turn - 1] ?? []), ...config.enemyOrders(turnState)];
    const modifiers =
      typeof config.modifiers === 'function'
        ? config.modifiers(turnState, turn)
        : config.modifiers;
    const preview = resolveSimPreview({
      schemaVersion: 1,
      seed,
      turn,
      state: turnState,
      orders,
      // Without upgrades the request stays byte-identical to the legacy
      // shape, preserving every pinned turn hash.
      modifiers: config.upgrades ? { ...modifiers, shipUpgrades: true } : modifiers,
      ...(config.upgrades ? { upgrades: config.upgrades } : {})
    });

    turns.push({ turn, hash: preview.hash, summary: preview.summary, events: preview.events });
    state = preview.nextState;

    if (preview.summary.enemyRemaining === 0) {
      result = 'win';
      turnCount = turn;
      break;
    }
    if (preview.summary.playerRemaining === 0) {
      result = 'loss';
      playerSunk = true;
      turnCount = turn;
      break;
    }
  }

  return { result, playerSunk, turnCount, turns, finalState: state };
}
