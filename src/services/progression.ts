import type { PlayerProgression, Prisma } from '@prisma/client';

const thresholds = [0, 100, 250, 450, 700];
export function progressionResponse(profile: PlayerProgression) {
  const level = thresholds.filter(threshold => profile.xp >= threshold).length;
  return {
    captain: { id: 'aurora_black', name: 'Aurora Black', rarity: 'Rare', xp: profile.xp, level, nextLevelXp: thresholds[level] ?? null },
    training: { sequence: profile.trainingSequence, nextSequence: profile.trainingSequence + 1, shardCost: 1, xpPerTraining: 25 },
    crew: { firstMate: profile.firstMate, gunneryChief: profile.gunneryChief, roster: [
      { id: 'calico_jim', name: 'Calico Jim', slot: 'firstMate' },
      { id: 'one_eyed_ella', name: 'One-Eyed Ella', slot: 'gunneryChief' }
    ] }
  };
}

export async function lockProgression(tx: Prisma.TransactionClient, playerId: string) {
  await tx.$executeRaw`INSERT INTO "PlayerProgression" ("playerId") VALUES (${playerId}::uuid) ON CONFLICT ("playerId") DO NOTHING`;
  // One lock order for all writers: profile before inventory. XP earned during
  // concurrent training/mission clears cannot overwrite the other increment.
  await tx.$queryRaw`SELECT "playerId" FROM "PlayerProgression" WHERE "playerId" = ${playerId}::uuid FOR UPDATE`;
  return tx.playerProgression.findUniqueOrThrow({ where: { playerId } });
}

export async function grantVerifiedClearXp(tx: Prisma.TransactionClient, playerId: string, missionId: string) {
  const claimed = await tx.missionProgress.updateMany({
    where: { playerId, missionId, captainXpGranted: false }, data: { captainXpGranted: true }
  });
  if (claimed.count !== 1) return;
  const profile = await lockProgression(tx, playerId);
  await tx.playerProgression.update({ where: { playerId }, data: { xp: Math.min(700, profile.xp + 25) } });
}
