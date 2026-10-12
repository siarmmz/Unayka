# MAP

One line per file as "path - what it does", sorted by path. Check here before searching the disk. Update it whenever a file is added, removed, renamed or changes purpose.

.claude/agents/haiku.md - subagent for fast execution, research, documentation and tests (ignored by Git)
.claude/agents/opus.md - subagent that analyzes, decides architecture and splits tasks (ignored by Git)
.claude/agents/sonnet.md - subagent that writes the code (ignored by Git)
.claude/local/state.json - initialization state, rules version and skills (ignored by Git)
.dockerignore - files excluded from the Docker build context
.editorconfig - C# formatting and naming rules (JetBrains Rider style)
.gitignore - files ignored by Git (includes the whole .claude folder)
CLAUDE.md - project rules and Claude Code workflow (version 6)
Dockerfile - multi stage build (Tailwind styles, .NET publish, ASP.NET runtime) on port 10000
MAP.md - this file map, one line per file, updated whenever a file is added, removed, renamed or changes purpose
PLAN.md - context between sessions (ignored by Git)
README.md - product description, stack, configuration, development, phases and open decisions
TASKS.md - tasks by phase (In progress, Next, On hold, Done)
src/Unayka.Core.Test/Accounts/AccountServiceTests.cs - external sign in, account linking and staff sign in tests
src/Unayka.Core.Test/Accounts/PhoneNumberTests.cs - phone normalization tests
src/Unayka.Core.Test/Administration/AdminRulesTests.cs - status rules, dashboard, slug, money, coupon and segment tests
src/Unayka.Core.Test/Administration/AdminServicesTests.cs - order cancel, product save, coupon revenue, customers and low stock tests
src/Unayka.Core.Test/Administration/TestContextFactory.cs - IDbContextFactory over the in memory test database
src/Unayka.Core.Test/Audit/AuditLogTests.cs - audit insert only tests
src/Unayka.Core.Test/Carts/CartServiceTests.cs - cart totals, stock and coupon tests
src/Unayka.Core.Test/Catalog/CatalogQueryTests.cs - filtering, sorting, paging and detail tests
src/Unayka.Core.Test/Catalog/StockLevelsTests.cs - stock level thresholds tests
src/Unayka.Core.Test/Configuration/AppEnvConfigTests.cs - environment configuration reading and production validation tests
src/Unayka.Core.Test/Counter/CashMathTests.cs - cash math, money input, wallet rule and preparation flow tests
src/Unayka.Core.Test/Counter/CounterServicesTests.cs - sale registration, shift close and board advance tests
src/Unayka.Core.Test/Coupons/CouponEvaluatorTests.cs - coupon rule tests
src/Unayka.Core.Test/Data/ManualTimeProvider.cs - controllable clock for tests
src/Unayka.Core.Test/Data/SeedTests.cs - seed idempotency, catalog, staff and demo data tests
src/Unayka.Core.Test/Data/TestDatabase.cs - in memory SQLite context for tests
src/Unayka.Core.Test/Data/TestStock.cs - test helper to seed stock per location
src/Unayka.Core.Test/Locations/LocationDirectoryTests.cs - location directory and stock allocation tests
src/Unayka.Core.Test/Orders/CheckoutFixture.cs - seeded in memory services for checkout tests
src/Unayka.Core.Test/Orders/OrderPlacementTests.cs - placement integration tests on SQLite
src/Unayka.Core.Test/Payments/PaymentTests.cs - mock gateway and payment transition tests
src/Unayka.Core.Test/Presentation/FormattersTests.cs - money, date and phone formatting tests
src/Unayka.Core.Test/Presentation/SafeTextTests.cs - text escaping and inline tag allowlist tests
src/Unayka.Core.Test/Presentation/SafeUrlTests.cs - URL safety and link target tests
src/Unayka.Core.Test/SharedEnvironment.cs - serializes tests that change environment variables
src/Unayka.Core.Test/Unayka.Core.Test.csproj - Core test project
src/Unayka.Core/Accounts/AccountService.cs - external (Google, Facebook) customer sign in, staff password sign in, lockout, bootstrap administrator
src/Unayka.Core/Accounts/AccountServiceExtensions.cs - services.AddUnaykaAccounts()
src/Unayka.Core/Accounts/ExternalProviders.cs - Google and Facebook provider names, configured or locally mocked
src/Unayka.Core/Accounts/IAccountService.cs - sign in contract, outcomes, result and ExternalIdentity
src/Unayka.Core/Accounts/IpHash.cs - one way hash of the client address
src/Unayka.Core/Accounts/LoginAttempt.cs - sign in attempt entity (identifier and IP hash)
src/Unayka.Core/Accounts/PhoneNumber.cs - normalizes +258 phones to the nine digit national number
src/Unayka.Core/Accounts/UnaykaPolicies.cs - authorization policy names (Staff, Management, Administration)
src/Unayka.Core/Accounts/User.cs - user entity (customer or staff)
src/Unayka.Core/Accounts/UserRole.cs - Customer, Cashier, Manager, Administrator
src/Unayka.Core/Administration/AdminAuditService.cs - paged read only audit log
src/Unayka.Core/Administration/AdminCatalogModels.cs - product and category rows and edit forms
src/Unayka.Core/Administration/AdminCatalogService.cs - product and category queries and audited commands
src/Unayka.Core/Administration/AdminCommon.cs - paging, result, local day, money input, slug, search pattern and audit name helpers
src/Unayka.Core/Administration/AdminCouponsService.cs - coupon list with revenue, form conversions and audited commands
src/Unayka.Core/Administration/AdminCustomersService.cs - customers with totals, recurrence segment and detail
src/Unayka.Core/Administration/AdminDashboardService.cs - dashboard data query (figures, top products, latest orders, low stock)
src/Unayka.Core/Administration/AdministrationServiceExtensions.cs - services.AddUnaykaAdministration()
src/Unayka.Core/Administration/AdminOrdersService.cs - order list, detail and audited status changes with stock restore
src/Unayka.Core/Administration/AdminPaymentsService.cs - payment list with filters and totals per status
src/Unayka.Core/Administration/CatalogValidator.cs - product and category validation
src/Unayka.Core/Administration/DashboardCalculator.cs - pure dashboard figures from order samples
src/Unayka.Core/Administration/OrderStatusRules.cs - allowed order status transitions
src/Unayka.Core/Audit/AuditEntry.cs - insert only audit entry entity
src/Unayka.Core/Audit/AuditLog.cs - audit log implementation
src/Unayka.Core/Audit/IAuditLog.cs - audit log contract
src/Unayka.Core/Carts/Cart.cs - server cart entity
src/Unayka.Core/Carts/CartCalculator.cs - pure totals from lines and coupon
src/Unayka.Core/Carts/CartItem.cs - cart line entity
src/Unayka.Core/Carts/CartModels.cs - cart line and cart view records, change and coupon apply results
src/Unayka.Core/Carts/CartService.cs - cart service with stock limits and totals recalculated from current prices
src/Unayka.Core/Carts/CartServiceExtensions.cs - services.AddUnaykaCarts()
src/Unayka.Core/Carts/CartSummary.cs - default ICartSummary reading the cart table
src/Unayka.Core/Carts/ICartService.cs - server cart contract (add, quantity, remove, clear, coupon)
src/Unayka.Core/Carts/ICartSummary.cs - cart cookie name and ICartSummary (item count)
src/Unayka.Core/Catalog/CatalogEnums.cs - ProductKind, CatalogStatus and ProductMediaKind
src/Unayka.Core/Catalog/CatalogModels.cs - ProductSearch, ProductPage, CategorySummary, ProductDetail and related read models
src/Unayka.Core/Catalog/CatalogQuery.cs - EF implementation with filters, text search, sorting and paging of 12
src/Unayka.Core/Catalog/CatalogServiceExtensions.cs - services.AddUnaykaCatalog()
src/Unayka.Core/Catalog/Category.cs - category entity inside a section, with status and sort order
src/Unayka.Core/Catalog/DigitalAsset.cs - private downloadable file of a digital product (storage key, name, type, size)
src/Unayka.Core/Catalog/ICatalogQuery.cs - catalog read contract (featured, best sellers, new arrivals, categories, search, detail, related)
src/Unayka.Core/Catalog/Product.cs - product entity: prices in minor units, kind (physical or digital), status, featured and new flags, tags, sales count
src/Unayka.Core/Catalog/ProductCardModel.cs - card record (images, new flag, kind) and the EF projection FromProduct
src/Unayka.Core/Catalog/ProductFact.cs - product information row (nutrition table, composition)
src/Unayka.Core/Catalog/ProductMedia.cs - product image entity (Prepared, Package, Elements, Gallery)
src/Unayka.Core/Catalog/ProductStock.cs - total stock of a product across active locations, unlimited for digital
src/Unayka.Core/Catalog/ProductVisibility.cs - public visibility rule: product, category and section all active
src/Unayka.Core/Catalog/Section.cs - section entity (for example Chás, Cosméticos, Livros), top level of the catalog
src/Unayka.Core/Catalog/SectionSlugs.cs - seeded section slugs (chas, cosmeticos, livros)
src/Unayka.Core/Catalog/StockLevel.cs - stock state (in stock, low stock, out of stock) from quantity
src/Unayka.Core/Catalog/StoreUrls.cs - shared route constants and URL builders for store pages
src/Unayka.Core/Catalog/Tag.cs - tag entity scoped to a section, many to many with products
src/Unayka.Core/Configuration/AppEnvConfig.cs - single typed reader of environment variables with production validation
src/Unayka.Core/Counter/CashMath.cs - pure cash shift totals, expected cash, difference and change
src/Unayka.Core/Counter/CashShift.cs - cash shift entity
src/Unayka.Core/Counter/CounterPreparation.cs - board query and status advance with audit
src/Unayka.Core/Counter/CounterSales.cs - validates the ticket, creates paid counter order, payment, stock movement and audit
src/Unayka.Core/Counter/CounterServiceExtensions.cs - services.AddUnaykaCounter()
src/Unayka.Core/Counter/CounterShifts.cs - shift implementation with audit
src/Unayka.Core/Counter/CounterTimes.cs - Maputo time formatting, start of day and age in minutes
src/Unayka.Core/Counter/CounterWallet.cs - counter mock wallet provider names, delay and approval rule (phone ending in 0 fails)
src/Unayka.Core/Counter/ICounterPreparation.cs - preparation board contract and records
src/Unayka.Core/Counter/ICounterSales.cs - catalog and sale registration contract, requests, receipt and errors
src/Unayka.Core/Counter/ICounterShifts.cs - shift contract (open, summarize, close, history) and records
src/Unayka.Core/Counter/MoneyInput.cs - parses typed MZN amounts into minor units
src/Unayka.Core/Counter/PreparationFlow.cs - preparation status transitions and initial status of a counter order
src/Unayka.Core/Counter/TicketLine.cs - counter ticket line record and total
src/Unayka.Core/Coupons/Coupon.cs - coupon entity and CouponKind
src/Unayka.Core/Coupons/CouponEvaluator.cs - pure coupon rules returning a discount or a rejection reason
src/Unayka.Core/Data/DataServiceExtensions.cs - services.AddUnaykaData(), provider selection, database create and seed
src/Unayka.Core/Data/DesignTimeDbContextFactory.cs - DbContext factory for dotnet ef migrations
src/Unayka.Core/Data/Migrations/ - EF Core SQLite migrations and model snapshot
src/Unayka.Core/Data/Seed/CatalogSeed.cs - seed data for 3 sections, 8 categories, 7 tags and 29 products (one digital e-book)
src/Unayka.Core/Data/Seed/DatabaseSeeder.cs - idempotent seeding of catalog, coupons and users
src/Unayka.Core/Data/Seed/DemoOrdersSeed.cs - demo customers, orders and payments over the last 30 days
src/Unayka.Core/Data/Seed/DigitalSeedFiles.cs - writes the placeholder e-book file into local private storage
src/Unayka.Core/Data/Seed/LocationSeed.cs - seed for Vilanculos and Maputo Polana with contacts and stock
src/Unayka.Core/Data/Seed/SeedOptions.cs - seed switches from environment (staff password, demo data)
src/Unayka.Core/Data/Seed/SiteImages.cs - Unsplash URL helper and site wide hero images
src/Unayka.Core/Data/UnaykaDbContext.cs - EF Core context, indexes, insert only audit guard
src/Unayka.Core/Data/UtcTicksConverter.cs - stores every DateTimeOffset as UTC ticks (sortable on SQLite and PostgreSQL)
src/Unayka.Core/Locations/ILocationDirectory.cs - read contract for active locations, contacts and stock per location
src/Unayka.Core/Locations/Location.cs - physical store location entity with address, hours, coordinates and map
src/Unayka.Core/Locations/LocationContact.cs - location contact entity (phone, WhatsApp, email) and ContactKind
src/Unayka.Core/Locations/LocationDirectory.cs - EF implementation of ILocationDirectory
src/Unayka.Core/Locations/LocationServiceExtensions.cs - services.AddUnaykaLocations()
src/Unayka.Core/Locations/LocationStock.cs - stock quantity of a product at a location
src/Unayka.Core/Locations/StockAllocator.cs - allocates and returns stock across locations in sort order
src/Unayka.Core/Orders/DigitalDownloadGrant.cs - download access of a user to a paid digital order line
src/Unayka.Core/Orders/IOrderNumberGenerator.cs - next order number contract (UN-000123)
src/Unayka.Core/Orders/IOrderPlacement.cs - online order placement contract
src/Unayka.Core/Orders/IOrderQueries.cs - customer order list and detail records and contract
src/Unayka.Core/Orders/Order.cs - order entity
src/Unayka.Core/Orders/OrderAccess.cs - who may view an order (staff, owner, placing browser)
src/Unayka.Core/Orders/OrderEnums.cs - OrderChannel, FulfillmentKind, OrderStatus
src/Unayka.Core/Orders/OrderItem.cs - order line entity with snapshot of name, image, price, kind and pickup location
src/Unayka.Core/Orders/OrderNumberGenerator.cs - order number generator from the highest stored number
src/Unayka.Core/Orders/OrderPlacement.cs - validates checkout, allocates stock per location, creates order and pending payment, audits
src/Unayka.Core/Orders/OrderPlacementModels.cs - place order request, outcomes, delivery fee constant
src/Unayka.Core/Orders/OrderQueries.cs - order list and detail queries
src/Unayka.Core/Orders/OrderServiceExtensions.cs - services.AddUnaykaOrders()
src/Unayka.Core/Payments/IPaymentGateway.cs - provider gateway contract and request/status records
src/Unayka.Core/Payments/IPaymentService.cs - payment start, refresh and retry contract
src/Unayka.Core/Payments/MockPaymentGateway.cs - mock wallet push (paid after 5 seconds, phone ending in 0 fails)
src/Unayka.Core/Payments/OnlineMethods.cs - methods accepted online (wallets and card) and IsOnline
src/Unayka.Core/Payments/Payment.cs - provider agnostic payment entity
src/Unayka.Core/Payments/PaymentEnums.cs - PaymentMethod and PaymentStatus
src/Unayka.Core/Payments/PaymentEvent.cs - raw payment event entity
src/Unayka.Core/Payments/PaymentService.cs - creates payments, resolves status, marks order paid, records events and audit
src/Unayka.Core/Payments/WalletMethods.cs - mobile wallet methods (M-Pesa, e-Mola, mKesh) and IsWallet
src/Unayka.Core/Presentation/Formatters.cs - MZN money, Maputo date and +258 phone formatting
src/Unayka.Core/Presentation/SafeText.cs - HTML escaping of visible text with a small inline tag allowlist
src/Unayka.Core/Presentation/SafeUrl.cs - URL safety (rejects javascript: and data:) and link target choice
src/Unayka.Core/Presentation/StatusLabels.cs - Portuguese labels and tones for order and payment statuses
src/Unayka.Core/Unayka.Core.csproj - Core project (rules and implementations by feature)
src/Unayka.slnx - solution with Core, Web and the two test projects
src/Unayka.Web.Test/Conventions/RazorTextRuleTests.cs - enforces no loose text in .razor markup
src/Unayka.Web.Test/Pages/AdminPagesTests.cs - staff access and 200 tests for every admin page
src/Unayka.Web.Test/Pages/CheckoutFlowTests.cs - cart to order flow tests over HTTP
src/Unayka.Web.Test/Pages/CounterPagesTests.cs - staff access and anonymous block tests for the counter pages
src/Unayka.Web.Test/Pages/LoginFlowTests.cs - login page, antiforgery and sign in flow tests
src/Unayka.Web.Test/Pages/PublicPagesTests.cs - public pages, health and SEO tests
src/Unayka.Web.Test/Pages/StorefrontTests.cs - 200, 404, search and product form tests
src/Unayka.Web.Test/Unayka.Web.Test.csproj - Web test project
src/Unayka.Web.Test/UnaykaWebFactory.cs - in memory test server factory
src/Unayka.Web/.env.example - documented environment variables
src/Unayka.Web/_Imports.razor - the only place for common usings, @inherits and universal injections
src/Unayka.Web/Components/Admin/_Imports.razor - usings for admin components
src/Unayka.Web/Components/Admin/AdminOptions.cs - select options for statuses, channels, kinds
src/Unayka.Web/Components/Admin/AdminPageBase.cs - base for admin pages (actor user id, date and channel helpers)
src/Unayka.Web/Components/Admin/AdminPageHeader.razor - page title with eyebrow and actions
src/Unayka.Web/Components/Admin/AdminPager.razor - previous and next buttons for interactive lists
src/Unayka.Web/Components/Admin/ErrorList.razor - validation messages
src/Unayka.Web/Components/Admin/SalesChart.razor - Tailwind bar chart of daily sales
src/Unayka.Web/Components/Admin/StatTile.razor - KPI tile
src/Unayka.Web/Components/Admin/ThumbnailImage.razor - small product image
src/Unayka.Web/Components/App.razor - HTML shell, head assets, Blazor and carousel scripts
src/Unayka.Web/Components/Base/UnaykaComponentBase.cs - base class for components exposing the view helpers
src/Unayka.Web/Components/Base/UnaykaLayoutBase.cs - base class for layouts exposing the same helpers
src/Unayka.Web/Components/Base/UnaykaView.cs - shared static view helpers (Text, ToUrl, formats)
src/Unayka.Web/Components/Checkout/OrderStatusLive.razor - interactive payment status with polling and retry
src/Unayka.Web/Components/Checkout/OrderSummaryCard.razor - order lines, totals and fulfillment information
src/Unayka.Web/Components/Checkout/PaymentChoices.cs - payment method labels and icons (wwwroot/images/pay)
src/Unayka.Web/Components/Counter/CounterNav.razor - pill links between sale, cash shift and preparation pages
src/Unayka.Web/Components/Counter/CounterPageBase.cs - base for counter pages exposing the signed in user id
src/Unayka.Web/Components/Counter/CounterTile.razor - touch friendly product tile for the sale grid
src/Unayka.Web/Components/Counter/PreparationColumn.razor - preparation board column with order cards
src/Unayka.Web/Components/Counter/ReceiptPanel.razor - receipt shown after a sale with the new sale button
src/Unayka.Web/Components/Counter/TicketRow.razor - ticket line with quantity buttons and remove
src/Unayka.Web/Components/Layout/AdminLayout.razor - administration layout with sidebar and sign out
src/Unayka.Web/Components/Layout/MainLayout.razor - store layout (announcement bar, SiteHeader, main, SiteFooter)
src/Unayka.Web/Components/Layout/SiteFooter.razor - footer with section links from data, contact, hours and wallet note
src/Unayka.Web/Components/Layout/SiteHeader.razor - sticky header with nav, search, account menu and cart count
src/Unayka.Web/Components/Pages/About.razor - about page
src/Unayka.Web/Components/Pages/Account/AccountPage.razor - /conta with greeting, orders list and sign out
src/Unayka.Web/Components/Pages/Account/Login.razor - /conta/entrar with Google, Facebook and staff sections
src/Unayka.Web/Components/Pages/Admin/_Imports.razor - AdminLayout, AdminPageBase and usings for admin pages
src/Unayka.Web/Components/Pages/Admin/AuditPage.razor - /admin/auditoria read only log
src/Unayka.Web/Components/Pages/Admin/Categories.razor - /admin/categorias list and inline form
src/Unayka.Web/Components/Pages/Admin/CouponsPage.razor - /admin/cupoes list and inline form
src/Unayka.Web/Components/Pages/Admin/CustomerDetailPage.razor - /admin/clientes/{id} detail with orders
src/Unayka.Web/Components/Pages/Admin/Customers.razor - /admin/clientes list
src/Unayka.Web/Components/Pages/Admin/DashboardPage.razor - /admin dashboard
src/Unayka.Web/Components/Pages/Admin/OrderDetailPage.razor - /admin/pedidos/{number} detail, payments timeline and status actions
src/Unayka.Web/Components/Pages/Admin/OrdersPage.razor - /admin/pedidos list with filters
src/Unayka.Web/Components/Pages/Admin/PaymentsPage.razor - /admin/pagamentos list with filters and totals
src/Unayka.Web/Components/Pages/Admin/ProductEdit.razor - /admin/produtos/novo and /admin/produtos/{id} form
src/Unayka.Web/Components/Pages/Admin/Products.razor - /admin/produtos list with active toggle
src/Unayka.Web/Components/Pages/Cart/CartPage.razor - /carrinho with lines, quantity forms, coupon and summary
src/Unayka.Web/Components/Pages/Checkout/CheckoutPage.razor - /checkout single page form (contact, fulfillment, wallet payment, summary)
src/Unayka.Web/Components/Pages/Checkout/OrderStatusPage.razor - /pedido/{number} access check, summary and live status
src/Unayka.Web/Components/Pages/Contact.razor - contact page (placeholder until channels are defined)
src/Unayka.Web/Components/Pages/Counter/CashShifts.razor - /balcao/caixa open, close and history of cash shifts
src/Unayka.Web/Components/Pages/Counter/PointOfSale.razor - /balcao point of sale
src/Unayka.Web/Components/Pages/Counter/PreparationBoardPage.razor - /balcao/preparacao board with 5 second refresh
src/Unayka.Web/Components/Pages/Error.razor - generic error page
src/Unayka.Web/Components/Pages/Home.razor - home with rotating hero carousel, Destaques and one row per active section, how it works, natural ingredients and contact strip
src/Unayka.Web/Components/Pages/Location.razor - location page (placeholder until address is defined)
src/Unayka.Web/Components/Pages/NotFound.razor - not found page
src/Unayka.Web/Components/Pages/Store/AllProducts.razor - /loja/todos full catalog with filters and search
src/Unayka.Web/Components/Pages/Store/CategoryPage.razor - /loja/categoria/{slug} catalog with category hero
src/Unayka.Web/Components/Pages/Store/ProductPage.razor - /produto/{slug} narrative page with order forms and mobile bottom bar
src/Unayka.Web/Components/Pages/Store/SectionShop.razor - /loja/{section} catalog of one section with banner and tag chips
src/Unayka.Web/Components/Pages/Store/Shop.razor - /loja with search, category chips, contained featured marquee, category shortcuts and new arrivals grid
src/Unayka.Web/Components/Routes.razor - router with default layout and not found page
src/Unayka.Web/Components/Shared/AccessDenied.razor - redirects anonymous users to sign in, shows a message to others
src/Unayka.Web/Components/Shared/EmptyState.razor - empty list message with optional action
src/Unayka.Web/Components/Shared/PageHero.razor - full width image header for content pages
src/Unayka.Web/Components/Shared/Pagination.razor - page links for listings
src/Unayka.Web/Components/Shared/PriceTag.razor - price with optional compare at price and discount
src/Unayka.Web/Components/Shared/ProductCard.razor - product card with a one tap Adicionar button
src/Unayka.Web/Components/Shared/SectionHeading.razor - eyebrow, title and subtitle
src/Unayka.Web/Components/Shared/SeoComponent.razor - head title, description, canonical, Open Graph and structured data
src/Unayka.Web/Components/Shared/StatusBadge.razor - colored order or payment status label
src/Unayka.Web/Components/Store/CatalogLinks.cs - catalog query string parsing and URL building, sort options and price ranges
src/Unayka.Web/Components/Store/CatalogView.razor - filters (section, category, tag chips), search and sort form, product grid, pagination
src/Unayka.Web/Components/Store/ProductGallery.razor - CSS only product gallery with thumbnails
src/Unayka.Web/Components/Store/ProductRowSection.razor - titled product row with "Ver mais" link, shared by home and /loja
src/Unayka.Web/Components/Store/StoreStructuredData.cs - schema.org Product and Store JSON-LD builders
src/Unayka.Web/Endpoints/Account/AccountEndpoints.cs - external sign in start and completion (mocked locally), staff sign in and sign out
src/Unayka.Web/Endpoints/Cart/CartCookies.cs - unayka_cart cookie read/write and user id claim helper
src/Unayka.Web/Endpoints/Cart/CartEndpoints.cs - POST /carrinho/adicionar, comprar-agora, atualizar, remover, cupao
src/Unayka.Web/Endpoints/Checkout/CheckoutEndpoints.cs - MapCheckoutEndpoints and POST /checkout/confirmar
src/Unayka.Web/Endpoints/Checkout/PlacedOrdersCookie.cs - protected cookie with the order numbers placed in this browser
src/Unayka.Web/Endpoints/HealthEndpoint.cs - /health reporting active database and storage
src/Unayka.Web/Endpoints/Security/ExternalAuthentication.cs - external cookie scheme and Google/Facebook handlers when keys exist
src/Unayka.Web/Endpoints/Security/RateLimitPolicies.cs - rate limit policy names (login, checkout, webhook)
src/Unayka.Web/Endpoints/Security/SecurityExtensions.cs - cookie auth, policies, antiforgery, data protection, rate limiting, security headers
src/Unayka.Web/Program.cs - app startup, loads .env first, validates configuration, feature service lines (AddUnayka*), database migrate (SQLite) or create (Postgres) and seed, security pipeline, feature endpoint lines (Map*Endpoints), Razor components
src/Unayka.Web/Unayka.Web.csproj - Web project (Blazor only)
src/Unayka.Web/wwwroot/images/pay/ - M-Pesa, e-Mola, mKesh, Visa and Mastercard icons
src/Unayka.Web/wwwroot/scripts/carousel.js - unayka-carousel custom element (fade rotation every 5 seconds, bullets, pause on hover)
src/Unayka.Web/wwwroot/styles/globals.css - Tailwind entry with theme colors, fonts and base layer

## Interactive render modes

No global render mode; AddInteractiveServerComponents is enabled in Program.cs.

src/Unayka.Web/Components/Pages/Counter/PointOfSale.razor - /balcao, InteractiveServer: live ticket, payment status and receipt
src/Unayka.Web/Components/Pages/Counter/CashShifts.razor - /balcao/caixa, InteractiveServer: live difference while counting the drawer
src/Unayka.Web/Components/Pages/Counter/PreparationBoardPage.razor - /balcao/preparacao, InteractiveServer: board refreshed by a timer
src/Unayka.Web/Components/Checkout/OrderStatusLive.razor - InteractiveServer: polls payment status every 2 seconds and retries without reload
src/Unayka.Web/Components/Pages/Admin/ - InteractiveServer on every administration page for filters, forms and toggles
