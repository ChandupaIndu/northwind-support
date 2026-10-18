# Northwind Support Desk

A support ticket management system: raise tickets, search and filter them, assign them to agents,
and move them through their lifecycle.

**.NET 10 Web API** + **React 19 / TypeScript** + **SQL Server**, in one monorepo.

---

## The assignment

You are joining a team that already has this app working. Complete the tasks **in order**.
Full requirements are in **[`ASSIGNMENT_CANDIDATE.md`](ASSIGNMENT_CANDIDATE.md)**.

| # | Task | Done looks like | Effort |
| --- | --- | --- | --- |
| 1 | **Fix a defect, add server-side filtering** | Root cause fixed with a regression test; every list filter applied in the database with a correct count | ~2.5 h |
| 2 | **Ticket escalation & assignment** | Automatic triage on create, escalation with history, SLA status, UI for all of it | ~7 h |
| 3 | **Login and authentication** | Agents sign in; API rejects unauthenticated calls; login UI | ~4 h |
| 4 | **Dockerise the API** | `docker compose up` gives a migrated, seeded API with no baked-in secret | ~1.5 h |

### Guidance

- **Time: 2 days.** Honestly marking a task "not finished" beats rushing all of them.
- **Fork** this repo to your own GitHub account and work there. **Do not push to the original.**
- **AI tools are allowed.** You must understand and be able to defend every line you submit.
- **Commit as you go** and keep the real history. Commit the Task 1 defect fix and the filtering
  separately from each other and from the feature work. A single squashed commit is not accepted.
- **Where the spec is ambiguous**, make a decision, write it down in the README, and move on.
- **Out of scope:** registration, password reset, roles, OAuth/SSO, refresh tokens, MFA,
  notifications, CI, caching, visual redesign. Mention them under *Known limitations* instead.

### Submission

1. Update this **README** with what you built, your design decisions (chosen, rejected and why),
   the defect's root cause, dev login credentials, how to run and test it, AI usage, and known
   limitations. A reviewer must be able to run it without asking you anything.
2. Add an **APPROACH.md** explaining your approach and thought process for each task.
3. Commit both, then send us the **link to your fork**, the **dev login credentials**, and a
   **rough note of time spent** on what. (If the fork is private, add the reviewers as collaborators.)

---

## Quick start

**Prerequisites:** .NET SDK 10 (`global.json`), Node.js 22 (`.nvmrc`; 20.19+ works), Docker Desktop or another Docker Engine with Compose.

```bash
cp .env.example .env          # required for Docker Compose; ignored by Git
npm install                   # installs the web app
docker compose up --build     # API + SQL Server; first start applies migrations and seeds
```

For host-run development instead of the API container:

```bash
npm install                   # installs the web app
npm run db:up                 # SQL Server 2022 in Docker on localhost:1433
npm run api                   # terminal 1: migrates, seeds, then serves the API
npm run web                   # terminal 2: React dev server
```

| What | Where |
| --- | --- |
| Web app | http://localhost:5173 |
| API | https://localhost:7043 (also http://localhost:5043) |
| Swagger | https://localhost:7043/swagger |

In Development the API applies migrations and seeds demo data on startup. **Never commit real
credentials or signing keys.** `.env.example` contains local-only fixtures; copy it to `.env` and
replace them with private values for your machine. The Docker Compose API connection string uses
`sqlserver` as the database host on the Compose network; the host-run connection string uses
`localhost`.

## Task 4: Dockerise the API

The API image is built from the repository root with `apps/api/Dockerfile`. Its SDK restore layer
copies the solution, shared build/package files, and API project files before copying API source,
so editing a `.cs` file can reuse the package restore layer. The final image uses the ASP.NET
runtime image and runs as its non-root `app` user on HTTP port 8080. Compose publishes the API at
`http://localhost:5080` and SQL Server at `localhost:1433`; SQL storage is kept in the named
`supportdesk-sql` volume. The web app is not containerized.

