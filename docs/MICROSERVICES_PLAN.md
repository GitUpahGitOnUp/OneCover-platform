# OneCover Microservices – Project Plan

## 1. Goal
Rebuild OneCover (employee benefits enrollment + disability claims) as independently
deployable microservices behind an API gateway. Two developers, one monorepo.

### Changes from the monolith
- **Payments are disbursements only.** Premiums are payroll-deducted, so we store no premium payments.
  Members see an estimated per-paycheck deduction (calculated, not stored).
- **Coverage tiers.** Premium depends on tier: EmployeeOnly, EmployeeSpouse, EmployeeChildren, Family.
  Prices live in a `PlanRate` table (one row per plan + tier). The rate is snapshotted on the
  enrollment so later rate changes don't alter what a member pays. STD/LTD are EmployeeOnly.

## 2. Architecture

| Component | Owns | Database | Port |
|---|---|---|---|
| Gateway (YARP) | routing, JWT check at the door | none | 5000 |
| Identity | users, login/register, JWT issuing, profile (/api/me) | IdentityDb | 5001 |
| Enrollment | plans, plan rates, eligibility, enrollments | EnrollmentDb | 5002 |
| Claims | BenefitClaim, claim workflow, benefit calculator | ClaimsDb | 5003 |
| Disbursements | payouts for approved claims | DisbursementsDb | 5004 |
| Web (Angular) | UI, calls the gateway only | none | 4200 |

### Rules
1. A service never references another service's project, only `contracts/`.
2. A service never reads another service's database.
3. User id comes from the JWT only (never URL or body). Every service validates the JWT itself.
4. Return DTOs, never entities. Named arguments in mapping methods.
5. `/internal/**` endpoints are service-to-service only. The gateway does NOT route them.

### How services talk
| From → To | Style | Purpose |
|---|---|---|
| Enrollment → Identity | HTTP `GET /internal/members/{id}` | employment type, salary for eligibility |
| Claims → Identity | HTTP `GET /internal/members/{id}` | salary for the benefit calculation |
| Claims → Enrollment | HTTP `GET /internal/enrollments/{id}` | verify ownership + active, get plan benefit terms |
| Claims → Disbursements | Event `ClaimApproved` | create a Pending disbursement |
| Disbursements → Claims | Event `DisbursementPaid` / `DisbursementFailed` (optional) | keep claim status in sync |

Claims copies the plan terms (benefit %, weekly max, elimination days) onto the claim when filed,
so later plan edits don't change an existing claim.

## 3. Ownership

| | Developer A | Developer B |
|---|---|---|
| Services | Identity, Enrollment | Claims, Disbursements |
| Also owns | Gateway, docker-compose, repo setup | CI (GitHub Actions), event transport, admin dashboard page |
| Angular | login/register, profile, plans, enrollments, admin plans | claims, disbursements, admin claims/payments, dashboard |
| Migrations | IdentityDb, EnrollmentDb | ClaimsDb, DisbursementsDb |

Shared: `contracts/` (changes by PR only, tell the other dev first) and `docs/`.

## 4. Endpoint lookup table
Gateway column = public path Angular calls. Auth: Anon / Any (logged in) / Member / Admin.

### Identity (A)
| Method | Path | Auth | Purpose |
|---|---|---|---|
| POST | /api/auth/register | Anon | register a member |
| POST | /api/auth/login | Anon | returns JWT |
| GET | /api/me | Any | own profile |
| PUT | /api/me | Any | update own profile |
| GET | /internal/members/{id} | internal | profile facts for other services: `Id, Role, FirstName, LastName, HireDate, AnnualSalary, EmploymentType`. Never `PasswordHash` or `SsnLast4` |

### Enrollment (A)
| Method | Path | Auth | Purpose |
|---|---|---|---|
| GET | /api/plans | Anon | active plans with their tier rates |
| GET | /api/plans/{id} | Anon | one plan |
| POST | /api/plans | Admin | create plan + rates |
| PUT | /api/plans/{id} | Admin | edit plan + rates |
| DELETE | /api/plans/{id} | Admin | retire plan (soft) |
| GET | /api/enrollments | Member | own enrollments, with estimated per-paycheck deduction |
| POST | /api/enrollments | Member | enroll in plan + choose tier (eligibility checked) |
| GET | /api/admin/enrollments/summary | Admin | counts for dashboard |
| GET | /internal/enrollments/{id} | internal | owner, status, plan terms for Claims |

### Claims (B)
| Method | Path | Auth | Purpose |
|---|---|---|---|
| GET | /api/claims | Member | own claims |
| GET | /api/claims/{id} | Any | one claim (member: own only) |
| POST | /api/claims | Member | file a claim |
| GET | /api/admin/claims | Admin | all claims (filterable) |
| PUT | /api/admin/claims/{id}/status | Admin | move claim through workflow; Approved publishes `ClaimApproved` |
| GET | /api/admin/claims/summary | Admin | counts for dashboard |

### Disbursements (B)
| Method | Path | Auth | Purpose |
|---|---|---|---|
| GET | /api/disbursements | Member | own benefit payouts |
| GET | /api/admin/disbursements | Admin | all payouts (filterable) |
| PUT | /api/admin/disbursements/{id}/status | Admin | mark Paid / Failed |
| GET | /api/admin/disbursements/summary | Admin | totals for dashboard |

