import { FastifyInstance, RouteHandlerMethod } from 'fastify';
import { z } from 'zod';
import { lockProgression, progressionResponse } from '../services/progression.js';

const createPlayerSchema = z.object({
  displayName: z.string().min(3).max(32).optional(),
  region: z.string().max(10).optional(),
  externalId: z.string().optional()
});

export function registerPlayerRoutes(app: FastifyInstance) {
  const trainingSchema = z.object({ sequence: z.number().int().min(1).max(2147483647) }).strict();
  const crewSchema = z.object({ firstMate: z.literal('calico_jim').nullable(), gunneryChief: z.literal('one_eyed_ella').nullable() }).strict();
  const progressionHandler = (operation: 'read' | 'train' | 'crew'): RouteHandlerMethod => async (request, reply) => {
    reply.header('Cache-Control', 'no-store');
    if (!request.user?.id) return reply.status(401).send({ error: 'unauthorized' });
    if (!z.object({}).strict().safeParse(request.query).success) return reply.status(400).send({ error: 'invalid_request' });
    const training = trainingSchema.safeParse(request.body);
    const crew = crewSchema.safeParse(request.body);
    if ((operation === 'train' && !training.success) || (operation === 'crew' && !crew.success)) return reply.status(400).send({ error: 'invalid_request' });
    const playerId = request.user.id;
    if (!await app.prisma.player.findUnique({ where: { id: playerId } })) return reply.status(404).send({ error: 'player_not_found' });
    const result = await app.prisma.$transaction(async tx => {
      let profile = await lockProgression(tx, playerId);
      if (operation === 'train' && training.success) {
        const sequence = training.data.sequence;
        // Any already-consumed sequence is a harmless retry, even at cap.
        if (sequence <= profile.trainingSequence) return progressionResponse(profile);
        if (sequence !== profile.trainingSequence + 1) return { error: 'training_sequence_conflict' };
        if (profile.xp >= 700) return { error: 'captain_at_cap' };
        const spent = await tx.inventoryItem.updateMany({ where: { playerId, itemKey: 'captain_shard', quantity: { gte: 1 } }, data: { quantity: { decrement: 1 } } });
        if (spent.count !== 1) return { error: 'insufficient_shards' };
        profile = await tx.playerProgression.update({ where: { playerId }, data: { xp: Math.min(700, profile.xp + 25), trainingSequence: sequence } });
      } else if (operation === 'crew' && crew.success) {
        profile = await tx.playerProgression.update({ where: { playerId }, data: crew.data });
      }
      return progressionResponse(profile);
    });
    return reply.status('error' in result ? 409 : 200).send(result);
  };
  app.get('/players/me/progression', progressionHandler('read'));
  app.post('/players/me/progression/train', progressionHandler('train'));
  app.post('/players/me/progression/crew', progressionHandler('crew'));

  app.get('/players/me/progress', async (request, reply) => {
    reply.header('Cache-Control', 'no-store');
    if (!request.user?.id) return reply.status(401).send({ error: 'unauthorized' });
    if (!z.object({}).strict().safeParse(request.query).success) {
      return reply.status(400).send({ error: 'invalid_request' });
    }
    const rows = await app.prisma.missionProgress.findMany({
      where: { playerId: request.user.id },
      select: { mission: { select: { code: true } }, status: true, lastResult: true, verifiedResult: true, verifiedStars: true },
      orderBy: { mission: { code: 'asc' } }
    });
    return { progress: rows.map((row) => ({
      missionCode: row.mission.code, status: row.status, lastResult: row.lastResult,
      verifiedResult: row.verifiedResult ?? null, verifiedStars: row.verifiedStars ?? null
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

