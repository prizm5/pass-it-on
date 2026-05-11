import { clearAdminAuth, loadAdminAuth, saveAdminAuth, type StoredAdminAuth } from './auth';

const configuredApiBaseUrl = import.meta.env.VITE_API_URL as string | undefined;
const runningOnLocalhost = window.location.hostname === 'localhost' || window.location.hostname === '127.0.0.1';
const pointsToLocalhostApi =
  typeof configuredApiBaseUrl === 'string' &&
  /^https?:\/\/(localhost|127\.0\.0\.1)(:\d+)?\/api\/?$/i.test(configuredApiBaseUrl);

export const API_BASE_URL =
  !runningOnLocalhost && pointsToLocalhostApi
    ? `${window.location.origin}/api`
    : configuredApiBaseUrl ?? 'http://localhost:5200/api';

type RequestOptions = RequestInit & {
  auth?: boolean;
  _retryAfterRefresh?: boolean;
};

export type PagedResponse<T> = {
  items: T[];
  page: number;
  pageSize: number;
  totalCount: number;
};

export type AuthResponse = StoredAdminAuth;

export type AnalyticsSummary = {
  totalActiveUsers: number;
  totalListings: number;
  activeListings: number;
  openReports: number;
  publishedBulletins: number;
  asOf: string;
};

export type AnalyticsEvent = {
  id: string;
  eventName: string;
  actorUserId: string | null;
  subjectType: string;
  subjectId: string | null;
  occurredAt: string;
};

export type AdminUserSummary = {
  id: string;
  email: string;
  displayName: string;
  role: string;
  status: string;
  createdAt: string;
};

export type AdminUserDetail = {
  id: string;
  email: string;
  displayName: string;
  preferredName: string | null;
  phone: string | null;
  role: string;
  status: string;
  createdAt: string;
  deletionRequestedAt: string | null;
  authIdentities: Array<{
    provider: string;
    lastLoginAt: string | null;
  }>;
};

export type AdminListingSummary = {
  id: string;
  ownerUserId: string;
  ownerEmail: string;
  title: string;
  category: string;
  status: string;
  reportCount: number;
  createdAt: string;
};

export type AdminListingReport = {
  id: string;
  reporterUserId: string;
  reporterEmail: string;
  reasonCode: string;
  description: string | null;
  status: string;
  createdAt: string;
};

export type AdminReportDetail = {
  id: string;
  listingId: string;
  listingTitle: string;
  reporterUserId: string;
  reporterEmail: string;
  reasonCode: string;
  description: string | null;
  status: string;
  createdAt: string;
  reviewedAt: string | null;
  reviewedByAdminUserId: string | null;
};

export type AdminBulletin = {
  id: string;
  title: string;
  body: string;
  status: string;
  publishAt: string | null;
  publishedAt: string | null;
  expiresAt: string | null;
  updatedAt: string;
};

export type AdminContentPage = {
  id: string;
  slug: string;
  title: string;
  body: string;
  contentType: string;
  status: string;
  publishedAt: string | null;
  updatedAt: string;
};

export type AuditEntry = {
  id: string;
  adminUserId: string;
  adminEmail: string;
  action: string;
  targetType: string;
  targetId: string;
  reason: string | null;
  createdAt: string;
};

export type ApiError = Error & {
  status: number;
  details?: unknown;
};

async function request<T>(path: string, options: RequestOptions = {}): Promise<T> {
  const headers = new Headers(options.headers);
  headers.set('Accept', 'application/json');

  if (options.body && !(options.body instanceof FormData)) {
    headers.set('Content-Type', 'application/json');
  }

  const authState = options.auth ? loadAdminAuth() : null;

  if (options.auth) {
    const auth = authState;
    if (!auth) {
      throw createError('Authentication required.', 401);
    }
    headers.set('Authorization', `Bearer ${auth.accessToken}`);
  }

  const response = await fetch(`${API_BASE_URL}${path}`, { ...options, headers });

  if (response.status === 204) {
    return null as T;
  }

  const contentType = response.headers.get('content-type') ?? '';
  const payload = contentType.includes('application/json') ? await response.json() : await response.text();

  if (response.status === 401 && options.auth && !options._retryAfterRefresh && authState) {
    const refreshed = await tryRefresh(authState.refreshToken);
    if (refreshed) {
      return request<T>(path, {
        ...options,
        _retryAfterRefresh: true,
      });
    }
  }

  if (!response.ok) {
    throw createError(extractMessage(payload) ?? `Request failed with ${response.status}.`, response.status, payload);
  }

  return payload as T;
}

async function tryRefresh(refreshToken: string): Promise<boolean> {
  try {
    const response = await fetch(`${API_BASE_URL}/auth/refresh`, {
      method: 'POST',
      headers: {
        Accept: 'application/json',
        'Content-Type': 'application/json',
      },
      body: JSON.stringify({ refreshToken }),
    });

    if (!response.ok) {
      clearAdminAuth();
      return false;
    }

    const payload = (await response.json()) as AuthResponse;
    saveAdminAuth(payload);
    return true;
  } catch {
    clearAdminAuth();
    return false;
  }
}

