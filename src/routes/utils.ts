import { FastifyInstance, FastifyReply } from 'fastify';
import type { Context } from 'unleash-client';

export async function ensureFlag(
  app: FastifyInstance,
  reply: FastifyReply,
  flagName: string,
  context?: Context
) {
  const enabled = app.flags.isEnabled(flagName, context);
  if (enabled) {
    return true;
  }

  // A ready flag service answering "disabled" is authoritative — the DB row
  // must not override the kill switch. The DB is a fallback for outages only.
  if (!app.flags.ready()) {
    const fallback = await app.prisma.featureFlag.findUnique({ where: { name: flagName } });
    if (fallback?.enabled) {
      return true;
    }
  }

  reply.status(403).send({ error: 'feature_disabled', flag: flagName });
  return false;
}

export function ensurePlayerOwnership(
  reply: FastifyReply,
  tokenPlayerId: string | undefined,
  targetPlayerId: string
) {
  if (!tokenPlayerId || tokenPlayerId !== targetPlayerId) {
    reply.status(403).send({ error: 'forbidden' });
    return false;
  }
  return true;
}

export function validateJsonLimit(
  reply: FastifyReply,
  value: unknown,
  maxSerializedLength = 10_000
) {
  try {
    const serialized = JSON.stringify(value);
    if (serialized.length > maxSerializedLength) {
      reply.status(400).send({ error: 'payload_too_large' });
      return false;
    }
    return true;
  } catch {
    reply.status(400).send({ error: 'invalid_json' });
    return false;
  }
}

