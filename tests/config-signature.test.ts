import { readFileSync } from 'node:fs';
import { describe, expect, it, vi } from 'vitest';
import { buildServer } from '../src/app.js';

describe('Unity config signature fixture', () => {
  it('matches the actual config route response and test-only signing key', async () => {
    const fixture = JSON.parse(readFileSync('unity/Assets/Tests/EditMode/config-signature.json', 'utf8'));
    expect(fixture.testOnlySigningKey).toBe(process.env.CONFIG_SIGNING_KEY);
    const app = buildServer({ testing: true });
    const lookup = vi.spyOn(app.prisma.configSnapshot, 'findFirst').mockResolvedValue(fixture.response.config);
    try {
      const response = await app.inject({ method: 'GET', url: '/config/signature-test' });
      expect(response.statusCode).toBe(200);
      expect(response.json()).toEqual(fixture.response);
      expect(response.json().signature).toMatch(/^[a-f0-9]{64}$/);
    } finally {
      lookup.mockRestore();
      await app.close();
    }
  });
});
