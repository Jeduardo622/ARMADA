import type { PrismaClient } from '@prisma/client';
import { UPGRADE_COMPONENTS } from '../economy/upgrades.js';
import type { CampaignLoadout } from '../sim/campaignLoadout.js';
import type { ShipUpgradeTiers } from '../sim/types.js';
import { progressionResponse } from './progression.js';

export async function validateBattleLoadout(prisma: PrismaClient, playerId: string,
  upgrades?: ShipUpgradeTiers, loadout?: CampaignLoadout) {
  if (upgrades) {
    const rows = await prisma.playerShipUpgrade.findMany({ where: { playerId } });
    const owned = new Map(rows.map(row => [row.component, row.tier]));
    for (const component of UPGRADE_COMPONENTS) {
      if (upgrades[component] > (owned.get(component) ?? 0)) return {
        error: 'upgrade_tiers_exceed_owned', component, claimed: upgrades[component], owned: owned.get(component) ?? 0
      };
    }
  }
  if (loadout) {
    const profile = await prisma.playerProgression.findUnique({ where: { playerId } });
    const level = profile ? progressionResponse(profile).captain.level : 1;
    if (loadout.captainLevel > level) return { error: 'captain_level_exceeds_owned', claimed: loadout.captainLevel, owned: level };
    // Both named starter crew are permanently owned; current assignments can
    // change while a battle is in flight without invalidating its snapshot.
  }
  return null;
}
