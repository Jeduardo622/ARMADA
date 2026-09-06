import { z } from 'zod';
import content from './starter-catalog.v1.json' with { type: 'json' };

// Free test content only. Strict presentation fields cannot become combat modifiers.
export const starterCosmetics = z.object({
  catalogVersion: z.number().int().positive(),
  catalog: z.array(z.object({
    sailId: z.string().min(1),
    displayName: z.string().min(1),
    sailColor: z.string().regex(/^#[0-9A-F]{6}$/).nullable()
  }).strict()).min(1)
}).strict().parse(content);
export const DEFAULT_SAIL_ID = 'default';
export const ownedSailIds = starterCosmetics.catalog.map((sail) => sail.sailId);
if (new Set(ownedSailIds).size !== ownedSailIds.length || !ownedSailIds.includes(DEFAULT_SAIL_ID)) {
  throw new Error('invalid_starter_cosmetics_catalog');
}

export function cosmeticsView(equippedId = DEFAULT_SAIL_ID) {
  return { ...starterCosmetics, ownedIds: ownedSailIds, equippedId };
}
