# MAP

One line per file as "path: what it does". Check before searching the disk.

CLAUDE.md: project rules and Claude Code workflow (version 3)
README.md: product description, stack, configuration, development, phases and open decisions
TASKS.md: tasks by phase (In progress, Next, On hold, Done)
MAP.md: this file map
PLAN.md: context between sessions (ignored by Git)
.gitignore: files ignored by Git (includes the whole .claude folder)
.claude/agents/opus.md: subagent that analyzes, decides architecture and splits tasks (ignored by Git)
.claude/agents/sonnet.md: subagent that writes the code (ignored by Git)
.claude/agents/haiku.md: subagent for fast execution, research, documentation and tests (ignored by Git)
.claude/local/state.json: initialization state, rules version and skills (ignored by Git)
Dockerfile: multi stage build (Tailwind styles, .NET publish, ASP.NET runtime) on port 10000
.dockerignore: files excluded from the Docker build context
.editorconfig: C# formatting and naming rules (JetBrains Rider style)
src/Unayka.slnx: solution with Core, Web and the two test projects
src/Unayka.Core/Configuration/AppEnvConfig.cs: single typed reader of environment variables with production validation
src/Unayka.Core/Presentation/SafeText.cs: HTML escaping of visible text with a small inline tag allowlist
src/Unayka.Core/Presentation/SafeUrl.cs: URL safety (rejects javascript: and data:) and link target choice
src/Unayka.Core/Presentation/Formatters.cs: MZN money, Maputo date and +258 phone formatting
src/Unayka.Web/Program.cs: app startup, loads .env first, validates configuration, feature service lines (AddUnayka*), database create and seed, security pipeline, feature endpoint lines (Map*Endpoints), Razor components
src/Unayka.Web/.env.example: documented environment variables
src/Unayka.Web/_Imports.razor: the only place for common usings, @inherits and universal injections
src/Unayka.Web/Endpoints/HealthEndpoint.cs: /health reporting active database and storage
src/Unayka.Web/Components/Base/UnaykaView.cs: shared static view helpers (Text, ToUrl, formats)
src/Unayka.Web/Components/Base/UnaykaComponentBase.cs: base class for components exposing the view helpers
src/Unayka.Web/Components/Base/UnaykaLayoutBase.cs: base class for layouts exposing the same helpers
src/Unayka.Web/Components/Shared/SeoComponent.razor: head title, description, canonical, Open Graph and structured data
src/Unayka.Web/Components/Layout/MainLayout.razor: store layout (announcement bar, SiteHeader, main, SiteFooter)
src/Unayka.Web/Components/Pages/Home.razor: home page with introduction and service list
src/Unayka.Web/Components/Pages/Services.razor: services page
src/Unayka.Web/Components/Pages/About.razor: about page
src/Unayka.Web/Components/Pages/Location.razor: location page (placeholder until address is defined)
src/Unayka.Web/Components/Pages/Contact.razor: contact page (placeholder until channels are defined)
src/Unayka.Web/Components/Pages/NotFound.razor: not found page
src/Unayka.Web/Components/Pages/Error.razor: generic error page
src/Unayka.Web/wwwroot/styles/globals.css: Tailwind entry with theme colors, fonts and base layer
src/Unayka.Core/Catalog/Category.cs: category entity (kind Coffee or Cosmetic)
src/Unayka.Core/Catalog/Product.cs: product entity with prices in minor units, stock and sales count
src/Unayka.Core/Catalog/ProductMedia.cs: product image entity (Prepared, Package, Elements, Gallery)
src/Unayka.Core/Catalog/ProductFact.cs: product information row (nutrition table, composition)
src/Unayka.Core/Catalog/CatalogEnums.cs: CategoryKind and ProductMediaKind
src/Unayka.Core/Catalog/ProductCardModel.cs: card record and the EF projection FromProduct
src/Unayka.Core/Catalog/StoreUrls.cs: shared route constants and URL builders for store pages
src/Unayka.Core/Accounts/User.cs: user entity (customer or staff)
src/Unayka.Core/Accounts/UserRole.cs: Customer, Cashier, Manager, Administrator
src/Unayka.Core/Accounts/OtpCode.cs: hashed login code entity
src/Unayka.Core/Accounts/LoginAttempt.cs: sign in attempt entity (identifier and IP hash)
src/Unayka.Core/Accounts/UnaykaPolicies.cs: authorization policy names (Staff, Management, Administration)
src/Unayka.Core/Accounts/PhoneNumber.cs: normalizes +258 phones to the nine digit national number
src/Unayka.Core/Accounts/OtpCodes.cs: code generation, hashing and limits
src/Unayka.Core/Accounts/IpHash.cs: one way hash of the client address
src/Unayka.Core/Accounts/IOtpSender.cs: IOtpSender and IDevelopmentOtpInbox contracts
src/Unayka.Core/Accounts/DevelopmentOtpSender.cs: development sender and in memory inbox (never logs the code)
src/Unayka.Core/Accounts/IAccountService.cs: sign in contract, outcomes and result
src/Unayka.Core/Accounts/AccountService.cs: phone code and staff password sign in, lockout, bootstrap administrator
src/Unayka.Core/Accounts/AccountServiceExtensions.cs: services.AddUnaykaAccounts()
src/Unayka.Core/Carts/Cart.cs: server cart entity
src/Unayka.Core/Carts/CartItem.cs: cart line entity
src/Unayka.Core/Carts/ICartSummary.cs: cart cookie name and ICartSummary (item count)
src/Unayka.Core/Carts/CartSummary.cs: default ICartSummary reading the cart table
src/Unayka.Core/Carts/CartServiceExtensions.cs: services.AddUnaykaCarts()
src/Unayka.Core/Coupons/Coupon.cs: coupon entity and CouponKind
src/Unayka.Core/Orders/Order.cs: order entity
src/Unayka.Core/Orders/OrderItem.cs: order line entity
src/Unayka.Core/Orders/OrderEnums.cs: OrderChannel, FulfillmentKind, OrderStatus
src/Unayka.Core/Payments/Payment.cs: provider agnostic payment entity
src/Unayka.Core/Payments/PaymentEvent.cs: raw payment event entity
src/Unayka.Core/Payments/PaymentEnums.cs: PaymentMethod and PaymentStatus
src/Unayka.Core/Counter/CashShift.cs: cash shift entity
src/Unayka.Core/Audit/AuditEntry.cs: insert only audit entry entity
src/Unayka.Core/Audit/IAuditLog.cs: audit log contract
src/Unayka.Core/Audit/AuditLog.cs: audit log implementation
src/Unayka.Core/Data/UnaykaDbContext.cs: EF Core context, indexes, insert only audit guard
src/Unayka.Core/Data/UtcTicksConverter.cs: stores every DateTimeOffset as UTC ticks (sortable on SQLite and PostgreSQL)
src/Unayka.Core/Data/DataServiceExtensions.cs: services.AddUnaykaData(), provider selection, database create and seed
src/Unayka.Core/Data/Seed/SeedOptions.cs: seed switches from environment (staff password, demo data)
src/Unayka.Core/Data/Seed/SiteImages.cs: Unsplash URL helper and site wide hero images
src/Unayka.Core/Data/Seed/CatalogSeed.cs: seed data for 6 categories and 24 products
src/Unayka.Core/Data/Seed/DatabaseSeeder.cs: idempotent seeding of catalog, coupons and users
src/Unayka.Core/Data/Seed/DemoOrdersSeed.cs: demo customers, orders and payments over the last 30 days
src/Unayka.Core/Presentation/StatusLabels.cs: Portuguese labels and tones for order and payment statuses
src/Unayka.Web/Endpoints/Security/SecurityExtensions.cs: cookie auth, policies, antiforgery, data protection, rate limiting, security headers
src/Unayka.Web/Endpoints/Security/RateLimitPolicies.cs: rate limit policy names (login, code, checkout, webhook)
src/Unayka.Web/Endpoints/Account/AccountEndpoints.cs: POST endpoints for phone code, staff sign in and sign out
src/Unayka.Web/Components/Layout/SiteHeader.razor: sticky header with nav, search, account menu and cart count
src/Unayka.Web/Components/Layout/SiteFooter.razor: footer with columns, contact, hours and wallet note
src/Unayka.Web/Components/Layout/AdminLayout.razor: administration layout with sidebar and sign out
src/Unayka.Web/Components/Pages/Account/Login.razor: /conta/entrar with phone code and staff sections
src/Unayka.Web/Components/Shared/ProductCard.razor: product card for listings
src/Unayka.Web/Components/Shared/PriceTag.razor: price with optional compare at price and discount
src/Unayka.Web/Components/Shared/SectionHeading.razor: eyebrow, title and subtitle
src/Unayka.Web/Components/Shared/EmptyState.razor: empty list message with optional action
src/Unayka.Web/Components/Shared/Pagination.razor: page links for listings
src/Unayka.Web/Components/Shared/StatusBadge.razor: colored order or payment status label
src/Unayka.Web/Components/Shared/PageHero.razor: full width image header for content pages
src/Unayka.Web/Components/Shared/AccessDenied.razor: redirects anonymous users to sign in, shows a message to others
src/Unayka.Core.Test/Data/TestDatabase.cs: in memory SQLite context for tests
src/Unayka.Core.Test/Data/ManualTimeProvider.cs: controllable clock for tests
src/Unayka.Core.Test/Data/SeedTests.cs: seed idempotency, catalog, staff and demo data tests
src/Unayka.Core.Test/Audit/AuditLogTests.cs: audit insert only tests
src/Unayka.Core.Test/Accounts/OtpCodesTests.cs: code format, hash and phone normalization tests
src/Unayka.Core.Test/Accounts/AccountServiceTests.cs: code expiry, attempts and staff sign in tests
src/Unayka.Web.Test/Pages/LoginFlowTests.cs: login page, antiforgery and sign in flow tests
src/Unayka.Core.Test: tests for configuration and presentation helpers
src/Unayka.Web.Test: in memory server tests for pages, health and the no loose text rule

