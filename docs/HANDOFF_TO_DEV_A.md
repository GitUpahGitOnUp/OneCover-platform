# Developer B -> Developer A: running checklist

Last updated: 2026-10-31. Developer B keeps this list current. Tick an item (`[x]`) when it is done, and add new ones at the
bottom of the right section. Changes to this file go through a PR you both approve, like the rest of `docs/`.

## 1. Pull requests waiting for your review

Please review in this order. CI first, because once it is merged every later PR gets a green check.

| # | Branch | What it does | Needs from you |
|---|---|---|---|
| 1 | `feature/ci-build-and-test` | GitHub Actions: restore, build and test `OneCover.slnx` on every PR and push to `main` | Approve. Then add the **`build-and-test`** check as required in the `main` branch protection rules |
| 2 | `feature/disbursements-health` | Anonymous `GET /health` on Disbursements (liveness only, no database check) | Approve |
| 3 | `feature/disbursements-summary` | `GET /api/admin/disbursements/summary` (Admin): counts and money totals per status | Approve |

- [ ] 1. CI approved and merged
- [ ] 1b. `build-and-test` set as a required check on `main`
- [ ] 2. Disbursements `/health` approved and merged
- [ ] 3. Disbursements `/summary` approved and merged

Note on CI: it deliberately has **no path filters**. A required check that a path filter skips stays "pending" forever and blocks
the PR. The whole solution builds in well under a minute, so one job is simpler. Tell B if you want filters anyway.

## 2. Things B needs from you (these block B's work)

- [ ] **Enrollment `GET /internal/enrollments/{id}`**: the response shape (plan type, benefit percent, weekly maximum,
      elimination days, coverage start date, status, tier). B's Claims service uses a stub until this exists.
- [ ] **Identity `GET /internal/members/{id}`**: confirm it is final. B plans to build the real Identity client first, against it.
- [ ] **Open question: `DisbursementPaid` / `DisbursementFailed` sync back to Claims.** B proposes an HTTP callback to an
      `/internal/**` endpoint on Claims, same `X-Internal-Api-Key` header. Not agreed yet, and it is optional. B cuts it first if behind.

## 3. Things for you to set up (your areas: gateway and docker-compose)

- [ ] **Gateway routes** (default JWT policy). Do **not** route `/internal/**`:
  - `/api/disbursements/**` and `/api/admin/disbursements/**` -> Disbursements (this already covers `/summary`)
  - `/api/claims/**` and `/api/admin/claims/**` -> Claims (coming; includes `/api/admin/claims/summary`)
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
