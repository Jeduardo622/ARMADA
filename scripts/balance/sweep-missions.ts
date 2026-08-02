// Mission 03-06 balance sweep: re-runs the mission-balance.md 200-seed
// sweeps (seeds 1-200) against the current engine so the doc's measured
// numbers can be refreshed after engine-wide changes (e.g. the D1-A
// broadside-arc accuracy curve). Methodology mirrors the original
// mission-balance arc probes: canonical scripted strategies per mission
// plus a passive (all-pass) baseline, run through runMission0X.
//
// Usage: npx tsx scripts/balance/sweep-missions.ts
// Read-only with respect to the sim: no fixture, tuning, or engine change.

import {
  MISSION_03_ENEMY_SHIP_IDS,
  MISSION_03_PLAYER_SHIP_IDS,
  MISSION_03_TURN_LIMIT,
  runMission03
} from '../../src/sim/mission03.js';
import {
  MISSION_04_ENEMY_SHIP_IDS,
  MISSION_04_PLAYER_SHIP_IDS,
  MISSION_04_TURN_LIMIT,
  runMission04
} from '../../src/sim/mission04.js';
import {
  MISSION_05_ESCORT_SHIP_IDS,
  MISSION_05_FLAGSHIP_ID,
  MISSION_05_PLAYER_SHIP_IDS,
  MISSION_05_TURN_LIMIT,
  runMission05
} from '../../src/sim/mission05.js';
import {
  MISSION_06_BOSS_ID,
  MISSION_06_PLAYER_SHIP_IDS,
  MISSION_06_REINFORCEMENT_ID,
  MISSION_06_TURN_LIMIT,
  runMission06
} from '../../src/sim/mission06.js';
import type { SimOrder } from '../../src/sim/types.js';

const SEEDS = Array.from({ length: 200 }, (_, i) => i + 1);

const fire = (shipId: string, target: string, slow = 0): SimOrder => ({
  shipId,
  action: 'broadside',
  targetShipId: target,
  side: 'starboard',
  turnDelta: 0,
  speedDelta: slow
});
const board = (shipId: string, target: string, slow = 0): SimOrder => ({
  shipId,
  action: 'boarding',
  targetShipId: target,
  turnDelta: 0,
  speedDelta: slow
});
const passOrders = (ids: readonly string[], turnLimit: number): SimOrder[][] =>
  Array.from({ length: turnLimit }, () =>
    ids.map((id) => ({ shipId: id, action: 'pass' }) as SimOrder)
  );

// --- Canonical strategies, copied verbatim from the pinned vitest suites ---

// Mission 03 sloop-first (tests/mission03.test.ts).
const m03SloopFirst: SimOrder[][] = Array.from({ length: MISSION_03_TURN_LIMIT }, (_, i) => {
  const target = i < 4 ? MISSION_03_ENEMY_SHIP_IDS[1] : MISSION_03_ENEMY_SHIP_IDS[0];
  return [
    fire(MISSION_03_PLAYER_SHIP_IDS[0], target),
    fire(MISSION_03_PLAYER_SHIP_IDS[1], target)
  ];
});

// Mission 04 parallel boarding + pure gunnery (tests/mission04.test.ts).
const m04Parallel: SimOrder[][] = Array.from({ length: MISSION_04_TURN_LIMIT }, (_, i) => {
  if (i < 3) {
    return [
      fire(MISSION_04_PLAYER_SHIP_IDS[0], MISSION_04_ENEMY_SHIP_IDS[0]),
      fire(MISSION_04_PLAYER_SHIP_IDS[1], MISSION_04_ENEMY_SHIP_IDS[1])
    ];
  }
  return [
    board(MISSION_04_PLAYER_SHIP_IDS[0], MISSION_04_ENEMY_SHIP_IDS[0], -2),
    board(MISSION_04_PLAYER_SHIP_IDS[1], MISSION_04_ENEMY_SHIP_IDS[1], -2)
  ];
});
const m04Gunnery: SimOrder[][] = Array.from({ length: MISSION_04_TURN_LIMIT }, (_, i) => {
  const target = i < 5 ? MISSION_04_ENEMY_SHIP_IDS[0] : MISSION_04_ENEMY_SHIP_IDS[1];
  const slow = i >= 3 ? -2 : 0;
  return [
    fire(MISSION_04_PLAYER_SHIP_IDS[0], target, slow),
    fire(MISSION_04_PLAYER_SHIP_IDS[1], target, slow)
  ];
});

