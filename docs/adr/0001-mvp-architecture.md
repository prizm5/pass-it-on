# ADR 0001: MVP monorepo architecture

## Status

Accepted

## Context

The MVP uses a React public frontend, a React admin frontend, an ASP.NET Core backend, PostgreSQL, social login, admin moderation, and mobile-first UX.

## Decision

The repository is organized as a monorepo with separate applications for the public site, admin portal, and API.

- `apps/web` owns the public user experience.
- `apps/admin` owns the admin portal.
- `apps/api` owns the domain model, API contracts, and backend implementation.
- `infra` holds local infrastructure assets.
- `docs` holds architecture and contract documentation.

## Consequences

- Frontend applications can iterate independently while sharing repository-level standards.
- Backend implementation remains the system-of-record for domain rules.
- CI can validate each application separately.
- Shared contracts can be added later without a repo reshape.
