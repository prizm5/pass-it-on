import { useEffect, useState } from 'react';
import { Link, useLocation, useNavigate } from 'react-router-dom';
import { api, type ListingSummary, type Profile } from '../lib/api';
import { clearAuth, loadAuth, saveAuth, type StoredAuth } from '../lib/auth';

function formatDate(value: string) {
  return new Intl.DateTimeFormat(undefined, {
    month: 'short',
    day: 'numeric',
    year: 'numeric',
  }).format(new Date(value));
}

function toSocialErrorMessage(reason: string | null) {
  if (!reason) {
    return 'Social sign-in failed. Please try again.';
  }

  const mappedReasons: Record<string, string> = {
    oauth_authentication_failed: 'We could not verify your provider session. Please try again.',
    oauth_principal_missing: 'Provider sign-in did not return user details. Please try again.',
    oauth_required_claims_missing: 'Your provider account is missing required profile details (email or account id).',
    oauth_user_not_found: 'Your linked account could not be found. Please try again.',
    oauth_user_inactive: 'Your account is not active. Please contact support.',
    incomplete_payload: 'The sign-in response was incomplete. Please try again.',
    invalid_token_payload: 'The sign-in response was invalid. Please try again.',
  };

  return mappedReasons[reason] ?? `Social sign-in failed (${reason.replaceAll('_', ' ')}).`;
}

