import { MISSION_01_CODE } from '../sim/mission01.js';
import { MISSION_02_CODE } from '../sim/mission02.js';
import { MISSION_03_CODE } from '../sim/mission03.js';
import { MISSION_04_CODE } from '../sim/mission04.js';
import { MISSION_05_CODE } from '../sim/mission05.js';
import { MISSION_06_CODE } from '../sim/mission06.js';

export type RewardItemKey = 'gold' | 'timber' | 'ore' | 'captain_shard' | 'cosmetic_token';

export interface RewardGrant {
  itemKey: RewardItemKey;
  quantity: number;
}

// Design-tunable quantities. Item mix follows the Rewards line of each
// docs/content/missions/*.md; economy sign-off may retune values without
// touching the grant flow.
const MISSION_01_GOLD = 100;
const MISSION_01_TIMBER = 50;
const MISSION_02_GOLD = 150;
const MISSION_02_ORE = 40;
const MISSION_02_CAPTAIN_SHARDS = 1;
const MISSION_03_GOLD = 200;
const MISSION_03_TIMBER = 75;
const MISSION_03_COSMETIC_TOKENS = 1;
const MISSION_04_GOLD = 250;
const MISSION_04_ORE = 60;
const MISSION_04_CAPTAIN_SHARDS = 2;
const MISSION_05_GOLD = 300;
const MISSION_05_TIMBER = 100;
const MISSION_05_COSMETIC_TOKENS = 1;
const MISSION_06_GOLD = 400;
const MISSION_06_ORE = 90;
const MISSION_06_CAPTAIN_SHARDS = 3;
const MISSION_06_COSMETIC_TOKENS = 2;

export const MISSION_REWARD_TABLE: Readonly<Record<string, readonly RewardGrant[]>> = {
  [MISSION_01_CODE]: [
    { itemKey: 'gold', quantity: MISSION_01_GOLD },
    { itemKey: 'timber', quantity: MISSION_01_TIMBER }
  ],
  [MISSION_02_CODE]: [
    { itemKey: 'gold', quantity: MISSION_02_GOLD },
    { itemKey: 'ore', quantity: MISSION_02_ORE },
    { itemKey: 'captain_shard', quantity: MISSION_02_CAPTAIN_SHARDS }
  ],
  [MISSION_03_CODE]: [
    { itemKey: 'gold', quantity: MISSION_03_GOLD },
    { itemKey: 'timber', quantity: MISSION_03_TIMBER },
    { itemKey: 'cosmetic_token', quantity: MISSION_03_COSMETIC_TOKENS }
  ],
  [MISSION_04_CODE]: [
    { itemKey: 'gold', quantity: MISSION_04_GOLD },
    { itemKey: 'ore', quantity: MISSION_04_ORE },
    { itemKey: 'captain_shard', quantity: MISSION_04_CAPTAIN_SHARDS }
  ],
  [MISSION_05_CODE]: [
    { itemKey: 'gold', quantity: MISSION_05_GOLD },
    { itemKey: 'timber', quantity: MISSION_05_TIMBER },
    { itemKey: 'cosmetic_token', quantity: MISSION_05_COSMETIC_TOKENS }
  ],
  [MISSION_06_CODE]: [
    { itemKey: 'gold', quantity: MISSION_06_GOLD },
    { itemKey: 'ore', quantity: MISSION_06_ORE },
    { itemKey: 'captain_shard', quantity: MISSION_06_CAPTAIN_SHARDS },
    { itemKey: 'cosmetic_token', quantity: MISSION_06_COSMETIC_TOKENS }
  ]
};

// Fail-closed: codes outside the table grant nothing.
export function missionRewardsForCode(code: string): readonly RewardGrant[] {
  return MISSION_REWARD_TABLE[code] ?? [];
}
