# TASKS

Only work still to do. A finished item is removed (Git history keeps it). Opus analyzes and details each item before any execution. Suggested model in parentheses: O = Opus, S = Sonnet, H = Haiku.

## In progress

- [ ] P13 (H) Responsive check in Chrome at 375, 768 and 1280 widths on home, loja, product, cart, checkout, contacto and admin product form (desktop already checked; the browser window could not be resized).

## Next

### L. Looping product rails (reported 2026-10-13)

- [ ] L6 (H) Check home and loja at 375, 768 and 1280 widths: loops, static rows below the threshold, Ver mais links (Chrome extension disconnected during the first check).

### R. Data only from the database, leaner Web, real products (approved, executing)

Notes from the user (2026-10-12):
- Images are set by URL. Upload is shown as an option but marked temporarily unavailable.
- No mock classes anywhere. Whatever is not configured fails like production (for example the payment gateway returns a gateway error when PaySuite keys are missing).
- Payment configuration (provider, methods, labels, icons, keys still empty) lives in the database, even if incomplete. Secrets stay in environment variables.
- Each payment method (M-Pesa, e-Mola, card) sits behind one interface (IPaymentMethod) so a method could use another provider; in practice all use PaySuite.
- Implement PaySuite from the official documentation (merges 5.1 and 5.3).

- [ ] R6 (O) Product images: 15 products with real photos by URL (Wikimedia Commons and Openverse, CC licenses, credit stored per image). Still missing: package photos for teas, applied photos for coconut oil, cosmetics beyond body care, book covers that match each title. The product page must show the credit line.
- [ ] R8 (S) Buttons with the pressed color equal to the text color: utilities now set explicit active colors and no static case was found; waiting for the user to name the exact buttons if it still happens.

### V. Visual polish and standardized product images (reported 2026-10-15)

- [ ] V1 (S) Google sign in button in Login.razor: generic classic yellow/orange fill (user choice), via the --color-brand-google* tokens in globals.css.
- [ ] V7 (S) Footer payment note: the card option (PaymentOptions id 4) has no image and falls back to bi-credit-card. Show the Visa and Mastercard logos by URL (Wikimedia Commons), supporting more than one logo per option if needed.
- [ ] V2 (S) ProductRail.razor and carousel.js: replace the arrow icons with a scroll hint everyone understands (Bootstrap Icons hand-index or mouse with a short swipe/scroll motion), keeping the arrows clickable on desktop.
- [ ] V3 (S) StoreProductCard.razor and ProductPage.razor: price in a highlight token (new --color-price), discount badge and old price in their own token, larger font for price and discount. Only globals.css tokens, then compile app.css.
- [ ] V4 (H) Image research: find royalty free photos with an open license (Unsplash, Pexels, Wikimedia Commons, Openverse) in the reference styles, matching type of product, not the exact brand: teas as a packaged pouch/box on a clean light background; e-books as a 3D cover mockup; cosmetics as a product shot on a light background. Quantities: 25 teas, 15 cosmetics, 3 e-books, 1 printed book. Record URL, license and credit per image. Google thumbnail links from the user are style references only, not to be reused (copyright).
- Decided: images are always URLs from the licensed sources, never downloaded or uploaded.
- [ ] V6 (S) Replace the non standardized product images in database.sqlite with the V4 set (same aspect ratio, light background), keeping credits; covers R6.

### Payments (PaySuite)

Confirmed from https://paysuite.tech/docs (2026-10-12): base https://paysuite.tech/api/v1, Bearer token; POST /payments with amount (MZN, decimal string), reference (unique, max 50), description, method (mpesa, emola, credit_card; optional), return_url, webhook_url; response data.id and data.checkout_url (hosted page). GET /payments/{id} returns status (pending, paid) and transaction. Webhooks payment.success and payment.failed with data.id, X-Signature = HMAC SHA256 of the raw body with the webhook secret. Refunds POST /refunds. mKesh is listed but not accepted by the method field, so it is counter only.

- [ ] 5.2 (O) Decide fees, payouts and the test environment (PaySuite sandbox keys).
- [ ] 5.5 (S) Polling BackgroundService for pending payments and tests for the webhook endpoint (valid signature applies, invalid returns 401).
- [ ] 5.6 (S) Charges screen with filters, timeline and actions (refunds once the policy is decided).

### Sign in and security

- [ ] 4.1 (S) Configure real Google and Facebook OAuth keys (without keys the button shows an unavailable message).
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
