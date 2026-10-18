import { fireEvent, render, screen, waitFor } from '@testing-library/react';
import { MemoryRouter, Route, Routes, useLocation } from 'react-router-dom';
import { afterEach, describe, expect, it, vi } from 'vitest';
import { request } from '../api/client';
import { AuthProvider } from './AuthContext';
import { RequireAuth } from './RequireAuth';
import { getAccessToken, saveAuthSession } from './authSession';
import { useAuth } from './useAuth';

function LoginDestination() {
  const location = useLocation();
  const from = (location.state as { from?: { pathname: string; search: string } } | null)?.from;
  return <p>Redirect target: {from?.pathname}{from?.search}</p>;
}

function ProtectedApiRequest() {
  const loadTickets = () => {
    void request('/tickets').catch(() => undefined);
  };
  return <button type="button" onClick={loadTickets}>Load protected tickets</button>;
}

function SignedInPage() {
  const { session, logout } = useAuth();
  return (
    <>
      <p>Signed in as {session?.agent.fullName}</p>
      <button type="button" onClick={logout}>Sign out</button>
    </>
  );
}

describe('RequireAuth', () => {
  afterEach(() => sessionStorage.clear());

  it('sends unauthenticated visitors to login with their requested location', () => {
    render(
      <AuthProvider>
        <MemoryRouter initialEntries={['/tickets/42?tab=history']}>
          <Routes>
            <Route path="/login" element={<LoginDestination />} />
            <Route element={<RequireAuth />}>
              <Route path="/tickets/:id" element={<p>Ticket detail</p>} />
            </Route>
          </Routes>
        </MemoryRouter>
      </AuthProvider>,
    );

    expect(screen.getByText('Redirect target: /tickets/42?tab=history')).toBeInTheDocument();
  });

  it('logout clears persisted and in-memory authentication state', async () => {
    saveAuthSession({
      accessToken: 'signed-token',
      expiresAtUtc: new Date(Date.now() + 60_000).toISOString(),
      agent: { id: 4, fullName: 'Sara Lindqvist', email: 'sara@example.test' },
    });

    render(
      <AuthProvider>
        <MemoryRouter initialEntries={['/tickets']}>
          <Routes>
            <Route path="/login" element={<p>Login page</p>} />
            <Route element={<RequireAuth />}>
              <Route path="/tickets" element={<SignedInPage />} />
            </Route>
          </Routes>
        </MemoryRouter>
      </AuthProvider>,
    );

    expect(screen.getByText('Signed in as Sara Lindqvist')).toBeInTheDocument();
    expect(getAccessToken()).toBe('signed-token');
    fireEvent.click(screen.getByRole('button', { name: 'Sign out' }));

    expect(await screen.findByText('Login page')).toBeInTheDocument();
    expect(getAccessToken()).toBeNull();
    expect(sessionStorage.length).toBe(0);
  });

  it('clears the session and redirects to login after a protected API request returns 401', async () => {
    saveAuthSession({
      accessToken: 'expired-token',
      expiresAtUtc: new Date(Date.now() + 60_000).toISOString(),
      agent: { id: 4, fullName: 'Sara Lindqvist', email: 'sara@example.test' },
    });
    vi.stubGlobal('fetch', vi.fn().mockResolvedValue(new Response(null, { status: 401 })));

    render(
      <AuthProvider>
        <MemoryRouter initialEntries={['/tickets']}>
          <Routes>
            <Route path="/login" element={<p>Login page</p>} />
            <Route element={<RequireAuth />}>
              <Route path="/tickets" element={<ProtectedApiRequest />} />
            </Route>
          </Routes>
        </MemoryRouter>
      </AuthProvider>,
    );

    fireEvent.click(screen.getByRole('button', { name: 'Load protected tickets' }));

    expect(await screen.findByText('Login page')).toBeInTheDocument();
    await waitFor(() => expect(getAccessToken()).toBeNull());
    expect(sessionStorage.length).toBe(0);
  });
});
