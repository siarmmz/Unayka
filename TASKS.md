# TASKS

Rule: Opus analyzes and details each item before any execution. Suggested model in parentheses: O = Opus, S = Sonnet, H = Haiku.

## In progress

### Sprint L: working store with mocks (requested 2026-10-12, execute directly)

Goal: a complete, lively e-commerce running locally on port 5000 with real data flow (SQLite, seeded catalog with images, accounts, cart, checkout, mock payment, administration and counter). External providers (PaySuite, SMS, Google) are mocked behind interfaces until their open decisions are resolved.

Wave 1 (one worktree, branch sprint-l/foundation, Sonnet):
- [x] L1 Foundation: EF Core (SQLite/Npgsql), entities for catalog, users, OTP, carts, coupons, orders, payments, audit; seed with categories and about 24 products with real photos; cookie auth with phone code (dev sender) and staff email/password; roles; layout with Google Fonts, header (logo, nav, search, account, cart count), rich footer; feature service registration in Program.cs; extension points for waves 2 features.

Note for L1: the database is created with EnsureCreated at startup (no migrations yet); migrations are a later task before production. The CSP allows 'unsafe-inline' scripts because the Blazor import map is inline; replace with a nonce later.

Wave 2 (four worktrees in parallel, Sonnet, split by feature folder):
- [x] L2 Store front: home (hero, featured, categories, best sellers, story), catalog with filters and search, category, product narrative page (branch sprint-l/storefront)
- [x] L3 Cart and checkout: server cart, drawer, coupon engine, single page checkout, mock wallet payment with status, order confirmation, customer account and orders (branch sprint-l/checkout)
  - MockPaymentGateway (provider "mock") is registered by AddUnaykaOrders; PaySuiteGateway replaces it after task 5.1. The mock resolves on read: Paid after 5 seconds, phone ending in 0 fails after 3 seconds with "Saldo insuficiente".
  - Not done: cart drawer (the header cart link goes to /carrinho) and merging a guest cart into the user cart on sign in (the cart only receives the user id when the signed in user adds an item).
- [x] L4 Administration: dashboard, products, categories, orders, coupons, customers, audit (branch sprint-l/admin)
- [x] L5 Counter: point of sale charge mode, cash shift, preparation board (branch sprint-l/counter)

- [x] Wave 3 (primary agent): merged, build and tests green, purchase flow, admin dashboard, counter and preparation checked in Chrome.

### Sprint M: cleaner store and navigation (requested 2026-10-12, done)

- [x] M1 Payments: MKesh added; WalletMethods lists M-Pesa, e-Mola and mKesh for checkout, retry and counter.
- [x] M2 Header: Início, Loja, Sobre, Contacto. Services page removed.
- [x] M3 /loja is a showroom (featured marquee, Cafés, Cosméticos naturais e chás, each with "Ver mais"); the full filtered catalog moved to /loja/todos; /loja/cafes and /loja/cosmeticos stay as "Ver mais" targets, out of the header.
- [x] M4 Teas use the existing three image gallery (prepared, package, plants).
- [x] M5 Seed: tea category with three teas, moringa ointment, photos of Black women replace off brief photos.
- [x] M6 Tests green, browser check on port 5000.

### Sprint N: tea first, home vs store, lighter and simpler (requested 2026-10-12, done)

- [x] N1 (O) Catalog focus: the shop sells teas (many kinds, coffee is one of them) and natural cosmetics. CategoryKind Coffee becomes Tea; coffee categories become tea subcategories; rows become Destaques, Chás (coffee included), Cosméticos naturais; /loja/cafes becomes /loja/chas.
- [x] N2 (S) Home distinct from /loja: hero carousel (CSS only, 3 slides), the three product rows, how it works (order, pay by wallet, pick up or delivery), natural ingredients story, contact strip.
- [x] N3 (S) Lighter palette: raise espresso and clay tones a step, keep high text contrast; fewer dark blocks.
- [x] N4 (S) Simpler UX for first time online shoppers: larger tap targets, one clear action per card ("Adicionar"), plain labels, fewer filters by default.
- [x] N5 (H) Copy review to Mozambican Portuguese (close to pt-PT): telemóvel, pequeno-almoço, contacto, autocarro style; remove pt-BR forms across all UI texts.
- [x] N6 (O) Customer sign in: Google and Facebook only (both free, no cost per login). Phone code sign in is removed: SMS and WhatsApp OTP are charged per message (WhatsApp authentication templates are billed by Meta per message), so cost grows with users and can pass the 5 USD per month ceiling. No Telegram. The phone number stays only as a contact and wallet field at checkout. Staff keep email and password. Real Google and Facebook keys come from environment variables; mocked locally until configured.

