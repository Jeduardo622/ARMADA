import { createHash, randomBytes } from 'node:crypto';
import type { Player, Prisma } from '@prisma/client';
import jwt from 'jsonwebtoken';
import { env } from '../config.js';

const CREDENTIAL_LIFETIME_MS = 180 * 24 * 60 * 60 * 1000;

export function guestCredentialDigest(credential: string): string {
  return createHash('sha256').update(credential).digest('hex');
}

export async function issueGuestCredential(tx: Prisma.TransactionClient, playerId: string) {
  const guestCredential = randomBytes(32).toString('base64url');
  const expiresAt = new Date(Date.now() + CREDENTIAL_LIFETIME_MS);
  await tx.guestCredential.create({
    data: { playerId, digest: guestCredentialDigest(guestCredential), expiresAt }
  });
  return { guestCredential, credentialExpiresAt: expiresAt.toISOString() };
}

export function accessSession(player: Player) {
  const issuedAt = Math.floor(Date.now() / 1000);
  const expiresAt = issuedAt + env.TOKEN_TTL_HOURS * 60 * 60;
  const token = jwt.sign(
    { sub: player.id, externalId: player.externalId, iat: issuedAt, exp: expiresAt },
    env.JWT_SECRET,
    { algorithm: 'HS256' }
  );
  return { token, player, accessExpiresAt: new Date(expiresAt * 1000).toISOString() };
}
