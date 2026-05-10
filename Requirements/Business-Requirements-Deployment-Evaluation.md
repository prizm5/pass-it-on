# Business Requirements Document (BRD)

## Pass It On

## Deployment Evaluation for Additional Platforms

## 1. Document control

- Version: 1.0
- Date: 2026-05-10
- Status: Draft for stakeholder review
- Prepared for: Product, Engineering, Security, Operations, and Deployment stakeholders

## 2. Purpose

Define business and operational requirements that must be met before deploying Pass It On to additional hosting platforms beyond the current local and containerized development setup.

This document provides a consistent evaluation framework so platform decisions can be made based on business outcomes, risk, and total cost of ownership, not only technical preference.

## 3. Background

Pass It On is a mobile-first platform for exchanging kids clothing and kid items within and across daycares. The solution includes:

- Public web application
- Admin web application
- ASP.NET Core API
- PostgreSQL data store
- Social login integrations
- Optional S3-compatible image upload pipeline

Current deployment enablement includes Docker Compose-based full-stack deployment and environment-variable based runtime configuration.

## 4. Business objectives

1. Expand deployment portability so the application can be hosted on multiple target platforms (cloud managed services, container platforms, or customer-managed infrastructure).
2. Reduce vendor lock-in risk for core runtime and data services.
3. Improve confidence in production readiness through explicit non-functional and compliance criteria.
4. Enable repeatable deployment with measurable operational outcomes.
5. Support growth in users, listings, and admin moderation activity without architecture rework.

## 5. In scope

- Platform evaluation criteria and acceptance thresholds
- Business-critical functional capabilities that must be preserved across platforms
- Security, privacy, and reliability requirements for deployment approval
- Operational and support requirements
- Migration and rollout requirements for adding a new platform

## 6. Out of scope

- Detailed low-level infrastructure-as-code templates
- UI redesign or feature backlog reprioritization
- Full disaster-recovery runbook authoring
- Legal contract negotiation with hosting vendors

## 7. Stakeholders

- Product owner: validates business fit and release priorities
- Engineering lead: validates architecture and implementation feasibility
- Operations or DevOps lead: validates deployability, monitoring, and support model
- Security lead: validates identity, secret management, and data protection controls
- Compliance or legal representative: validates data handling and retention obligations
- Admin operations representative: validates moderation and operational continuity

## 8. Business capabilities and required outcomes

### 8.1 User and exchange capabilities

1. User registration and profile management must remain available and consistent.
2. Public listing discovery and detail viewing must remain available and performant.
3. Listing creation with image support must remain available when enabled.
4. Social login must remain functional with approved identity providers.

### 8.2 Admin and trust capabilities

1. Admin authentication and authorization must be enforced.
2. Admin moderation workflows must be available with auditability.
3. Reporting and abuse handling endpoints must remain operational.

### 8.3 Platform portability outcomes

1. Application must run without code changes other than environment-specific configuration.
2. Runtime configuration must be supplied via environment variables or secure platform configuration stores.
3. Data persistence must support PostgreSQL-compatible managed or self-managed options.
4. Object storage integration must support S3-compatible APIs or an approved abstraction path.

## 9. Functional deployment requirements

1. The platform shall support independent deployment of API, public web app, and admin web app.
2. The platform shall provide HTTPS termination and secure routing for all externally accessible services.
3. The platform shall support database connectivity with encrypted transport in production.
4. The platform shall support secret injection for JWT keys, OAuth secrets, and storage credentials.
5. The platform shall support health checks for API and web services.
6. The platform shall support environment-specific configuration for ports, URLs, admin seed controls, and image storage options.

## 10. Non-functional requirements

### 10.1 Availability and resiliency

1. Production target availability: at least 99.9% monthly for externally facing services.
2. Mean time to recover from service outage: less than 60 minutes for Sev-1 incidents.
3. Recovery point objective (RPO): 24 hours maximum data loss.
4. Recovery time objective (RTO): 4 hours maximum for full service restoration.

### 10.2 Performance

1. API p95 response time under normal load: less than 500 ms for standard CRUD endpoints.
2. Public and admin app initial page load p95: less than 3 seconds on typical mobile broadband.
3. Platform must demonstrate horizontal scaling path for API and web tiers.

### 10.3 Security and privacy

1. All traffic must be encrypted in transit using TLS.
2. Secrets must not be stored in source control.
3. Access to production infrastructure must support role-based access control and audit logs.
4. Data retention and deletion support must align with platform policy and published site policies.
5. File upload controls must include content type restrictions, size limits, and malware risk mitigation processes.

### 10.4 Observability and supportability

