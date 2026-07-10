import { describe, expect, it } from 'vitest';

describe('test environment', () => {
  it('loads explicit non-secret service configuration', () => {
    expect(process.env.NODE_ENV).toBe('test');
    expect(process.env.JWT_SECRET).toBe('test-only-jwt-secret');
    expect(process.env.DATABASE_URL).toBe('postgres://test:test@localhost:5432/armada_test');
  });
});
