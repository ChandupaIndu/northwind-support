import { useState, type FormEvent } from 'react';
import { useLocation, useNavigate, Navigate } from 'react-router-dom';
import { toErrorMessage } from '../../api/client';
import { useAuth } from '../../auth/useAuth';

interface ReturnLocation {
  pathname: string;
  search: string;
  hash: string;
}

interface LoginLocationState {
  from?: ReturnLocation;
}

export function LoginPage() {
  const { session, login } = useAuth();
  const navigate = useNavigate();
  const location = useLocation();
  const [email, setEmail] = useState('');
  const [password, setPassword] = useState('');
  const [error, setError] = useState<string>();
  const [validationError, setValidationError] = useState<string>();
  const [isSubmitting, setIsSubmitting] = useState(false);

  const from = (location.state as LoginLocationState | null)?.from;
  const destination = from ? `${from.pathname}${from.search}${from.hash}` : '/tickets';

  if (session) return <Navigate to={destination} replace />;

  const submit = async (event: FormEvent<HTMLFormElement>) => {
    event.preventDefault();
    const cleanEmail = email.trim();
    if (!/^[^\s@]+@[^\s@]+\.[^\s@]+$/.test(cleanEmail)) {
      setValidationError('Enter a valid email address.');
      setError(undefined);
      return;
    }
    if (!password) {
      setValidationError('Enter your password.');
      setError(undefined);
      return;
    }

    setValidationError(undefined);
    setError(undefined);
    setIsSubmitting(true);
    try {
      await login(cleanEmail, password);
      await navigate(destination, { replace: true });
    } catch (cause) {
      setError(toErrorMessage(cause, 'Unable to sign in. Check your credentials and try again.'));
    } finally {
      setIsSubmitting(false);
    }
  };

  return (
    <main className="auth-page">
      <section className="card auth-card" aria-labelledby="login-heading">
        <h1 id="login-heading">Sign in</h1>
        <form className="form" onSubmit={(event) => void submit(event)} noValidate>
          <div className="field">
            <label className="field__label" htmlFor="login-email">Email</label>
            <input
              id="login-email"
              type="email"
              autoComplete="username"
              required
              value={email}
              onChange={(event) => setEmail(event.target.value)}
              aria-describedby={validationError ? 'login-validation-error' : undefined}
            />
          </div>
          <div className="field">
            <label className="field__label" htmlFor="login-password">Password</label>
            <input
              id="login-password"
              type="password"
              autoComplete="current-password"
              required
              value={password}
              onChange={(event) => setPassword(event.target.value)}
              aria-describedby={validationError ? 'login-validation-error' : undefined}
            />
          </div>
          {validationError && <p id="login-validation-error" className="field__error" role="alert">{validationError}</p>}
          {error && <p className="field__error" role="alert">{error}</p>}
          <button type="submit" className="button button--primary" disabled={isSubmitting}>
            {isSubmitting ? 'Signing in...' : 'Sign in'}
          </button>
        </form>
      </section>
    </main>
  );
}
