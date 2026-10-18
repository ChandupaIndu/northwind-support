import { fireEvent, render, screen } from '@testing-library/react';
import { MemoryRouter, Route, Routes, useLocation } from 'react-router-dom';
import { afterEach, describe, expect, it, vi } from 'vitest';
import { ApiError } from '../../api/client';
import { authApi } from '../../api/auth';
import { AuthProvider } from '../../auth/AuthContext';
import { LoginPage } from './LoginPage';

vi.mock('../../api/auth', () => ({ authApi: { login: vi.fn(), me: vi.fn() } }));

const loginApi = vi.mocked(authApi.login);

function CurrentPath() {
  const location = useLocation();
  return <output data-testid="current-path">{location.pathname}{location.search}{location.hash}</output>;
}

describe('LoginPage', () => {
  afterEach(() => {
    sessionStorage.clear();
    vi.clearAllMocks();
  });

  it('shows rejected credentials and stays on the login route', async () => {
    loginApi.mockRejectedValue(new ApiError(401, 'Invalid email or password.'));
    render(
      <AuthProvider>
        <MemoryRouter initialEntries={['/login']}>
          <Routes>
            <Route path="/login" element={<><LoginPage /><CurrentPath /></>} />
          </Routes>
        </MemoryRouter>
      </AuthProvider>,
    );

    fireEvent.change(screen.getByLabelText('Email'), { target: { value: 'alex@example.test' } });
    fireEvent.change(screen.getByLabelText('Password'), { target: { value: 'wrong-password' } });
    fireEvent.click(screen.getByRole('button', { name: 'Sign in' }));

    expect(await screen.findByRole('alert')).toHaveTextContent('Invalid email or password.');
    expect(screen.getByTestId('current-path')).toHaveTextContent('/login');
    expect(screen.getByLabelText('Password')).toHaveValue('wrong-password');
  });

  it('blocks invalid email input before calling the login API', async () => {
    render(
      <AuthProvider>
        <MemoryRouter initialEntries={['/login']}>
          <Routes><Route path="/login" element={<LoginPage />} /></Routes>
        </MemoryRouter>
      </AuthProvider>,
    );

    fireEvent.change(screen.getByLabelText('Email'), { target: { value: 'not-an-email' } });
    fireEvent.change(screen.getByLabelText('Password'), { target: { value: 'anything' } });
    fireEvent.click(screen.getByRole('button', { name: 'Sign in' }));

    expect(await screen.findByRole('alert')).toHaveTextContent('Enter a valid email address.');
    expect(loginApi).not.toHaveBeenCalled();
  });

  it('returns to the originally requested route after successful login', async () => {
    loginApi.mockResolvedValue({
      accessToken: 'signed-token',
      expiresAtUtc: new Date(Date.now() + 60_000).toISOString(),
      agent: { id: 4, fullName: 'Sara Lindqvist', email: 'sara@example.test' },
    });
    render(
      <AuthProvider>
        <MemoryRouter initialEntries={[{ pathname: '/login', state: { from: { pathname: '/tickets/42', search: '?tab=history', hash: '#history' } } }]}>
          <Routes>
            <Route path="/login" element={<LoginPage />} />
            <Route path="/tickets/:id" element={<CurrentPath />} />
          </Routes>
        </MemoryRouter>
      </AuthProvider>,
    );

    fireEvent.change(screen.getByLabelText('Email'), { target: { value: 'sara@example.test' } });
    fireEvent.change(screen.getByLabelText('Password'), { target: { value: 'fixture-password' } });
    fireEvent.click(screen.getByRole('button', { name: 'Sign in' }));

    expect(await screen.findByTestId('current-path')).toHaveTextContent('/tickets/42?tab=history#history');
    expect(loginApi).toHaveBeenCalledWith('sara@example.test', 'fixture-password');
  });
});
