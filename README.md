# Unayka

Coffee and cosmetics shop with a modern salon atmosphere. Works as an online store and as a point of sale at the counter, in the same system, with the same orders, payments and audit. Market: Mozambique. Currency: metical (MZN). UI in Mozambican Portuguese. Payments by mobile wallet through PaySuite (paysuite.co.mz).

Working rules and code standard are in CLAUDE.md. Tasks are in TASKS.md and the file map is in MAP.md.

## Stack

.NET 10, Blazor Web App, Entity Framework Core, Tailwind CSS v4 (CLI), DotNetEnv, Npgsql for PostgreSQL in production and SQLite as local fallback. Images in S3 compatible storage with local folder fallback. Multi stage Docker, port 10000.

No appsettings.json, no launchSettings.json and no infrastructure project.

## Structure

```
Unayka/
  Dockerfile
  .dockerignore
  .gitignore
  README.md
  CLAUDE.md
  MAP.md
  TASKS.md
  PLAN.md                      (ignored by Git)
  database.sqlite              (SQLite fallback, created at runtime, not ignored by Git)
  .claude/
    agents/                    (opus, sonnet, haiku; whole .claude folder ignored by Git)
    local/state.json
  src/
    Unayka.slnx
    Unayka.Core/               (domain, rules, interfaces and implementations)
    Unayka.Web/                (Blazor, minimal endpoints, Program.cs, Tailwind)
      .env.example
      .env                     (ignored by Git)
      wwwroot/styles/globals.css
      wwwroot/styles/app.css   (generated, ignored by Git)
    Unayka.Core.Test/
    Unayka.Web.Test/
```

Core is organized by feature: Catalog, Orders, Cart, Coupons, Payments, Customers, Auth, Storage, Audit, Reports, Cash and Data.

## Environment configuration

All configuration comes from environment variables. The file src/Unayka.Web/.env is loaded by DotNetEnv on the first line of Program.cs without overriding variables already set. The static class AppEnvConfig, in Unayka.Core, is the single read point. Validate() runs at startup and, in production, fails with the list of missing required variables.

The full, commented list of variables is in src/Unayka.Web/.env.example. Variable names are complete and grouped by prefix: APP_, DATABASE_, STORAGE_, PAYMENT_, AUTH_ and RATE_LIMIT_. Standard ASP.NET variables (ASPNETCORE_URLS, ASPNETCORE_ENVIRONMENT) keep their framework names.

Database: PostgreSQL (Npgsql) is used when DATABASE_POSTGRESQL_CONNECTION_HOST and DATABASE_POSTGRESQL_CONNECTION_DATABASE are set, otherwise SQLite. The SQLite file is database.sqlite at the repository root by default, works without any .env, can be overridden with DATABASE_SQLITE_FILE_PATH and is not ignored by Git. Storage: S3 is used when endpoint, bucket and keys are set and it answers at startup, otherwise STORAGE_LOCAL_UPLOADS_PATH (default data/uploads), outside wwwroot, served by a controlled endpoint.

## Development

Prerequisites: .NET 10 SDK, Node.js and the global Tailwind CLI.

```
npm install -g tailwindcss @tailwindcss/cli
```

If `@import "tailwindcss"` does not resolve, set NODE_PATH to the output of `npm root -g`.

```
cp src/Unayka.Web/.env.example src/Unayka.Web/.env
cd src/Unayka.Web
tailwindcss -i ./wwwroot/styles/globals.css -o ./wwwroot/styles/app.css --watch
dotnet watch run
```

Production styles build:

```
tailwindcss -i ./wwwroot/styles/globals.css -o ./wwwroot/styles/app.css --minify
```

Development login (never in production): on first run in a non production environment the database is created and seeded with the catalog, three coupons, demo customers and demo orders. Staff accounts are admin@unayka.co.mz (Administrator), gerente@unayka.co.mz (Manager) and caixa@unayka.co.mz (Cashier). Their password is the value of AUTH_SEED_STAFF_PASSWORD, or "unayka2026" when the variable is empty and the environment is not Production. In Production no staff is seeded unless AUTH_SEED_STAFF_PASSWORD is set. Customers sign in at /conta/entrar with a phone code; in Development the code is shown on the page as "Código de teste". The demo customer phone is 841234567.

Tests:

```
dotnet test src/Unayka.slnx
```

## Docker

Three stages: Node with Tailwind, .NET 10 SDK and ASP.NET 10 runtime. Listens on port 10000.

```
docker build -t unayka .
docker run --env-file src/Unayka.Web/.env -p 10000:10000 unayka
```

## Features

Public store: home, services, about, location, contact, catalog and product, all with static rendering and SEO. The product page is a scrolling narrative (prepared product, package, elements, nutrition table or composition) with an always visible order button.

Cart and checkout: side drawer, coupon, single page checkout with contact, pickup or delivery and mobile wallet payment, live status by webhook with polling fallback, retry with another number. Server side cart, totals always recalculated on the server.

Coupons: percentage or fixed value, subtotal condition, scope by name or category with patterns, date and usage limits, stacking, status and audit. Pure engine in Core.

Payments: PaySuiteGateway behind IPaymentGateway, webhook with signature and idempotency, polling in a BackgroundService, provider agnostic model.

Authentication: Google and phone +258 with SMS code. Roles Customer, Cashier, Manager and Administrator. Rate limiting and immutable audit.

Administration: charge mode and cash shift, charges, reconciliation, login attempts, finance and payouts, sales and products, customers, orders and preparation, stock, health and alerts.

## Phases

1. Solution, AppEnvConfig, .env, Tailwind, UnaykaComponentBase, SEO, public pages, health and Dockerfile.
2. Data model, SQLite and PostgreSQL, catalog, products with media and nutrition table, images and audit.
3. Product page, cart, coupons and checkout.
4. Google and phone authentication, login attempts, rate limiting and roles.
5. PaySuite, webhook, polling and charges screen.
6. Charge mode, cash shift and counter orders.
7. Dashboard, finance, payouts, reconciliation, stock and alerts.

Phase detail is in TASKS.md.

## Open decisions

SMS provider in Mozambique. PaySuite real fees, payouts and test environment. Tax rules (VAT, receipts, NUIT). Stock reservation during checkout and expiry. Delivery or pickup only. Coffee also as packaged beans. Counter hardware. Hosting for PostgreSQL, S3 and backups. Languages besides Portuguese. Refund policy and personal data retention.
