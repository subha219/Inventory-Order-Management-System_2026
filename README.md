# Inventra

**Inventra** is a warehouse, inventory and order management system for small and mid-size businesses. It covers the full flow from purchasing and receiving, through storage, picking and shipping, to sales, returns, stock counts and reporting, with a separate dashboard and menu for every job role.

It is built on **ASP.NET Core (.NET 10)** with a Clean Architecture back end and a Razor Pages + Vue 3 front end that needs no JavaScript build step.

> Inventra is based on the open-source *Warehouse Inventory Management System* by [Indotalent](https://indotalent.com) and extends it. See [License](#license).

---

## Contents

1. [Highlights](#highlights)
2. [Role-based dashboards](#role-based-dashboards)
3. [Functional features](#functional-features)
4. [Technical overview](#technical-overview)
5. [Project structure](#project-structure)
6. [Getting started](#getting-started)
7. [Configuration](#configuration)
8. [Users, roles and security](#users-roles-and-security)
9. [Email settings](#email-settings)
10. [Database scripts](#database-scripts)
11. [Docker](#docker)
12. [Deploying to IIS](#deploying-to-iis)
13. [Preparing a new client](#preparing-a-new-client)
14. [Acknowledgments](#acknowledgments)
15. [License](#license)

---

## Highlights

- **Complete stock lifecycle**: purchase order, goods receive, put-away, storage, pick, pack, ship, return, count, adjust, scrap.
- **Job-role dashboards**: Administrator, Manager, Finance, Warehouse Operator, Warehouse Supervisor, Inventory Controller, Quality / Returns Inspector, Buyer, Sales, Customer Service and Auditor each land on a dashboard built for their work.
- **Warehouse operations**: bin locations, wave picking, packing and shipping, dock scheduling, tasks and labour, stock holds, returns inspection, replenishment.
- **Traceability**: batches and expiry, serial numbers, recall trace, full audit log.
- **Planning**: reorder suggestions, demand forecast, ABC and aging analysis, supplier scorecard, landed cost and costing.
- **Automation**: rules that watch stock and raise alerts, scheduled reports by e-mail, daily low-stock mail, partner portals, webhooks and API keys.
- **Administration**: users, job roles, per-module permissions, approvals, e-mail (SMTP) settings editable by the administrator, role presets with test logins.
- **Installable and responsive**: works on desktop and phone, light and dark theme, optional install as an app (PWA), barcode scanning with a camera or USB scanner.

---

## Role-based dashboards

Every user signs in to the dashboard of their job. Roles are stored in the `AspNetRoles` table as real job roles; at sign-in each job role is expanded into the menu areas it needs.

| Job role | Dashboard focus |
|---|---|
| **Administrator** | Business analytics (revenue, purchases, stock, trends) with quick buttons for users, roles, permissions, approvals, audit log and e-mail settings. A system overview (people, approvals, changes) is available at `/Dashboards/AdminDashboard`. |
| **Manager** | Revenue and purchases, stock value, deliveries and orders, low stock, expiring batches, warehouse and customer breakdowns. |
| **Finance / Accountant** | Revenue, purchases, average order value, largest sales and purchase orders, top customers and vendors, status of orders. |
| **Warehouse Operator** | My tasks, orders to pick or ship, pick waves, packing, today's dock schedule, receipts and shipments. |
| **Warehouse Supervisor** | Team tasks and overdue work, pack-to-ship, dock on-time rate, stock counts, holds. |
| **Inventory Controller** | Stock value, reorder points, expiring batches, holds, counts, stock by warehouse. |
| **Quality / Returns Inspector** | Holds by reason, expiring batches, returns to inspect, return rate, scrapped units. |
| **Buyer / Purchasing** | Items to reorder, draft and open purchase orders, spend, top vendors, purchase status. |
| **Sales** | Revenue, orders, average order value, drafts to confirm, orders waiting for delivery, top customers. |
| **Customer Service** | Open orders, late deliveries, order cycle time, return rate. |
| **Auditor / Read-only** | Changes and deletions, stock count differences, stock value. Read-only: the server refuses any change. |

Dashboards show headline numbers, a "what needs doing" list, and trends, and refresh on demand.

---

## Functional features

### Sales
Customer groups, categories, customers and contacts; sales orders with taxes; backorders; sales returns; sales reports with a summary strip; customer-facing partner portals.

### Purchasing
Vendor groups, categories, vendors and contacts; purchase orders; purchase returns; reorder suggestions that create draft purchase orders; supplier portals; supplier scorecard; purchase reports.

### Inventory and products
Units of measure, product groups, products with reorder points; barcode and QR **scan and label** printing; **batches and expiry** (first-expired-first-out); **serial numbers**; **kits and assembly** (bill of materials); stock availability; **recall trace**; bulk **data import**; demand forecast; **Ask Inventory** (plain-English questions about low stock, top sellers, slow movers); costing and landed cost; ABC and aging; inventory insights.

### Warehouse operations
Warehouses and **storage locations** (zones, bins); receiving and put-away; pick lists and **wave picking**; packing and shipping; **dock schedule**; tasks and labour tracking; **stock holds** (quality, damaged, recall); returns inspection; bin moves and replenishment rules; operations KPIs.

### Inventory documents
Delivery orders, goods receive, transfer out and transfer in, positive and negative adjustments, scrapping, stock counts and cycle counts. Documents can be printed as PDF.

### Reports
Transaction report, stock report, movement report (pivot table), sales report, purchase report. Report grids support filtering, grouping, search and Excel export.

### Automation and integration
Automation rules (stock and expiry watchers), scheduled reports e-mailed on a timetable, daily low-stock e-mail, in-app notifications, webhook endpoints with signed requests, integration API keys, partner portal links.

### Administration
Users, roles, job-role presets, per-module and per-action permissions, approval workflow, audit log, company settings, taxes, number sequences, e-mail settings, to-do lists, profile.

---

## Technical overview

**Back end**
- ASP.NET Core headless API, Clean Architecture, CQRS with MediatR, Repository / Unit of Work.
- Entity Framework Core with SQL Server.
- ASP.NET Identity with JWT authentication; role-based authorization plus a permission filter.
- FluentValidation, AutoMapper, Serilog.
- MailKit / MimeKit for e-mail.
- Global rate limiting on API calls.

**Front end**
- ASP.NET Core Razor Pages with Vue 3 (loaded directly, no build system).
- Syncfusion UI components (Community Edition), Axios, AdminLTE shell with a modern theme.
- Hand-written SVG charts and a shared dashboard engine (`persona-dashboard.js`).

---

## Project structure

```
Inventra.sln
├── Core
│   ├── Domain                  entities and base types
│   └── Application             features (commands, queries, handlers), services, DTOs
├── Infrastructure
│   └── Infrastructure          EF Core, Identity, e-mail, seeders, navigation menu
├── Presentation
│   └── InventraUI              API controllers (BackEnd), Razor pages (FrontEnd), wwwroot
├── Scripts                     SQL scripts (roles, data clean-up)
├── Dockerfile
└── docker-compose.yml
```

---

## Getting started

**Requirements**: .NET 10 SDK, SQL Server (LocalDB, Express or full), Visual Studio 2022 or later (or the `dotnet` CLI).

1. Clone the repository and open `Inventra.sln`.
2. Set the connection string (see [Configuration](#configuration)).
3. Set the secrets: a JWT key and the default admin password (user secrets or environment variables).
4. Build and run the `InventraUI` project, or from a terminal:
   ```
   dotnet run --project Presentation/InventraUI
   ```
5. Open `http://localhost:5249` and sign in as the default admin.

The database is created automatically on the first start, and roles, the default admin, the company record and system warehouses are seeded.

---

## Configuration

Settings live in `Presentation/InventraUI/appsettings.json`. Keep secrets out of source control: use `dotnet user-secrets` or environment variables (double underscore for nesting).

| Setting | Purpose |
|---|---|
| `ConnectionStrings:DefaultConnection` | SQL Server connection string. |
| `Jwt:Key` | **Required**, at least 32 characters. Signing key for tokens. |
| `AspNetIdentity:DefaultAdmin:Email` / `Password` | Default administrator login created on first start (`admin@root.com`). |
| `SmtpSettings` | Fallback mail settings. Administrators normally set these in the app, see [Email settings](#email-settings). |
| `LowStockAlerts` | Daily low-stock e-mail: `Enabled`, `Recipients`, `SendAtHourUtc`. |
| `Reports:` | Time zone offset for scheduled reports. |
| `RateLimiting:GlobalPermitPerMinute` | API requests per client per minute (pages and static files are not limited). |
| `IsDemoVersion` | `true` fills an empty database with demo customers, products and documents. Keep `false` for real use. |
| `CreateDemoLogins` | `true` creates one test login per job role on every start. Set `false` for a real client. |

Example (PowerShell):

```
dotnet user-secrets init --project Presentation/InventraUI
dotnet user-secrets set "Jwt:Key" "<at least 32 random characters>" --project Presentation/InventraUI
dotnet user-secrets set "AspNetIdentity:DefaultAdmin:Password" "<strong password>" --project Presentation/InventraUI
```

---

## Users, roles and security

- **Administrator** and ten **job roles** are stored in `AspNetRoles`. Assign them under *Users > Change Roles*, or use *Role Presets* to apply a job role to a user or create a test login for it.
- At sign-in a job role is expanded into the menu areas it needs. The sidebar and every page are checked against these roles; the server enforces them on every API call.
- **Permissions** page: switch create, edit and delete on or off per module.
- **Approvals**: adjustments and similar documents can require a decision before they take effect.
- **Auditor / Read-only** can view everything including the audit log, but every change request is refused by the server.
- **Audit log** records who created, changed or deleted what and when.
- Passwords are hashed by ASP.NET Identity. API calls are rate limited. Security headers are added to every response.

---

## Email settings

Administrators open **Users > Email Settings** to enter the SMTP server, port, user name, password, From address and connection security, and can send a test message from the same page.

- Changes apply immediately, no restart.
- The password is stored encrypted in `App_Data/smtp-settings.json` with its key beside it. Never commit `App_Data` (it is in `.gitignore`).
- Until settings are saved there, the `SmtpSettings` section of `appsettings.json` is used.
- All system mail uses these settings: scheduled reports, automation alerts, the daily low-stock mail, reorder alerts.

---

## Database scripts

In `Scripts/`:

| Script | Purpose |
|---|---|
| `AddJobRoles.sql` | Adds the ten job roles to `AspNetRoles` (safe to run more than once). |
| `CleanRolesToJobRoles.sql` | Keeps only Administrator and the job roles, removing old menu-area roles. Read the preview first. |
| `ClearDataForNewClient.sql` | Clears all business data and extra users for a new client set-up. Ends with `ROLLBACK`; change to `COMMIT` after reading the preview. Back up first. |
| `002_AddProductBatch.sql` | Adds the product batch table to an existing database. |

---

## Docker

```
docker compose up --build
```

The compose file starts SQL Server and the application. Its passwords and keys are **development placeholders**: change them before any real use. See `README_DOCKER_DEPLOYMENT.md` for details.

---

## Deploying to IIS

1. Publish the `InventraUI` project to a folder.
2. Copy the output to the IIS site folder (install the ASP.NET Core Hosting Bundle for .NET 10).
3. Set the connection string, `Jwt__Key` and the admin password as environment variables or in a protected configuration.
4. Make sure the application pool identity can write to the `App_Data` and `wwwroot/app_data` folders.

See Microsoft's guide: https://learn.microsoft.com/en-us/aspnet/core/host-and-deploy/iis/

---

## Preparing a new client

1. Back up, stop the app, run `Scripts/ClearDataForNewClient.sql` (preview first, then `COMMIT`).
2. Set `IsDemoVersion` to `false` and `CreateDemoLogins` to `false`.
3. Start the app, sign in as admin, change the admin password.
4. Fill in *Settings > My Company*, taxes, units of measure, warehouses and storage locations.
5. Enter the e-mail settings and send a test.
6. Create users and give each a job role.

---

## Acknowledgments

- [Indotalent](https://indotalent.com): original *Warehouse Inventory Management System*.
- [Syncfusion Community Edition](https://www.syncfusion.com/products/communitylicense): UI components.
- [AdminLTE](https://github.com/ColorlibHQ/AdminLTE) (MIT): admin template.
- Vue.js, Axios, MailKit, MediatR, FluentValidation, AutoMapper, Serilog.

---

## License

This work is licensed under a [Creative Commons Attribution 4.0 International License (CC BY 4.0)](http://creativecommons.org/licenses/by/4.0/).

The original author's terms are kept in `LICENSE.txt` and apply to this project. In particular, the author's credit link in the page footer must be retained unless a credit-removal license has been purchased from the original author.
