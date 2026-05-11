import { clearAuth, loadAuth, saveAuth, type StoredAuth } from './auth';

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

export type BulletinPost = {
  id: string;
  title: string;
  body: string;
  publishedAt: string | null;
  expiresAt: string | null;
};

export type ContentPage = {
  id: string;
  slug: string;
  title: string;
  body: string;
  contentType: string;
  publishedAt: string | null;
};

export type ListingSummary = {
  id: string;
  title: string;
  category: string;
  size: string | null;
  ageRange: string | null;
  condition: string;
  status: string;
  thumbnailUrl: string | null;
  createdAt: string;
};

export type ListingImage = {
  id: string;
  publicUrl: string;
  sortOrder: number;
  width: number;
  height: number;
};

export type ListingImageUploadUrlResponse = {
  uploadUrl: string;
  storageKey: string;
  publicUrl: string;
  expiresAt: string;
  maxFileSizeBytes: number;
  allowedContentTypes: string[];
};

export type ListingDetail = {
  id: string;
  ownerUserId: string;
  ownerDisplayName: string;
  title: string;
  description: string;
  category: string;
  size: string | null;
  ageRange: string | null;
  condition: string;
  contactPreference: string;
  status: string;
  images: ListingImage[];
  createdAt: string;
  updatedAt: string;
};

export type AuthResponse = StoredAuth;

export type Profile = {
  id: string;
  email: string;
  displayName: string;
  preferredName: string | null;
  phone: string | null;
  avatarUrl: string | null;
  role: string;
  status: string;
  createdAt: string;
};

export type DeletionRequestResponse = {
  message: string;
  scheduledAt: string;
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

  const authState = options.auth ? loadAuth() : null;

  if (options.auth) {
    const auth = authState;
    if (!auth) {
      throw createError('Authentication required.', 401);
    }
    headers.set('Authorization', `Bearer ${auth.accessToken}`);
  }

  const response = await fetch(`${API_BASE_URL}${path}`, {
    ...options,
    headers,
  });

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
      clearAuth();
      return false;
    }

    const payload = (await response.json()) as AuthResponse;
    saveAuth(payload);
    return true;
  } catch {
    clearAuth();
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

export const api = {
  getOAuthStartUrl: (provider: 'google' | 'facebook', returnPath = '/profile') => {
    const returnUrl = new URL(returnPath, window.location.origin).toString();
    return `${API_BASE_URL}/auth/oauth/${provider}/start?returnUrl=${encodeURIComponent(returnUrl)}`;
  },
  getHomeBulletin: () => request<BulletinPost | null>('/content/home-bulletin'),
  getBulletins: (limit = 6) => request<BulletinPost[]>(`/content/bulletins?limit=${limit}`),
  getPage: (slug: string) => request<ContentPage>(`/content/pages/${slug}`),
  getListings: (search = '') => request<PagedResponse<ListingSummary>>(`/listings${search}`),
  getListing: (listingId: string) => request<ListingDetail>(`/listings/${listingId}`),
  getMyListings: () => request<PagedResponse<ListingSummary>>('/listings/me', { auth: true }),
  createListing: (body: {
    title: string;
    description: string;
    category: string;
    size?: string | null;
    ageRange?: string | null;
    condition: string;
    contactPreference: string;
  }) => request<ListingDetail>('/listings', { method: 'POST', body: JSON.stringify(body), auth: true }),
  updateListing: (listingId: string, body: Record<string, unknown>) => request<ListingDetail>(`/listings/${listingId}`, {
    method: 'PATCH',
    body: JSON.stringify(body),
    auth: true,
  }),
  activateListing: (listingId: string) => request<{ id: string; status: string }>(`/listings/${listingId}/activate`, {
    method: 'POST',
    auth: true,
  }),
  archiveListing: (listingId: string) => request<{ id: string; status: string }>(`/listings/${listingId}/archive`, {
    method: 'POST',
    auth: true,
  }),
  markListingUnavailable: (listingId: string) => request<{ id: string; status: string }>(`/listings/${listingId}/mark-unavailable`, {
    method: 'POST',
    auth: true,
  }),
  requestListingImageUploadUrl: (
    listingId: string,
    body: { fileName: string; contentType: string; fileSizeBytes: number },
  ) => request<ListingImageUploadUrlResponse>(`/listings/${listingId}/images/upload-url`, {
    method: 'POST',
    body: JSON.stringify(body),
    auth: true,
  }),
  attachListingImage: (
    listingId: string,
    body: { storageKey: string; width?: number; height?: number; sortOrder?: number },
  ) => request<ListingImage>(`/listings/${listingId}/images`, {
    method: 'POST',
    body: JSON.stringify(body),
    auth: true,
  }),
  deleteListingImage: (listingId: string, imageId: string) => request<void>(`/listings/${listingId}/images/${imageId}`, {
    method: 'DELETE',
    auth: true,
  }),
  reorderListingImages: (listingId: string, imageIds: string[]) => request<ListingImage[]>(`/listings/${listingId}/images/reorder`, {
    method: 'POST',
    body: JSON.stringify({ imageIds }),
    auth: true,
  }),
  register: (body: { email: string; password: string; displayName: string }) => request<AuthResponse>('/auth/register', {
    method: 'POST',
    body: JSON.stringify(body),
  }),
  login: (body: { email: string; password: string }) => request<AuthResponse>('/auth/login', {
    method: 'POST',
    body: JSON.stringify(body),
  }),
  logout: (refreshToken: string) => request<void>('/auth/logout', {
    method: 'POST',
    body: JSON.stringify({ refreshToken }),
    auth: true,
  }),
  getProfile: () => request<Profile>('/profile/me', { auth: true }),
  updateProfile: (body: { displayName?: string; preferredName?: string; phone?: string }) => request<Profile>('/profile/me', {
    method: 'PATCH',
    body: JSON.stringify(body),
    auth: true,
  }),
  requestDeletion: () => request<DeletionRequestResponse>('/profile/me/deletion-request', {
    method: 'POST',
    auth: true,
  }),
  cancelDeletion: () => request<{ message: string }>('/profile/me/deletion-request', {
    method: 'DELETE',
    auth: true,
  }),
  reportListing: (body: { listingId: string; reasonCode: string; description?: string }) => request<{ reportId: string; message: string }>('/reports', {
    method: 'POST',
    body: JSON.stringify(body),
    auth: true,
  }),
};
