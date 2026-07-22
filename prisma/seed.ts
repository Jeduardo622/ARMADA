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
  },
  {
    code: 'mission-08-eye-of-the-wind',
    name: 'Eye of the Wind',
    description: 'Wind turn-rate tactics'
  },
  { code: 'mission-09-iron-bow', name: 'Iron Bow', description: 'Ramming tactics' },
  {
    code: 'mission-10-sail-cutter',
    name: 'Sail-Cutter',
    description: 'Chain-shot ammo choice'
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
  { name: 'config_api', description: 'Serve config snapshots' },
  // Player-facing PvP match lifecycle; unlike inventory_grant_api this mints
  // nothing, so it is seeded enabled with the other player-facing flags.
  { name: 'pvp_api', description: 'Enable PvP match endpoints' }
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

  // Player-facing flags start enabled on first create only; reseeds preserve
  // the current enabled state so an operator's DB kill-switch disable survives
  // redeploys (tests/flags.test.ts pins this convention).
  for (const flag of featureFlags) {
    await prisma.featureFlag.upsert({
      where: { name: flag.name },
      update: {},
      create: { ...flag, enabled: true }
    });
  }

  // Trusted-service gate for POST /inventory/{playerId}/grant: seeded disabled
  // and never force-enabled, so players cannot mint economy materials unless
  // an operator deliberately flips it (update: {} preserves that choice).
  await prisma.featureFlag.upsert({
    where: { name: 'inventory_grant_api' },
    update: {},
    create: {
      name: 'inventory_grant_api',
      description: 'Enable trusted-service inventory grants (mints items; keep off in production)',
      enabled: false
    }
  });
}

main()
  .catch((e) => {
    console.error(e);
    process.exit(1);
  })
  .finally(async () => {
    await prisma.$disconnect();
  });

