import * as m1 from '../../src/sim/mission01.js';
import * as m2 from '../../src/sim/mission02.js';
import * as m3 from '../../src/sim/mission03.js';
import * as m4 from '../../src/sim/mission04.js';
import * as m5 from '../../src/sim/mission05.js';
import * as m6 from '../../src/sim/mission06.js';
import * as m7 from '../../src/sim/mission07.js';
import * as m8 from '../../src/sim/mission08.js';
import * as m9 from '../../src/sim/mission09.js';
import * as m10 from '../../src/sim/mission10.js';
import type { SimOrder } from '../../src/sim/types.js';

const orders = (players: readonly string[], limit: number, target: (turn: number, ship: number) => string,
  modifiers: (turn: number) => Partial<SimOrder> = () => ({})): SimOrder[][] =>
  Array.from({ length: limit }, (_, turn) => players.map((shipId, ship) => ({
    shipId, action: 'broadside', targetShipId: target(turn, ship), side: 'starboard', turnDelta: 0, speedDelta: 0,
    ...modifiers(turn)
  })));
const slow = (turn: number) => ({ speedDelta: turn >= 3 ? -2 : 0 });

// Pinned winning plays from the corresponding mission regression suites.
// These use real simulation, never fabricated completion outcomes.
export const campaignWinFixtures = [
  { code: m1.MISSION_01_CODE, seed: 16, turns: orders([m1.MISSION_01_PLAYER_SHIP_ID], m1.MISSION_01_TURN_LIMIT, () => m1.MISSION_01_ENEMY_SHIP_ID) },
  { code: m2.MISSION_02_CODE, seed: 3, turns: orders(m2.MISSION_02_PLAYER_SHIP_IDS, m2.MISSION_02_TURN_LIMIT, (i) => m2.MISSION_02_ENEMY_SHIP_IDS[i < 4 ? 0 : 1]) },
  { code: m3.MISSION_03_CODE, seed: 1, turns: orders(m3.MISSION_03_PLAYER_SHIP_IDS, m3.MISSION_03_TURN_LIMIT, (i) => m3.MISSION_03_ENEMY_SHIP_IDS[i < 4 ? 1 : 0]) },
  { code: m4.MISSION_04_CODE, seed: 6, turns: orders(m4.MISSION_04_PLAYER_SHIP_IDS, m4.MISSION_04_TURN_LIMIT, (_, ship) => m4.MISSION_04_ENEMY_SHIP_IDS[ship],
    (i) => i < 3 ? {} : { action: 'boarding', speedDelta: -2 }) },
  { code: m5.MISSION_05_CODE, seed: 2, turns: orders(m5.MISSION_05_PLAYER_SHIP_IDS, m5.MISSION_05_TURN_LIMIT,
    (i) => i < 4 ? m5.MISSION_05_FLAGSHIP_ID : m5.MISSION_05_ESCORT_SHIP_IDS[i < 6 ? 0 : 1], slow) },
  { code: m6.MISSION_06_CODE, seed: 2, turns: orders(m6.MISSION_06_PLAYER_SHIP_IDS, m6.MISSION_06_TURN_LIMIT,
    (i) => i >= 5 && i < 7 ? m6.MISSION_06_REINFORCEMENT_ID : m6.MISSION_06_BOSS_ID, slow) },
  { code: m7.MISSION_07_CODE, seed: 21, turns: orders(m7.MISSION_07_PLAYER_SHIP_IDS, m7.MISSION_07_TURN_LIMIT,
    (i) => m7.MISSION_07_ENEMY_SHIP_IDS[i < 5 ? 0 : 1], slow) },
  { code: m8.MISSION_08_CODE, seed: 9, turns: orders(m8.MISSION_08_PLAYER_SHIP_IDS, m8.MISSION_08_TURN_LIMIT,
    (i) => m8.MISSION_08_ENEMY_SHIP_IDS[i < 5 ? 0 : 1], (i) => ({ ...slow(i), turnDelta: i === 1 ? 60 : i === 2 ? -60 : 0 })) },
  { code: m9.MISSION_09_CODE, seed: 87, turns: orders(m9.MISSION_09_PLAYER_SHIP_IDS, m9.MISSION_09_TURN_LIMIT,
    (i) => m9.MISSION_09_ENEMY_SHIP_IDS[i < 5 ? 0 : 1], (i) => ({ speedDelta: i < 2 ? 2 : 0 })) },
  { code: m10.MISSION_10_CODE, seed: 5, turns: orders(m10.MISSION_10_PLAYER_SHIP_IDS, m10.MISSION_10_TURN_LIMIT,
    (i) => m10.MISSION_10_ENEMY_SHIP_IDS[i < 5 ? 0 : 1], (i) => ({ ammo: i < 3 ? 'chain' : 'round' })) }
];
