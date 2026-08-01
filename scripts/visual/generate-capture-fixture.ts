/**
 * Generates the committed visual-capture fixture: a fully resolved
 * pvp-skirmish-2v2 battle replayed with the same seed and order strategy as
 * the pinned focus-fire-vs-split fixture in tests/pvpScenario.test.ts
 * (seed 11, side A focus fire, side B split fire — a side A win at turn 6).
 * The output is server-wire-shaped JSON (camelCase) that the Unity capture
 * tool (unity/Assets/Editor/VisualCapture/SpectatorVisualCapture.cs)
 * deserializes straight into the client DTOs and feeds to the real
 * SpectatorRenderer.
 *
 * Deterministic end to end: same engine, same seed, same orders — rerunning
 * this script must produce a byte-identical fixture unless the sim itself
 * changed, in which case the guard below fails loudly instead of silently
 * re-deriving different baselines.
 *
 * Run: npx tsx scripts/visual/generate-capture-fixture.ts
 */
import { mkdirSync, writeFileSync } from 'node:fs';
import { dirname, resolve } from 'node:path';
import { fileURLToPath } from 'node:url';
import { resolveSimPreview } from '../../src/sim/engine.js';
import {
  PVP_DEFAULT_SEED,
  PVP_SCENARIO_CODE,
  PVP_SIDE_A_SHIP_IDS,
  PVP_SIDE_B_SHIP_IDS,
  PVP_TURN_LIMIT,
  createPvpModifiers,
  createPvpSkirmishState,
  pvpResultForTurn
} from '../../src/sim/pvpScenario.js';
import type { SimOrder, SimState } from '../../src/sim/types.js';

const root = resolve(dirname(fileURLToPath(import.meta.url)), '../..');
const outputPath = resolve(
  root,
  'unity/Assets/Editor/VisualCapture/Fixtures/pvp-seed11-focus-fire.json'
);

// Mirrors the fixture pin in tests/pvpScenario.test.ts; if the sim moves,
// this guard breaks before a drifted fixture can be written.
// Re-derived for the D1-A broadside-arc curve (was 7).
const PINNED_FOCUS_FIRE_TURN = 6;

const isAfloat = (state: SimState, id: string) =>
  (state.ships.find((ship) => ship.id === id)?.hp ?? 0) > 0;

const firstAfloat = (state: SimState, ids: readonly string[]) =>
  ids.find((id) => isAfloat(state, id)) ?? ids[0];

const fire = (shipId: string, target: string): SimOrder => ({
  shipId,
  action: 'broadside',
  targetShipId: target,
  side: 'starboard',
  turnDelta: 0,
  speedDelta: 0
});

const focusFireSideA = (state: SimState): SimOrder[] =>
  PVP_SIDE_A_SHIP_IDS.filter((id) => isAfloat(state, id)).map((id) =>
    fire(id, firstAfloat(state, PVP_SIDE_B_SHIP_IDS))
  );

const splitFireSideB = (state: SimState): SimOrder[] =>
  PVP_SIDE_B_SHIP_IDS.map((id, index) => ({ id, index }))
    .filter(({ id }) => isAfloat(state, id))
    .map(({ id, index }) => {
      const paired = PVP_SIDE_A_SHIP_IDS[index];
      return fire(id, isAfloat(state, paired) ? paired : firstAfloat(state, PVP_SIDE_A_SHIP_IDS));
    });

const shipsAtStart = createPvpSkirmishState().ships;

let state = createPvpSkirmishState();
const turns = [];
let result: ReturnType<typeof pvpResultForTurn> = 'ongoing';
let turn = 1;
for (; turn <= PVP_TURN_LIMIT; turn++) {
  const preview = resolveSimPreview({
    schemaVersion: 1,
    seed: PVP_DEFAULT_SEED,
    turn,
    state: { ...state, turn },
    orders: [...focusFireSideA(state), ...splitFireSideB(state)],
    modifiers: createPvpModifiers()
  });
  turns.push({
    turn: preview.turn,
    hash: preview.hash,
    summary: preview.summary,
    events: preview.events
  });
  state = preview.nextState;
  result = pvpResultForTurn(preview.summary, turn);
  if (result !== 'ongoing') {
    break;
  }
}

if (result !== 'side_a' || turn !== PINNED_FOCUS_FIRE_TURN) {
  console.error(
    `[generate-capture-fixture] fixture drift: expected side_a at turn ${PINNED_FOCUS_FIRE_TURN}, ` +
      `got ${result} at turn ${turn}. The sim moved; re-derive baselines deliberately.`
  );
  process.exit(1);
}

const fixture = {
  scenario: PVP_SCENARIO_CODE,
  seed: PVP_DEFAULT_SEED,
  turnLimit: PVP_TURN_LIMIT,
  wind: createPvpSkirmishState().wind,
  description:
    'Pinned focus-fire-vs-split fixture battle (tests/pvpScenario.test.ts): side A win at turn 6.',
  shipsAtStart,
  turns
};

mkdirSync(dirname(outputPath), { recursive: true });
writeFileSync(outputPath, `${JSON.stringify(fixture, null, 2)}\n`);
console.log(
  `[generate-capture-fixture] wrote ${outputPath} (${turns.length} turns, result ${result})`
);
