# OneCover Platform

Employee benefits enrollment and disability claims, rebuilt as microservices behind an API gateway.
Derived from the OneCover monolith (see "Monolith reference"). Two developers, one monorepo.

Read `docs/MICROSERVICES_PLAN.md` first. It is the source of truth for scope, endpoints, events and order of work.

## Stack
ASP.NET Core Web API (.NET 10), EF Core 10, SQL Server, YARP gateway, JWT, Docker Compose,
Angular + Angular Material, xUnit, Vitest.

## Layout
- `services/identity/OneCover.Identity.Api` – users, login/register, JWT issuing, profile (`/api/me`)
- `services/enrollment/OneCover.Enrollment.Api` – plans, tier rates, eligibility, enrollments
- `services/claims/OneCover.Claims.Api` – benefit claims, claim workflow, benefit calculator
- `services/disbursements/OneCover.Disbursements.Api` – benefit payouts for approved claims
- `gateway/` – YARP reverse proxy, the only address the web app calls
- `contracts/OneCover.Contracts` – shared DTOs and event types (the ONLY shared code)
- `web/` – Angular app (see `web/CLAUDE.md` once it exists)
- `docs/` – plan, endpoint table, architecture notes

Each service follows controllers → services → repositories, with its own `DbContext`, migrations and tests.

## Ownership
- Developer A: Identity, Enrollment, gateway, docker-compose.
- Developer B: Claims, Disbursements, CI, event transport.
- Only a service's owner changes its migrations. Changes to `contracts/` or `docs/` go through a PR the other developer approves.

## Commands
- Build everything: `dotnet build` (from repo root)
- Run a service: `cd services/<name>/OneCover.<Name>.Api && dotnet run`
- Tests: `dotnet test` · `cd web && ng test`
- Web: `cd web && ng serve` (http://localhost:4200)

## Architecture rules (do not break)
1. A service never references another service's project. Only `contracts/`.
2. A service never reads another service's database. Each service owns its tables.
3. Services talk by HTTP (`/internal/**`, behind an interface such as `IEnrollmentClient`) or by events (`ClaimApproved`).
4. `/internal/**` endpoints are service-to-service only. The gateway does not route them.
5. Event handlers must be idempotent (the same event delivered twice creates one result).
6. Secrets (JWT key, connection strings) never go in source control. Use user-secrets locally and env vars in Docker.

## Conventions
- Name the claim entity `BenefitClaim` (avoids a clash with `System.Security.Claims.Claim`).
- Return DTOs, never entities. Build DTOs with **named arguments** in mapping methods, never positional.
- Members only get their own data. Take the user id from the JWT, never from the URL or body.
- Store only the last 4 of the SSN.
- Every service validates the JWT itself; the gateway check is an extra layer, not a replacement.
- One error format across all services: RFC 9457 ProblemDetails from a global exception handler.
- Async/await all the way down for I/O. No `.Result` or `.Wait()`.
- Inject `TimeProvider` for "today" so date rules are testable.
- Business rules live inside the entity or class that owns them (encapsulation). Controllers stay thin.
- XML doc comments (`/// <summary>`) on public classes, methods, interfaces, DTO properties and enum values
  whose names don't fully explain them.

## Domain rules carried over from the monolith
**Disability benefit (STD/LTD)**
- Weekly benefit = annual salary / 52 x plan benefit percent, capped at the plan's weekly maximum.
- Total benefit = (weekly / 7) x payable days. The first `EliminationDays` of a leave are unpaid (7 for STD, 90 for LTD).
- Calculated only on the server, once, when the claim is filed. The request has no amount fields.
  A claim keeps the plan terms it was filed under, even if the plan changes later.

**Claims**
- Only against an active STD or LTD enrollment.
- Leave can't end before it starts or begin before coverage did, and one claim can't exceed one year.
- Workflow (every move writes a history row):
  Submitted → InReview ⇄ InfoRequested; InReview → Approved | Denied; Approved/Denied → Closed.

**FMLA eligibility:** at least 12 months since hire date AND at least 1,250 estimated hours in the last 12 months.
Hours come from hire date and employment type (full-time = 40/week, part-time = 20).

**Security:** someone else's record returns 404 (the API never confirms it exists). The UI hides what a role can't do,
but every service enforces its own rules.

## What changed from the monolith
- **Payments are disbursements only.** Premiums are payroll-deducted, so no premium payments are stored.
  Members see a calculated, estimated per-paycheck deduction. `PaymentType.Premium` is gone; `Payment` becomes `Disbursement`.
- **Coverage tiers:** EmployeeOnly, EmployeeSpouse, EmployeeChildren, Family. Prices live in a `PlanRate` table
  (plan + tier). The rate is snapshotted on the enrollment. STD/LTD are EmployeeOnly.
- **Approving a claim no longer creates a payment in the same transaction.** Claims publishes `ClaimApproved`;
  Disbursements creates a Pending disbursement (eventual consistency).
- `/api/me` belongs to Identity. The admin dashboard is composed in Angular from per-service `/summary` endpoints.

## Monolith reference
The original monolith is a sibling folder: `../OneCover` (read-only reference).
Port logic from it, don't edit it. Key files:
- `api/OneCover.Api/Models/` – `BenefitClaim`, `ClaimWorkflow`, `Plan`, `Enrollment`, `Payment`, `User`
- `api/OneCover.Api/Services/` – `ClaimService`, `BenefitCalculator`, `EligibilityService`, `EnrollmentService`, `AuthService`
- `api/OneCover.Api/Controllers/` – current routes
- `docs/BRD.md`, `PROJECT_PLAN.md` – requirements and original scope
- `web/` – Angular app to repoint at the gateway

## Team workflow
- `main` is protected: PR + 1 approval + green CI. No direct pushes.
- Branches: `feature/<service>-<thing>`, `fix/...`. Squash-merge, delete the branch after.
- Keep PRs small (one plan checklist item). Pull `main` daily.
- Personal files (`CLAUDE.local.md`, `DEV_JOURNAL.md`) are gitignored.