- [x] N7 Positioning: the shop is a modern online store of natural teas and cosmetics, not a salon; every salon reference removed from UI, CLAUDE.md (version 4) and README.

### Sprint O: card payments and payment icons (requested 2026-10-12, done)

- [x] O1 Online checkout accepts Visa and Mastercard cards besides M-Pesa, e-Mola and mKesh (OnlineMethods); the wallet number is asked only for wallets; cash stays counter only.
- [x] O2 Payment icons from wwwroot/images/pay on checkout options and in the footer, sized 40px on white tiles.
- [x] O4 /loja redesigned to differ from the home (search, category chips and shortcuts, new arrivals); featured marquee contained and auto scrolling; home hero is a rotating carousel with bullets; Tailwind output regenerated and checked in Chrome.
- [ ] O3 (H) Before the real gateway: confirm in the PaySuite documentation whether cards are supported and how (hosted page redirect, fields, webhook), and record it here. Until then card payments go through the mock gateway.

Decision (2026-10-12): all images and content are provisional mocks (placeholder stock photos, nutrition table shown as an image placeholder) to be replaced by the final products later.

## Next

### Sprint P: managed catalog, locations, digital products and a livelier store (requested 2026-10-12, executing)

Analysis summary: the catalog is hard coded around CategoryKind (Tea, Cosmetic) and the UI has rows, routes and seeds per kind. The store must become data driven: everything a customer sees (sections, categories, tags, locations, contacts, maps) is registered in the administration and the pages only loop and render. The database uses EnsureCreated (no migrations), so every schema change in this sprint requires deleting the local database.sqlite; adding the first EF migration is part of P1.

Naming decisions (UI in Mozambican Portuguese, code in English):
- Section (UI "Secção"): top level of the catalog, for example Chás, Cosméticos, Livros. Chosen over "Partição" (technical) and "Departamento" (heavy for a small shop). Each section has slug, name, description, image, sort order, active flag and drives a home row and /loja/{section-slug}.
- Category (UI "Categoria"): belongs to one section, for example Chás: Café, Ervas, Chá preto. A product has exactly one category (and so one section).
- Tag (UI "Etiqueta"): free labels scoped to a section, for example Calmante, Sem cafeína, Hidratante; a product has zero or more tags; used as filter chips.
- Location (UI "Loja" or "Local"): a physical point, for example Vilanculos, Maputo Polana.
- Cart keeps the name "Carrinho": it is the standard term in Mozambique and Portugal for online shops (not a pt-BR form). The header shows a bag icon plus the visible label "Carrinho" and the item count; the card action becomes "Adicionar ao carrinho" (short "Adicionar" with the bag icon only where width is tight), and after adding, a confirmation with "Ver carrinho" and "Continuar a comprar".

