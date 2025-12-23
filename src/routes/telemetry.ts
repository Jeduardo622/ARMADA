import { FastifyInstance } from 'fastify';
import { z } from 'zod';
import { ensureFlag, ensurePlayerOwnership, validateJsonLimit } from './utils.js';

const telemetrySchema = z.object({
  schemaVersion: z.number().int().positive(),
  playerId: z.string().uuid().optional(),
  missionCode: z.string().optional(),
  payload: z.record(z.any())
});

export function registerTelemetryRoutes(app: FastifyInstance) {
  app.post('/telemetry/ingest', async (request, reply) => {
    if (!(await ensureFlag(app, reply, 'telemetry_ingest'))) {
      return;
    }

    const parsed = telemetrySchema.safeParse(request.body);
    if (!parsed.success) {
      return reply.status(400).send({ error: parsed.error.format() });
    }

    if (
      parsed.data.playerId &&
      !ensurePlayerOwnership(reply, request.user?.id, parsed.data.playerId)
    ) {
      return;
    }

    if (!validateJsonLimit(reply, parsed.data.payload)) {
      return;
    }

    await app.prisma.telemetryEvent.create({
      data: {
        schemaVersion: parsed.data.schemaVersion,
        playerId: parsed.data.playerId,
        missionCode: parsed.data.missionCode,
        payload: parsed.data.payload
      }
    });

    return reply.status(202).send({ status: 'queued' });
  });
}

