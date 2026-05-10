# Pass It On WordPress Deployment Review

## 1. Executive summary

Pass It On is currently a multi-application system built from:

- React public frontend
- React admin frontend
- ASP.NET Core API
- PostgreSQL database
- OAuth social login and JWT session model
- Optional S3-style direct image upload flow

Because of this architecture, Pass It On is not a native WordPress application today. It can still be deployed alongside WordPress in multiple ways, but each approach has different cost, risk, and long-term maintenance impact.

Primary recommendation:

- Do not rewrite the core product into WordPress at this stage.
- Use WordPress only where it adds clear value (content authoring, SEO pages, bulletin or FAQ publishing), while keeping the existing transactional platform and API as system of record.

## 2. Current architecture evidence

This review is based on the implementation in:

- [apps/api/src/PassItOn.Api/Program.cs](apps/api/src/PassItOn.Api/Program.cs)
- [apps/web/src/lib/api.ts](apps/web/src/lib/api.ts)
- [apps/web/src/lib/auth.ts](apps/web/src/lib/auth.ts)
- [apps/admin/src/lib/api.ts](apps/admin/src/lib/api.ts)
- [docker-compose.yml](docker-compose.yml)

Observed platform characteristics relevant to WordPress:

1. API is an ASP.NET Core service with JWT auth, OAuth providers, CORS policy, role-based admin authorization, and PostgreSQL persistence.
2. Web and admin apps are separate SPA applications built with Vite and React.
3. Frontends store auth state client-side and call API endpoints directly.
4. Deployment is container friendly and configuration driven through environment variables.

## 3. WordPress fit assessment

### 3.1 What fits well with WordPress

1. Content-driven pages (FAQ, policies, bulletin posts) can be managed effectively via WordPress editorial workflows.
2. SEO landing pages and static marketing sections can benefit from WordPress plugins and publishing tools.
3. Non-technical content teams can publish without code deployments.

### 3.2 What does not fit well with WordPress

1. Existing domain logic for listings, moderation, profile lifecycle, and reporting already exists in a separate ASP.NET API and would be duplicated if rewritten.
2. Current auth model uses JWT plus OAuth callbacks integrated with API behavior; replacing with WordPress user/auth patterns introduces high migration complexity.
3. Admin portal is an independent React application, not a WordPress wp-admin extension.
4. Image upload path is S3 presigned URL workflow that is already integrated in API contracts.

## 4. Deployment options

## Option A: WordPress as content shell, keep current product unchanged (recommended)

Description:

- Run WordPress only for public content pages.
- Keep existing web app, admin app, and ASP.NET API unchanged.
- Link or route users from WordPress pages into the React application for transactional features.

Pros:

1. Lowest migration risk.
2. Fastest time to value for editorial content.
3. Minimal impact to existing auth, data, and moderation logic.
4. Preserves current engineering velocity.

Cons:

1. Multi-system operations (WordPress plus current stack).
2. Potential visual inconsistency unless shared design standards are enforced.
3. Requires URL and navigation strategy to avoid user confusion.

Implementation outline:

1. Deploy WordPress on chosen host with managed updates and backups.
2. Keep Pass It On stack deployed as-is on container capable host.
3. Publish content pages in WordPress and route app actions to existing React routes.
4. Set canonical URLs, sitemap strategy, and analytics across both surfaces.
5. Add SSO or at least coherent session UX if cross-surface login is needed.

Estimated effort:

- 2 to 5 weeks depending on branding integration and SEO migration scope.

## Option B: Headless WordPress CMS with existing React and API stack

Description:

- Use WordPress only as headless content management backend.
- React apps fetch content from WordPress REST or GraphQL while transactional data stays in ASP.NET API.

Pros:

1. Content editing in WordPress with unified React frontends.
2. Better UX consistency than Option A.
3. No full rewrite of core domain logic.

Cons:

