import { describe, expect, it } from 'vitest';
import { classifyTask } from '../../scripts/harness/classifier.mjs';
import fixtures from './fixtures/routing.json';

describe('classifyTask', () => {
  it.each(fixtures)('$id', (fixture) => {
    expect(classifyTask(fixture.input)).toEqual(fixture.expected);
  });
});
