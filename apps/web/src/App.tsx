import { Link, NavLink, Outlet } from 'react-router-dom';
import { useAuth } from './auth/useAuth';

/** Application shell: the header, the navigation, and wherever the router puts the page. */
export function App() {
  const { session, logout } = useAuth();
  return (
    <div className="app">
      <header className="app__header">
        <Link className="app__brand" to="/tickets">
          Northwind Support
        </Link>

        <nav className="app__nav" aria-label="Main">
          <NavLink to="/tickets">Tickets</NavLink>
          <NavLink to="/customers">Customers</NavLink>
        </nav>
        {session && (
          <div className="app__account">
            <span>{session.agent.fullName}</span>
            <button type="button" className="button button--ghost" onClick={logout}>Sign out</button>
          </div>
        )}
      </header>

      <main className="app__main">
        <Outlet />
      </main>
    </div>
  );
}
