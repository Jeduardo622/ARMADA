import { FastifyInstance } from 'fastify';
import { z } from 'zod';
import { ensureFlag, ensurePlayerOwnership, validateJsonLimit } from './utils.js';

const completeSchema = z.object({
  playerId: z.string().uuid(),
  result: z.record(z.any()).optional(),
  bestScore: z.number().int().positive().optional()
});

export function registerMissionRoutes(app: FastifyInstance) {
  app.get('/missions', async (request, reply) => {
    if (!(await ensureFlag(app, reply, 'missions_api'))) {
      return;
    }

    const missions = await app.prisma.mission.findMany({
      where: { isActive: true },
      orderBy: { code: 'asc' }
    });

    return { missions };
  });

  app.post('/missions/:code/complete', async (request, reply) => {
    const params = z.object({ code: z.string().min(3) }).safeParse(request.params);
    if (!params.success) {
      return reply.status(400).send({ error: params.error.format() });
    }

    if (!(await ensureFlag(app, reply, 'missions_api', { missionCode: params.data.code }))) {
      return;
    }

    const parsed = completeSchema.safeParse(request.body);
    if (!parsed.success) {
      return reply.status(400).send({ error: parsed.error.format() });
    }

    if (parsed.data.result && !validateJsonLimit(reply, parsed.data.result)) {
      return;
    }

    if (!ensurePlayerOwnership(reply, request.user?.id, parsed.data.playerId)) {
      return;
    }

    const mission = await app.prisma.mission.findFirst({ where: { code: params.data.code } });
    if (!mission) {
      return reply.status(404).send({ error: 'mission_not_found' });
    }

    const player = await app.prisma.player.findUnique({ where: { id: parsed.data.playerId } });
    if (!player) {
      return reply.status(404).send({ error: 'player_not_found' });
    }

    const progress = await app.prisma.missionProgress.upsert({
      where: { playerId_missionId: { playerId: player.id, missionId: mission.id } },
      update: {
        status: 'COMPLETED',
        bestScore: parsed.data.bestScore ?? undefined,
        lastResult: parsed.data.result ?? undefined
      },
      create: {
        playerId: player.id,
        missionId: mission.id,
        status: 'COMPLETED',
        bestScore: parsed.data.bestScore,
        lastResult: parsed.data.result
      }
    });

    request.log.info(
      {
        actor: request.user?.id,
        playerId: player.id,
        missionCode: params.data.code,
        requestId: request.id
      },
      'mission_completed'
    );

    return reply.status(200).send({ progress });
  });
}