function createError(message: string, status: number, details?: unknown): ApiError {
  const error = new Error(message) as ApiError;
  error.status = status;
  error.details = details;
  return error;
}

function extractMessage(payload: unknown) {
  if (!payload || typeof payload !== 'object') {
    return null;
  }

  if ('error' in payload && typeof payload.error === 'string') {
    return payload.error;
  }

  if ('title' in payload && typeof payload.title === 'string') {
    return payload.title;
  }

  return null;
}

export const adminApi = {
  login: (body: { email: string; password: string }) => request<AuthResponse>('/auth/login', {
    method: 'POST',
    body: JSON.stringify(body),
  }),
  logout: (refreshToken: string) => request<void>('/auth/logout', {
    method: 'POST',
    body: JSON.stringify({ refreshToken }),
    auth: true,
  }),
  getSummary: () => request<AnalyticsSummary>('/admin/analytics/summary', { auth: true }),
  getEvents: () => request<PagedResponse<AnalyticsEvent>>('/admin/analytics/events?page=1&pageSize=8', { auth: true }),
  getAudit: () => request<PagedResponse<AuditEntry>>('/admin/audit?page=1&pageSize=8', { auth: true }),
  getUsers: (query = '') => request<PagedResponse<AdminUserSummary>>(`/admin/users${query}`, { auth: true }),
  getUser: (userId: string) => request<AdminUserDetail>(`/admin/users/${userId}`, { auth: true }),
  suspendUser: (userId: string, reason: string) => request<{ id: string; status: string }>(`/admin/users/${userId}/suspend`, {
    method: 'POST',
    body: JSON.stringify({ reason }),
    auth: true,
  }),
  restoreUser: (userId: string, reason: string) => request<{ id: string; status: string }>(`/admin/users/${userId}/restore`, {
    method: 'POST',
    body: JSON.stringify({ reason }),
    auth: true,
  }),
  deleteUser: (userId: string, reason: string) => request<{ id: string; status: string }>(`/admin/users/${userId}/delete`, {
    method: 'POST',
    body: JSON.stringify({ reason }),
    auth: true,
  }),
  getListings: (query = '') => request<PagedResponse<AdminListingSummary>>(`/admin/listings${query}`, { auth: true }),
  getListingReports: (listingId: string) => request<AdminListingReport[]>(`/admin/listings/${listingId}/reports`, { auth: true }),
  removeListing: (listingId: string, reason: string) => request<{ id: string; status: string }>(`/admin/listings/${listingId}/remove`, {
    method: 'POST',
    body: JSON.stringify({ reason }),
    auth: true,
  }),
  restoreListing: (listingId: string, reason: string) => request<{ id: string; status: string }>(`/admin/listings/${listingId}/restore`, {
    method: 'POST',
    body: JSON.stringify({ reason }),
    auth: true,
  }),
  getReports: (query = '') => request<PagedResponse<AdminReportDetail>>(`/admin/reports${query}`, { auth: true }),
  resolveReport: (reportId: string, reason: string, removeListing: boolean) => request<{ id: string; status: string }>(`/admin/reports/${reportId}/resolve`, {
    method: 'POST',
    body: JSON.stringify({ reason, removeListing }),
    auth: true,
  }),
  dismissReport: (reportId: string, reason: string) => request<{ id: string; status: string }>(`/admin/reports/${reportId}/dismiss`, {
    method: 'POST',
    body: JSON.stringify({ reason }),
    auth: true,
  }),
  getBulletins: () => request<PagedResponse<AdminBulletin>>('/admin/content/bulletins?page=1&pageSize=20', { auth: true }),
  createBulletin: (body: { title: string; body: string; publishAt?: string | null; expiresAt?: string | null }) => request<AdminBulletin>('/admin/content/bulletins', {
    method: 'POST',
    body: JSON.stringify(body),
    auth: true,
  }),
  updateBulletin: (bulletinId: string, body: { title?: string; body?: string; publishAt?: string | null; expiresAt?: string | null }) => request<AdminBulletin>(`/admin/content/bulletins/${bulletinId}`, {
    method: 'PATCH',
    body: JSON.stringify(body),
    auth: true,
  }),
  publishBulletin: (bulletinId: string) => request<AdminBulletin>(`/admin/content/bulletins/${bulletinId}/publish`, { method: 'POST', auth: true }),
  unpublishBulletin: (bulletinId: string) => request<AdminBulletin>(`/admin/content/bulletins/${bulletinId}/unpublish`, { method: 'POST', auth: true }),
  getPages: () => request<AdminContentPage[]>('/admin/content/pages', { auth: true }),
  createPage: (body: { slug: string; title: string; body: string; contentType: string }) => request<AdminContentPage>('/admin/content/pages', {
    method: 'POST',
    body: JSON.stringify(body),
    auth: true,
  }),
  updatePage: (pageId: string, body: { title?: string; body?: string }) => request<AdminContentPage>(`/admin/content/pages/${pageId}`, {
    method: 'PATCH',
    body: JSON.stringify(body),
    auth: true,
  }),
  publishPage: (pageId: string) => request<AdminContentPage>(`/admin/content/pages/${pageId}/publish`, { method: 'POST', auth: true }),
};