# Administration (sprint-l/admin)
src/Unayka.Core/Administration/AdminCommon.cs: paging, result, local day, money input, slug, search pattern and audit name helpers
src/Unayka.Core/Administration/OrderStatusRules.cs: allowed order status transitions
src/Unayka.Core/Administration/DashboardCalculator.cs: pure dashboard figures from order samples
src/Unayka.Core/Administration/AdminDashboardService.cs: dashboard data query (figures, top products, latest orders, low stock)
src/Unayka.Core/Administration/AdminOrdersService.cs: order list, detail and audited status changes with stock restore
src/Unayka.Core/Administration/AdminCatalogModels.cs: product and category rows and edit forms
src/Unayka.Core/Administration/CatalogValidator.cs: product and category validation
src/Unayka.Core/Administration/AdminCatalogService.cs: product and category queries and audited commands
src/Unayka.Core/Administration/AdminCouponsService.cs: coupon list with revenue, form conversions and audited commands
src/Unayka.Core/Administration/AdminCustomersService.cs: customers with totals, recurrence segment and detail
src/Unayka.Core/Administration/AdminPaymentsService.cs: payment list with filters and totals per status
src/Unayka.Core/Administration/AdminAuditService.cs: paged read only audit log
src/Unayka.Core/Administration/AdministrationServiceExtensions.cs: services.AddUnaykaAdministration()
src/Unayka.Web/Components/Admin/AdminPageBase.cs: base for admin pages (actor user id, date and channel helpers)
src/Unayka.Web/Components/Admin/AdminOptions.cs: select options for statuses, channels, kinds
src/Unayka.Web/Components/Admin/AdminPageHeader.razor: page title with eyebrow and actions
src/Unayka.Web/Components/Admin/StatTile.razor: KPI tile
src/Unayka.Web/Components/Admin/SalesChart.razor: Tailwind bar chart of daily sales
src/Unayka.Web/Components/Admin/AdminPager.razor: previous and next buttons for interactive lists
src/Unayka.Web/Components/Admin/ErrorList.razor: validation messages
src/Unayka.Web/Components/Admin/ThumbnailImage.razor: small product image
src/Unayka.Web/Components/Admin/_Imports.razor: usings for admin components
src/Unayka.Web/Components/Pages/Admin/_Imports.razor: AdminLayout, AdminPageBase and usings for admin pages
src/Unayka.Web/Components/Pages/Admin/DashboardPage.razor: /admin dashboard
src/Unayka.Web/Components/Pages/Admin/OrdersPage.razor: /admin/pedidos list with filters
src/Unayka.Web/Components/Pages/Admin/OrderDetailPage.razor: /admin/pedidos/{number} detail, payments timeline and status actions
src/Unayka.Web/Components/Pages/Admin/Products.razor: /admin/produtos list with active toggle
src/Unayka.Web/Components/Pages/Admin/ProductEdit.razor: /admin/produtos/novo and /admin/produtos/{id} form
src/Unayka.Web/Components/Pages/Admin/Categories.razor: /admin/categorias list and inline form
src/Unayka.Web/Components/Pages/Admin/CouponsPage.razor: /admin/cupoes list and inline form
src/Unayka.Web/Components/Pages/Admin/Customers.razor: /admin/clientes list
src/Unayka.Web/Components/Pages/Admin/CustomerDetailPage.razor: /admin/clientes/{id} detail with orders
src/Unayka.Web/Components/Pages/Admin/PaymentsPage.razor: /admin/pagamentos list with filters and totals
src/Unayka.Web/Components/Pages/Admin/AuditPage.razor: /admin/auditoria read only log
src/Unayka.Core.Test/Administration/AdminRulesTests.cs: status rules, dashboard, slug, money, coupon and segment tests
src/Unayka.Core.Test/Administration/AdminServicesTests.cs: order cancel, product save, coupon revenue, customers and low stock tests
src/Unayka.Core.Test/Administration/TestContextFactory.cs: IDbContextFactory over the in memory test database
src/Unayka.Web.Test/Pages/AdminPagesTests.cs: staff access and 200 tests for every admin page

## Interactive render modes

None yet. AddInteractiveServerComponents is enabled in Program.cs; there is no global render mode.

Administration pages (all InteractiveServer, real interactivity with filters, forms and toggles): DashboardPage, OrdersPage, OrderDetailPage, Products, ProductEdit, Categories, CouponsPage, Customers, CustomerDetailPage, PaymentsPage, AuditPage.
