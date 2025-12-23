-- CreateEnum
CREATE TYPE "MissionStatus" AS ENUM ('NOT_STARTED', 'IN_PROGRESS', 'COMPLETED', 'FAILED');

-- Extensions
CREATE EXTENSION IF NOT EXISTS "pgcrypto";

-- CreateTable Player
CREATE TABLE "Player" (
    "id" UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    "externalId" TEXT UNIQUE,
    "displayName" TEXT,
    "region" TEXT,
    "createdAt" TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    "updatedAt" TIMESTAMPTZ NOT NULL DEFAULT NOW()
);

-- CreateTable InventoryItem
CREATE TABLE "InventoryItem" (
    "id" UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    "playerId" UUID NOT NULL,
    "itemKey" TEXT NOT NULL,
    "quantity" INTEGER NOT NULL DEFAULT 0,
    "metadata" JSONB,
    "createdAt" TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    "updatedAt" TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    CONSTRAINT "InventoryItem_playerId_fkey" FOREIGN KEY ("playerId") REFERENCES "Player"("id") ON DELETE CASCADE ON UPDATE CASCADE,
    CONSTRAINT "InventoryItem_player_item_unique" UNIQUE ("playerId", "itemKey")
);
CREATE INDEX "InventoryItem_playerId_idx" ON "InventoryItem"("playerId");

-- CreateTable Mission
CREATE TABLE "Mission" (
    "id" UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    "code" TEXT NOT NULL UNIQUE,
    "name" TEXT NOT NULL,
    "description" TEXT,
    "configVersion" INTEGER NOT NULL DEFAULT 1,
    "isActive" BOOLEAN NOT NULL DEFAULT TRUE,
    "createdAt" TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    "updatedAt" TIMESTAMPTZ NOT NULL DEFAULT NOW()
);

-- CreateTable MissionProgress
CREATE TABLE "MissionProgress" (
    "id" UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    "playerId" UUID NOT NULL,
    "missionId" UUID NOT NULL,
    "status" "MissionStatus" NOT NULL DEFAULT 'NOT_STARTED',
    "bestScore" INTEGER,
    "lastResult" JSONB,
    "updatedAt" TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    "createdAt" TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    CONSTRAINT "MissionProgress_playerId_fkey" FOREIGN KEY ("playerId") REFERENCES "Player"("id") ON DELETE CASCADE ON UPDATE CASCADE,
    CONSTRAINT "MissionProgress_missionId_fkey" FOREIGN KEY ("missionId") REFERENCES "Mission"("id") ON DELETE CASCADE ON UPDATE CASCADE,
    CONSTRAINT "MissionProgress_player_mission_unique" UNIQUE ("playerId", "missionId")
);
CREATE INDEX "MissionProgress_missionId_idx" ON "MissionProgress"("missionId");

-- CreateTable ConfigSnapshot
CREATE TABLE "ConfigSnapshot" (
    "id" SERIAL PRIMARY KEY,
    "namespace" TEXT NOT NULL,
    "version" INTEGER NOT NULL,
    "content" JSONB NOT NULL,
    "checksum" TEXT NOT NULL,
    "createdAt" TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    CONSTRAINT "ConfigSnapshot_namespace_version_unique" UNIQUE ("namespace", "version")
);
CREATE INDEX "ConfigSnapshot_namespace_idx" ON "ConfigSnapshot"("namespace");

-- CreateTable FeatureFlag
CREATE TABLE "FeatureFlag" (
    "id" SERIAL PRIMARY KEY,
    "name" TEXT NOT NULL UNIQUE,
    "description" TEXT,
    "enabled" BOOLEAN NOT NULL DEFAULT FALSE,
    "defaultState" TEXT,
    "createdAt" TIMESTAMPTZ NOT NULL DEFAULT NOW()
);

-- CreateTable TelemetryEvent
CREATE TABLE "TelemetryEvent" (
    "id" BIGSERIAL PRIMARY KEY,
    "playerId" UUID,
    "missionCode" TEXT,
    "schemaVersion" INTEGER NOT NULL,
    "payload" JSONB NOT NULL,
    "receivedAt" TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    CONSTRAINT "TelemetryEvent_playerId_fkey" FOREIGN KEY ("playerId") REFERENCES "Player"("id") ON DELETE SET NULL ON UPDATE CASCADE
);

-- Triggers to keep updatedAt current (for Postgres)
CREATE OR REPLACE FUNCTION set_updated_at() RETURNS TRIGGER AS $$
BEGIN
  NEW."updatedAt" = NOW();
  RETURN NEW;
END;
$$ LANGUAGE plpgsql;

CREATE TRIGGER set_player_updated_at BEFORE UPDATE ON "Player" FOR EACH ROW EXECUTE PROCEDURE set_updated_at();
CREATE TRIGGER set_inventory_updated_at BEFORE UPDATE ON "InventoryItem" FOR EACH ROW EXECUTE PROCEDURE set_updated_at();
CREATE TRIGGER set_mission_updated_at BEFORE UPDATE ON "Mission" FOR EACH ROW EXECUTE PROCEDURE set_updated_at();
CREATE TRIGGER set_progress_updated_at BEFORE UPDATE ON "MissionProgress" FOR EACH ROW EXECUTE PROCEDURE set_updated_at();

