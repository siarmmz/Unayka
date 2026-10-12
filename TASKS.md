# TASKS

Only work still to do. A finished item is removed (Git history keeps it). Opus analyzes and details each item before any execution. Suggested model in parentheses: O = Opus, S = Sonnet, H = Haiku.

## In progress

- [ ] P13 (H) Responsive check in Chrome at 375, 768 and 1280 widths on home, loja, product, cart, checkout, contacto and admin product form (desktop already checked; the browser window could not be resized).

## Next

### R. Data only from the database, leaner Web, real products (awaiting go ahead)

- [ ] R1 (O) Move every hardcoded content out of code: addresses, contacts, home texts, social networks, product data. Seed classes in Core/Data/Seed (CatalogSeed, LocationSeed, DemoOrdersSeed, SiteImages, DigitalSeedFiles) are removed; the committed database.sqlite becomes the only source of demo data. MockPaymentGateway stays only until 5.3.
- [ ] R2 (S) Social networks managed in the dashboard (name, URL, Bootstrap Icons class, order, active).
- [ ] R3 (S) Icons only from Bootstrap Icons via CDN link (`bi bi-*`); remove every inline svg and StoreIcon. Card logos (visa, mastercard) stay as images.
- [ ] R4 (O) Reduce the repository below 100 files: inline single use Web components into their pages (each page owns its @code and rendering), replace helper classes with records or tuples, merge small Core files per feature, drop per page base classes where possible.
- [ ] R5 (S) Remove the home "Ingredientes naturais" block and any home section that speaks about content not registered in the database.
- [ ] R6 (O) Recreate every product with real, professional images: teas and coffee (prepared cup, package, jar if applicable), cosmetics (product, ingredients, face or body area applied). No ambiguous items (bags and similar). Books: one cover image, angled so the spine thickness shows, front slightly rotated.
- [ ] R7 (S) Dashboard navigation is nested per section: tags, categories and similar are created inside the product or category being edited, not on global screens.
- [ ] R8 (S) Fix buttons whose pressed or active color equals their text color.

### Payments (PaySuite)

- [ ] 5.1 (H) Read the official PaySuite documentation and record here what was confirmed, including whether cards are supported and how (hosted page, fields, webhook). Until then wallets and cards go through the mock gateway.
- [ ] 5.2 (O) Decide the design from that reading (webhook, polling, fees, payouts, test environment).
- [ ] 5.3 (S) PaySuiteGateway behind IPaymentGateway, replacing the mock, with contract tests.
- [ ] 5.4 (S) Raw payment events, states and idempotency.
- [ ] 5.5 (S) Webhook with signature verification and polling BackgroundService; test that an invalid signature is rejected.
- [ ] 5.6 (S) Charges screen with filters, timeline and actions (refunds once the policy is decided).

### Sign in and security

- [ ] 4.1 (S) Real Google and Facebook OAuth keys from environment variables (mocked locally today).
- [ ] 4.4 (S) Login attempts with 90 day retention and temporary block.
- [ ] 4.5 (S) Review rate limiting policies (login, webhook, checkout), CSP with a nonce instead of 'unsafe-inline', persistent data protection keys; test that rate limiting returns 429.

### Data and storage

- [ ] D1 (S) PostgreSQL migrations (only SQLite migrations exist; Postgres still uses EnsureCreated).
- [ ] D2 (S) S3 storage with local fallback for product images and digital files (IDigitalFileStore is the seam).
- [ ] D3 (S) Counter sales use the cashier's location instead of the first active location.

### Counter and management

- [ ] 6.3 (S) Simple receipt by print or SMS.
- [ ] 7.2 (S) Finance by day and month, real or estimated fee, monthly CSV export.
- [ ] 7.3 (S) Payouts to PAYOUT_PHONE (IPayoutGateway or manual record).
- [ ] 7.4 (S) Reconciliation.
- [ ] 7.6 (S) Stock alerts, movements, batch and expiry per location.
- [ ] 7.7 (S) Health and alerts.

### Content

- [ ] C1 (H) Replace provisional images and texts with the final products.
- [ ] C2 (H) Real Facebook and Instagram links in the footer (placeholders today).

## On hold

- Delivery zones and fees per location: after pickup is live.
- Loyalty card, gift vouchers, table orders via QR, offline counter mode, WhatsApp notifications: after the management items.

### Open decisions (blocking the listed items)

- Tax rules (VAT, receipts, NUIT): blocks 6.3.
- Stock reservation during checkout: stock is decremented at placement until decided.
- Counter hardware (receipt printer, barcode reader): blocks 6.3.
- Hosting for PostgreSQL, S3 bucket and backups: blocks D1, D2 and deploy.
- Refund policy and personal data retention: blocks 5.6.
- Languages besides Portuguese: undefined.
