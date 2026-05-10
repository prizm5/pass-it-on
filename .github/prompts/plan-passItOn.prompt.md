**Plan: Pass It On MVP**

I based this on the requirements in Requirements/Readme.md plus your decisions: contact-only exchange, admin moderation/content/analytics/reporting, Google + Facebook + email/password auth, basic search/filtering, and no payment fields in MVP. I also saved the full plan to session memory so it can be refined from here.

**Recommended build shape**

1. Build three apps around one shared domain model: a React public site, a React admin portal, and an ASP.NET Core API with PostgreSQL.
2. Keep MVP narrow: registration, profile management, self-deletion, listings with photos, home bulletin, FAQ/policy pages, search/category filters, admin moderation, and analytics.
3. Exclude for now: in-app messaging, payment features, favorites, daycare verification workflows, advanced recommendations, and provider account linking.

**Implementation phases**

1. Foundation: scaffold the repo into public frontend, admin frontend, backend API, and shared infra/CI. Decide early whether admin is a separate deployment or a separate app behind the same API.
2. Backend-first design: define entities and API contracts for users, auth identities, listings, listing images, bulletin posts, reports, analytics events, and admin audit records. This is the main blocker for the rest.
3. Auth and permissions: implement Google, Facebook, and email/password with user/admin roles, plus a clear deletion/anonymization flow.
4. Data and uploads: model minimal PII in Postgres, use soft-delete where moderation needs traceability, and design the image pipeline before listing creation.
5. Public app: build mobile-first flows for login, home bulletin, listing browse/detail, create/edit listing, profile, self-delete, FAQ, and policy pages.
6. Admin app: add dashboard, users, listings, bulletins, reports, and analytics views, with audit logging on every admin action.
7. Launch hardening: reporting flows, policy/retention content, analytics validation, responsive QA, auth/authorization testing, and upload abuse testing.

**Critical architecture decisions**

1. Treat uploads as a security-sensitive subsystem from day one: accept only JPEG/PNG, validate by file content not extension, rename files to generated IDs, serve them from separate storage/origin, and enforce size/rate limits.
2. Keep the exchange model simple in MVP: listing status plus contact preferences, not request/chat workflows.
3. Make bulletin, FAQ, and policy content admin-managed instead of file-managed so updates do not require redeploys.
4. Assume users are adults/guardians. If minors can register directly, privacy/compliance requirements need a separate review before implementation.

**Immediate next steps**

1. Lock the remaining product decisions before any scaffolding work starts.
	Confirm whether listings are platform-wide in MVP or scoped by daycare, what contact details are visible on listings versus profiles, and whether bulletin/FAQ/policy content is fully admin-managed from launch.
2. Convert the plan into a build backlog.
	Create epics for public app, admin app, backend API, infrastructure, analytics, and security. Break each epic into MVP stories with acceptance criteria and explicit dependencies.
3. Define the backend contract first.
	Write the initial domain model, API resources, auth model, and deletion/anonymization rules. This should produce a concrete checklist of entities, endpoints, and role permissions before UI implementation begins.
4. Scaffold the repository around the agreed architecture.
	Create separate apps for the public frontend, admin frontend, and ASP.NET Core API, plus shared local-development infrastructure, environment configuration, and CI.
5. Build the highest-risk backend slices first.
	Implement authentication, user/profile management, listing CRUD, image upload validation/storage, and admin moderation endpoints before deeper frontend polish.
6. Build the public frontend against stable API contracts.
	Prioritize login, home bulletin, listing browse/detail, create listing, profile management, self-deletion, and policy/FAQ pages in mobile-first layouts.
7. Build the admin portal once RBAC and moderation endpoints exist.
	Prioritize user moderation, listing moderation, bulletin management, report review, and a basic analytics summary view.
8. Add launch-readiness checks before calling MVP complete.
	Validate upload abuse protections, deletion/anonymization behavior, unauthorized admin access handling, mobile responsiveness, and analytics event coverage.

**Suggested first deliverables**

1. Architecture decision record for MVP scope and unresolved product rules.
2. Domain model and API endpoint draft.
3. Repo/app scaffold with local run instructions.
4. Initial backlog with estimates and delivery order.
5. Security checklist for uploads, auth, and account deletion.

**MVP backlog**

**Epic 1: Product rules and architecture baseline**

