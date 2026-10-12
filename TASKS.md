# TASKS

Rule: Opus analyzes and details each item before any execution. Suggested model in parentheses: O = Opus, S = Sonnet, H = Haiku.

## In progress

None.

## Next

### Phase 0: repository base

- [ ] 0.1 (O) Review and confirm this phase plan and the split by feature folder
- [ ] 0.2 (S) Initialize Git repository, .gitignore, .dockerignore and .editorconfig (JetBrains Rider style)
- [ ] 0.3 (S) Create solution src/Unayka.slnx with Unayka.Core, Unayka.Web, Unayka.Core.Test and Unayka.Web.Test
- [ ] 0.4 (S) Create Directory.Build.props (nullable, analyzers, TreatWarningsAsErrors)
- [ ] 0.5 (H) Generate initial MAP.md from the created structure

### Phase 1: foundation and public pages

- [ ] 1.1 (S) AppEnvConfig in Unayka.Core (typed properties, TryGet, IsProduction, UsePostgres, UseS3, Validate)
- [ ] 1.2 (S) .env.example and DotNetEnv loading on the first line of Program.cs
- [ ] 1.3 (S) Tailwind v4: globals.css (@import, @source, @theme, @layer), app.css generation
- [ ] 1.4 (S) UnaykaComponentBase, UnaykaLayoutBase, helpers (Text, ToUrl, ToUrlTarget, FormatMoney, FormatDate, FormatPhone) and _Imports.razor
- [ ] 1.5 (S) SeoComponent (title, description, canonical, Open Graph, structured data)
- [ ] 1.6 (S) Static public pages: home, services, about, location, contact
- [ ] 1.7 (S) /health endpoint (active server, database and storage)
- [ ] 1.8 (S) Multi stage Dockerfile (Node/Tailwind, .NET 10 SDK, ASP.NET 10 runtime), port 10000
- [ ] 1.9 (S) Tests: AppEnvConfig, ToUrl safety, in memory server, /health 200, SEO title, no loose text in .razor rule

### Phase 2: data, catalog, images and audit

- [ ] 2.1 (O) Design the data model (provider agnostic payments, amounts in minor units)
- [ ] 2.2 (S) DbContext with Npgsql or SQLite selection and migrations
- [ ] 2.3 (S) Catalog: categories, products, media (Prepared, Package, Elements) with order, nutrition table
- [ ] 2.4 (S) Image storage: S3 with local fallback served by a controlled endpoint, contract test
- [ ] 2.5 (S) Immutable, insert only audit log
- [ ] 2.6 (S) Public catalog and listing pages

### Phase 3: product, cart, coupons and checkout

- [ ] 3.1 (S) Scrolling product page (prepared image, package, elements, table), fixed order button, Product structured data
- [ ] 3.2 (S) Server side cart with side drawer and total recalculation
- [ ] 3.3 (O) Design the coupon engine (pure function in Core)
- [ ] 3.4 (S) Coupon engine and tests for every rule combination
- [ ] 3.5 (S) Coupon administration (create, change, pause, counter, revenue) with audit
- [ ] 3.6 (S) Single page checkout (contact, pickup or delivery, payment), zero total order without gateway

### Phase 4: authentication and security

- [ ] 4.1 (S) Google login (OAuth)
- [ ] 4.2 (S) Phone login +258 with alphanumeric code, hash, expiry, limits, IOtpSender with development implementation
- [ ] 4.3 (S) Roles Customer, Cashier, Manager, Administrator and ADMIN_BOOTSTRAP_EMAIL
- [ ] 4.4 (S) Login attempts with 90 day retention and temporary block
- [ ] 4.5 (S) Per policy rate limiting, antiforgery, cookies, HSTS, headers and CSP, persistent protection keys
- [ ] 4.6 (S) Tests: webhook rejects invalid signature, rate limiting returns 429

### Phase 5: PaySuite

- [ ] 5.1 (H) Read the official PaySuite documentation and record here what was confirmed
- [ ] 5.2 (O) Decide the design from that reading (webhook, polling, fees, payouts, test environment)
- [ ] 5.3 (S) IPaymentGateway, PaySuiteGateway and a fake implementation for contract tests
- [ ] 5.4 (S) Payment entity, raw events, states and idempotency
- [ ] 5.5 (S) Webhook with signature verification and polling BackgroundService
- [ ] 5.6 (S) Charges screen with filters, timeline and actions

### Phase 6: counter

- [ ] 6.1 (S) Charge mode (products or free amount, phone, live status, QR or link, cash or terminal)
- [ ] 6.2 (S) Cash shift (opening amount, closing with count and difference per method)
- [ ] 6.3 (S) Simple receipt by print or SMS
- [ ] 6.4 (S) Counter orders and preparation board (New, Preparing, Ready, Delivered)

### Phase 7: management

- [ ] 7.1 (S) Sales and products dashboard
- [ ] 7.2 (S) Finance by day and month, real or estimated fee, monthly CSV export
- [ ] 7.3 (S) Payouts to PAYOUT_PHONE (IPayoutGateway or manual record)
- [ ] 7.4 (S) Reconciliation
- [ ] 7.5 (S) Customers and recurrence
- [ ] 7.6 (S) Stock (alerts, movements, batch and expiry)
- [ ] 7.7 (S) Health and alerts

## On hold

- Loyalty card: later phase, after Phase 7
- Gift vouchers: later phase, after Phase 7
- Salon service bookings: later phase, after Phase 7
- Table orders via QR: later phase, after Phase 7
- Offline counter mode: later phase, after Phase 7
- WhatsApp notifications: later phase, after Phase 7

### Open decisions (blocking the listed items)

- SMS provider in Mozambique: undecided; blocks 4.2
- PaySuite real fees, payouts by API and test environment: resolved by 5.1
- Tax rules (VAT, receipts, NUIT): undefined; blocks 6.3
- Stock reservation during checkout and expiry period: undefined; blocks 3.6
- Delivery or store pickup only: undefined; blocks 3.6
- Coffee also sold as packaged beans: undefined; blocks 2.3
- Counter hardware (receipt printer, barcode reader): undefined; blocks 6.3
- Hosting for PostgreSQL, S3 bucket and backups: undefined; blocks deploy
- Languages besides Portuguese: undefined
- Refund policy and personal data retention: undefined; blocks 5.6

## Done

- [x] Base context files created (CLAUDE.md, TASKS.md, MAP.md, README.md, .gitignore, state.json, subagents)
- [x] Rules and context files written in English
- [x] Rules added: .claude ignored and reviewed by the primary agent, small commits per task, no AI mention, no push unless asked
