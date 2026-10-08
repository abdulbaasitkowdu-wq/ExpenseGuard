import { describe, expect, it } from 'vitest';
import { apiError } from './api';

describe('apiError', () => {
  it.each([
    [403, 'forbidden'],
    [409, 'conflict'],
    [422, 'validation'],
    [500, 'error'],
  ])('maps HTTP %s to %s state', (status, kind) => {
    expect(apiError({ response: { status, data: {} } }).kind).toBe(kind);
  });

  it('keeps problem details returned by the API', () => {
    expect(apiError({ response: { status: 409, data: { detail: 'Stale version.' } } }).message)
      .toBe('Stale version.');
  });
});
