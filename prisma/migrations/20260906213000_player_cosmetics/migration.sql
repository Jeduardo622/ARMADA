-- Additive presentation-only equipment. Leave this table in place on application rollback.
CREATE TABLE "PlayerCosmetics" (
  "playerId" UUID NOT NULL,
  "equippedSail" TEXT NOT NULL DEFAULT 'default',
  CONSTRAINT "PlayerCosmetics_pkey" PRIMARY KEY ("playerId"),
  CONSTRAINT "PlayerCosmetics_playerId_fkey" FOREIGN KEY ("playerId") REFERENCES "Player"("id") ON DELETE CASCADE ON UPDATE CASCADE
);
