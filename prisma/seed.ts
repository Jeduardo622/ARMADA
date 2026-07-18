import { PrismaClient } from '@prisma/client';
import crypto from 'crypto';

const prisma = new PrismaClient();

// Codes must match the runtime mission slugs (MISSION_0X_CODE in
// src/sim/mission0X.ts); tests/seed-catalog.test.ts pins the alignment.
const missionSeeds = [
  { code: 'mission-01-fair-wind', name: 'Fair Wind', description: 'Intro mission' },
  { code: 'mission-02-weather-gage', name: 'Weather Gage', description: 'Wind awareness' },
  { code: 'mission-03-raking-shot', name: 'Raking Shot', description: 'Positioning drill' },
  {
    code: 'mission-04-boarding-party',
    name: 'Boarding Party',
    description: 'Boarding risk/reward'
  },
  { code: 'mission-05-line-break', name: 'Line Break', description: 'Break the enemy line' },
  {
    code: 'mission-06-dreadnought-siege',
    name: 'Dreadnought Siege',
    description: 'Boss encounter with phases'
  },
  {
    code: 'mission-07-burning-seas',
    name: 'Burning Seas',
    description: 'Status effects: fire and slow'
  }
];

// Short codes seeded before the runtime slugs were finalized. Renamed in
// place so existing MissionProgress rows keep their mission references; the
// rename is skipped once a row with the full slug exists.
const legacyMissionCodes = [
  { from: 'mission-01', to: 'mission-01-fair-wind' },
  { from: 'mission-02', to: 'mission-02-weather-gage' },
  { from: 'mission-03', to: 'mission-03-raking-shot' }
];

const configContent = {
  balanceVersion: 1,
  rewards: { gold: 100, timber: 50 },
  drops: []
};

const featureFlags = [
  { name: 'missions_api', description: 'Enable missions endpoints' },
  { name: 'inventory_api', description: 'Enable inventory endpoints' },
  { name: 'sim_stub', description: 'Expose deterministic sim stub' },
  { name: 'telemetry_ingest', description: 'Allow telemetry ingestion' },
  { name: 'config_api', description: 'Serve config snapshots' }
];

function checksum(content: unknown) {
  return crypto.createHash('sha256').update(JSON.stringify(content)).digest('hex');
}

async function main() {
  for (const { from, to } of legacyMissionCodes) {
    const target = await prisma.mission.findUnique({ where: { code: to } });
    if (!target) {
      await prisma.mission.updateMany({ where: { code: from }, data: { code: to } });
    }
  }

  for (const mission of missionSeeds) {
    await prisma.mission.upsert({
      where: { code: mission.code },
      update: {},
      create: mission
    });
  }

  await prisma.configSnapshot.upsert({
    where: { namespace_version: { namespace: 'gameplay', version: 1 } },
    update: {},
    create: {
      namespace: 'gameplay',
      version: 1,
      content: configContent,
      checksum: checksum(configContent)
    }
  });

  for (const flag of featureFlags) {
    await prisma.featureFlag.upsert({
      where: { name: flag.name },
      update: { enabled: true },
      create: { ...flag, enabled: true }
    });
  }
}

main()
  .catch((e) => {
    console.error(e);
    process.exit(1);
  })
  .finally(async () => {
    await prisma.$disconnect();
  });

