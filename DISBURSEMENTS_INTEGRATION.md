# Disbursements: what other services must match

Status: **DRAFT** from Developer B. The Disbursements service is built against the assumptions below.
If something here doesn't suit Identity, the gateway or compose, say so and B changes the Disbursements side.
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

**Identity must:** put the user's numeric id in `sub` and a single role string in `role`.

## 2. User id type

`MemberId` is an `int` everywhere in Disbursements (entity, DTO, database column).
If Identity uses `Guid`, B changes it in one PR plus a new migration. Tell B before the Identity schema is final.

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

## 5. `ClaimApproved` (planned, NOT built yet)

Proposed event in `contracts/`:

```csharp
public record ClaimApproved(int ClaimId, int MemberId, decimal Amount, DateOnly ApprovedOn);
```

- Disbursements creates one Pending disbursement per `ClaimId`. Delivering the same event twice creates one row
  (a unique index on `ClaimId` enforces it).
- Proposed transport: Claims sends an HTTP POST to `/internal/events/claim-approved` on Disbursements.
  The endpoint is protected by a shared `X-Internal-Key` header (env var `Internal__Key`). **Not agreed yet.**
- Due date is currently `ApprovedOn + 7 days` (a placeholder; a constant in `Services/DisbursementService.cs`).

## 6. Configuration for compose

| Name | Purpose |
|---|---|
| `ConnectionStrings__DisbursementsDb` | database connection string; database name `DisbursementsDb` |
| `Jwt__Issuer`, `Jwt__Audience`, `Jwt__Key` | token validation (section 1) |
| `Internal__Key` | service-to-service key (section 5, once agreed) |
| Port | 5004 inside compose (the plan's port) |

Planned: `/health` endpoint and a Dockerfile.
The database schema is applied with EF migrations. The service owner (B) runs them.

## 7. Open questions for A

1. User id: `int` or `Guid`?
2. Event transport: HTTP callback (proposed) or RabbitMQ?
3. Service-to-service key: is a shared header acceptable?
4. Due date rule: 7 days after approval, or something else?
5. Are the JWT claim names and values in section 1 what Identity will issue?