Copy `.env.example` to `.env` before starting Compose. Set `MSSQL_SA_PASSWORD` and
`Jwt__SigningKey` to private local values; the checked-in example values are disposable fixtures,
not production credentials. Compose passes both to the containers at runtime. The API's connection
string is assembled at runtime with `Server=sqlserver`, and the environment defaults to
`Development`. No signing key, connection string, or development appsettings file is copied into
the image. Compose starts the API only after SQL Server's health check reports healthy. On first
start against an empty volume, Development startup applies migrations and seeds demo data.
`GET http://localhost:5080/health` checks database reachability and is anonymous.

The migrations present are `20260926183612_InitialCreate`,
`20261017162826_Task2EscalationHistory`, and `20261017193620_Task3AgentAuthentication`.
API logs from the disposable Compose SQL Server confirmed all three applied and Development
seeding completed. This was verified locally; the instructions below distinguish it from a
production deployment, where migrations should run as a controlled one-time deployment step.

```bash
docker compose up --build
# In another terminal:
curl -i http://localhost:5080/health
curl -i http://localhost:5080/api/tickets  # unauthenticated API calls correctly return 401
docker compose exec api whoami             # should print a non-root user
```

Sign in through the web app to verify seeded ticket data. To use the container API from the Vite
development server, set the proxy target before starting it:

```bash
VITE_API_PROXY_TARGET=http://localhost:5080 npm run web
```

In PowerShell, use `$env:VITE_API_PROXY_TARGET='http://localhost:5080'; npm run web`.

`docker compose down` stops containers and keeps the database volume. To verify a completely fresh
database locally, `docker compose down -v && docker compose up --build` removes the Compose volumes,
then recreates the database, runs migrations, and seeds it. **This deletes the local Compose
database data; use it only when that data is disposable.** Do not use this command against any
shared or production database. In a real deployment, run reviewed EF migrations as a controlled,
single deployment step (for example, a migration job) before rolling out API replicas. This avoids
giving every API instance schema-change responsibility or having replicas race to migrate.

To confirm restore caching, run `docker compose build --progress plain api`, edit a source `.cs`
file, then build again and check that the `dotnet restore` layer is reported as cached. To report
image sizes, compare the local runtime image with the SDK image using:

```bash
docker image inspect northwind-support-api:local --format '{{.Size}}'
docker image inspect mcr.microsoft.com/dotnet/sdk:10.0 --format '{{.Size}}'
```

Measured with the `docker image inspect ... --format '{{.Size}}'` commands above:

| Image | Bytes | MiB |
| --- | ---: | ---: |
| API runtime image | 454,871,864 | 433.80 |
| .NET 10 SDK image | 1,303,854,013 | 1,243.45 |

The API image is smaller because it contains the published application and ASP.NET runtime,
while the SDK image also includes build tools and the .NET SDK. Sizes vary by platform/build.

### Local agent sign-in

The Development seeder provisions the same local-only password for the six demo agents below.
Passwords are stored as ASP.NET Core Identity hashes; these are assessment fixtures and must not
be used outside a local development database.

| Agent | Email | Status |
| --- | --- | --- |
| Alex Turner | `alex.turner@northwind-support.example` | Active |
| Ben Osei | `ben.osei@northwind-support.example` | Active |
| Priya Nair | `priya.nair@northwind-support.example` | Active |
| Sara Lindqvist | `sara.lindqvist@northwind-support.example` | Active |
| Marcus Doyle | `marcus.doyle@northwind-support.example` | Active |
| Yuki Tanaka | `yuki.tanaka@northwind-support.example` | Inactive; cannot sign in |

**Local-only password for the five active demo agents:** `Northwind-Local-Only-2026!`

The API reads its JWT signing key from `Jwt:SigningKey`, supplied as the environment variable
`Jwt__SigningKey`. `.env.example` contains a clearly marked local fixture value. Replace it with a
private random key in any non-local environment; do not commit that key. Tokens last 60 minutes.
The signing key must contain at least 32 UTF-8 bytes. The issuer and audience can be configured
with `Jwt__Issuer` and `Jwt__Audience`.