### Gateway route table
| Path prefix | Cluster | Auth policy |
|---|---|---|
| /api/auth/** | identity | anonymous |
| /api/me/** | identity | default (JWT) |
| GET /api/plans/** (method-matched route) | enrollment | anonymous |
| /api/plans/** (all other methods) | enrollment | default (JWT) |
| /api/enrollments/**, /api/admin/enrollments/** | enrollment | default |
| /api/claims/**, /api/admin/claims/** | claims | default |
| /api/disbursements/**, /api/admin/disbursements/** | disbursements | default |
| /internal/** | not routed | – |

A YARP route has one auth policy, so `/api/plans/**` needs two routes: one matching `GET` only (anonymous) and one for the rest.
Role checks (Member vs Admin) stay in each service.

Renamed from the monolith: /api/payments → /api/disbursements, /api/admin/payments → /api/admin/disbursements.
The old /api/admin/dashboard is replaced by the four summary endpoints.

## 5. Events (contracts/)
| Event | Fields | Publisher → Consumer |
|---|---|---|
| ClaimApproved | ClaimId, MemberId, Amount, ApprovedOn | Claims → Disbursements |
| DisbursementPaid | DisbursementId, ClaimId, PaidOn | Disbursements → Claims |
| DisbursementFailed | DisbursementId, ClaimId, Reason | Disbursements → Claims |

Transport (RabbitMQ vs HTTP callback vs in-memory) is decided in Phase 0. The event shapes are the same either way.
Handlers must be idempotent (the same event delivered twice must not create two disbursements).

## 6. Phases
Within a phase, A and B work in parallel. Each box is one PR-sized chunk.

### Phase 0 – Setup and contracts (together)
- [ ] A: GitHub repo, collaborator, branch protection on `main` (`.gitignore`, README, CLAUDE.md done)
- [x] A: `.gitignore`, README, CLAUDE.md
- [ ] A: `docker-compose.yml` (SQL Server only); [x] folder skeleton
- [ ] B: GitHub Actions: build + test on PR, path filters per service
- [ ] Both: review this doc, agree on endpoint table + event shapes
- [ ] Both: `contracts/` project with DTOs and events
- [ ] Both: decide event transport
- [ ] Both: create Issues from this checklist, set up the Project board

### Phase 1 – Foundations (parallel)
- [ ] A: Identity service: User, Role, register/login, JWT, /api/me (includes FMLA eligibility via `IEligibilityService`), /internal/members
- [ ] B: Disbursements service: Disbursement entity, repository, service, member + admin endpoints
- [ ] B: Disbursements consumes `ClaimApproved` (test with a hand-published event)
- [ ] B: dev-only test JWTs so B can call Admin/Member endpoints before Identity is ready

### Phase 2 – Core domain (parallel)
- [ ] A: Enrollment service: Plan, PlanRate, CoverageTier, seed data
- [ ] A: Enrollment rules: plan is active, no duplicate active enrollment, tier has a PlanRate, STD/LTD only EmployeeOnly. (The monolith has no employment-type rule for enrolling; any such restriction is a new decision.)
- [ ] A: Enrollments: enroll with tier, rate snapshot, estimated per-paycheck deduction
- [ ] A: /internal/enrollments/{id}
- [ ] B: Claims service: BenefitClaim, ClaimWorkflow, BenefitCalculator
- [ ] B: Claims uses an `IEnrollmentClient` (stub first, then the real HTTP client once A's endpoint exists)
- [ ] B: file claim, admin status change, publish `ClaimApproved`

### Phase 3 – Integration
- [ ] A: Gateway (YARP) with the route table above
- [ ] A: Compose runs all services + gateway + one SQL Server with 4 databases
- [ ] A+B: Angular base URL → gateway; update payments → disbursements pages
- [ ] A: Angular: tier picker on enrollment, deduction display
- [ ] B: Angular: dashboard composed from the four summary endpoints
- [ ] Both: end-to-end walk: register → enroll → file claim → approve → disbursement → paid

### Phase 4 – Quality and polish
- [ ] Unit tests: eligibility, rate lookup, claim workflow, calculator, idempotent consumer
- [ ] Integration tests per service (WebApplicationFactory)
- [ ] Consistent error responses (ProblemDetails) in every service
- [ ] Health checks (/health) per service, used in compose
- [ ] README: architecture diagram, how to run, who owns what
- [ ] Postman collection against the gateway
- [ ] Interview talking points for each design choice

## 7. Team workflow
- `main` protected: PR + 1 approval + green CI. No direct pushes.
- Branches: `feature/<service>-<thing>`, `fix/...`. Squash-merge, delete branch after.
- Keep PRs small (one checklist box). Pull `main` daily.
- CODEOWNERS: each service folder lists its owner, who gets auto-requested.
- Only the owner changes a service's migrations (the ModelSnapshot conflicts badly).
- Secrets: `.env.example` committed, real `.env` ignored; JWT key via user-secrets / env vars.
- Anything in `contracts/` or `docs/` changes through a PR the other dev must approve.

## 8. If we fall behind (cut in this order)
1. Disbursements → Claims status sync events (keep `ClaimApproved` only)
2. Dashboard summary endpoints (show a simpler admin page)
3. Idempotency/retry polish on the consumer
4. Integration tests (keep unit tests)
5. Docker health checks

Never cut: JWT-only identity, service-owned databases, tier pricing, disbursement flow.

## 9. Open questions
- Event transport: RabbitMQ or simpler?
- Is Docker required by the assignment, or just compose for demo?
- Which plan types get tiers: Dental, Vision, Accident, CriticalIllness get tiers; STD/LTD are EmployeeOnly. **Still open: Life and Pet.** Decide before building PlanRate.
- Pay frequency for the deduction display (biweekly default?)
- ~~Exact fields on Identity's `/internal/members/{id}`~~ Decided, see the Identity endpoint table.
- ~~Where FMLA eligibility lives~~ Decided: Identity (`/api/me`). Enrollment does not need it.
