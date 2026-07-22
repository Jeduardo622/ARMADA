import { readFileSync } from 'node:fs';
import { resolve } from 'node:path';
import { describe, it, expect, afterEach, afterAll } from 'vitest';
import { buildServer } from '../src/app.js';

const app = buildServer({ testing: true });

/* eslint-disable @typescript-eslint/no-explicit-any */
const flags = app.flags as any;
const prisma = app.prisma as any;
/* eslint-enable @typescript-eslint/no-explicit-any */
const originalIsEnabled = flags.isEnabled;
const originalReady = flags.ready;
const originalFlagLookup = prisma.featureFlag.findUnique;

afterEach(() => {
  flags.isEnabled = originalIsEnabled;
  flags.ready = originalReady;
  prisma.featureFlag.findUnique = originalFlagLookup;
});

afterAll(async () => {
  await app.close();
});

// /config/:namespace is the simplest ensureFlag-gated route: when the flag
// check passes, the stub prisma has no snapshot so it returns 404
// config_not_found — distinct from the 403 feature_disabled gate.
const fetchConfig = () => app.inject({ method: 'GET', url: '/config/gameplay' });

describe('ensureFlag kill-switch semantics', () => {
  it('treats a ready flag service reporting disabled as final, even with an enabled DB row', async () => {
    let fallbackLookups = 0;
    flags.ready = () => true;
    flags.isEnabled = () => false;
    prisma.featureFlag.findUnique = async () => {
      fallbackLookups += 1;
      return { enabled: true };
    };

    const res = await fetchConfig();
    expect(res.statusCode).toBe(403);
    expect(res.json()).toEqual({ error: 'feature_disabled', flag: 'config_api' });
    // The kill switch must not depend on DB state at all when the service is
    // healthy: no fallback read on an authoritative answer.
    expect(fallbackLookups).toBe(0);
  });

  it('falls back to an enabled DB row while the flag service is unavailable', async () => {
    flags.ready = () => false;
    flags.isEnabled = () => false;
    prisma.featureFlag.findUnique = async () => ({ enabled: true });

    const res = await fetchConfig();
    expect(res.statusCode).toBe(404);
    expect(res.json()).toEqual({ error: 'config_not_found' });
  });

  it('fails closed while the flag service is unavailable and the DB row is disabled', async () => {
    flags.ready = () => false;
    flags.isEnabled = () => false;
    prisma.featureFlag.findUnique = async () => ({ enabled: false });

    const res = await fetchConfig();
    expect(res.statusCode).toBe(403);
    expect(res.json()).toEqual({ error: 'feature_disabled', flag: 'config_api' });
  });

  it('fails closed while the flag service is unavailable and no DB row exists', async () => {
    flags.ready = () => false;
    flags.isEnabled = () => false;
    prisma.featureFlag.findUnique = async () => null;

    const res = await fetchConfig();
    expect(res.statusCode).toBe(403);
    expect(res.json()).toEqual({ error: 'feature_disabled', flag: 'config_api' });
  });
});

// Same source-pin pattern as tests/seed-catalog.test.ts: the seed script must
// never force a flag back on during a reseed, or an operator's DB kill-switch
// disable is silently reverted by the next deploy.
describe('feature flag seed conventions', () => {
  const seedSource = readFileSync(resolve(__dirname, '../prisma/seed.ts'), 'utf8');

  it('does not force-enable any flag on reseed', () => {
    expect(seedSource).not.toMatch(/update:\s*\{[^}]*\benabled\b/);
  });
});