// Mission 05 line-break (tests/mission05.test.ts).
const m05LineBreak: SimOrder[][] = Array.from({ length: MISSION_05_TURN_LIMIT }, (_, i) => {
  const slow = i >= 3 ? -2 : 0;
  const target =
    i < 4
      ? MISSION_05_FLAGSHIP_ID
      : i < 6
        ? MISSION_05_ESCORT_SHIP_IDS[0]
        : MISSION_05_ESCORT_SHIP_IDS[1];
  return MISSION_05_PLAYER_SHIP_IDS.map((id) => fire(id, target, slow));
});

// Mission 06 siege variants (tests/mission06.test.ts).
function m06Siege(reinforceFrom: number, reinforceUntil: number): SimOrder[][] {
  return Array.from({ length: MISSION_06_TURN_LIMIT }, (_, i) => {
    const slow = i >= 3 ? -2 : 0;
    const target =
      i >= reinforceFrom && i < reinforceUntil ? MISSION_06_REINFORCEMENT_ID : MISSION_06_BOSS_ID;
    return MISSION_06_PLAYER_SHIP_IDS.map((id) => fire(id, target, slow));
  });
}
const m06SwatMid = m06Siege(5, 7);
const m06BossOnly = m06Siege(99, 99);

// --- Aggregation ---

interface OutcomeLike {
  result: 'win' | 'loss';
  failReason: 'timeout' | 'sunk' | 'flanked' | null;
  turnCount: number;
  damageProfile: { playerHullDamageFraction: number };
  bonusObjectives: Record<string, boolean>;
  finalPlayers: { hp: number }[];
}

function sweep(
  label: string,
  run: (seed: number) => OutcomeLike,
  playerCount: number
) {
  let wins = 0;
  const lossMix: Record<string, number> = {};
  let winTurnSum = 0;
  const winTurnHistogram: Record<number, number> = {};
  const bonusInWins: Record<string, number> = {};
  let wipes = 0;
  let runsLosingShip = 0;
  let winsLosingShip = 0;
  let shipsLostSum = 0;
  let damageFractionSum = 0;
  let maxDamageFraction = 0;
  for (const seed of SEEDS) {
    const outcome = run(seed);
    const shipsLost = outcome.finalPlayers.filter((ship) => ship.hp === 0).length;
    shipsLostSum += shipsLost;
    if (shipsLost > 0) runsLosingShip += 1;
    if (shipsLost === playerCount) wipes += 1;
    damageFractionSum += outcome.damageProfile.playerHullDamageFraction;
    maxDamageFraction = Math.max(maxDamageFraction, outcome.damageProfile.playerHullDamageFraction);
    if (outcome.result === 'win') {
      wins += 1;
      winTurnSum += outcome.turnCount;
      winTurnHistogram[outcome.turnCount] = (winTurnHistogram[outcome.turnCount] ?? 0) + 1;
      if (shipsLost > 0) winsLosingShip += 1;
      for (const [name, hit] of Object.entries(outcome.bonusObjectives)) {
        if (hit) bonusInWins[name] = (bonusInWins[name] ?? 0) + 1;
      }
    } else {
      const reason = outcome.failReason ?? 'unknown';
      lossMix[reason] = (lossMix[reason] ?? 0) + 1;
    }
  }
  const summary = {
    label,
    seeds: SEEDS.length,
    wins,
    winRatePct: Math.round((wins / SEEDS.length) * 1000) / 10,
    lossMix,
    avgWinTurn: wins > 0 ? Math.round((winTurnSum / wins) * 100) / 100 : null,
    winTurnHistogram,
    bonusInWins,
    runsLosingAtLeastOneShip: runsLosingShip,
    winsLosingAtLeastOneShip: winsLosingShip,
    passiveWipesOrFullWipes: wipes,
    avgShipsLost: Math.round((shipsLostSum / SEEDS.length) * 100) / 100,
    avgPlayerFleetDamageFraction: Math.round((damageFractionSum / SEEDS.length) * 1000) / 1000,
    maxPlayerFleetDamageFraction: maxDamageFraction
  };
  console.log(JSON.stringify(summary));
  return summary;
}

