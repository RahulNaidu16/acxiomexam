# AcxiomCRM Frontend
Open `index.html` in a browser (internet needed for Bootstrap/Chart.js CDN) — or run `python3 -m http.server`.
Demo logins: admin@acxiom.com / Admin@123, manager@acxiom.com / Manager@123, sales@acxiom.com / Sales@123.
Covers: login/register/logout, 3-attempt lockout, password policy, role-scoped data, dashboard KPIs + Chart.js charts,
Customers/Leads/Opportunities/Follow-Ups/Activities CRUD with search + pagination, lead status workflow & conversion,
client-side validation (required/email/phone/length/date/number) and business rules, user admin, audit log.
Data is stored in localStorage (demo). An ASP.NET Core 8 API backend is available in [backend/README.md](backend/README.md); it includes Identity authentication, role-scoped CRM APIs, server-side validation, audit logging, and SQLite persistence. The frontend still runs independently on localStorage and has not yet been wired to the API.