**Visual Studio 2026:** run `npm run db:up`, open `SupportDesk.sln`, set
**SupportDesk.Presentation** as the startup project, and press F5 on the **https** profile.

## Task 3: Login and authentication

`POST /api/auth/login` accepts an email and password and returns a signed JWT, its UTC expiry, and
the agent's ID, name, and email. Unknown email, incorrect password, and inactive-agent attempts
share the same 401 response. `GET /api/auth/me` returns the current agent. A fallback authorization
policy protects API endpoints unless they explicitly allow anonymous access; login is anonymous.
Swagger remains available in Development.

`PasswordHash` is stored on `Agent` because the requirement authenticates existing agent records
and does not add a separate user or role lifecycle. Keeping the salted hash with the agent avoids
a second identity table and login join. The Task 3 migration adds the column so existing agents
remain migratable. Migration `20261017193620_Task3AgentAuthentication` adds the nullable column;
Development seeding hashes the local fixture password when agents are seeded.

The web app keeps the token in `sessionStorage` and sends it as a Bearer token through the shared
API client. Session storage survives page refreshes in the current tab, but not closing that tab;
JavaScript running in the page can access it, so this is less resistant to XSS than an HttpOnly
cookie. Authenticated 401 responses clear the session and send the user to login. After successful
login the requested route is restored. Logout removes the tab's session. There are no refresh
tokens or server-side token revocation.

## Tests

```bash
npm test                      # dotnet test, then the web tests (Vitest)
```

`npm test` runs both suites and neither needs a running SQL Server. Previously verified results
(not rerun for this documentation update):

| Check | Command | Result |
| --- | --- | --- |
| Backend tests | `dotnet test SupportDesk.sln --no-restore` | 76 passed |
| Frontend tests | `npm run test --workspace apps/web` | 22 passed across 7 files |
| Frontend lint | `npm run lint --workspace apps/web` | Passed |
| Frontend type-check | `npm run typecheck --workspace apps/web` | Passed |
| Frontend production build | `npm run build --workspace apps/web` | Passed |

Backend coverage includes domain/application tests, architecture checks, SQLite in-memory query
tests, password-hasher and login-handler tests, JWT signature/claims tests, and HTTP authentication
integration tests. `AuthenticationHttpTests` uses `WebApplicationFactory`/`TestServer` with an
isolated in-memory SQLite database to verify identical 401 responses for unknown email and wrong
password, `/api/auth/me` with and without a token, and a protected tickets endpoint with and without
a token. The suite does not apply migrations to a live SQL Server.

## Commands

| Command | What it does |
| --- | --- |
| `docker compose up --build` / `docker compose down` | Start API + SQL Server / stop containers (database volume retained) |
| `npm run db:up` / `db:down` / `db:reset` | Start / stop / wipe SQL Server for host-run development |
| `npm run api` / `npm run web` | Run the API / the React app |
| `npm run build` | Build both |
| `npm run lint` / `typecheck` / `format` | Web lint and types / `dotnet format` |
| `npm run ef -- migrations add <Name>` | Add an EF Core migration |
| `npm run db:migrate` | Apply EF Core migrations to the configured database |

## API at a glance

| Endpoint | Description |
| --- | --- |
| `POST /api/auth/login` | Exchange local agent credentials for a 60-minute JWT |
| `GET /api/auth/me` | Return the authenticated agent |
| `GET /api/tickets` | Paged list with `search`, `status`, `priority`, `categoryId`, `customerId`, `assignedAgentId`, `unassignedOnly`, `slaStatus`, `sortBy`, `sortDirection`. Filters and count run in the database before paging. |
| `GET /api/tickets/{id}` | One ticket |
| `POST /api/tickets` | Create a ticket and return the automatic triage decision |
| `PATCH /api/tickets/{id}/status` · `/assignment` | Change status / assignee |
| `POST /api/tickets/{id}/escalate` | Increase priority one level, recalculate the SLA, re-evaluate assignment, and record the reason and actor |
| `GET /api/tickets/{id}/escalations` | Immutable escalation history, newest first |
| `GET /api/customers` · `/api/agents` · `/api/categories` | Reference data |

