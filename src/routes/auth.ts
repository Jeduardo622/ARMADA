import { FastifyInstance } from 'fastify';
import { z } from 'zod';
import { accessSession, guestCredentialDigest, issueGuestCredential } from '../auth/guestCredentials.js';

const guestSchema = z.object({
  externalId: z.string().optional(),
  displayName: z.string().max(32).optional(),
  region: z.string().max(10).optional()
}).strict();

const refreshSchema = z.object({
  guestCredential: z.string().regex(/^[A-Za-z0-9_-]{43}$/)
}).strict();

export function registerAuthRoutes(app: FastifyInstance) {
  app.post('/auth/guest', async (request, reply) => {
    reply.header('Cache-Control', 'no-store');
    const parsed = guestSchema.safeParse(request.body ?? {});
    if (!parsed.success) {
      return reply.status(400).send({ error: parsed.error.format() });
    }

    // Public identifiers are not credentials. Never resume or link an identity
    // through this unauthenticated registration endpoint.
    if (parsed.data.externalId) {
      return reply.status(400).send({ error: 'external_id_not_supported' });
    }

    return app.prisma.$transaction(async (tx) => {
      const player = await tx.player.create({
        data: {
          displayName: parsed.data.displayName,
          region: parsed.data.region
        }
      });
      return { ...accessSession(player), ...await issueGuestCredential(tx, player.id) };
    });
  });

  app.post('/auth/refresh', async (request, reply) => {
    reply.header('Cache-Control', 'no-store');
    const parsed = refreshSchema.safeParse(request.body);
    if (!parsed.success) return reply.status(401).send({ error: 'unauthorized' });
    const credential = await app.prisma.guestCredential.findUnique({
      where: { digest: guestCredentialDigest(parsed.data.guestCredential) },
      include: { player: true }
    });
    if (!credential || credential.revokedAt || credential.expiresAt.getTime() <= Date.now() || !credential.player) {
      return reply.status(401).send({ error: 'unauthorized' });
    }
    return { ...accessSession(credential.player), credentialExpiresAt: credential.expiresAt.toISOString() };
  });

  // Existing, unexpired JWTs can enroll without trusting a caller-supplied ID.
  app.post('/auth/guest/credential', async (request, reply) => {
    reply.header('Cache-Control', 'no-store');
    if (!request.user?.id) return reply.status(401).send({ error: 'unauthorized' });
    if (!z.object({}).strict().safeParse(request.body ?? {}).success) {
      return reply.status(400).send({ error: 'invalid_request' });
    }
    const player = await app.prisma.player.findUnique({ where: { id: request.user.id } });
    if (!player) return reply.status(401).send({ error: 'unauthorized' });
    return app.prisma.$transaction(async (tx) => ({
      ...accessSession(player), ...await issueGuestCredential(tx, player.id)
    }));
  });
}

