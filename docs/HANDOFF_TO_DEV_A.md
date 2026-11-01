# Developer B -> Developer A: running checklist

Last updated: 2026-11-01 (after the Claims `/health` and both Dockerfile PRs). Developer B keeps this list current. Tick an item (`[x]`) when it is done, and add new ones at the
bottom of the right section. Changes to this file go through a PR you both approve, like the rest of `docs/`.

**If you are an AI assistant reading this for Developer A:** read in this order. Section 6 is the machine-checkable contract between
the services (exact paths, headers, JSON shapes, status codes, how to run things and what to test). Section 2 lists what Developer B
needs from A's services. Sections 1, 3, 4 and 5 are the human checklist. Do not guess a shape that is not written here: if
Section 6 and the code disagree, the code on the PR branch wins, and you should tell the developer so this file gets fixed.

## 1. Pull requests waiting for your review

Please review in this order. CI first, because once it is merged every later PR gets a green check.

| # | Branch | What it does | Needs from you |
|---|---|---|---|
| 1 | `feature/ci-build-and-test` | GitHub Actions: restore, build and test `OneCover.slnx` on every PR and push to `main` | Approve. Then add the **`build-and-test`** check as required in the `main` branch protection rules |
| 2 | `feature/disbursements-health` | Anonymous `GET /health` on Disbursements (liveness only, no database check) | Approve |
| 3 | `feature/disbursements-summary` | `GET /api/admin/disbursements/summary` (Admin): counts and money totals per status | Approve |
| 4 | `feature/claims-repository-and-dtos` | Claims: `IClaimRepository`, response DTOs (`ClaimDto`, `ClaimHistoryDto`) and mappings. No endpoints yet | Approve. Claims only returns ids (no member or plan names): the web app looks names up from Identity and Enrollment |

GitHub PR numbers: CI #10, this handoff doc #11, Claims repository #12, Disbursements summary #13, Disbursements health #14,
Claims file-a-claim #15, Claims payout alert #16, Claims admin and approval #17, Claims health #18, Claims Dockerfile #19,
Disbursements Dockerfile #20, Claims no self-approval #21 (stacked on #16). The Claims PRs #12, #15, #17, #16 are **stacked** (each is based on the previous one's branch, not on `main`),
and #18 is stacked on #16 and #20 on #19. Merge in the order of the checklist below. After each squash-merge GitHub normally retargets the
next PR to `main`; if it does not, or if conflicts appear, tell B.

The checklist branches have been merged locally into `DeveloperABranch` in the listed order.
This does not constitute GitHub PR approval or merging into `main`; branch-protection setup remains a GitHub-side task.

- [ ] 1. CI approved and merged
- [ ] 1b. `build-and-test` set as a required check on `main`
- [ ] 2. Disbursements `/health` approved and merged
- [ ] 3. Disbursements `/summary` approved and merged
- [ ] 4. Claims repository and DTOs approved and merged
- [ ] 5. This handoff document approved and merged (`docs/handoff-to-dev-a`)
- [ ] 6. Claims "file a claim" (`feature/claims-file-a-claim`): `POST /api/claims`, `GET /api/claims`, `GET /api/claims/{id}` (Member role).
      **Built on top of PR 4**, so it shows PR 4's changes too until PR 4 is merged. Please merge PR 4 first. B will retarget or rebase after.
      Uses a stub Enrollment client and a stub Identity client for now (see section 2).
- [ ] 7. Claims admin endpoints + `ClaimApproved` publishing (`feature/claims-admin-and-approval`): `GET /api/admin/claims`,
      `GET /api/admin/claims/{id}`, `PUT /api/admin/claims/{id}/status` (Admin role). Approving sends `ClaimApproved` to Disbursements
      over HTTP with retries. **Built on top of PR 6** (which is on top of PR 4): merge 4, then 6, then 7.
      Tested end to end with both services running: the Pending disbursement appears, and a second delivery creates no duplicate.
