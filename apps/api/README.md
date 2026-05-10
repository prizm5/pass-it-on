# Pass It On API

This application owns the backend domain model and HTTP surface for the MVP.

## Current scaffold

- Minimal ASP.NET Core API host
- Route groups for public and admin contracts
- Domain entities and enums aligned to the planning checklist
- Root `.env` loading for local secrets and Docker Compose overrides

## Next backend steps

1. Finalize contract shapes and request or response DTOs.
2. Add persistence, migrations, and repositories or modules.
3. Implement authentication and authorization.
4. Replace placeholder responses with real behavior.