## Task 1: filter defect and SQL filtering

The ticket list effect originally depended only on `search`, `page`, and `pageSize`, so changing a status or another filter did not run the effect. Search later caused a request using the latest filter state, which made the earlier change appear stale. The fix lists each request-affecting filter as an effect dependency and delays only requests whose search text changed; other filter changes request immediately. The hook test verifies the status value in the request.

Filtering lives in `TicketQueries`: `ApplyFilters` adds each supplied predicate to the same `IQueryable`, so filters combine with AND and remain database-side. The query counts that filtered source before it applies sorting and paging. The SQLite query tests exercise real results and capture SQL to confirm the filtered count is issued by the database.

To add a regular filter, add its bound property to `TicketQuery` and one conditional predicate in `ApplyFilters`; keep the predicate translatable by EF Core. For a derived SLA state, add the SQL expression in `ApplySlaFilter`. Add it to the web filter model/API serialization if it is user-selectable, and cover result membership and filtered counts in `TicketQueriesTests`. Sorting, count, and paging should not need restructuring.

## Task 2: triage, SLA, assignment, and escalation

Ticket creation applies the configured priority and SLA rules, then assigns the least-loaded
active agent who meets specialization and capacity rules; ties use agent ID. If no agent qualifies,
the ticket is created unassigned with a reason. Escalation raises priority one level, recalculates
the due date and assignment, and appends an immutable history row. Critical and closed tickets
cannot be escalated. Base SLA windows are Critical 4 h, High 8 h, Medium 24 h, and Low 72 h;
Premium customers use half the window with a 1 h floor. At-risk starts at 25% remaining. These
values are configuration, and SLA status is derived from due/resolution timestamps and the injected
clock. `slaStatus` filtering is applied in the database before count and paging.

The `(TicketId, EscalatedAtUtc)` index supports newest-first history reads. Ticket deletion
cascades escalation history, while agent deletion is restricted to retain actor/assignee references.
The backend tests cover triage rules, SLA boundaries, assignment cases, escalation outcomes and
rejections. Frontend tests cover escalation validation and server errors.

## Design decisions

- **Filter trigger:** the hook uses explicit dependencies rather than watching the whole filters object, which changes identity on every update. Search alone is debounced to avoid a request per keystroke; the other filters are immediate as the acceptance criteria require.
- **Server-side query composition:** predicates are appended to a shared EF query before `CountAsync`; filtering results in memory would make counts and pages wrong and would load unnecessary rows.
- **Task 2 triage:** a shared application service owns priority, SLA, and assignment decisions for create and escalation. Category flags drive policy, and settings supply SLA windows. A hand-coded category-name switch or separate create/escalation rule implementations would duplicate policy and drift.
- **Assignment:** automatic selection filters active, qualified, under-capacity agents, then orders by open count and ID. Escalation keeps its current agent when still eligible, as the assessment explicitly allows; manual assignment checks activity and specialist qualification but remains a team-lead override for capacity.
- **SLA status and history:** SLA state is derived from due/resolution timestamps and the injected clock, not persisted as mutable state. Escalations are separate append-only records; history is ordered by timestamp then ID, and the composite index supports that lookup. Ticket deletion cascades history; agent deletion is restricted to preserve referenced identities.
- **Validation/errors:** request validation is centralized in the action filter and shared exception middleware returns problem details. Escalation records its actor from the authenticated name claim; the public request body contains only the reason.
- **Authentication:** ASP.NET Core JWT bearer validation protects the API with a fallback authorization policy. Login remains anonymous and all invalid credential cases use the same response. The built-in Identity password hasher stores salted hashes; no custom password cryptography was introduced.
- **Token persistence:** the browser uses `sessionStorage` to persist login across refreshes in a tab while ending the session when that tab closes. An HttpOnly cookie would reduce JavaScript access to credentials, but would require a cookie/CSRF design not otherwise present in this API.
- **Signing key:** the key comes from configuration/environment rather than code. Development configuration includes an explicitly local fixture so the demo runs immediately; deployments must set a private key through `Jwt__SigningKey`.