- [ ] 8. Claims payout alert (`feature/claims-payout-alert`): flags approved claims whose payout Disbursements never confirmed.
      Adds a column to Claims' own table (migration `AddPayoutRequestedAt`, nullable, B owns it), `GET /api/admin/claims/summary`
      (counts per status + `awaitingPayoutCount`), a `?awaitingPayout=true` list filter, and `POST /api/admin/claims/{id}/resend-payout`.
      **Built on top of PR 7**: merge 4, 6, 7, then 8. Tested with both services running, including Disbursements down (503, flag stays).
- [ ] 9. Claims `/health` (`feature/claims-health`, #18): anonymous `GET /health`, same as Disbursements. **Built on top of PR 8.**
- [ ] 10. Claims Dockerfile (`feature/claims-dockerfile`, #19): multi-stage, non-root, listens on 8080 inside the container, plus the repo-root
      `.dockerignore`. Independent of the other Claims PRs (based on `main`). Build from the **repo root**:
      `docker build -f services/claims/OneCover.Claims.Api/Dockerfile -t onecover-claims .`
- [ ] 11. Disbursements Dockerfile (`feature/disbursements-dockerfile`, #20): same pattern. **Built on top of PR 10** (reuses its `.dockerignore`).
      `docker build -f services/disbursements/OneCover.Disbursements.Api/Dockerfile -t onecover-disbursements .`
      Both images were built and started with dummy settings; auth-protected routes answered 401 as expected, and Claims `/health` answered 200.
- [ ] 12. Claims no self-approval (`feature/claims-no-self-approval`, #21): the member who filed a claim can't approve it (409). **Built on top of PR 8 (#16).**

Note on CI: it deliberately has **no path filters**. A required check that a path filter skips stays "pending" forever and blocks
the PR. The whole solution builds in well under a minute, so one job is simpler. Tell B if you want filters anyway.

## 2. Things B needs from you (these block B's work)

- [x] **Enrollment `GET /internal/enrollments/{id}`**: implemented with the contract below. `coverageStartDate` is the enrollment's `EnrolledOn` date.
      Claims calls it with the shared `X-Internal-Api-Key` header.
          fields (matching the Claims client contract in `services/claims/.../Clients/EnrollmentTerms.cs`):

  | Field | Type | Meaning |
  |---|---|---|
  | `id` | int | the enrollment |
  | `memberId` | int | the owner. Claims returns 404 if it is not the caller |
  | `planType` | string | `"STD"` or `"LTD"` (anything else cannot have a claim) |
  | `isActive` | bool | coverage is in force |
  | `coverageStartDate` | date `yyyy-MM-dd` | a leave cannot start before this |
  | `benefitPercent` | decimal | a fraction, `0.60` for 60% (not `60`) |
  | `weeklyMaximum` | decimal | most the plan pays per week |
  | `eliminationDays` | int | unpaid waiting days (7 STD, 90 LTD) |

  Return 404 when there is no such enrollment. Claims calls it with the shared `X-Internal-Api-Key` header.
- [x] **Identity `GET /internal/members/{id}`**: implemented with the response shape below; it does not expose `passwordHash` or `ssnLast4`.
      Claims is wired to call it with the shared `X-Internal-Api-Key` header.
- [ ] **Open question: `DisbursementPaid` / `DisbursementFailed` sync back to Claims.** B proposes an HTTP callback to an
      `/internal/**` endpoint on Claims, same `X-Internal-Api-Key` header. Not agreed yet, and it is optional. B cuts it first if behind.

## 3. Things for you to set up (your areas: gateway and docker-compose)

- [x] **Gateway routes** (default JWT policy). Do **not** route `/internal/**`:
  - `/api/disbursements/**` and `/api/admin/disbursements/**` -> Disbursements (this already covers `/summary`)
  - `/api/claims/**` and `/api/admin/claims/**` -> Claims (all built now, see PRs 6, 7 and 8; this covers `/summary` and `/resend-payout`)
- [x] **docker-compose settings per service** (env vars, never in source control):

  | Service | Port | Env vars |
  |---|---|---|
  | Identity | 5001 | `ConnectionStrings__IdentityDb`, `Jwt__Key`, `Jwt__Issuer`, `Jwt__Audience`, `InternalApiKey` |
  | Enrollment | 5002 | `ConnectionStrings__EnrollmentDb`, `Jwt__Key`, `Jwt__Issuer`, `Jwt__Audience`, `InternalApiKey` |
  | Disbursements | 5004 | `ConnectionStrings__DisbursementsDb`, `Jwt__Key`, `Jwt__Issuer`, `Jwt__Audience`, `InternalApiKey` |
  | Claims | 5003 | `ConnectionStrings__ClaimsDb`, `Jwt__Key`, `Jwt__Issuer`, `Jwt__Audience`, `InternalApiKey`, `Identity__BaseUrl`, `Enrollment__BaseUrl`, `Disbursements__BaseUrl` |

  `Jwt__Key` must be identical in every service. `InternalApiKey` must be the same in every API service.
  Claims' three base URLs are required. Compose uses service DNS names and port 8080 inside the network.
- [x] **Container health check**: Identity, Enrollment, Claims, and Disbursements expose anonymous liveness `GET /health`.
      Compose uses `service_started` for API dependencies; the images intentionally contain no `curl` or `wget`.
      Containers listen on **8080 inside**; map host ports 5001 (Identity), 5002 (Enrollment), 5003 (Claims), and 5004 (Disbursements) to 8080.
- [ ] Angular base URL pointing at the gateway (`web/` does not exist yet).

## 4. Docs and contracts changes B will send you

- [x] Tick the done items in the `docs/MICROSERVICES_PLAN.md` checklist. Done in this PR. Please check that you agree with the ticks, and
      with the new `resend-payout` row in the Claims endpoint table.
- [x] `docs/DISBURSEMENTS_INTEGRATION.md`: `/summary` and `/health` shown as built, `ClaimApproved` shown as built, container port is 8080. Done in this PR.

## 5. For your information (decisions B made inside Claims and Disbursements)

No action needed unless you disagree.

- Business-rule failures return **409** (as agreed). Missing or not-yours returns **404** with one fixed message.
- Money rounds half **away from zero** (the monolith used banker's rounding).
- The one-year claim limit counts both end days: `endDate >= startDate.AddYears(1)` is refused (slightly stricter than the monolith).
- Disbursement due date = approval date + 7 days (one constant in `DisbursementService`).
- A claim with a total benefit of 0 (a leave no longer than the elimination period) **cannot be approved**; it can only be denied.
  Disbursements refuses an amount of 0, so approving it would promise a payout that can never be created.
- **Known gap (partly handled, please say if you accept the rest):** Claims saves the approval first and then sends `ClaimApproved`
  (3 tries, short waits). If Disbursements stays down through all 3 tries, the claim stays Approved, the admin still sees success,
  and **no disbursement is created** until someone re-sends the event. Disbursements is idempotent, so a re-send is safe.
  PR 8 makes this **visible and fixable**: such claims are flagged (`awaitingPayoutCount` on the summary, a filter on the list) and an
  admin can click "Re-send payout". It is deliberately manual: nobody is paged. **Decision (B, 2026-11-01): no outbox and no automatic
  re-sender.** The alert plus the manual re-send is the accepted design. Say so if you disagree.
- **For the dashboard (Angular, later):** show `awaitingPayoutCount` from `/api/admin/claims/summary` as a warning badge when it is above 0,
  linking to the claims list filtered with `?awaitingPayout=true`, with a "Re-send payout" button per row.
- Claims that were approved **before** PR 8's column existed show as awaiting payout until re-sent (re-sending is harmless). This only
  affects B's local dev data; there is no real data yet.
- **Decided (B, 2026-11-01): nobody approves a claim they filed.** Another admin must approve it. Built in `BenefitClaim.MoveTo` on
  `feature/claims-no-self-approval` (stacked on PR 8): approving with a user id equal to the claim's `memberId` is a **409**. It only
  matters when a member is later promoted to Admin, since roles already keep the two apart. Denying and reviewing are not restricted.
  Say so if you want the same rule for Denied, or want Enrollment/Identity to enforce anything similar.

## 6. Integration contracts and how to verify them (for Developer A and A's AI assistant)

This section is the exact wire-level contract. Everything here was read from the code on the PR branches above. JSON is camelCase.
Enums are sent and accepted as **strings** (`"InReview"`), never numbers. Dates are `yyyy-MM-dd`. Money is a JSON number (decimal).

### 6.1 Authentication every service shares
- Header `Authorization: Bearer <JWT>`. Signed HS256 with the shared secret `Jwt:Key` (at least 32 bytes), and `iss`/`aud` must equal
  `Jwt:Issuer` / `Jwt:Audience`. Expiry is checked. Services read claim **`sub`** (the user id, an integer written as text) and claim
  **`role`** (`Member` or `Admin`) by those literal names. Inbound claim renaming is OFF, so Identity must issue exactly `sub` and `role`.
- Identity's tokens must therefore validate in Claims and Disbursements unchanged. **Test this first** once Identity can log in:
  log in, then call `GET /api/claims` on Claims with the token. Expect 200 (empty list), not 401.
- Wrong or missing token: 401. Right token, wrong role: 403. Someone else's record, or no such record: **404**, never 403.
- Every error body is RFC 9457 ProblemDetails (`application/problem+json`) with `status` and `title`.
- Service-to-service calls use header **`X-Internal-Api-Key: <InternalApiKey>`**. A missing or wrong key is 401. The value is compared in
  constant time. `/internal/**` is never routed by the gateway: through the gateway it must return 404, not 401.

### 6.2 Claims -> Disbursements (built, B's side; A only needs the gateway not to route it)
`POST {Disbursements__BaseUrl}/internal/events/claim-approved` with the key header and body
`{"claimId": 7, "memberId": 3, "amount": 1234.56, "approvedOn": "2026-11-01"}` (contract type `OneCover.Contracts.Events.ClaimApproved`).
Answers **204** whether it created the disbursement or it already existed (idempotent). Claims tries up to 3 times, retrying only network
errors, timeouts, 5xx, 408 and 429, and gives up at once on any other 4xx.

### 6.3 What Claims calls on A's services
Both calls send `X-Internal-Api-Key`. Return **404** (empty or ProblemDetails body) when the id does not exist.

**Enrollment `GET /internal/enrollments/{id}`** -> 200:
```json
{ "id": 1, "memberId": 3, "planType": "STD", "isActive": true, "coverageStartDate": "2026-01-01",
  "benefitPercent": 0.60, "weeklyMaximum": 1500, "eliminationDays": 7 }
```
`planType` is exactly `"STD"` or `"LTD"` for disability plans (other plans may return their own value; Claims refuses them).
`benefitPercent` is a fraction (0.60 not 60). Claims returns 404 to a member when `memberId` is not the caller.

**Identity `GET /internal/members/{id}`** -> 200. Claims reads only `id` and `annualSalary` (number), but the plan says to return
`id, role, firstName, lastName, hireDate, annualSalary, employmentType`. Never return `passwordHash` or `ssnLast4`.

Claims now calls the real Identity and Enrollment endpoints. Canned client implementations remain available for isolated tests;
the application registers the HTTP clients.

### 6.4 Claims public API (what the gateway and Angular will call)
All under `/api`. Member = role `Member`, Admin = role `Admin`.

| Method and path | Role | Body / query | Success |
|---|---|---|---|
| `POST /api/claims` | Member | `{"enrollmentId": 1, "reason": "Surgery", "startDate": "2026-03-01", "endDate": "2026-04-15"}` | 201 + claim |
| `GET /api/claims` | Member | optional `?page=1&pageSize=50` | 200, own claims |
| `GET /api/claims/{id}` | Member | | 200 (own only, else 404) |
| `GET /api/admin/claims` | Admin | optional `?status=InReview`, `?awaitingPayout=true`, `?page=1&pageSize=50` | 200 list |
| `GET /api/admin/claims/{id}` | Admin | | 200 |
| `PUT /api/admin/claims/{id}/status` | Admin | `{"status": "InReview", "note": "optional"}` | 200 + claim |
| `GET /api/admin/claims/summary` | Admin | | 200 `{totalClaims, claimsByStatus:[{status,count}], awaitingPayoutCount}` |
| `POST /api/admin/claims/{id}/resend-payout` | Admin | none | 200; 409 if the claim was never approved or its payout is already confirmed; 503 if Disbursements is still unreachable |

The claim JSON has: `id, memberId, enrollmentId, reason, startDate, endDate, status, planBenefitPercent, planWeeklyMaximum,
planEliminationDays, weeklyBenefit, totalBenefit, submittedAt, payoutRequestedAt, awaitingPayout, nextStatuses, history`.
The request has **no amount fields**; the server computes them once, from the salary and plan terms at filing time.
Illegal workflow moves and business-rule failures are **409**. Workflow: Submitted -> InReview <-> InfoRequested; InReview -> Approved or
Denied; Approved or Denied -> Closed. A claim whose `totalBenefit` is 0 cannot be approved, and neither can the member who filed it
(409; needs `feature/claims-no-self-approval`).

### 6.5 Disbursements public API
| Method and path | Role | Success |
|---|---|---|
| `GET /api/disbursements` | Member | own payouts |
| `GET /api/admin/disbursements` | Admin | all payouts (filterable) |
| `PUT /api/admin/disbursements/{id}/status` | Admin | mark Paid or Failed |
| `GET /api/admin/disbursements/summary` | Admin | counts and money totals per status |
| `GET /health` | anonymous | 200 `Healthy` |

Due date = approval date + 7 days. Disbursements refuses an amount of 0.

### 6.6 Running and testing locally
- Settings (env vars in Docker, `dotnet user-secrets` locally): see the table in Section 3. API services **refuse to start**
  if a required setting is blank. `Jwt__Key` and `InternalApiKey` must be identical across the API services.
- Local ports without Docker (the `http` launch profile): Claims 5094, Disbursements 5175. Compose ports: Claims 5003, Disbursements 5004.
- Unit and integration tests need no database or running service. From the repo root: `dotnet test`.
- Development only: Disbursements has `GET /dev/token?userId=3&role=Admin`, which returns a test JWT (it does not exist outside Development,
  so it never works in Docker). Real tokens come from Identity.

### 6.7 Checks A's AI should run once A's endpoints exist (in this order)
1. **Token handoff.** Log in via Identity, then call `GET /api/claims` on Claims. Expect 200 `[]`. A 401 means the claim names, issuer, audience or key differ.
2. **Enrollment contract.** `GET /internal/enrollments/{id}` with the key returns exactly the fields in 6.3; without the key, 401; unknown id, 404.
   Check the type of `benefitPercent` (0.60) and the date format.
3. **Identity contract.** `GET /internal/members/{id}` returns `annualSalary` as a number and never returns `passwordHash` or `ssnLast4`.
4. **Gateway.** `/api/claims/**`, `/api/admin/claims/**`, `/api/disbursements/**`, `/api/admin/disbursements/**` reach the right service with a valid token.
   `/internal/**` returns 404 through the gateway.
5. **End to end** (the plan's last Phase 3 item): register -> enroll in STD -> `POST /api/claims` -> as Admin move it to `InReview` then
   `Approved` -> `GET /api/admin/disbursements` shows one `Pending` row for that claim with the right amount -> mark it `Paid`.
   Then `POST /api/admin/claims/{id}/resend-payout` on that approved claim: still exactly one disbursement (Disbursements is idempotent).
   Trying to set an already Approved claim to Approved again is an illegal workflow move: expect 409.
6. **Failure path.** Stop Disbursements, approve a claim: the approval still returns 200, `awaitingPayout` is `true` and the summary's
   `awaitingPayoutCount` is 1. Start Disbursements, `POST /api/admin/claims/{id}/resend-payout`: flag clears, one disbursement exists.
7. **Security.** A member asking for another member's claim gets 404. A Member token on an `/api/admin/**` route gets 403. A token with the
   `memberId` in the body or URL instead of `sub` is ignored.
