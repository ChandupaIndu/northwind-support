# Approach and thought process

This note records the implementation decisions and available verification evidence for the four
assessment tasks. The assignment asks the candidate to understand and defend the submitted work;
the personal time and AI-reflection prompts at the end need candidate confirmation.

## Task 1 — defect fix and filtering

The defect was in the ticket-list effect: it observed search, page, and page size, but omitted
other request-affecting filters. Changing status updated UI state without issuing a request; a
later search change happened to send the new status and made the behavior look intermittent. The
fix includes each filter in the effect dependencies and debounces only search text, so a filter
change requests immediately while typing does not issue one request per keystroke. The hook test
checks that the status is present in the request.

Backend filters are composed as predicates on the EF `IQueryable` in `TicketQueries`, before the
filtered count, sorting, and paging. This keeps filtering and counts in SQL and lets a new filter
be added as another predicate instead of a query rewrite. SQLite query tests exercise returned
rows/counts and inspect generated SQL. The SLA filter follows the same query composition pattern.

## Task 2 — triage, SLA, and escalation

Priority, due date, eligibility, and assignment decisions are centralized in an application triage
service used by both ticket creation and escalation. Category flags and configuration drive the
rules, avoiding a category-name switch and duplicated policy. Eligible agents are filtered by
activity, specialization where required, and capacity; least-open-ticket count wins with agent ID
as a deterministic tie-break. If no agent qualifies, creation remains successful and records the
unassigned reason.

SLA status is derived from due/resolution timestamps and an injected clock rather than saved as
mutable state. Escalation recalculates the due date and assignment, and writes a separate history
row. The `(TicketId, EscalatedAtUtc)` index supports newest-first history retrieval. Task 3
replaces client-supplied escalation actor data with the authenticated identity claim.

## Task 3 — authentication

The assignment allows either credentials on `Agent` or a linked users table. `PasswordHash` was
added to `Agent` because sign-in is for existing agents and no separate user/role lifecycle is
required. This keeps the salted credential hash with its account and avoids a second table and
login join. The migration preserves existing agents; the Development seeder creates hashes with
ASP.NET Core Identity's password hasher rather than persisting plaintext credentials.

The authentication migration is `20261017193620_Task3AgentAuthentication`; the nullable hash
column lets existing agent rows remain valid during migration.

JWT validation and a fallback authorization policy protect endpoints centrally, with login and
the documented anonymous endpoints allowed explicitly. The key is configuration/environment
driven, and the selected token lifetime is 60 minutes. The browser stores the token in
`sessionStorage`: refreshes in a tab retain login, while closing the tab ends that stored session.
Because page JavaScript can read it, this has more XSS exposure than an HttpOnly cookie; moving to
cookies would require a deliberate cookie and CSRF design. Escalation actor identity comes from
the validated token claim rather than request input.

Available test evidence includes password-hasher and login-handler tests, JWT signature/claim
tests, and HTTP tests hosted by `WebApplicationFactory`/`TestServer` with isolated in-memory
SQLite. The HTTP tests cover identical 401 responses for unknown email and wrong password,
`/api/auth/me` with and without a token, and a protected tickets endpoint with and without a token.
The reported backend suite result was 76 passing tests; the frontend suite result was 22 passing
tests. These results do not establish browser Network-panel inspection of the Vite proxy.

## Task 4 — Docker

The API uses a multi-stage .NET 10 SDK/runtime image. Project and shared package/build metadata
are copied before source so a source-only change can reuse restore. `.dockerignore` excludes
local secrets, build outputs, Git data, Node modules, and the web app. Compose supplies database
and JWT settings at runtime, waits for SQL Server health, publishes the API on port 5080, and
persists SQL data in its named volume. The API listens on HTTP 8080 and runs as the non-root
`app` user. Development startup applies migrations and seeds the local database; a deployment
should run reviewed migrations as one controlled deployment step before API replicas start.

Verification evidence supplied for the disposable Compose environment: the image built and ran;
SQL Server was healthy; API logs confirmed all three migrations and Development seeding; `/health`,
`/api/auth/me`, and `/api/tickets` returned HTTP 200; the API ran as `app`; restore-layer reuse was
observed after a source-only change; browser login and ticket listing worked. The browser proxy
request was not independently inspected in Chrome's Network panel. Image sizes measured with the
same Docker image-inspection method were 454,871,864 bytes (433.80 MiB) for the API runtime image
and 1,303,854,013 bytes (1,243.45 MiB) for the .NET SDK image. These figures are specific to the
measured images and environment.

The migration sequence present in the repository is `20260926183612_InitialCreate`,
`20261017162826_Task2EscalationHistory`, and `20261017193620_Task3AgentAuthentication`.

## Test and frontend check evidence

Previously reported successful commands (not rerun for this documentation edit):

- `dotnet test SupportDesk.sln --no-restore`: 76 backend tests passed.
- `npm run test --workspace apps/web`: 22 frontend tests passed across 7 files.
- `npm run lint --workspace apps/web`: passed.
- `npm run typecheck --workspace apps/web`: passed.
- `npm run build --workspace apps/web`: production build passed.

## Candidate completion before submission

- Actual effort: `[enter total time and rough Task 1–4 breakdown; the available evidence does not
  establish hours spent]`.
- Fork: local `origin` is `https://github.com/ChandupaIndu/northwind-support`; `[confirm this is
  the candidate's fork and that reviewer access/visibility is correct]`.
- AI reflection required by the assignment: `[give one personally verified example accepted
  as-is, if there was one; none is established in the available history. The history does support
  a changed behavior: a Task 2 test showed an error and empty-history message appearing together,
  so the UI was changed to show empty state only after a successful empty response. Candidate:
  confirm this account and explain your own review/verification workflow; do not infer acceptance
  from test outcomes alone.]`.
