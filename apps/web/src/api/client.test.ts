import { afterEach, describe, expect, it, vi } from 'vitest';
import { ApiError, request } from './client';
import { AUTH_EXPIRED_EVENT, clearAuthSession, getAccessToken, saveAuthSession } from '../auth/authSession';

describe('API request authentication', () => {
  afterEach(() => {
    clearAuthSession();
    vi.unstubAllGlobals();
    vi.restoreAllMocks();
  });

  it('attaches the session token as a Bearer header', async () => {
    saveAuthSession({
      accessToken: 'signed-token',
      expiresAtUtc: new Date(Date.now() + 60_000).toISOString(),
      agent: { id: 4, fullName: 'Sara Lindqvist', email: 'sara@example.test' },
    });
    const fetchMock = vi.fn().mockResolvedValue(new Response(JSON.stringify({ ok: true }), { status: 200 }));
    vi.stubGlobal('fetch', fetchMock);

    await request<{ ok: boolean }>('/tickets');

    expect(fetchMock).toHaveBeenCalledWith('/api/tickets', expect.objectContaining({
      headers: expect.objectContaining({ Authorization: 'Bearer signed-token' }),
    }));
  });

  it('clears the stored token and emits an auth-expired event for a protected 401', async () => {
    saveAuthSession({
      accessToken: 'expired-token',
      expiresAtUtc: new Date(Date.now() + 60_000).toISOString(),
      agent: { id: 4, fullName: 'Sara Lindqvist', email: 'sara@example.test' },
    });
    const fetchMock = vi.fn().mockResolvedValue(new Response(JSON.stringify({ detail: 'Unauthorized' }), { status: 401 }));
    vi.stubGlobal('fetch', fetchMock);
    const expired = vi.fn();
    window.addEventListener(AUTH_EXPIRED_EVENT, expired);

    await expect(request('/tickets')).rejects.toBeInstanceOf(ApiError);

    expect(getAccessToken()).toBeNull();
    expect(expired).toHaveBeenCalledOnce();
    window.removeEventListener(AUTH_EXPIRED_EVENT, expired);
  });

  it('does not treat rejected login credentials as an expired authenticated session', async () => {
    const fetchMock = vi.fn().mockResolvedValue(new Response(JSON.stringify({ detail: 'Invalid email or password.' }), { status: 401 }));
    vi.stubGlobal('fetch', fetchMock);
    const expired = vi.fn();
    window.addEventListener(AUTH_EXPIRED_EVENT, expired);

    await expect(request('/auth/login', { method: 'POST', body: '{}' })).rejects.toBeInstanceOf(ApiError);

    expect(expired).not.toHaveBeenCalled();
    window.removeEventListener(AUTH_EXPIRED_EVENT, expired);
  });
});
