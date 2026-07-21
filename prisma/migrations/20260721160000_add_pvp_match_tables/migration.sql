-- CreateTable Match + MatchParticipant (additive; no existing tables or rows change)
CREATE TABLE "Match" (
    "id" UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    "code" TEXT NOT NULL,
    "status" TEXT NOT NULL DEFAULT 'WAITING_FOR_OPPONENT',
    "scenarioCode" TEXT NOT NULL,
    "seed" INTEGER NOT NULL,
    "modifiers" JSONB NOT NULL,
    "turnNumber" INTEGER NOT NULL DEFAULT 1,
    "state" JSONB NOT NULL,
    "turnEvents" JSONB NOT NULL DEFAULT '[]',
    "result" TEXT,
    "createdAt" TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    "updatedAt" TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    CONSTRAINT "Match_code_unique" UNIQUE ("code")
);
CREATE INDEX "Match_status_idx" ON "Match"("status");

CREATE TRIGGER set_match_updated_at BEFORE UPDATE ON "Match" FOR EACH ROW EXECUTE PROCEDURE set_updated_at();

CREATE TABLE "MatchParticipant" (
    "id" UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    "matchId" UUID NOT NULL,
    "playerId" UUID NOT NULL,
    "side" TEXT NOT NULL,
    "pendingOrders" JSONB,
    "pendingTurn" INTEGER,
    "createdAt" TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    "updatedAt" TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    CONSTRAINT "MatchParticipant_matchId_fkey" FOREIGN KEY ("matchId") REFERENCES "Match"("id") ON DELETE CASCADE ON UPDATE CASCADE,
    CONSTRAINT "MatchParticipant_playerId_fkey" FOREIGN KEY ("playerId") REFERENCES "Player"("id") ON DELETE CASCADE ON UPDATE CASCADE,
    CONSTRAINT "MatchParticipant_match_side_unique" UNIQUE ("matchId", "side"),
    CONSTRAINT "MatchParticipant_match_player_unique" UNIQUE ("matchId", "playerId")
);
CREATE INDEX "MatchParticipant_playerId_idx" ON "MatchParticipant"("playerId");

CREATE TRIGGER set_match_participant_updated_at BEFORE UPDATE ON "MatchParticipant" FOR EACH ROW EXECUTE PROCEDURE set_updated_at();
