# OneCover web app

Angular 21 member-facing frontend for OneCover. The browser calls the YARP gateway only; it does
not know the addresses of individual services.

## Run locally

Start the backend stack from the repository root, then:

```powershell
cd web
npm ci
npm start
```

Open `http://localhost:4200` or `http://127.0.0.1:4200`. The gateway must allow the exact origin in
`Cors:AllowedOrigins`; both local origins are allowed by default.

## Gateway configuration

Set `public/config.json`'s `apiBaseUrl` to the gateway address for the target environment. This
file is loaded at runtime, so the same frontend build can be hosted against different gateways.
Do not add direct service URLs or secrets to frontend configuration.

Authentication uses the Identity service's bearer token. The frontend keeps the session in
`sessionStorage` and attaches the token only to requests whose URL targets the configured gateway.
UI role checks are for presentation only; authorization is enforced by each API service.

## Current scope

- Member sign-in and registration
- Member profile and FMLA eligibility from Identity
- Active plan catalog and member enrollment
- Per-paycheck deduction shown from the Enrollment response

Claims, disbursements, and admin pages are not implemented yet.

Run `npm run build` for a production build and `npm test -- --watch=false` for the unit tests.