1. Ticket P1.1: Lock unresolved MVP product rules. Size: S. Depends on: none.
	Scope: confirm whether listings are platform-wide or daycare-scoped in MVP, what contact information is exposed on listing details, and whether bulletin, FAQ, and policy content are fully admin-managed at launch.
	Acceptance criteria: decisions are written down in one place; each open question has an explicit chosen rule; no MVP ticket depends on undefined product behavior.
2. Ticket P1.2: Define roles, permissions, and deletion policy. Size: S. Depends on: P1.1.
	Scope: define user and admin roles, allowed actions per role, self-deletion behavior, admin audit retention, and listing moderation states.
	Acceptance criteria: role matrix exists; deletion and anonymization rules are documented; moderation states and transitions are listed.
3. Ticket P1.3: Draft the MVP architecture decision record. Size: S. Depends on: P1.1, P1.2.
	Scope: capture the selected stack, deployment shape, upload-storage approach, analytics boundaries, and excluded features.
	Acceptance criteria: one ADR documents the chosen architecture; excluded scope is explicit; major technical decisions have rationale.

**Epic 2: Infrastructure and repository setup**

1. Ticket I2.1: Scaffold the monorepo/app structure. Size: M. Depends on: P1.3.
	Scope: create the public React app, admin React app, ASP.NET Core API, and shared project-level configuration folders.
	Acceptance criteria: each app starts locally; folder ownership is clear; a top-level README explains the layout.
2. Ticket I2.2: Add local development infrastructure. Size: M. Depends on: I2.1.
	Scope: set up PostgreSQL and supporting local services, environment files, secrets placeholders, and local run scripts.
	Acceptance criteria: a new developer can start the stack locally; environment variables are documented; database connectivity is working.
3. Ticket I2.3: Add baseline CI. Size: M. Depends on: I2.1.
	Scope: add build, lint, and test workflows for all three apps.
	Acceptance criteria: CI runs on pull requests; failures are visible per app; the default branch is protected from silent breakage.

**Epic 3: Identity, users, and authorization**

1. Ticket A3.1: Model users, identities, and roles in the backend. Size: M. Depends on: P1.2, I2.2.
	Scope: define database entities and migrations for users, auth identities, roles, sessions or refresh tokens, and admin audit references.
	Acceptance criteria: schema supports Google, Facebook, and email/password login; roles are persisted; migrations apply cleanly.
2. Ticket A3.2: Implement email/password authentication. Size: M. Depends on: A3.1.
	Scope: registration, login, logout, password hashing, token issuance, and protected API access.
	Acceptance criteria: a user can sign up, sign in, and access protected routes; weak or invalid auth requests are rejected; tokens expire and refresh as designed.
3. Ticket A3.3: Implement Google and Facebook login. Size: M. Depends on: A3.1.
	Scope: OAuth flows, identity mapping, and account creation on first login.
	Acceptance criteria: users can authenticate with Google and Facebook in non-local environments; failed provider flows return safe errors; duplicate account creation rules are defined.
4. Ticket A3.4: Build profile management and self-deletion APIs. Size: M. Depends on: A3.2, A3.3.
	Scope: profile read/update, account deletion request, lockout after deletion request, and deletion/anonymization workflow.
	Acceptance criteria: users can update their profile; users can trigger self-deletion; deleted accounts lose access immediately; retained audit data matches the documented policy.

**Epic 4: Listings, uploads, and moderation**

1. Ticket L4.1: Model listings, listing images, and reports. Size: M. Depends on: I2.2, P1.2.
	Scope: define entities, migrations, lifecycle states, categories, and report records.
	Acceptance criteria: schema supports active, archived, unavailable, and removed listings; reports are linked to users and listings; search/filter fields are indexed.
2. Ticket L4.2: Implement listing CRUD APIs. Size: M. Depends on: L4.1, A3.2.
	Scope: create, edit, archive, remove, and fetch listings with ownership and admin checks.
	Acceptance criteria: authenticated users can manage their own listings; public listing reads support browse and detail views; unauthorized edits are blocked.
3. Ticket L4.3: Implement secure image upload pipeline. Size: L. Depends on: L4.1, I2.2.
	Scope: JPEG/PNG allowlist, content-based type validation, generated storage names, isolated processing, size limits, and separate serving origin.
	Acceptance criteria: invalid or oversized uploads are rejected; uploaded files are stored outside the app origin; only processed safe variants are served; upload limits are enforced.
4. Ticket L4.4: Implement listing browse, search, and category filtering APIs. Size: M. Depends on: L4.2.
	Scope: paginated listing feed, keyword search, category filters, and sort order.
	Acceptance criteria: API supports the agreed filters; empty and partial search states behave consistently; query performance is acceptable for MVP-scale usage.
