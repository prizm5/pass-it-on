import { useEffect, useState } from 'react';
import { adminApi, type AdminUserDetail, type AdminUserSummary } from '../lib/api';
import { loadAdminAuth } from '../lib/auth';

function formatDate(value: string) {
  return new Intl.DateTimeFormat(undefined, {
    month: 'short',
    day: 'numeric',
    year: 'numeric',
  }).format(new Date(value));
}

export function UsersPage() {
  const auth = loadAdminAuth();
  const [users, setUsers] = useState<AdminUserSummary[]>([]);
  const [selectedUser, setSelectedUser] = useState<AdminUserDetail | null>(null);
  const [search, setSearch] = useState('');
  const [message, setMessage] = useState<string | null>(null);

  async function loadUsers(currentSearch = search) {
    try {
      const query = currentSearch ? `?q=${encodeURIComponent(currentSearch)}&page=1&pageSize=20` : '?page=1&pageSize=20';
      const payload = await adminApi.getUsers(query);
      setUsers(payload.items);
    } catch (err) {
      setMessage(err instanceof Error ? err.message : 'Unable to load users.');
    }
  }

  useEffect(() => {
    if (!auth || auth.user.role !== 'Admin') {
      return;
    }
    void loadUsers('');
  }, []);

  async function loadUserDetail(userId: string) {
    try {
      const payload = await adminApi.getUser(userId);
      setSelectedUser(payload);
    } catch (err) {
      setMessage(err instanceof Error ? err.message : 'Unable to load user detail.');
    }
  }

  async function runAction(action: 'suspend' | 'restore' | 'delete', userId: string) {
    const reason = window.prompt(`Reason for ${action}ing this user:`)?.trim();
    if (!reason) {
      return;
    }

    try {
      if (action === 'suspend') {
        await adminApi.suspendUser(userId, reason);
      } else if (action === 'restore') {
        await adminApi.restoreUser(userId, reason);
      } else {
        await adminApi.deleteUser(userId, reason);
      }

      await loadUsers();
      if (selectedUser?.id === userId) {
        await loadUserDetail(userId);
      }
      setMessage(`User ${action} action completed.`);
    } catch (err) {
      setMessage(err instanceof Error ? err.message : `Unable to ${action} user.`);
    }
  }

  if (!auth || auth.user.role !== 'Admin') {
    return <section className="page-stack"><p className="status-banner error">Sign in as an admin from the dashboard to access user moderation.</p></section>;
  }

  return (
    <section className="page-stack">
      <header className="section-heading">
        <p className="eyebrow">Users</p>
        <h2>User moderation queue</h2>
        <p className="section-copy">Search member accounts, inspect sign-in methods, and apply moderation actions.</p>
      </header>

      <article className="info-card filter-card">
        <div className="filter-grid single-action-grid">
          <label>
            <span>Search by email or name</span>
            <input value={search} onChange={(event) => setSearch(event.target.value)} placeholder="guardian@example.com" />
          </label>
          <button className="primary-button" type="button" onClick={() => void loadUsers(search)}>Search</button>
        </div>
      </article>

      {message ? <p className="status-banner">{message}</p> : null}

      <div className="dashboard-grid">
        <article className="info-card">
          <div className="table-stack">
            {users.map((user) => (
              <button key={user.id} className="table-row button-reset" type="button" onClick={() => void loadUserDetail(user.id)}>
                <div>
                  <strong>{user.displayName}</strong>
                  <p>{user.email}</p>
                </div>
                <div className="row-meta">
                  <span className="chip subdued">{user.status}</span>
                  <span className="card-meta">{formatDate(user.createdAt)}</span>
                </div>
              </button>
            ))}
          </div>
        </article>

        <article className="info-card">
          {selectedUser ? (
            <div className="page-stack">
              <div>
                <h3>{selectedUser.displayName}</h3>
                <p>{selectedUser.email}</p>
              </div>
              <div className="info-grid compact">
                <div><span className="info-label">Status</span><strong>{selectedUser.status}</strong></div>
                <div><span className="info-label">Role</span><strong>{selectedUser.role}</strong></div>
                <div><span className="info-label">Phone</span><strong>{selectedUser.phone || 'Not set'}</strong></div>
                <div><span className="info-label">Deletion requested</span><strong>{selectedUser.deletionRequestedAt ? formatDate(selectedUser.deletionRequestedAt) : 'No'}</strong></div>
              </div>
              <div>
                <span className="info-label">Auth providers</span>
                <div className="chip-row">
                  {selectedUser.authIdentities.map((identity) => (
                    <span key={identity.provider} className="chip">{identity.provider}</span>
                  ))}
                </div>
              </div>
              <div className="button-row">
                <button className="secondary-button" type="button" onClick={() => void runAction('suspend', selectedUser.id)}>Suspend</button>
                <button className="secondary-button" type="button" onClick={() => void runAction('restore', selectedUser.id)}>Restore</button>
                <button className="secondary-button danger" type="button" onClick={() => void runAction('delete', selectedUser.id)}>Delete</button>
              </div>
            </div>
          ) : (
            <p>Select a user to inspect moderation details.</p>
          )}
        </article>
      </div>
    </section>
  );
}