1. Centralized logging must be available for API and deployment events.
2. Metrics and alerts must be configurable for uptime, error rates, latency, and resource saturation.
3. Health endpoint monitoring must be available with alerting hooks.
4. Deployment rollback path must be defined and tested.

## 11. Compliance and governance requirements

1. Platform hosting region selection must support the business data residency needs.
2. Platform must provide a documented incident response pathway.
3. Platform must allow evidence collection for access logs, deployment logs, and data backup verification.
4. Platform must support periodic vulnerability scanning for container images and dependencies.

## 12. Platform evaluation scorecard

Evaluate each candidate platform with the scorecard below.

Scoring scale:

- 0 = does not meet requirement
- 1 = partially meets requirement with major gap
- 2 = meets requirement with known trade-offs
- 3 = fully meets requirement

Required minimum:

- Overall weighted score: at least 75%
- Any critical category score below 2.0 requires mitigation plan and approval

| Category | Weight | Critical | Example checks |
| --- | ---: | :---: | --- |
| Security and secrets management | 20% | Yes | Secret store, RBAC, audit trails, TLS defaults |
| Reliability and backup capabilities | 15% | Yes | HA options, managed backups, restore testing |
| Deployment automation support | 15% | Yes | CI or CD integration, rollback strategy |
| Performance and scaling | 10% | Yes | Autoscaling options, resource controls |
| Cost and pricing predictability | 10% | No | Baseline monthly cost, growth sensitivity |
| Operations and observability | 10% | Yes | Logs, metrics, alerting, dashboards |
| PostgreSQL support maturity | 10% | Yes | Managed Postgres or equivalent compatibility |
| S3-compatible object storage path | 5% | No | Native S3 or compatible API strategy |
| Regional and compliance fit | 5% | Yes | Region availability, policy alignment |

## 13. Candidate platforms for assessment

At minimum, evaluate:

1. AWS-centric deployment (for example ECS or EKS with RDS and S3)
2. Azure-centric deployment (for example App Service or AKS with Azure Database for PostgreSQL and Blob strategy)
3. GCP-centric deployment (for example Cloud Run or GKE with Cloud SQL and object storage strategy)
4. Kubernetes on customer-managed infrastructure
5. Traditional VM deployment with container runtime and managed PostgreSQL

## 14. Risks and mitigations

1. Risk: provider-specific services increase lock-in.
   - Mitigation: prefer standards-based runtime, PostgreSQL compatibility, and S3-compatible storage abstractions.
2. Risk: social login redirect and domain management complexity across environments.
   - Mitigation: enforce environment-specific OAuth configuration checklist and validation tests.
3. Risk: operational maturity gap on new platform.
   - Mitigation: require logging, alerting, backup restore drill, and runbook sign-off before go-live.
4. Risk: cost growth from unbounded scale settings.
   - Mitigation: define budget alerts, resource limits, and monthly cost reviews.

## 15. Deployment readiness gates

A platform is eligible for production deployment only when all gates pass:

1. Business capability parity gate: all critical user and admin capabilities validated.
2. Security gate: secrets, access controls, TLS, and vulnerability scanning verified.
3. Reliability gate: backup and restore test completed; failover or recovery procedures demonstrated.
4. Performance gate: p95 response and page load targets met in staging test.
5. Operations gate: monitoring, alerting, and rollback runbook tested.
6. Cost gate: approved monthly cost estimate and scaling envelope documented.

## 16. Evidence required for approval

1. Completed platform scorecard with objective evidence references.
2. Architecture diagram and deployment topology for the selected platform.
3. Security checklist sign-off.
4. Performance test summary and baseline metrics.
5. Backup and restore test report.
6. Go-live and rollback plan approved by engineering and operations.

## 17. Success metrics after go-live

1. Uptime meets or exceeds 99.9% in the first 90 days.
2. Sev-1 incident count remains within approved threshold.
3. Mean deployment time and rollback time trend downward after first 3 releases.
4. No critical security findings remain open beyond agreed remediation SLA.
5. User-reported availability issues do not materially increase after migration.

## 18. Assumptions

1. Core architecture remains React frontends plus ASP.NET Core API plus PostgreSQL.
2. Environment-variable based configuration remains the primary runtime configuration model.
3. OAuth providers and storage integrations can be configured per environment.

## 19. Dependencies

1. Finalized API contracts and admin moderation workflows
2. CI or CD pipeline maturity for target platform
3. Access to security and compliance review resources
4. Operational ownership model for on-call and incident response

## 20. Approval

This BRD requires approval from Product, Engineering, Security, and Operations before selecting a production deployment target.