5. Ticket L4.5: Implement reporting and moderation APIs. Size: M. Depends on: L4.2, A3.1.
	Scope: report submission, admin hide/remove actions, moderation notes, and audit logging.
	Acceptance criteria: users can submit reports; admins can moderate listings; each admin action produces an audit record.

**Epic 5: Content and bulletin management**

1. Ticket C5.1: Model bulletin, FAQ, and policy content. Size: S. Depends on: P1.1, I2.2.
	Scope: define content entities or storage model for weekly bulletins, FAQs, and policy pages.
	Acceptance criteria: content records support publish status and timestamps; public pages can fetch current content; admins can own content changes.
2. Ticket C5.2: Build public content APIs. Size: S. Depends on: C5.1.
	Scope: endpoints for home bulletin, FAQ content, and policies.
	Acceptance criteria: public clients can fetch published content without admin access; unpublished content is not visible publicly.
3. Ticket C5.3: Build admin content management APIs. Size: M. Depends on: C5.1, A3.1.
	Scope: create, edit, publish, and archive bulletins and static content.
	Acceptance criteria: admins can manage content; non-admins cannot; changes are auditable.

**Epic 6: Public web app**

1. Ticket W6.1: Build app shell, routing, and auth integration. Size: M. Depends on: I2.1, A3.2.
	Scope: set up routing, session handling, protected routes, shared layout, and API client configuration.
	Acceptance criteria: public and authenticated routes behave correctly; auth state survives refresh; unauthorized users are redirected appropriately.
2. Ticket W6.2: Build the home page with bulletin section. Size: S. Depends on: W6.1, C5.2.
	Scope: mobile-first home page, weekly updates section, and basic calls to action.
	Acceptance criteria: home page renders on mobile and desktop; bulletin content loads from the API; empty bulletin state is handled.
3. Ticket W6.3: Build listing browse and detail pages. Size: M. Depends on: W6.1, L4.4.
	Scope: shared listings/forum page, listing cards, detail page, search input, and category filters.
	Acceptance criteria: users can browse, search, and filter listings; detail pages show photos and contact guidance; loading and empty states are covered.
4. Ticket W6.4: Build create and edit listing flows. Size: M. Depends on: W6.1, L4.2, L4.3.
	Scope: listing form, image upload UI, validation, draft/edit support, and success/error handling.
	Acceptance criteria: authenticated users can create and update listings with photos; unsupported files are rejected clearly; form validation matches backend rules.
5. Ticket W6.5: Build profile management and self-deletion UI. Size: M. Depends on: W6.1, A3.4.
	Scope: profile page, edit flow, deletion confirmation, and post-deletion redirect.
	Acceptance criteria: users can view and update their profile; deletion requires explicit confirmation; deleted accounts are signed out immediately.
6. Ticket W6.6: Build FAQ and policy pages. Size: S. Depends on: W6.1, C5.2.
	Scope: FAQ page, retention policy, privacy/data collection policy, and deletion-rights information.
	Acceptance criteria: required content is publicly accessible; the pages render correctly on mobile; policy links are discoverable from the profile and footer areas.

**Epic 7: Admin portal**

1. Ticket M7.1: Build admin shell and access control. Size: M. Depends on: I2.1, A3.1, A3.2.
	Scope: admin routing, admin session checks, shared layout, and unauthorized handling.
	Acceptance criteria: only admins can access admin routes; non-admin users are denied cleanly; admin navigation covers the MVP sections.
2. Ticket M7.2: Build user moderation screens. Size: M. Depends on: M7.1, A3.4.
	Scope: user list, search, detail view, suspend/delete actions, and audit visibility.
	Acceptance criteria: admins can find and moderate users; actions are confirmed before execution; audit history is visible where relevant.
3. Ticket M7.3: Build listing moderation and report review screens. Size: M. Depends on: M7.1, L4.5.
	Scope: reported listings queue, review flow, remove/hide actions, and status updates.
	Acceptance criteria: admins can review reports end to end; moderated listings reflect updated public visibility; audit data is captured.
4. Ticket M7.4: Build bulletin and content management screens. Size: M. Depends on: M7.1, C5.3.
	Scope: CRUD interfaces for bulletins, FAQ items, and policies.
	Acceptance criteria: admins can create and publish content without code changes; edits appear on the public site after publish.