const adapt = <T extends {
  result: 'win' | 'loss';
  failReason: 'timeout' | 'sunk' | 'flanked' | null;
  turnCount: number;
  damageProfile: { playerHullDamageFraction: number };
  bonusObjectives: Record<string, boolean>;
  turns: unknown[];
}>(
  outcome: T,
  finalPlayers: { hp: number }[]
): OutcomeLike => ({ ...outcome, finalPlayers });

// runMission0X does not expose finalState, so recover final player hp from
// the outcome's remaining-hp view: per-ship for 03; for 04-06 approximate
// "ship lost" via the last turn's summary.sunk accumulation instead.
// Simpler and exact for all four: count sunk player ships from turn events.
function sunkPlayers(
  turns: { summary: { sunk: string[] } }[],
  playerIds: readonly string[]
): { hp: number }[] {
  const sunk = new Set<string>();
  for (const turn of turns) {
    for (const id of turn.summary.sunk) {
      if (playerIds.includes(id)) sunk.add(id);
    }
  }
  return playerIds.map((id) => ({ hp: sunk.has(id) ? 0 : 1 }));
}

console.log('# mission 03');
sweep('m03 canonical sloopFirst', (seed) => {
  const o = runMission03(seed, m03SloopFirst);
  return adapt(o, sunkPlayers(o.turns, MISSION_03_PLAYER_SHIP_IDS));
}, 2);
sweep('m03 passive', (seed) => {
  const o = runMission03(seed, passOrders(MISSION_03_PLAYER_SHIP_IDS, MISSION_03_TURN_LIMIT));
  return adapt(o, sunkPlayers(o.turns, MISSION_03_PLAYER_SHIP_IDS));
}, 2);

console.log('# mission 04');
sweep('m04 canonical parallelBoarding', (seed) => {
  const o = runMission04(seed, m04Parallel);
  return adapt(o, sunkPlayers(o.turns, MISSION_04_PLAYER_SHIP_IDS));
}, 2);
sweep('m04 gunnery', (seed) => {
  const o = runMission04(seed, m04Gunnery);
  return adapt(o, sunkPlayers(o.turns, MISSION_04_PLAYER_SHIP_IDS));
}, 2);
sweep('m04 passive', (seed) => {
  const o = runMission04(seed, passOrders(MISSION_04_PLAYER_SHIP_IDS, MISSION_04_TURN_LIMIT));
  return adapt(o, sunkPlayers(o.turns, MISSION_04_PLAYER_SHIP_IDS));
}, 2);

console.log('# mission 05');
sweep('m05 canonical lineBreak', (seed) => {
  const o = runMission05(seed, m05LineBreak);
  return adapt(o, sunkPlayers(o.turns, MISSION_05_PLAYER_SHIP_IDS));
}, 3);
sweep('m05 passive', (seed) => {
  const o = runMission05(seed, passOrders(MISSION_05_PLAYER_SHIP_IDS, MISSION_05_TURN_LIMIT));
  return adapt(o, sunkPlayers(o.turns, MISSION_05_PLAYER_SHIP_IDS));
}, 3);

console.log('# mission 06');
sweep('m06 canonical swatMid', (seed) => {
  const o = runMission06(seed, m06SwatMid);
  return adapt(o, sunkPlayers(o.turns, MISSION_06_PLAYER_SHIP_IDS));
}, 3);
sweep('m06 bossOnly', (seed) => {
  const o = runMission06(seed, m06BossOnly);
  return adapt(o, sunkPlayers(o.turns, MISSION_06_PLAYER_SHIP_IDS));
}, 3);
sweep('m06 passive', (seed) => {
  const o = runMission06(seed, passOrders(MISSION_06_PLAYER_SHIP_IDS, MISSION_06_TURN_LIMIT));
  return adapt(o, sunkPlayers(o.turns, MISSION_06_PLAYER_SHIP_IDS));
}, 3);
