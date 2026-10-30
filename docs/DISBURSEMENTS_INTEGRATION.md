# Disbursements: what other services must match

Status: **AGREED** by Developer A and Developer B (2026-10-30). The Disbursements service is built against the
settings below. If something changes, update this file in a PR the other developer approves.
The "Where to change it" column shows how small each change is.

## 1. Token (JWT) settings

Every service validates the JWT itself. Disbursements expects:

| Setting | Value | Where to change it |
|---|---|---|
| Issuer | `onecover-identity` | env var `Jwt__Issuer` (no code change) |
| Audience | `onecover` | env var `Jwt__Audience` (no code change) |
| Signing | HMAC-SHA256 with a shared key, at least 32 characters | env var `Jwt__Key` (same value in every service) |
| Member id claim | `sub`, a whole number (`int`) | `Security/ClaimsPrincipalExtensions.cs`, `Program.cs` (`NameClaimType`) |
| Role claim | `role`, with values exactly `Member` and `Admin` | `Program.cs` (`RoleClaimType`) and the `[Authorize(Roles = ...)]` attributes |

Claim names are read exactly as written in the token (inbound claim renaming is switched off).

**Identity does (confirmed):** put the user's numeric id in `sub` and a single role string in `role`.

## 2. User id type

`MemberId` is an `int` everywhere in Disbursements (entity, DTO, database column).
Confirmed: Identity's `User.Id` is an `int`, and Identity puts it in the `sub` claim. No change needed.

## 3. Endpoints (built)

| Method | Path | Role |
|---|---|---|
| GET | `/api/disbursements?page=&pageSize=` | Member (own rows only; someone else's id returns 404) |
| GET | `/api/disbursements/{id}` | Member |
| GET | `/api/admin/disbursements?status=&page=&pageSize=` | Admin |
| GET | `/api/admin/disbursements/{id}` | Admin |
| PUT | `/api/admin/disbursements/{id}/status` | Admin. Body `{ "status": "Paid" }` or `{ "status": "Failed", "reason": "..." }` |

Planned: `GET /api/admin/disbursements/summary`.

**Gateway:** route `/api/disbursements/**` and `/api/admin/disbursements/**` to Disbursements, default (JWT) policy.
Do not route `/internal/**`.

Enums travel as text (`"Pending"`, `"Paid"`, `"Failed"`). Dates are `yyyy-MM-dd`.

## 4. Errors

Every error is RFC 9457 ProblemDetails (`application/problem+json`).

| Case | Status |
|---|---|
| No or invalid token | 401 |
| Wrong role | 403 |
| Not found, or someone else's record | 404 |
| Business rule broken (wrong state, missing reason) or concurrent edit | 409 |

## 5. `ClaimApproved` (agreed, NOT built yet)

Event in `contracts/`:

```csharp
public record ClaimApproved(int ClaimId, int MemberId, decimal Amount, DateOnly ApprovedOn);
```

- **Transport: HTTP callback.** After approving a claim, Claims sends a POST to
  `/internal/events/claim-approved` on Disbursements. The gateway does not route `/internal/**`.
- **Auth: shared key.** The caller sends the header `X-Internal-Api-Key`, and the service compares it with the
  setting `InternalApiKey` (same approach as Identity's `InternalApiKeyAttribute`).
- **Idempotent.** Disbursements creates one Pending disbursement per `ClaimId`. Delivering the same event
  twice creates one row (a unique index on `ClaimId` enforces it), and the second call still returns success.
- **Due date: `ApprovedOn` + 7 days** (agreed). It is a constant in `Services/DisbursementService.cs`.

## 6. Configuration for compose

| Name | Purpose |
|---|---|
| `ConnectionStrings__DisbursementsDb` | database connection string; database name `DisbursementsDb` |
| `Jwt__Issuer`, `Jwt__Audience`, `Jwt__Key` | token validation (section 1) |
| `InternalApiKey` | service-to-service key, sent as the header `X-Internal-Api-Key` (section 5) |
| Port | 5004 inside compose (the plan's port) |

Planned: `/health` endpoint and a Dockerfile.
The database schema is applied with EF migrations. The service owner (B) runs them.

## 7. Open questions

1. How does Disbursements tell Claims that a payout is `Paid` or `Failed` (`DisbursementPaid` and
   `DisbursementFailed`)? Proposed: the same pattern, an HTTP callback to an `/internal/**` endpoint on Claims.

Closed: user id is `int`; callback transport; shared-key header; 7-day due date; JWT claims.
