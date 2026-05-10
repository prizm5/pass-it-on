export type StoredAdminAuth = {
  accessToken: string;
  refreshToken: string;
  expiresInSeconds: number;
  user: {
    id: string;
    email: string;
    displayName: string;
    role: string;
  };
};

const STORAGE_KEY = 'pass-it-on.admin-auth';

export function loadAdminAuth(): StoredAdminAuth | null {
  const raw = window.localStorage.getItem(STORAGE_KEY);
  if (!raw) {
    return null;
  }

  try {
    return JSON.parse(raw) as StoredAdminAuth;
  } catch {
    window.localStorage.removeItem(STORAGE_KEY);
    return null;
  }
}

export function saveAdminAuth(auth: StoredAdminAuth) {
  window.localStorage.setItem(STORAGE_KEY, JSON.stringify(auth));
  window.dispatchEvent(new CustomEvent('pass-it-on:admin-auth-changed'));
}

export function clearAdminAuth() {
  window.localStorage.removeItem(STORAGE_KEY);
  window.dispatchEvent(new CustomEvent('pass-it-on:admin-auth-changed'));
}