export function ProfilePage() {
  const location = useLocation();
  const navigate = useNavigate();
  const [auth, setAuth] = useState<StoredAuth | null>(() => loadAuth());
  const [profile, setProfile] = useState<Profile | null>(null);
  const [myListings, setMyListings] = useState<ListingSummary[]>([]);
  const [loading, setLoading] = useState(false);
  const [message, setMessage] = useState<string | null>(null);
  const [socialMessage, setSocialMessage] = useState<string | null>(null);
  const [loginEmail, setLoginEmail] = useState('');
  const [loginPassword, setLoginPassword] = useState('');
  const [registerEmail, setRegisterEmail] = useState('');
  const [registerPassword, setRegisterPassword] = useState('');
  const [registerDisplayName, setRegisterDisplayName] = useState('');
  const [displayName, setDisplayName] = useState('');
  const [preferredName, setPreferredName] = useState('');
  const [phone, setPhone] = useState('');

  useEffect(() => {
    const query = new URLSearchParams(location.search);
    const socialFlag = query.get('social');
    const reason = query.get('reason');

    if (!socialFlag) {
      return;
    }

    if (socialFlag === 'success') {
      setSocialMessage('Signed in with social account.');
    } else if (socialFlag === 'error') {
      setSocialMessage(toSocialErrorMessage(reason));
    }

    query.delete('social');
    query.delete('reason');
    const nextSearch = query.toString();
    void navigate(
      {
        pathname: location.pathname,
        search: nextSearch ? `?${nextSearch}` : '',
      },
      { replace: true },
    );
  }, [location.pathname, location.search, navigate]);

  useEffect(() => {
    const syncAuth = () => setAuth(loadAuth());
    window.addEventListener('pass-it-on:auth-changed', syncAuth);
    return () => window.removeEventListener('pass-it-on:auth-changed', syncAuth);
  }, []);

  useEffect(() => {
    if (!auth) {
      setProfile(null);
      setMyListings([]);
      return;
    }

    let cancelled = false;

    async function load() {
      try {
        setLoading(true);
        setMessage(null);
        const [profilePayload, listingsPayload] = await Promise.all([
          api.getProfile(),
          api.getMyListings(),
        ]);
        if (!cancelled) {
          setProfile(profilePayload);
          setDisplayName(profilePayload.displayName);
          setPreferredName(profilePayload.preferredName ?? '');
          setPhone(profilePayload.phone ?? '');
          setMyListings(listingsPayload.items);
        }
      } catch (err) {
        if (!cancelled) {
          setMessage(err instanceof Error ? err.message : 'Unable to load your profile.');
        }
      } finally {
        if (!cancelled) {
          setLoading(false);
        }
      }
    }

    void load();

    return () => {
      cancelled = true;
    };
  }, [auth]);

  async function login(event: React.FormEvent<HTMLFormElement>) {
    event.preventDefault();
    try {
      setMessage('Signing in...');
      const nextAuth = await api.login({ email: loginEmail, password: loginPassword });
      saveAuth(nextAuth);
      setMessage('Signed in.');
    } catch (err) {
      setMessage(err instanceof Error ? err.message : 'Unable to sign in.');
    }
  }

  async function register(event: React.FormEvent<HTMLFormElement>) {
    event.preventDefault();
    try {
      setMessage('Creating account...');
      const nextAuth = await api.register({
        email: registerEmail,
        password: registerPassword,
        displayName: registerDisplayName,
      });
      saveAuth(nextAuth);
      setMessage('Account created and signed in.');
    } catch (err) {
      setMessage(err instanceof Error ? err.message : 'Unable to create account.');
    }
  }

  function startSocialLogin(provider: 'google' | 'facebook') {
    window.location.href = api.getOAuthStartUrl(provider, '/auth/callback');
  }

  async function updateProfile(event: React.FormEvent<HTMLFormElement>) {
    event.preventDefault();
    try {
      setMessage('Saving profile...');
      const updated = await api.updateProfile({ displayName, preferredName, phone });
      setProfile(updated);
      const current = loadAuth();
      if (current) {
        saveAuth({
          ...current,
          user: {
            ...current.user,
            displayName: updated.displayName,
          },
        });
      }
      setMessage('Profile updated.');
    } catch (err) {
      setMessage(err instanceof Error ? err.message : 'Unable to update profile.');
    }
  }

  async function requestDeletion() {
    try {
      const response = await api.requestDeletion();
      setProfile((current) => current ? { ...current, status: 'DeletionRequested' } : current);
      setMessage(response.message);
    } catch (err) {
      setMessage(err instanceof Error ? err.message : 'Unable to request deletion.');
    }
  }

  async function cancelDeletion() {
    try {
      const response = await api.cancelDeletion();
      setProfile((current) => current ? { ...current, status: 'Active' } : current);
      setMessage(response.message);
    } catch (err) {
      setMessage(err instanceof Error ? err.message : 'Unable to cancel deletion.');
    }
  }

  async function signOut() {
    const current = loadAuth();
    try {
      if (current) {
        await api.logout(current.refreshToken);
      }
    } catch {
      // Local sign-out still matters even if the API call fails.
    }
    clearAuth();
    setMessage('Signed out.');
  }

  async function updateListingStatus(listingId: string, action: 'activate' | 'archive' | 'unavailable') {
    try {
      setMessage('Updating listing...');
      if (action === 'activate') {
        await api.activateListing(listingId);
      } else if (action === 'archive') {
        await api.archiveListing(listingId);
      } else {
        await api.markListingUnavailable(listingId);
      }

      const refreshed = await api.getMyListings();
      setMyListings(refreshed.items);
      setMessage('Listing updated.');
    } catch (err) {
      setMessage(err instanceof Error ? err.message : 'Unable to update listing.');
    }
  }

  if (!auth) {
    return (
      <section className="page-stack">
        <header className="section-heading">
          <p className="eyebrow">Profile</p>
          <h2>Sign in or create your account</h2>
          <p className="section-copy">
            Sign in with email/password or continue with Google/Facebook.
          </p>
        </header>

        <div className="page-grid auth-grid">
          <article className="form-card">
            <h3>Continue with social account</h3>
            <p className="muted-copy">Use your provider account to sign in or create a new account in one step.</p>
            <div className="listing-row-actions">
              <button className="secondary-button" type="button" onClick={() => startSocialLogin('google')}>
                Continue with Google
              </button>
              <button className="secondary-button" type="button" onClick={() => startSocialLogin('facebook')}>
                Continue with Facebook
              </button>
            </div>
          </article>
        </div>

        <div className="page-grid auth-grid">
          <form className="form-card" onSubmit={login}>
            <h3>Sign in</h3>
            <label>
              <span>Email</span>
              <input type="email" value={loginEmail} onChange={(event) => setLoginEmail(event.target.value)} required />
            </label>
            <label>
              <span>Password</span>
              <input type="password" value={loginPassword} onChange={(event) => setLoginPassword(event.target.value)} required />
            </label>
            <button className="primary-button" type="submit">Sign in</button>
          </form>

          <form className="form-card" onSubmit={register}>
            <h3>Create account</h3>
            <label>
              <span>Display name</span>
              <input value={registerDisplayName} onChange={(event) => setRegisterDisplayName(event.target.value)} required />
            </label>
            <label>
              <span>Email</span>
              <input type="email" value={registerEmail} onChange={(event) => setRegisterEmail(event.target.value)} required />
            </label>
            <label>
              <span>Password</span>
              <input type="password" value={registerPassword} onChange={(event) => setRegisterPassword(event.target.value)} required />
            </label>
            <button className="primary-button" type="submit">Create account</button>
          </form>
        </div>

        {message ? <p className="status-banner">{message}</p> : null}
      </section>
    );
  }

  return (
    <section className="page-stack">
      <header className="section-heading">
        <p className="eyebrow">Profile</p>
        <h2>{profile?.displayName ?? auth.user.displayName}</h2>
        <p className="section-copy">
          Edit your contact details, manage your active listings, and control your account deletion request.
        </p>
      </header>

      {loading ? <p className="status-banner">Loading your account...</p> : null}
      {socialMessage ? <p className="status-banner">{socialMessage}</p> : null}
      {message ? <p className="status-banner">{message}</p> : null}

      <div className="page-grid profile-grid">
        <form className="form-card" onSubmit={updateProfile}>
          <div className="section-heading compact-heading">
            <div>
              <p className="eyebrow">Account</p>
              <h3>Profile details</h3>
            </div>
            <button className="ghost-button" type="button" onClick={signOut}>Sign out</button>
          </div>
          <label>
            <span>Email</span>
            <input value={profile?.email ?? auth.user.email} disabled />
          </label>
          <label>
            <span>Display name</span>
            <input value={displayName} onChange={(event) => setDisplayName(event.target.value)} required />
          </label>
          <label>
            <span>Preferred name</span>
            <input value={preferredName} onChange={(event) => setPreferredName(event.target.value)} />
          </label>
          <label>
            <span>Phone</span>
            <input value={phone} onChange={(event) => setPhone(event.target.value)} />
          </label>
          <button className="primary-button" type="submit">Save profile</button>
        </form>

        <article className="info-card danger-card">
          <h3>Account lifecycle</h3>
          <p>
            Member since {profile ? formatDate(profile.createdAt) : 'today'}. Current status: {profile?.status ?? 'Active'}.
          </p>
          {profile?.status === 'DeletionRequested' ? (
            <button className="secondary-button" type="button" onClick={cancelDeletion}>
              Cancel deletion request
            </button>
          ) : (
            <button className="secondary-button danger" type="button" onClick={requestDeletion}>
              Request account deletion
            </button>
          )}
          <p className="muted-copy">
            Deletion uses a grace period and revokes your active sessions immediately.
          </p>
        </article>
      </div>

      <article className="info-card">
        <div className="section-heading compact-heading">
          <div>
            <p className="eyebrow">Your listings</p>
            <h3>Inventory management</h3>
          </div>
          <Link className="primary-button" to="/listings/new">Post a new item</Link>
        </div>

        <div className="stack-list">
          {myListings.length > 0 ? (
            myListings.map((listing) => (
              <article key={listing.id} className="listing-row">
                <div>
                  <Link className="text-link" to={`/listings/${listing.id}`}>{listing.title}</Link>
                  <p>{listing.category}{listing.size ? ` · Size ${listing.size}` : ''}</p>
                </div>
                <div className="listing-row-actions">
                  <span className="chip subdued">{listing.status}</span>
                  <button className="secondary-button" type="button" onClick={() => updateListingStatus(listing.id, 'activate')}>
                    Activate
                  </button>
                  <button className="secondary-button" type="button" onClick={() => updateListingStatus(listing.id, 'unavailable')}>
                    Mark unavailable
                  </button>
                  <button className="secondary-button" type="button" onClick={() => updateListingStatus(listing.id, 'archive')}>
                    Archive
                  </button>
                </div>
              </article>
            ))
          ) : (
            <p>You have not posted any listings yet.</p>
          )}
        </div>
      </article>
    </section>
  );
}