Wave 1 (primary agent, sequential, before any worktree):
- [x] P0 (O) Rules: add to CLAUDE.md (version 5) the responsive rule (mobile, tablet and desktop always; one responsive element first; separate per breakpoint blocks rendering the same prepared data only as a last resort) and the color rule (every color comes from a theme token in globals.css, including brand colors of Google and Facebook; no Tailwind default palette, no white or black literals, no hex in markup). Update state.json.
- [ ] P1 (O then S) Data model, Core/Catalog and Core/Locations: entities Section, Category (SectionId, replaces Kind), Tag, ProductTag, Product gains Kind (Physical, Digital), IsNew (manual only), IsFeatured stays, Status (Active, Inactive, Archived) replacing IsActive; Location (name, city, address, phones, emails, WhatsApp, opening hours, latitude, longitude, map embed URL, sort order, active); ContactChannel per Location; LocationStock (ProductId, LocationId, Quantity) replacing Product.StockQuantity for physical products; DigitalAsset (ProductId, storage key, file name, size); DigitalDownloadGrant (OrderLineId, UserId, download count for audit only). OrderLine gets LocationId (pickup location per line; an order may span several locations). Remove CategoryKind and every reference (about 10 files). First EF migration replaces EnsureCreated. Seed rewritten: sections Chás and Cosméticos naturais plus one example Livros section with a digital e-book, two locations (Vilanculos, Maputo Polana) with contacts and coordinates.

Wave 2 (worktrees in parallel, Sonnet, split by feature folder):
- [ ] P2 (S) Administration of the catalog (Web/Components/Pages/Admin/Catalog, Core/Catalog services): CRUD for sections, categories, tags; product form with section, category, tags, kind, flags (Destaque, Novo), stock per location, digital file upload; product list subpages Todos, Novos, Destaques, Inativos, Arquivados. Deletion rule: a product, category, section, tag or location referenced by any order line is never deleted, only archived (the delete button becomes "Arquivar"); unreferenced items may be deleted. Every change audited.
- [ ] P3 (S) Archive visibility (Core/Catalog queries, Web product page and account orders): archived and inactive products return 404 on the public URL and never appear in listings, search or sitemap; the signed in customer who bought one still sees it in "As minhas compras" with name, image and price from the order snapshot (order lines must snapshot name, image and price at purchase time).
- [ ] P4 (S) Locations and contacts (Core/Locations, Web/Components/Pages/Admin/Locations, Contact page, footer): admin CRUD for locations, contact channels and map coordinates; /contacto lists contacts grouped by location ("Vilanculos: ...", "Maputo, Polana: ...") with an embedded map per location (OpenStreetMap embed, no paid key, allowed in the CSP frame-src); footer contact column comes from the same data.
- [ ] P5 (S) Stock and availability per location (Core/Catalog, Core/Orders, cart, checkout): product page and cards show availability per location; checkout groups lines by pickup location and lets the customer change the location per line; cart validates and recalculates on the server that every physical line has stock at its chosen location, with a clear message per line otherwise; stock is decremented at the paid location; counter sales use the cashier's location. Open decision "stock reservation during checkout" still applies: decrement on payment confirmation until decided.
- [ ] P6 (S) Digital products (Core/Orders/Downloads, Web account pages, storage): a digital line needs no location or delivery; after payment the order and "As minhas compras" show "Descarregar" links served by a controlled endpoint with a per user authorization (no expiry, no limit) (files never public in S3 or wwwroot); download count and audit; a cart with only digital items skips the delivery step.
- [ ] P7 (S) Data driven store front (Web/Components/Pages/Home, Store, Layout): home rows, /loja chips, header and footer links come from the active sections in sort order; /loja/{section} replaces /loja/chas and /loja/cosmeticos (old URLs redirect 301); tag chips filter inside a section; badges "Novo" and "Destaque" from the flags.
- [ ] P8 (S) Interactivity, one script module (Web/wwwroot/scripts/carousel.js, Home, ProductRowSection, product card): hero accepts touch and mouse swipe (pointer events, horizontal threshold, direction aware: left goes next, right goes previous) plus autoplay paused on interaction and on hover; every Destaques and product row scrolls automatically and accepts manual swipe and drag with scroll snap; product cards with several images cross fade between them with the same transition as the hero, driven by one shared timer so every card changes at the same moment; respects prefers-reduced-motion. Pages stay static SSR (progressive enhancement, no InteractiveServer).
- [ ] P9 (S) Sharing (product page and card): "Partilhar" button using the Web Share API on mobile, with fallback links for WhatsApp, Facebook and "Copiar link"; Open Graph image and description already come from SeoComponent.
- [ ] P10 (S) Header, cart and footer (Web/Components/Layout): bag icon plus "Carrinho" label and count badge, responsive (label hidden only below the smallest breakpoint, count always visible); add to cart confirmation; footer reorganized into Loja (sections from data), Ajuda (como comprar, entregas, pagamentos), Contactos (from locations), payment icons, social links; mobile accordion layout.
- [ ] P11 (S) Brand sign in buttons (Web/Components/Pages/Account): Google button follows Google's branding guidelines (official four color logo, light or dark variant, "Continuar com Google"); Facebook button in Facebook blue with white logo, "Continuar com Facebook"; both colors as theme tokens.
- [ ] P12 (S) Palette and tokens (globals.css plus a sweep of every .razor): introduce semantic tokens (accent, accent-strong, accent-soft, surface, surface-alt, ink, muted, border, success, warning, danger, brand-google, brand-facebook) and a more vivid palette (warmer terracotta accent, fresher leaf green, keeping AA contrast for text); replace every use of Tailwind default colors, white and black in markup with tokens so changing one variable changes the whole site. Test added: no .razor uses a non token color class.

