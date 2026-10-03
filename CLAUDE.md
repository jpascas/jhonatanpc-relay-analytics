
## Applications
- .Net 8 API located in `backend` folder
- Angular SPA located in `frontend` folder

## Commands
- Backend tests, from the `backend` folder. Paste the summary lines. Never say tests pass without running them in this session.
  - Unit tests only (fast, no Docker): `dotnet test Relay.Api.Tests`
  - Unit + integration (needs Docker running; Testcontainers starts a throwaway SQL Server): `dotnet test`. Slices run this one.
- Frontend tests: `cd frontend && npx ng test --watch=false`
- Database: `docker compose up -d sqlserver`, then `dotnet ef database update --project backend/Relay.Api`
- Full stack (DB + API + frontend): `docker compose up -d --build` → http://localhost:4200 (API on http://localhost:8080)

## Tests
Show each new test failing first, then passing.

## Dependencies
No new NuGet or npm packages. Propose one with the reason and wait.
In general No new packages without asking, even outside the project.

## Don't touch
- `seed.sql`, `schema.sql`: source data, used as-is.
