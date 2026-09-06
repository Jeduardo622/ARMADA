import { z } from 'zod';

// PvE only. The server validates this frozen snapshot against the owner;
// no request may supply a damage multiplier directly.
export const campaignLoadoutSchema = z.object({
  schemaVersion: z.literal(1),
  captainLevel: z.number().int().min(1).max(5),
  firstMate: z.literal('calico_jim').nullable(),
  gunneryChief: z.literal('one_eyed_ella').nullable()
}).strict();
export type CampaignLoadout = z.infer<typeof campaignLoadoutSchema>;
export function campaignDamageScale(loadout: CampaignLoadout): number {
  const pair = loadout.firstMate === 'calico_jim' && loadout.gunneryChief === 'one_eyed_ella';
  return (100 + (loadout.captainLevel - 1) + (pair ? 2 : 0)) / 100;
}
