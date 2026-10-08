import { describe, expect, it } from 'vitest';
import { apiErrorMessage } from './Shared';

describe('apiErrorMessage', () => {
  it('does not call a failed login a session expiry', () => {
    expect(apiErrorMessage({ response: { status: 401 }, config: { url: '/auth/login' } }))
      .toBe('Username or password is incorrect.');
  });

  it('keeps session-expired copy for later 401s', () => {
    expect(apiErrorMessage({ response: { status: 401 }, config: { url: '/claims' } }))
      .toBe('Your session expired. Sign in again.');
  });
});
