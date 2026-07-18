import { FastifyInstance } from 'fastify';
import { z } from 'zod';
import { ensureFlag, ensurePlayerOwnership } from './utils.js';
import {
  MAX_UPGRADE_TIER,
  UPGRADE_COMPONENTS,
  upgradeCatalog,
  upgradeCostsFor
} from '../economy/upgrades.js';

const purchaseSchema = z.object({
  playerId: z.string().uuid(),
  component: z.enum(UPGRADE_COMPONENTS)
});

// Thrown inside the purchase transaction so every partial write rolls back
// before the mapped 4xx is sent.
class PurchaseRejection extends Error {
  constructor(
    readonly statusCode: number,
    readonly body: Record<string, unknown>
  ) {
    super(String(body.error));
  }
}

export function registerUpgradeRoutes(app: FastifyInstance) {
  app.get('/upgrades', async (request, reply) => {
    if (!(await ensureFlag(app, reply, 'inventory_api'))) {
      return;
    }

    const playerId = request.user?.id;
    if (!playerId) {
      return reply.status(401).send({ error: 'unauthorized' });
    }

    const rows = await app.prisma.playerShipUpgrade.findMany({ where: { playerId } });
    const tiers = new Map(rows.map((row) => [row.component, row.tier]));
    const owned = UPGRADE_COMPONENTS.map((component) => ({
      component,
      tier: tiers.get(component) ?? 0
    }));

    return { catalog: upgradeCatalog(), owned };
  });

  app.post('/upgrades/purchase', async (request, reply) => {
    const parsed = purchaseSchema.safeParse(request.body);
    if (!parsed.success) {
      return reply.status(400).send({ error: parsed.error.format() });
    }

    if (!ensurePlayerOwnership(reply, request.user?.id, parsed.data.playerId)) {
      return;
    }

    if (!(await ensureFlag(app, reply, 'inventory_api', { playerId: parsed.data.playerId }))) {
      return;
    }

    const player = await app.prisma.player.findUnique({ where: { id: parsed.data.playerId } });
    if (!player) {
      return reply.status(404).send({ error: 'player_not_found' });
    }

    const { playerId, component } = parsed.data;

    // The tier claim and every cost decrement are conditional writes inside
    // one transaction: a concurrent purchase or a short inventory row makes a
    // conditional write miss its row, which throws and rolls everything back.
    // Purchases are strictly sequential — the claim is keyed to the tier read
    // inside the transaction, so tiers can never be skipped or double-bought.
    const purchase = () =>
      app.prisma.$transaction(async (tx) => {
        const existing = await tx.playerShipUpgrade.findUnique({
          where: { playerId_component: { playerId, component } }
        });
        const currentTier = existing?.tier ?? 0;
        if (currentTier >= MAX_UPGRADE_TIER) {
          throw new PurchaseRejection(400, { error: 'max_tier_reached', component });
        }
        const targetTier = currentTier + 1;
        const costs = upgradeCostsFor(component, targetTier);

        if (existing) {
          const claimed = await tx.playerShipUpgrade.updateMany({
            where: { playerId, component, tier: currentTier },
            data: { tier: targetTier }
          });
          if (claimed.count !== 1) {
            throw new PurchaseRejection(409, { error: 'upgrade_conflict', component });
          }
        } else {
          // The unique constraint makes the loser of a concurrent create throw
          // P2002, which maps to upgrade_conflict below without retrying.
          await tx.playerShipUpgrade.create({ data: { playerId, component, tier: targetTier } });
        }

        for (const cost of costs) {
          const spent = await tx.inventoryItem.updateMany({
            where: { playerId, itemKey: cost.itemKey, quantity: { gte: cost.quantity } },
            data: { quantity: { decrement: cost.quantity } }
          });
          if (spent.count !== 1) {
            throw new PurchaseRejection(400, {
              error: 'insufficient_funds',
              itemKey: cost.itemKey
            });
          }
        }

        return { upgrade: { playerId, component, tier: targetTier }, spent: costs };
      });

    let result;
    try {
      result = await purchase();
    } catch (error) {
      if (error instanceof PurchaseRejection) {
        return reply.status(error.statusCode).send(error.body);
      }
      const lostCreateRace =
        typeof error === 'object' && error !== null && (error as { code?: string }).code === 'P2002';
      if (lostCreateRace) {
        return reply.status(409).send({ error: 'upgrade_conflict', component });
      }
      throw error;
    }

    request.log.info(
      {
        actor: request.user?.id,
        playerId,
        component,
        tier: result.upgrade.tier,
        spent: result.spent,
        requestId: request.id
      },
      'upgrade_purchased'
    );

    return reply.status(200).send(result);
  });
}
