import { useEffect, useState } from 'react';
import { Link, useNavigate } from 'react-router-dom';
import { saveAuth } from '../lib/auth';

export function AuthCallbackPage() {
  const navigate = useNavigate();
  const [message, setMessage] = useState('Finalizing social sign-in...');

  function redirectToProfile(status: 'success' | 'error', reason?: string) {
    const query = new URLSearchParams({ social: status });
    if (reason) {
      query.set('reason', reason);
    }
    void navigate(`/profile?${query.toString()}`, { replace: true });
  }

  useEffect(() => {
    const hash = window.location.hash.startsWith('#') ? window.location.hash.slice(1) : window.location.hash;
    if (!hash) {
      setMessage('No social sign-in payload was found.');
      return;
    }

    const params = new URLSearchParams(hash);
    const oauthError = params.get('oauthError');
    if (oauthError) {
      window.history.replaceState({}, document.title, `${window.location.pathname}${window.location.search}`);
      redirectToProfile('error', oauthError);
      return;
    }

    const accessToken = params.get('accessToken');
    const refreshToken = params.get('refreshToken');
    const expiresInSecondsRaw = params.get('expiresInSeconds');
    const userId = params.get('userId');
    const userEmail = params.get('userEmail');
    const userDisplayName = params.get('userDisplayName');
    const userRole = params.get('userRole');

    if (
      !accessToken ||
      !refreshToken ||
      !expiresInSecondsRaw ||
      !userId ||
      !userEmail ||
      !userDisplayName ||
      !userRole
    ) {
      redirectToProfile('error', 'incomplete_payload');
      return;
    }

    const expiresInSeconds = Number.parseInt(expiresInSecondsRaw, 10);
    if (Number.isNaN(expiresInSeconds)) {
      redirectToProfile('error', 'invalid_token_payload');
      return;
    }

    saveAuth({
      accessToken,
      refreshToken,
      expiresInSeconds,
      user: {
        id: userId,
        email: userEmail,
        displayName: userDisplayName,
        role: userRole,
      },
    });

    window.history.replaceState({}, document.title, `${window.location.pathname}${window.location.search}`);
    redirectToProfile('success');
  }, [navigate]);

  return (
    <section className="page-stack">
      <header className="section-heading">
        <p className="eyebrow">Profile</p>
        <h2>Social sign-in</h2>
        <p className="section-copy">{message}</p>
      </header>

      <Link className="text-link" to="/profile">Return to profile</Link>
    </section>
  );
}
