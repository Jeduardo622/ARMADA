import { FastifyInstance } from 'fastify';
import { z } from 'zod';

const createPlayerSchema = z.object({
  displayName: z.string().min(3).max(32).optional(),
  region: z.string().max(10).optional(),
  externalId: z.string().optional()
});

export function registerPlayerRoutes(app: FastifyInstance) {
  app.get('/players/me/progress', async (request, reply) => {
    reply.header('Cache-Control', 'no-store');
    if (!request.user?.id) return reply.status(401).send({ error: 'unauthorized' });
    if (!z.object({}).strict().safeParse(request.query).success) {
      return reply.status(400).send({ error: 'invalid_request' });
    }
    const rows = await app.prisma.missionProgress.findMany({
      where: { playerId: request.user.id },
      select: { mission: { select: { code: true } }, status: true, lastResult: true },
      orderBy: { mission: { code: 'asc' } }
    });
    return { progress: rows.map((row) => ({
      missionCode: row.mission.code, status: row.status, lastResult: row.lastResult
    })) };
  });

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

