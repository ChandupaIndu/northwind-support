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
| 4 | **Dockerise the API** *(optional)* | `docker compose up` gives a migrated, seeded API with no baked-in secret | ~1.5 h |

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

**Prerequisites:** .NET SDK 10 (`global.json`), Node.js 22 (`.nvmrc`; 20.19+ works), Docker.

```bash
cp .env.example .env          # optional, every value has a working default
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
credentials.**

**Visual Studio 2026:** run `npm run db:up`, open `SupportDesk.sln`, set
**SupportDesk.Presentation** as the startup project, and press F5 on the **https** profile.

## Tests

```bash
npm test                      # dotnet test, then the web tests (Vitest)
```

Both pass on a clean checkout and neither needs a database.

Backend coverage includes domain/application tests, architecture checks, and SQLite in-memory query tests. It does not apply migrations to a live SQL Server or run HTTP-level API integration tests.

## Commands

| Command | What it does |
| --- | --- |
| `npm run db:up` / `db:down` / `db:reset` | Start / stop / wipe SQL Server |
| `npm run api` / `npm run web` | Run the API / the React app |
| `npm run build` | Build both |
| `npm run lint` / `typecheck` / `format` | Web lint and types / `dotnet format` |
| `npm run ef -- migrations add <Name>` | Add an EF Core migration |

## API at a glance

| Endpoint | Description |
| --- | --- |
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

## Design decisions

- **Filter trigger:** the hook uses explicit dependencies rather than watching the whole filters object, which changes identity on every update. Search alone is debounced to avoid a request per keystroke; the other filters are immediate as the acceptance criteria require.
- **Server-side query composition:** predicates are appended to a shared EF query before `CountAsync`; filtering results in memory would make counts and pages wrong and would load unnecessary rows.
- **Task 2 triage:** a shared application service owns priority, SLA, and assignment decisions for create and escalation. Category flags drive policy, and settings supply SLA windows. A hand-coded category-name switch or separate create/escalation rule implementations would duplicate policy and drift.
- **Assignment:** automatic selection filters active, qualified, under-capacity agents, then orders by open count and ID. Escalation keeps its current agent when still eligible, as the assessment explicitly allows; manual assignment checks activity and specialist qualification but remains a team-lead override for capacity.
- **SLA status and history:** SLA state is derived from due/resolution timestamps and the injected clock, not persisted as mutable state. Escalations are separate append-only records; history is ordered by timestamp then ID, and the composite index supports that lookup. Ticket deletion cascades history; agent deletion is restricted to preserve referenced identities.
- **Validation/errors:** request validation is centralized in the action filter and shared exception middleware returns problem details. Before authentication exists, the escalation request carries the required actor explicitly; Task 3 should source it from the authenticated identity.

## AI usage

Codex was used to inspect the assessment and repository, help draft documentation and focused tests, and review implementation details. I retained the suggested approach of exercising `TicketQueries` through the existing SQLite fixture because it runs the EF query against real data. During test iteration, an initial assertion assumed a history-load error would suppress the empty-history message; the test exposed that the component showed both, so I changed the component to show the empty message only after a successful empty response. I verified the changes with the repository's test, lint, typecheck, and build commands listed above. I did not use or verify other AI tools, and I have no separate prompt transcript to report.

## Known limitations

- Tasks 3 and 4 are not implemented: there is no authentication or production-grade identity/secret handling, and the API is not containerized.
- Escalation actor names are supplied by the caller until authentication can provide a trusted identity; this is suitable only for the current assessment stage.
- Automated assignment is based on current open-ticket counts and does not reserve capacity transactionally, so concurrent ticket creation can exceed an agent's limit.
- SQL behavior is tested with SQLite in-memory; the migration script is generated and checked, but these checks do not replace applying the migration against a running SQL Server instance.
- Notifications, background processing, roles, and other explicitly out-of-scope features remain unimplemented.

### Task 2 design decisions

- Priority, due-date and assignment decisions live in one application triage service shared by ticket creation and escalation. Category flags drive forced Critical priority and specialist eligibility; no category names are embedded in the rules.
- Eligible agents must be active, qualified when required, and below their open-ticket limit. The least busy agent wins; ties use the lowest agent ID. If none qualify, creation succeeds with an unassigned ticket and an explanation.
- SLA windows, the Premium multiplier and floor, and the AtRisk threshold come from the `Sla` configuration section. SLA status is derived using the injected clock. List filtering translates status rules to SQL before count and paging.
- Until Task 3 authentication is added, escalation requests include and validate `escalatedBy`. The `(TicketId, EscalatedAtUtc)` index supports newest-first history reads. Ticket deletion cascades its history; agent deletion is restricted so referenced agent IDs remain intact.

Errors are `ProblemDetails`: `400` validation, `404` not found, `409` conflict or business rule.

**Seed data:** 5 categories, 6 customers (3 Premium), 6 agents (one inactive, one at their limit)
and 40 tickets across every status, priority and SLA state.

## Troubleshooting

| Symptom | Fix |
| --- | --- |
| API cannot reach the database | `npm run db:up`, wait ~20 s, check `docker compose ps` |
| Port 1433 already in use | Change the port in `docker-compose.yml` and `.env` |
| Browser warns about the certificate | `dotnet dev-certs https --trust`, or use the web app, which proxies |
| Web app shows "Unable to load tickets" | The API is not running, or `VITE_API_PROXY_TARGET` points at the wrong port |
| SQL Server slow on Apple Silicon | Enable Rosetta in Docker Desktop → Settings → General |
