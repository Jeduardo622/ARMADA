-- Additive only: legacy completions remain unrated until replay verification.
ALTER TABLE "MissionProgress"
  ADD COLUMN "verifiedResult" JSONB,
  ADD COLUMN "verifiedStars" INTEGER,
  ADD CONSTRAINT "MissionProgress_verified_result_pair" CHECK (
    ("verifiedResult" IS NULL AND "verifiedStars" IS NULL)
    OR ("verifiedResult" IS NOT NULL AND "verifiedStars" IS NOT NULL AND "verifiedStars" BETWEEN 1 AND 3)
  );