1. New integration layer and mapping models required.
2. Operational complexity increases (two backend systems).
3. Caching and publish invalidation must be designed carefully.

Implementation outline:

1. Define content model in WordPress for FAQ, policies, and bulletins.
2. Add secure content fetch integration in React apps.
3. Add caching strategy and publish hooks for content freshness.
4. Keep existing API as source of truth for accounts, listings, reports, and moderation.
5. Add observability for WordPress API dependency.

Estimated effort:

- 4 to 8 weeks depending on content model depth and integration quality expectations.

## Option C: Full WordPress application rewrite

Description:

- Rebuild user-facing and admin features as WordPress theme and plugin ecosystem.
- Recreate listings, moderation, auth, reporting, analytics, and upload workflows in PHP and WordPress architecture.

Pros:

1. Single platform if completed successfully.
2. Potentially simpler hosting if organization is already WordPress-centric.

Cons:

1. Highest engineering cost and delivery risk.
2. Significant parity risk with existing business logic.
3. Security and performance hardening burden shifts heavily to WordPress customization quality.
4. Ongoing plugin compatibility and upgrade risk.
5. Migration complexity for auth, data, and admin operations.

Implementation outline:

1. Domain mapping workshop from current API contracts to WordPress data model.
2. Custom plugin development for listings, report workflows, and moderation actions.
3. Custom auth bridge for OAuth and token/session behavior.
4. Migration scripts for data model and media handling.
5. Extensive security, performance, and regression testing before launch.

Estimated effort:

- 4 to 9 months depending on team expertise and quality bar.

## 5. Pros and cons summary table

| Criteria | Option A: Content shell | Option B: Headless CMS | Option C: Full rewrite |
| --- | --- | --- | --- |
| Delivery speed | High | Medium | Low |
| Business risk | Low | Medium | High |
| Cost | Low to medium | Medium | High |
| Feature parity risk | Low | Low to medium | High |
| Editorial flexibility | High | High | High |
| Operational complexity | Medium | Medium to high | Medium |
| Long-term maintainability | High | Medium to high | Medium |

## 6. Security and compliance considerations for WordPress

Regardless of option, enforce:

1. Managed patching and update policy for WordPress core, themes, and plugins.
2. Strict plugin allow-list and regular vulnerability scanning.
3. Web application firewall and bot protection.
4. Role-based access and audit logging for editorial actions.
5. Secret management outside repository.
6. Backup and restore testing cadence.

## 7. Recommended implementation path

Recommended path: Option A now, with a planned evaluation of Option B later.

Reasoning:

1. Preserves current product architecture and working deployment model.
2. Delivers WordPress value where it is strongest (content and publishing).
3. Avoids expensive rewrite before product maturity and usage patterns stabilize.
4. Keeps the door open to deeper CMS integration without discarding existing investment.

## 8. Suggested phased rollout

Phase 1:

1. Stand up WordPress for content pages only.
2. Define navigation, branding, and analytics continuity.
3. Keep core app and admin workflows on current stack.

Phase 2:

1. Move selected content types to WordPress API consumption in React apps.
2. Add content cache and publishing workflows.
3. Validate performance and operational impact.

Phase 3:

1. Reassess full consolidation only if there is clear business value and funding.
2. Run proof of concept before any rewrite commitment.

## 9. Decision checklist for go or no-go

Before adopting WordPress in production, confirm:

1. Which option is selected and why.
2. Ownership model for WordPress operations and patching.
3. Security controls and monitoring are in place.
4. SEO and URL strategy is approved.
5. Backup, restore, and incident response runbooks are tested.
6. Cost model is approved for 12-month horizon.

## 10. Final recommendation

For this codebase, deploying as a pure WordPress application is not recommended for the current phase.

Best fit:

- Use WordPress for content and publishing.
- Keep transactional product functionality in the existing React plus ASP.NET Core plus PostgreSQL stack.
- Re-evaluate deeper WordPress integration only after measured business outcomes justify additional complexity.
