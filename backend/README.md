# AcxiomCRM API

ASP.NET Core 8 Web API for the static AcxiomCRM demo. It uses ASP.NET Core Identity, EF Core, and SQLite (`backend/AcxiomCRM.Api/acxiomcrm.db`). The database schema is created on first run. Identity manages password hashing, password policy, roles, lockout, and the authentication cookie; password hashes are never returned by the API.

## Run

Install the .NET 8 SDK, then from `backend/AcxiomCRM.Api` run:

```powershell
dotnet run
```

The API listens on `http://localhost:5080`; health check: `GET /health`. To bootstrap the first administrator, set `BootstrapAdmin__Email` and `BootstrapAdmin__Password` in the process environment before starting the API. The bootstrap password must contain at least 8 characters, including uppercase, lowercase, a digit, and a non-alphanumeric character. Do not commit credentials. Public registration creates SalesExecutive users only; administrators can create users and assign roles through `/api/users`.

The backend allows credentialed browser requests from the common local development origins on ports 5500, 5501, and 5173. Keep the frontend and API on the same hostname (`localhost`) so the Lax/Strict cookies are sent; opening `index.html` as a `file://` URL does not provide a permitted CORS origin. Configure production origins in the `Frontend:Origins` appsettings array or with indexed environment keys such as `Frontend__Origins__0`; use HTTPS and secret bootstrap credentials.

## Deploy to Railway

The API includes a Dockerfile and Railway health check configuration. In Railway, create a project from the GitHub repository `RahulNaidu16/acxiomexam`, then set the service Root Directory to `/backend/AcxiomCRM.Api`. Railway builds the Dockerfile in that directory. Once deployed, generate a Railway public domain for the API.

Add a persistent Railway Volume mounted at `/data`, then configure these service variables in Railway (do not commit secrets):

| Variable | Value |
| --- | --- |
| `ConnectionStrings__Default` | `Data Source=/data/acxiomcrm.db` |
| `Frontend__Origins__0` | `https://acxiomexam.vercel.app` |
| `BootstrapAdmin__Email` | Your chosen admin email |
| `BootstrapAdmin__Password` | A unique password meeting the policy above |

Railway supplies `PORT` automatically. The API listens on that port and serves `GET /health` for deployment health checks. After deployment, test `https://<your-railway-domain>/health`. The current Vercel frontend remains a localStorage demo; deployment of this API alone does not connect the frontend to it.

## Authentication and CSRF

Authentication uses an HttpOnly cookie. Before every state-changing request, call `GET /api/auth/csrf`, then send the returned `token` as `X-CSRF-TOKEN`; retain cookies with browser `credentials: "include"` or `curl -c`/`-b`. Login and registration are also CSRF-protected. Auth endpoints are limited to 10 requests per minute per client IP. Identity locks accounts after three failed attempts for five minutes.

Typical sequence:

1. `GET /api/auth/csrf` and retain the response cookie and token.
2. `POST /api/auth/login` with `{ "email": "...", "password": "..." }`, the token header, and the cookie.
3. Fetch protected resources with the authentication cookie.
4. Refresh the CSRF token when needed and send it on every `POST`, `PUT`, `PATCH`, or `DELETE` request.
5. `POST /api/auth/logout` to end the session.

## API routes

- `GET /api/auth/csrf`, `POST /api/auth/register`, `POST /api/auth/login`, `POST /api/auth/logout`, `GET /api/auth/me`
- Customers: `GET|POST /api/customers`, `GET|PUT|DELETE /api/customers/{id}`. Search/filter: `?search=...&status=...`.
- Leads: `GET|POST /api/leads`, `GET|PUT|DELETE /api/leads/{id}`, `POST /api/leads/{id}/convert`.
- Opportunities: `GET|POST /api/opportunities`, `GET|PUT|DELETE /api/opportunities/{id}`.
- Follow-ups: `GET|POST /api/followups`, `GET|PUT /api/followups/{id}`, `PATCH /api/followups/{id}/status`.
- Activities: `GET|POST /api/activities`, `GET|PUT|DELETE /api/activities/{id}`.
- Dashboard/reporting: `GET /api/dashboard`, `GET /api/reports/pipeline`.
- Administration: `GET|POST /api/users`, `PUT /api/users/{id}`, `POST /api/users/{id}/unlock` (Admin only); `GET /api/audit` (Admin only, paginated and filterable).

Protected CRUD responses use DTOs. SalesExecutive data is restricted to the signed-in user's assigned records; Admin and Manager can see organization-wide CRM data. Customer email and phone are unique. Opportunity and follow-up business rules, ownership checks, model validation, and audit logging are enforced server-side. Customer deletion deactivates the record; deleting leads/opportunities/activities is restricted to non-sales roles.

## Current frontend status

The existing `js/app.js` is still the localStorage demo and has not been switched to these endpoints. The API can be exercised independently; replacing the demo persistence with cookie/CSRF-aware fetch calls is a separate frontend integration step.