5. Ticket M7.5: Build a basic analytics summary view. Size: S. Depends on: M7.1, T8.2.
	Scope: simple dashboard showing registrations, listings created, reports submitted, and bulletin activity.
	Acceptance criteria: metrics are visible to admins; data matches tracked events closely enough for MVP reporting.

**Epic 8: Analytics, testing, and launch readiness**

1. Ticket T8.1: Define the analytics event taxonomy. Size: S. Depends on: P1.3.
	Scope: name the business events to track across frontend and backend and define payload boundaries.
	Acceptance criteria: tracked events are listed with trigger points; no unnecessary personal data is included; analytics ownership is clear.
2. Ticket T8.2: Implement analytics instrumentation. Size: M. Depends on: T8.1, W6.1, A3.2, L4.2.
	Scope: add page and feature usage tracking plus backend business event logging.
	Acceptance criteria: agreed events are emitted; admin analytics can consume the tracked data; instrumentation failures do not break core user flows.
3. Ticket T8.3: Add backend automated tests for critical flows. Size: M. Depends on: A3.4, L4.5, C5.3.
	Scope: auth, profile updates, self-deletion, listing CRUD, moderation, and upload validation tests.
	Acceptance criteria: critical backend flows have automated coverage; regressions fail CI; test setup is documented.
4. Ticket T8.4: Add frontend end-to-end tests for critical flows. Size: M. Depends on: W6.6, M7.4.
	Scope: login, create listing, browse/filter listings, profile update, self-deletion, and core admin moderation flows.
	Acceptance criteria: end-to-end coverage exists for MVP-critical paths; tests run in CI or a documented pre-release pipeline.
5. Ticket T8.5: Complete the MVP launch checklist. Size: S. Depends on: T8.2, T8.3, T8.4.
	Scope: responsive QA, upload abuse checks, deletion verification, authorization checks, content review, and analytics validation.
	Acceptance criteria: each launch risk has a pass/fail result; unresolved blockers are listed explicitly; MVP readiness can be assessed from one checklist.

**Recommended delivery order**

1. P1.1 to P1.3
2. I2.1 to I2.3
3. A3.1 to A3.4
4. L4.1 to L4.5
5. C5.1 to C5.3
6. W6.1 to W6.6
7. M7.1 to M7.5
8. T8.1 to T8.5

**API/domain checklist**

**1. Product rules to lock before finalizing contracts**

1. Decide whether listings are platform-wide in MVP or scoped to daycare membership.
2. Decide which contact fields are shown on listing details versus kept only on profile records.
3. Confirm whether bulletin, FAQ, and policy content is fully admin-managed from launch.
4. Confirm whether account deletion is immediate anonymization or immediate lockout followed by background anonymization.
5. Confirm whether admins can hard-delete content in MVP or only soft-remove it.

**2. Core domain entities**

1. User
	Purpose: account owner for public-site usage.
	Minimum fields: id, email, displayName, firstName or preferredName, lastName if needed, phone or alternate contact if allowed, avatarUrl optional, role, status, createdAt, updatedAt, deletedAt, deletionRequestedAt.
2. AuthIdentity
	Purpose: external or local login identity linked to a user.
	Minimum fields: id, userId, provider, providerSubject, emailAtProvider, passwordHash for local accounts only, lastLoginAt, createdAt.
3. RefreshSession or AuthSession
	Purpose: refresh-token tracking and session revocation.
	Minimum fields: id, userId, tokenHash, expiresAt, revokedAt, createdAt, lastUsedAt, userAgent optional, ipAddress optional if allowed by policy.
4. Listing
	Purpose: item available for exchange.
	Minimum fields: id, ownerUserId, title, description, category, size optional, condition, ageRange optional, locationScope if needed, contactPreference, status, createdAt, updatedAt, archivedAt, removedAt.
5. ListingImage
	Purpose: safe, processed images attached to listings.
	Minimum fields: id, listingId, storageKey, publicUrl, sortOrder, width, height, createdAt.
6. ListingReport
	Purpose: report abusive or inappropriate listings.
	Minimum fields: id, listingId, reporterUserId, reasonCode, description optional, status, createdAt, reviewedAt, reviewedByAdminUserId.
7. BulletinPost
	Purpose: weekly updates shown on the home page.
	Minimum fields: id, title, body, status, publishAt, publishedAt, expiresAt optional, createdByUserId, updatedByUserId, createdAt, updatedAt.
8. ContentPage or separate FaqItem and PolicyDocument entities
	Purpose: admin-managed FAQ and policy content.
	Minimum fields if unified: id, slug, title, body, contentType, status, publishedAt, updatedAt, updatedByUserId.