## AI usage

OpenAI Codex assisted with repository inspection, implementation, documentation, and test
iteration. One concrete change in the available work history: a Task 2 test exposed that the
escalation-history error and empty-history message could appear together; the component was
adjusted so the empty message appears only after a successful empty response. The backend suite
was reported passing with 76 tests and the frontend suite with 22 tests. These suite results do
not prove every AI-assisted line was independently reviewed.

**Candidate: fill these from your own recollection; the available repository/history does not
establish these examples. Do not submit invented details.**

- Accepted from AI as-is: `[specific suggestion/change you reviewed and accepted]`.
- Changed or rejected, and why: `[specific suggestion/change and your reason]`.
- Verification evidence available: backend `dotnet test SupportDesk.sln --no-restore` was reported
  with 76 passing tests; `npm run test --workspace apps/web` was reported with 22 passing tests.
  Docker verification also covered image build/start, SQL readiness and migration/seeding logs,
  health/auth/ticket HTTP responses, non-root runtime, and restore-layer reuse. `[Candidate:
  confirm which checks you personally ran and describe your review/manual verification workflow.]`
- AI bug: the Task 2 test finding above is evidenced; `[confirm it is accurate and note any other AI-introduced bug or state that there was none]`.

No separate prompt transcript or other AI tools are evidenced in the available history; add any
omitted tools or workflows yourself.

## Known limitations

- Registration, password reset, roles, refresh tokens, MFA, OAuth/SSO, and notifications are outside scope. Tokens cannot be revoked server-side before their 60-minute expiry.
- The shared demo password and Development signing key are for local assessment use only. Production identity lifecycle, secret rotation, lockout/rate limiting, and deployment secret management are not implemented.
- The Docker Compose Development setup automatically migrates its local database; production migration execution and secret management remain deployment responsibilities.
- Automated assignment is based on current open-ticket counts and does not reserve capacity transactionally, so concurrent ticket creation can exceed an agent's limit.
- Automated query tests use SQLite in-memory. Separately, the disposable local Docker Compose SQL
  Server verification confirmed from API logs that all three EF migrations and Development seeding
  ran; the seeded ticket endpoint returned HTTP 200. This does not verify production deployment
  migration procedures.
- Browser login and ticket listing were exercised against the Vite proxy, but the request was not
  independently confirmed in Chrome's Network panel.
- Notifications, background processing, roles, and other explicitly out-of-scope features remain unimplemented.

**Seed data:** 5 categories, 6 customers (3 Premium), 6 agents (one inactive, one at their limit)
and 40 tickets across every status, priority and SLA state.

## Submission details

- **Fork URL:** local Git `origin` is `https://github.com/ChandupaIndu/northwind-support`.
  `[Candidate: confirm this is your assessment fork and reviewers can access it; the remote URL
  alone does not establish the fork relationship or visibility.]`
- **Time spent:** `[Candidate: enter actual total and rough Task 1–4 breakdown; available history
  does not establish hours spent.]`
- **Commit-message note:** the visible defect-fix subject is `fix: reload tickets when filters
  change`; the assignment asks for the root cause in that commit message. The subject alone does
  not state the root cause. No Git history was changed as part of this documentation update.

## Troubleshooting

| Symptom | Fix |
| --- | --- |
| API cannot reach the database | `npm run db:up`, wait ~20 s, check `docker compose ps` |
| Port 1433 already in use | Change the port in `docker-compose.yml` and `.env` |
| Browser warns about the certificate | `dotnet dev-certs https --trust`, or use the web app, which proxies |
| Web app shows "Unable to load tickets" | The API is not running, or `VITE_API_PROXY_TARGET` points at the wrong port |
| SQL Server slow on Apple Silicon | Enable Rosetta in Docker Desktop → Settings → General |
