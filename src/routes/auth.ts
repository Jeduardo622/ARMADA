import { FastifyInstance } from 'fastify';
import jwt from 'jsonwebtoken';
import { z } from 'zod';
import { env } from '../config.js';

const guestSchema = z.object({
  externalId: z.string().optional(),
  displayName: z.string().optional(),
  region: z.string().optional()
});

export function registerAuthRoutes(app: FastifyInstance) {
  app.post('/auth/guest', async (request, reply) => {
    const parsed = guestSchema.safeParse(request.body);
    if (!parsed.success) {
      return reply.status(400).send({ error: parsed.error.format() });
    }

    const player =
      (parsed.data.externalId &&
        (await app.prisma.player.findUnique({ where: { externalId: parsed.data.externalId } }))) ||
      (await app.prisma.player.create({
        data: {
          externalId: parsed.data.externalId,
          displayName: parsed.data.displayName,
          region: parsed.data.region
        }
      }));

    const token = jwt.sign(
      { sub: player.id, externalId: player.externalId },
      env.JWT_SECRET,
      { expiresIn: `${env.TOKEN_TTL_HOURS}h` }
    );

    return { token, player };
  });
}

