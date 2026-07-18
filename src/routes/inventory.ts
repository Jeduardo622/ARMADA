import { FastifyInstance } from 'fastify';
import { z } from 'zod';
import { ensureFlag, ensurePlayerOwnership, validateJsonLimit } from './utils.js';

const grantSchema = z.object({
  itemKey: z.string().min(1),
  quantity: z.number().int().positive().max(1_000_000).default(1),
  metadata: z.record(z.any()).optional()
});

export function registerInventoryRoutes(app: FastifyInstance) {
  app.get('/inventory/:playerId', async (request, reply) => {
    const params = z.object({ playerId: z.string().uuid() }).safeParse(request.params);
    if (!params.success) {
      return reply.status(400).send({ error: params.error.format() });
    }

    if (!ensurePlayerOwnership(reply, request.user?.id, params.data.playerId)) {
      return;
    }

    if (!(await ensureFlag(app, reply, 'inventory_api', { playerId: params.data.playerId }))) {
      return;
    }

    const items = await app.prisma.inventoryItem.findMany({
      where: { playerId: params.data.playerId }
    });

    return { items };
  });

  app.post('/inventory/:playerId/grant', async (request, reply) => {
    const params = z.object({ playerId: z.string().uuid() }).safeParse(request.params);
    if (!params.success) {
      return reply.status(400).send({ error: params.error.format() });
    }

    if (!ensurePlayerOwnership(reply, request.user?.id, params.data.playerId)) {
      return;
    }

    // Grants mint currency, so they sit behind their own trusted-service flag
    // (seeded disabled) instead of the player-facing inventory_api flag:
    // otherwise any authenticated player could mint upgrade materials.
    if (!(await ensureFlag(app, reply, 'inventory_grant_api', { playerId: params.data.playerId }))) {
      return;
    }

    const parsed = grantSchema.safeParse(request.body);
    if (!parsed.success) {
      return reply.status(400).send({ error: parsed.error.format() });
    }

    if (parsed.data.metadata && !validateJsonLimit(reply, parsed.data.metadata)) {
      return;
    }

    const player = await app.prisma.player.findUnique({ where: { id: params.data.playerId } });
    if (!player) {
      return reply.status(404).send({ error: 'player_not_found' });
    }

    const item = await app.prisma.inventoryItem.upsert({
      where: {
        playerId_itemKey: {
          playerId: params.data.playerId,
          itemKey: parsed.data.itemKey
        }
      },
      update: {
        quantity: { increment: parsed.data.quantity },
        metadata: parsed.data.metadata
      },
      create: {
        playerId: params.data.playerId,
        itemKey: parsed.data.itemKey,
        quantity: parsed.data.quantity,
        metadata: parsed.data.metadata
      }
    });

    request.log.info(
      {
        actor: request.user?.id,
        playerId: params.data.playerId,
        itemKey: parsed.data.itemKey,
        quantity: parsed.data.quantity,
        requestId: request.id
      },
      'inventory_grant'
    );

    return reply.status(200).send({ item });
  });
}

