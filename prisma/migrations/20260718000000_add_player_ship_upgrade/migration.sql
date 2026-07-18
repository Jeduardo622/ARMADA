-- CreateTable PlayerShipUpgrade (additive; no existing tables or rows change)
CREATE TABLE "PlayerShipUpgrade" (
    "id" UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    "playerId" UUID NOT NULL,
    "component" TEXT NOT NULL,
    "tier" INTEGER NOT NULL DEFAULT 0,
    "createdAt" TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    "updatedAt" TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    CONSTRAINT "PlayerShipUpgrade_playerId_fkey" FOREIGN KEY ("playerId") REFERENCES "Player"("id") ON DELETE CASCADE ON UPDATE CASCADE,
    CONSTRAINT "PlayerShipUpgrade_player_component_unique" UNIQUE ("playerId", "component")
);
CREATE INDEX "PlayerShipUpgrade_playerId_idx" ON "PlayerShipUpgrade"("playerId");

CREATE TRIGGER set_player_ship_upgrade_updated_at BEFORE UPDATE ON "PlayerShipUpgrade" FOR EACH ROW EXECUTE PROCEDURE set_updated_at();
