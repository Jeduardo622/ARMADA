-- Additive; no XP inferred from legacy client results or completion status.
CREATE TABLE "PlayerProgression" (
  "playerId" UUID PRIMARY KEY REFERENCES "Player"("id") ON DELETE CASCADE ON UPDATE CASCADE,
  "xp" INTEGER NOT NULL DEFAULT 0 CHECK ("xp" BETWEEN 0 AND 700),
  "trainingSequence" INTEGER NOT NULL DEFAULT 0 CHECK ("trainingSequence" BETWEEN 0 AND 28),
  "firstMate" TEXT CHECK ("firstMate" IS NULL OR "firstMate" = 'calico_jim'),
  "gunneryChief" TEXT CHECK ("gunneryChief" IS NULL OR "gunneryChief" = 'one_eyed_ella')
);
ALTER TABLE "MissionProgress" ADD COLUMN "captainXpGranted" BOOLEAN NOT NULL DEFAULT false;
