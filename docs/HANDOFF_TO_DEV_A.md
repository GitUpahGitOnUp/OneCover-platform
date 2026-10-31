# Developer B -> Developer A: running checklist

Last updated: 2026-10-31 (after Claims step 2). Developer B keeps this list current. Tick an item (`[x]`) when it is done, and add new ones at the
bottom of the right section. Changes to this file go through a PR you both approve, like the rest of `docs/`.

## 1. Pull requests waiting for your review

Please review in this order. CI first, because once it is merged every later PR gets a green check.

| # | Branch | What it does | Needs from you |
|---|---|---|---|
| 1 | `feature/ci-build-and-test` | GitHub Actions: restore, build and test `OneCover.slnx` on every PR and push to `main` | Approve. Then add the **`build-and-test`** check as required in the `main` branch protection rules |
| 2 | `feature/disbursements-health` | Anonymous `GET /health` on Disbursements (liveness only, no database check) | Approve |
| 3 | `feature/disbursements-summary` | `GET /api/admin/disbursements/summary` (Admin): counts and money totals per status | Approve |
| 4 | `feature/claims-repository-and-dtos` | Claims: `IClaimRepository`, response DTOs (`ClaimDto`, `ClaimHistoryDto`) and mappings. No endpoints yet | Approve. Claims only returns ids (no member or plan names): the web app looks names up from Identity and Enrollment |

- [ ] 1. CI approved and merged
- [ ] 1b. `build-and-test` set as a required check on `main`
- [ ] 2. Disbursements `/health` approved and merged
- [ ] 3. Disbursements `/summary` approved and merged
- [ ] 4. Claims repository and DTOs approved and merged
- [ ] 5. This handoff document approved and merged (`docs/handoff-to-dev-a`)
- [ ] 6. Claims "file a claim" (`feature/claims-file-a-claim`): `POST /api/claims`, `GET /api/claims`, `GET /api/claims/{id}` (Member role).
      **Built on top of PR 4**, so it shows PR 4's changes too until PR 4 is merged. Please merge PR 4 first. B will retarget or rebase after.
      Uses a stub Enrollment client and a stub Identity client for now (see section 2).

Note on CI: it deliberately has **no path filters**. A required check that a path filter skips stays "pending" forever and blocks
the PR. The whole solution builds in well under a minute, so one job is simpler. Tell B if you want filters anyway.

## 2. Things B needs from you (these block B's work)

- [ ] **Enrollment `GET /internal/enrollments/{id}`**: B's Claims service uses a stub until this exists. Claims needs exactly these
      fields (B's temporary shape, in `services/claims/.../Clients/EnrollmentTerms.cs`; tell B if yours will differ):

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
- [ ] **Identity `GET /internal/members/{id}`**: the plan lists it, but B cannot find it in the repo (`main` or `DeveloperABranch`).
      Is it built, and on which branch? B wants to build the real Identity client first, against it.
      Claims reads only `id` and `annualSalary` from the response (a number; B assumes the default camelCase JSON names).
- [ ] **Open question: `DisbursementPaid` / `DisbursementFailed` sync back to Claims.** B proposes an HTTP callback to an
      `/internal/**` endpoint on Claims, same `X-Internal-Api-Key` header. Not agreed yet, and it is optional. B cuts it first if behind.

## 3. Things for you to set up (your areas: gateway and docker-compose)

- [ ] **Gateway routes** (default JWT policy). Do **not** route `/internal/**`:
  - `/api/disbursements/**` and `/api/admin/disbursements/**` -> Disbursements (this already covers `/summary`)
  - `/api/claims/**` and `/api/admin/claims/**` -> Claims (`/api/claims` is built, see PR 6; admin routes and
    `/api/admin/claims/summary` are coming)
- [ ] **docker-compose settings per service** (env vars, never in source control):

  | Service | Port | Env vars |
  |---|---|---|
  | Disbursements | 5004 | `ConnectionStrings__DisbursementsDb`, `Jwt__Key`, `Jwt__Issuer`, `Jwt__Audience`, `InternalApiKey` |
  | Claims | 5003 | `ConnectionStrings__ClaimsDb`, `Jwt__Key`, `Jwt__Issuer`, `Jwt__Audience`, and later `InternalApiKey` plus the Disbursements address |

  `Jwt__Key` must be identical in every service. `InternalApiKey` must be the same in Claims and Disbursements.
- [ ] **Container health check**: Disbursements answers `GET /health` with 200 `Healthy` and no token (after PR 2 merges).
      Claims will do the same. B will add Dockerfiles for both.
- [ ] Angular base URL pointing at the gateway (`web/` does not exist yet).

## 4. Docs and contracts changes B will send you

- [ ] Tick the done items in the `docs/MICROSERVICES_PLAN.md` checklist (B's items are done but still unticked).
- [ ] `docs/DISBURSEMENTS_INTEGRATION.md`: change "Planned: `/summary`" to built, and add `/health` to the endpoint list.

## 5. For your information (decisions B made inside Claims and Disbursements)

No action needed unless you disagree.

- Business-rule failures return **409** (as agreed). Missing or not-yours returns **404** with one fixed message.
- Money rounds half **away from zero** (the monolith used banker's rounding).
- The one-year claim limit counts both end days: `endDate >= startDate.AddYears(1)` is refused (slightly stricter than the monolith).
- Disbursement due date = approval date + 7 days (one constant in `DisbursementService`).
- **Open design question for the owner:** should the person who filed a claim be barred from approving it themselves?
  Not in any doc, so not implemented.