Wave 3 (primary agent):
- [ ] P13 (H) Tests: section, category and tag CRUD, archive rule (no delete when referenced), archived 404 public and visible in orders, stock per location in checkout, digital download (buyer allowed, other user and anonymous refused), token only color rule. Responsive check in Chrome at 375, 768 and 1280 widths on home, loja, product, cart, checkout, contacto and admin product form.
- [ ] P14 (O) Merge, update MAP.md, regenerate Tailwind, build green, commits grouped by problem solved.

Owner decisions (2026-10-12):
- Physical products: pickup only at a location; delivery zones and fees come later.
- Digital downloads: unlimited for the buyer while signed in; links are still served through the controlled, per user endpoint (no public file URL), no expiry and no count limit.
- "Novo" flag: manual only, stays until unchecked in the administration (no NewUntil, no automatic rule).
- One order may mix locations: the order is split into one pickup group per location (OrderLine carries LocationId, each group has its own preparation and pickup status); the customer chooses the location per line, defaulting to the first location with stock.

### Phase 0: repository base

- [x] 0.1 (O) Review and confirm this phase plan and the split by feature folder
- [x] 0.2 (S) Initialize Git repository, .gitignore, .dockerignore and .editorconfig (JetBrains Rider style)
- [x] 0.3 (S) Create solution src/Unayka.slnx with Unayka.Core, Unayka.Web, Unayka.Core.Test and Unayka.Web.Test
- [x] 0.4 (S) Enable nullable, analyzers and TreatWarningsAsErrors in each project file (no Directory.Build.props)
- [x] 0.5 (H) Generate initial MAP.md from the created structure

### Phase 1: foundation and public pages

- [x] 1.1 (S) AppEnvConfig in Unayka.Core (typed properties, TryGet, IsProduction, UsePostgres, UseS3, Validate)
- [x] 1.2 (S) .env.example and DotNetEnv loading on the first line of Program.cs
- [x] 1.3 (S) Tailwind v4: globals.css (@import, @source, @theme, @layer), app.css generation
- [x] 1.4 (S) UnaykaComponentBase, UnaykaLayoutBase, helpers (Text, ToUrl, ToUrlTarget, FormatMoney, FormatDate, FormatPhone) and _Imports.razor
- [x] 1.5 (S) SeoComponent (title, description, canonical, Open Graph, structured data)
- [x] 1.6 (S) Static public pages: home, services, about, location, contact
- [x] 1.7 (S) /health endpoint (active server, database and storage)
- [x] 1.8 (S) Multi stage Dockerfile (Node/Tailwind, .NET 10 SDK, ASP.NET 10 runtime), port 10000. Image build not verified yet: Docker is not installed on this machine
- [x] 1.9 (S) Tests: AppEnvConfig, ToUrl safety, in memory server, /health 200, SEO title, no loose text in .razor rule

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