9. AnalyticsEvent
	Purpose: internal event log for business activity.
	Minimum fields: id, eventName, actorUserId optional, subjectType, subjectId, propertiesJson, occurredAt.
10. AdminAudit
	 Purpose: immutable audit trail for admin actions.
	 Minimum fields: id, adminUserId, action, targetType, targetId, reason optional, metadataJson optional, createdAt.

**3. Required enums and state models**

1. UserRole: user, admin.
2. UserStatus: active, suspended, deletion_requested, deleted.
3. AuthProvider: local, google, facebook.
4. ListingStatus: draft if used, active, unavailable, archived, removed.
5. ListingCondition: new, like_new, good, fair, worn or equivalent simplified set.
6. ContactPreference: email, phone, profile_contact, other_defined_option.
7. ReportReasonCode: inappropriate_content, spam, duplicate, prohibited_item, safety_concern, other.
8. ReportStatus: open, under_review, resolved, dismissed.
9. ContentStatus: draft, published, archived.
10. AuditAction: user_suspended, user_deleted, listing_removed, listing_restored, bulletin_published, content_updated, report_resolved, plus any other admin-only actions.

**4. Entity relationships to define explicitly**

1. One User to many AuthIdentities only if multi-provider linking is allowed later; otherwise still model the relationship so expansion is possible without redesign.
2. One User to many RefreshSessions.
3. One User to many Listings.
4. One Listing to many ListingImages.
5. One Listing to many ListingReports.
6. One User to many ListingReports as reporter.
7. One User to many BulletinPosts and content updates as creator or editor.
8. One User to many AdminAudit records as acting admin.
9. Optional relationship from ListingReport to reviewing admin user.

**5. Database and persistence checklist**

1. Add unique constraints for user email and provider plus providerSubject where appropriate.
2. Add indexes for listing browse paths: status, category, createdAt, ownerUserId, and any location/daycare scope field.
3. Add indexes for report queues: status, createdAt, reviewedAt.
4. Add indexes for bulletin and content publish lookups: status, publishAt or publishedAt, slug.
5. Decide which records are soft-deleted versus anonymized versus hard-deleted.
6. Define migration strategy and naming conventions before schema work starts.
7. Decide whether long-form content bodies are stored in plain text, markdown, or rich-text JSON.

**6. Public API resource checklist**

1. Auth resources
	Endpoints to define: register, login, logout, refresh token, begin Google login, begin Facebook login, OAuth callback handling.
	Contract decisions: token format, refresh strategy, error shape, duplicate-account behavior, password rules.
2. Profile resources
	Endpoints to define: get current user profile, update current user profile, request self-deletion.
	Contract decisions: editable fields, masked fields, validation rules, deletion response behavior.
3. Listing resources
	Endpoints to define: create listing, update listing, archive listing, remove listing, get listing detail, list public listings, list current user listings.
	Contract decisions: required fields, pagination shape, search/filter query params, ownership checks, public versus private fields.
4. Upload resources
	Endpoints to define: request upload, attach processed image to listing, delete image from listing if allowed.
	Contract decisions: direct-to-storage versus API-streamed uploads, upload limits, accepted MIME types, processing lifecycle, failure responses.
5. Report resources
	Endpoints to define: submit listing report.
	Contract decisions: allowed reasons, rate limiting, duplicate-report handling, anonymous reporting policy if any.
6. Bulletin and content resources
	Endpoints to define: get active home bulletin, list published FAQs, get published policy pages.
	Contract decisions: slug scheme, publish semantics, preview behavior if any.
7. Analytics ingestion resources if not handled fully server-side
	Endpoints to define: event ingestion or feature usage logging endpoint if needed.
	Contract decisions: accepted payloads, PII boundaries, failure tolerance.

**7. Admin API resource checklist**

1. Admin auth guard
	Define how admin-only authorization is enforced at the API boundary.
2. User administration
	Endpoints to define: list users, get user detail, suspend user, restore user if supported, delete or anonymize user if allowed.
	Contract decisions: searchable fields, exposed personal data, confirmation requirements, audit reason requirement.
3. Listing moderation
	Endpoints to define: list listings for moderation, remove listing, restore listing if supported, inspect listing reports.
	Contract decisions: moderation notes, public visibility after moderation, audit requirements.
4. Report review
	Endpoints to define: list reports, get report detail, mark report resolved, dismiss report.
	Contract decisions: resolution notes, reviewer identity capture, state transition rules.
