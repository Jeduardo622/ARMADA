import { describe, it, expect, beforeAll, afterAll } from 'vitest';
import { buildServer } from '../src/app.js';

const app = buildServer({ testing: true });

beforeAll(async () => {
  await app.ready();
});

afterAll(async () => {
  await app.close();
});

describe('health endpoints', () => {
  it('returns ok for healthz', async () => {
    const res = await app.inject({ method: 'GET', url: '/healthz' });
    expect(res.statusCode).toBe(200);
    expect(res.json()).toHaveProperty('status', 'ok');
  });

  it('returns ready for readyz', async () => {
    const res = await app.inject({ method: 'GET', url: '/readyz' });
    expect(res.statusCode).toBe(200);
    expect(res.json()).toHaveProperty('status', 'ready');
  });
});

describe('sim stub', () => {
  it('is deterministic for same payload', async () => {
    const payload = {
      schemaVersion: 1,
      seed: 42,
      turn: 1,
      state: {
        turn: 1,
        wind: { direction: 90, speed: 5 },
        ships: [
          {
            id: 's1',
            side: 'player',
            position: { x: 0, y: 0 },
            heading: 90,
            speed: 3,
            hp: 120,
            sail: 80,
            crew: 50
          },
          {
            id: 's2',
            side: 'enemy',
            position: { x: 120, y: 0 },
            heading: 270,
            speed: 2,
            hp: 120,
            sail: 80,
            crew: 40
          }
        ]
      },
      orders: [
        { shipId: 's1', action: 'broadside', targetShipId: 's2', side: 'starboard', turnDelta: 0, speedDelta: 0 },
        { shipId: 's2', action: 'maneuver', turnDelta: -15, speedDelta: 1 }
      ]
    };

    const res1 = await app.inject({ method: 'POST', url: '/sim/preview', payload });
    const res2 = await app.inject({ method: 'POST', url: '/sim/preview', payload });
    const body1 = res1.json().result;
    const body2 = res2.json().result;
    expect(body1.hash).toEqual(body2.hash);
    expect(body1.events.some((e: { type: string }) => e.type === 'broadside')).toBe(true);
    expect(body1.summary).toHaveProperty('playerRemaining');
    expect(body1.summary).toHaveProperty('enemyRemaining');
  });

  it('rejects orders that reference unknown ships', async () => {
    const payload = {
      schemaVersion: 1,
      seed: 1,
      turn: 1,
      state: {
        turn: 1,
        wind: { direction: 0, speed: 0 },
        ships: [
          {
            id: 's1',
            side: 'player',
            position: { x: 0, y: 0 },
            heading: 0,
            speed: 0,
            hp: 10,
            sail: 10,
            crew: 5
          }
        ]
      },
      orders: [{ shipId: 'missing', action: 'pass' }]
    };

    const res = await app.inject({ method: 'POST', url: '/sim/preview', payload });
    expect(res.statusCode).toBe(400);
  });
});

describe('auth + ownership guards', () => {
  it('rejects tab-suffixed JSON content types', async () => {
    const res = await app.inject({
      method: 'POST',
      url: '/telemetry/ingest',
      headers: { 'content-type': 'application/json\ta' },
      payload: JSON.stringify({ schemaVersion: 1, payload: { event: 'test' } })
    });
    expect(res.statusCode).toBe(415);
  });

  it('blocks access when player id mismatches token subject', async () => {
    const res = await app.inject({
      method: 'GET',
      url: '/players/11111111-1111-1111-1111-111111111111'
    });
    expect(res.statusCode).toBe(403);
  });

  it('rejects oversized telemetry payloads', async () => {
    const bigPayload = { schemaVersion: 1, payload: { blob: 'x'.repeat(11000) } };
    const res = await app.inject({ method: 'POST', url: '/telemetry/ingest', payload: bigPayload });
    expect(res.statusCode).toBe(400);
  });
});

