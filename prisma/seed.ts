import { PrismaClient } from '@prisma/client';
import crypto from 'crypto';

const prisma = new PrismaClient();

const missionSeeds = [
  { code: 'mission-01', name: 'Fair Wind', description: 'Intro mission' },
  { code: 'mission-02', name: 'Weather Gage', description: 'Wind awareness' },
  { code: 'mission-03', name: 'Raking Shot', description: 'Positioning drill' }
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

