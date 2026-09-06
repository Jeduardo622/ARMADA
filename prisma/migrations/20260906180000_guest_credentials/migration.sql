-- Additive guest session storage; public player IDs are never login secrets.
CREATE TABLE "GuestCredential" (
  "id" UUID NOT NULL DEFAULT gen_random_uuid(),
  "playerId" UUID NOT NULL,
  "digest" TEXT NOT NULL,
  "expiresAt" TIMESTAMP(3) NOT NULL,
  "revokedAt" TIMESTAMP(3),
  "createdAt" TIMESTAMP(3) NOT NULL DEFAULT CURRENT_TIMESTAMP,
  CONSTRAINT "GuestCredential_pkey" PRIMARY KEY ("id"),
  CONSTRAINT "GuestCredential_playerId_fkey" FOREIGN KEY ("playerId") REFERENCES "Player"("id") ON DELETE CASCADE ON UPDATE CASCADE
);
CREATE UNIQUE INDEX "GuestCredential_digest_key" ON "GuestCredential"("digest");
CREATE INDEX "GuestCredential_playerId_idx" ON "GuestCredential"("playerId");