5. Bulletin and content management
	Endpoints to define: create, update, publish, archive bulletins; create, update, publish, archive FAQ and policy content.
	Contract decisions: draft versus published behavior, scheduled publishing if included, slug collisions.
6. Analytics summary
	Endpoints to define: admin dashboard summary metrics and basic trends.
	Contract decisions: aggregation granularity, caching, date-range support, event source of truth.
7. Audit access
	Endpoints to define: view audit records for admin actions if needed in MVP.
	Contract decisions: retention, filtering, redaction rules.

**8. Role and permission checklist**

1. Public users can browse published listings, bulletin content, FAQ, and policy pages without authentication if desired.
2. Authenticated users can manage only their own profile and listings.
3. Authenticated users can submit reports subject to rate limits.
4. Admins can manage users, listings, reports, bulletins, FAQ content, policy content, and analytics views.
5. Every admin mutation should create an AdminAudit record.
6. Self-deletion should revoke current access immediately and block future login until deletion handling is complete.

**9. Validation and business-rule checklist**

1. Registration validation: allowed email formats, password policy, duplicate email behavior, social-login fallback rules.
2. Profile validation: required versus optional contact fields, display name rules, allowed lengths.
3. Listing validation: title length, description length, required category, allowed condition values, allowed image count, supported image formats, max file size.
4. Search/filter validation: accepted query length, allowed category values, page-size caps, sort options.
5. Report validation: reason required, free-text max length, duplicate-report throttling.
6. Content validation: title/body length limits, required publish fields, valid slugs.
7. Admin action validation: reason required for destructive actions if desired.

**10. Upload and media checklist**

1. Decide whether uploads go through the API or directly to object storage with signed upload instructions.
2. Validate files by content signature, not only extension or request MIME type.
3. Allow only JPEG and PNG in MVP.
4. Enforce maximum file size, image count per listing, and request rate limits.
5. Rename stored files using generated identifiers only.
6. Process images in an isolated worker or constrained environment.
7. Serve processed images from a separate origin or storage domain.
8. Define how failed processing is surfaced to users and cleaned up in storage.
9. Decide whether image deletion is immediate or deferred when a listing is archived or removed.

**11. Deletion, retention, and privacy checklist**

1. Define exactly what personal data is stored for users and why.
2. Define what is anonymized versus retained for audit or legal reasons after self-deletion.
3. Define retention windows for deleted users, removed listings, audit records, analytics events, and reports.
4. Ensure profile self-deletion flow aligns with published policy content.
5. Ensure analytics payloads avoid unnecessary personal data.
6. Decide whether IP addresses or user agents are retained for security and for how long.

**12. API conventions checklist**

1. Choose REST naming conventions and versioning approach.
2. Define shared error response shape for validation, auth, and authorization failures.
3. Define pagination response structure for listing and admin list endpoints.
4. Define date-time format and timezone handling.
5. Define id format: integer, UUID, or ULID.
6. Define optimistic concurrency strategy for admin and content updates if needed.
7. Define API documentation source of truth, such as OpenAPI.

**13. Testing checklist for the API/domain layer**

1. Authentication tests for local and social login success and failure paths.
2. Authorization tests proving users cannot edit or delete others' listings or profiles.
3. Listing lifecycle tests for create, update, archive, remove, and public visibility rules.
4. Upload validation tests for MIME spoofing, unsupported files, oversized files, and too many files.
5. Self-deletion tests covering immediate lockout and post-deletion data state.
6. Moderation tests for report submission, report resolution, and listing removal.
7. Content publication tests for draft versus published visibility.
8. Analytics tests proving events are emitted without breaking user flows.

**14. Recommended implementation order for the API/domain layer**

1. Lock product rules, roles, deletion policy, and core enums.
2. Define ids, shared API conventions, and error shapes.
3. Model User, AuthIdentity, RefreshSession, and AdminAudit.
4. Model Listing, ListingImage, and ListingReport.
5. Model BulletinPost and FAQ or policy content entities.
6. Draft public auth, profile, listing, upload, report, and content endpoints.
7. Draft admin moderation, content-management, analytics, and audit endpoints.
8. Review the whole contract against the backlog before scaffolding implementation.

**Suggested work order for the next session**

1. Review and trim the backlog to the smallest acceptable first release.
2. Review and finalize the API/domain checklist against the approved backlog.
3. Scaffold the repository structure once the backlog and API contract are agreed.
