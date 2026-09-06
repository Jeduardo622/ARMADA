import { FastifyInstance } from 'fastify';
import { z } from 'zod';
import { cosmeticsView, ownedSailIds } from '../cosmetics/catalog.js';

const emptyQuery = z.object({}).strict();
const equipRequest = z.object({
  sailId: z.string().refine((id) => ownedSailIds.includes(id))
}).strict();

export function registerCosmeticsRoutes(app: FastifyInstance) {
  app.get('/players/me/cosmetics', async (request, reply) => {
    reply.header('Cache-Control', 'no-store');
    const playerId = request.user?.id;
    if (!playerId) return reply.status(401).send({ error: 'unauthorized' });
    if (!emptyQuery.safeParse(request.query).success) return reply.status(400).send({ error: 'invalid_request' });
    if (!(await app.prisma.player.findUnique({ where: { id: playerId } }))) {
      return reply.status(404).send({ error: 'player_not_found' });
    }
    const saved = await app.prisma.playerCosmetics.findUnique({ where: { playerId } });
    return cosmeticsView(saved?.equippedSail);
  });

  app.post('/players/me/cosmetics/equip', async (request, reply) => {
    reply.header('Cache-Control', 'no-store');
    const playerId = request.user?.id;
    if (!playerId) return reply.status(401).send({ error: 'unauthorized' });
    const parsed = equipRequest.safeParse(request.body);
    if (!parsed.success || !emptyQuery.safeParse(request.query).success) {
      return reply.status(400).send({ error: 'invalid_request' });
    }
    if (!(await app.prisma.player.findUnique({ where: { id: playerId } }))) {
      return reply.status(404).send({ error: 'player_not_found' });
    }
    // Native PostgreSQL upsert on one primary key handles simultaneous first applies.
    // No currency or inventory write: both starter sails are free and already owned.
    const saved = await app.prisma.playerCosmetics.upsert({
      where: { playerId },
      create: { playerId, equippedSail: parsed.data.sailId },
      update: { equippedSail: parsed.data.sailId }
    });
    return cosmeticsView(saved.equippedSail);
  });
}
