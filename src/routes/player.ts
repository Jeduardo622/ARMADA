import { FastifyInstance } from 'fastify';
import { z } from 'zod';

const createPlayerSchema = z.object({
  displayName: z.string().min(3).max(32).optional(),
  region: z.string().max(10).optional(),
  externalId: z.string().optional()
});

export function registerPlayerRoutes(app: FastifyInstance) {
  app.post('/players', async (request, reply) => {
    const parsed = createPlayerSchema.safeParse(request.body);
    if (!parsed.success) {
      return reply.status(400).send({ error: parsed.error.format() });
    }

    const player = await app.prisma.player.create({
      data: parsed.data
    });

    return reply.status(201).send({ player });
  });

  app.get('/players/:id', async (request, reply) => {
    const params = z.object({ id: z.string().uuid() }).safeParse(request.params);
    if (!params.success) {
      return reply.status(400).send({ error: params.error.format() });
    }

    if (request.user?.id !== params.data.id) {
      return reply.status(403).send({ error: 'forbidden' });
    }

    const player = await app.prisma.player.findUnique({
      where: { id: params.data.id }
    });

    if (!player) {
      return reply.status(404).send({ error: 'player_not_found' });
    }

    return { player };
  });
}

