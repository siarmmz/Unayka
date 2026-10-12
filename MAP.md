# MAP

Numbered list, one line per file as "N. path - what it does", sorted by path; the number is the index and is renumbered on every change. Check here before searching the disk. Update it whenever a file is added, removed, renamed or changes purpose.

1. .claude/agents/haiku.md - subagent for fast execution, research, documentation and tests (ignored by Git)
2. .claude/agents/opus.md - subagent that analyzes, decides architecture and splits tasks (ignored by Git)
3. .claude/agents/sonnet.md - subagent that writes the code (ignored by Git)
4. .claude/local/state.json - initialization state, rules version and skills (ignored by Git)
5. .dockerignore - files excluded from the Docker build context
6. .editorconfig - C# formatting and naming rules (JetBrains Rider style)
7. .gitignore - files ignored by Git (includes the whole .claude folder)
8. CLAUDE.md - project rules and Claude Code workflow (version 8)
9. database.sqlite - SQLite database with the store data (catalog, image URLs, site content, payment options, locations); the only source of data, no seed in code
10. Dockerfile - multi stage build (Tailwind styles, .NET publish, ASP.NET runtime) on port 10000
11. MAP.md - this file map, one line per file, updated whenever a file is added, removed, renamed or changes purpose
12. PLAN.md - context between sessions (ignored by Git)
13. README.md - product description, stack, configuration, development, phases and open decisions
14. src/Unayka.Core.Test/AccountTests.cs - external sign in, staff sign in and phone normalization tests
15. src/Unayka.Core.Test/AdministrationTests.cs - status rules, dashboard, admin services, archive rule and catalog CRUD tests
16. src/Unayka.Core.Test/CatalogTests.cs - catalog query, public visibility, stock level and location directory tests over database.sqlite
17. src/Unayka.Core.Test/CommerceTests.cs - cart, coupon, digital download, order placement, pickup and PaySuite payment tests
18. src/Unayka.Core.Test/CounterTests.cs - cash math, preparation flow and counter sale, shift and board tests
19. src/Unayka.Core.Test/PlatformTests.cs - audit, environment configuration, formatting, safe text and safe URL tests
20. src/Unayka.Core.Test/TestSupport.cs - test helpers: in memory copy of database.sqlite, clock, stock, file store, checkout fixture and fake PaySuite client
21. src/Unayka.Core.Test/Unayka.Core.Test.csproj - Core test project
22. src/Unayka.Core/Accounts/Accounts.cs - users, roles, login attempts, phone and IP helpers, external providers, account service (sign in, first administrator setup) and registration
23. src/Unayka.Core/Administration/AdminCatalog.cs - product, section, category and tag administration models, validation and audited services (categories and tags filtered by section)
24. src/Unayka.Core/Administration/AdminCommon.cs - paging, result, local day, money input parsing, slug and audit helpers, location administration and services.AddUnaykaAdministration()
25. src/Unayka.Core/Administration/AdminSales.cs - orders, payments, customers, coupons, dashboard and audit log administration services
26. src/Unayka.Core/Audit/Audit.cs - insert only audit entry, contract and implementation
27. src/Unayka.Core/Carts/Carts.cs - server cart entities, models, totals, pickup locations, cart service and registration
28. src/Unayka.Core/Catalog/Catalog.cs - catalog entities (section with preparation flag, category, tag, product, media with credit, facts, digital asset), read models, stock, visibility, store URLs and contracts
29. src/Unayka.Core/Catalog/CatalogQuery.cs - EF catalog query, local digital file store, catalog URL parsing and building (CatalogLinks) and services.AddUnaykaCatalog()
30. src/Unayka.Core/Configuration/AppEnvConfig.cs - single typed reader of environment variables with production validation
31. src/Unayka.Core/Counter/Counter.cs - cash shift, ticket, cash math, preparation flow, counter providers, contracts and counter services (sales, shifts, board)
32. src/Unayka.Core/Coupons/Coupons.cs - coupon entity and pure coupon rules
33. src/Unayka.Core/Data/DataServiceExtensions.cs - services.AddUnaykaData(), provider selection and database migration (no seeding)
34. src/Unayka.Core/Data/Migrations/ - single EF Core SQLite migration (InitialCreate) and model snapshot
35. src/Unayka.Core/Data/UnaykaDbContext.cs - EF Core context, indexes, insert only audit guard, UTC ticks converter and design time factory
36. src/Unayka.Core/Locations/Locations.cs - locations, contacts with link and icon, stock per location, directory, allocator and registration
37. src/Unayka.Core/Orders/Orders.cs - orders, lines, download grants, placement, queries, number generator, digital downloads and services.AddUnaykaOrders()
38. src/Unayka.Core/Payments/Payments.cs - payments, events, payment options from the database, IPaymentMethod per method, PaySuite client (create, status, signed webhook), payment service and registration
39. src/Unayka.Core/Presentation/Presentation.cs - MZN money, Maputo date and +258 phone formatting, safe text, safe URL and status labels
40. src/Unayka.Core/Site/Site.cs - site content blocks and their admin service, payment option administration and services.AddUnaykaSite()
41. src/Unayka.Core/Unayka.Core.csproj - Core project (rules and implementations by feature)
42. src/Unayka.slnx - solution with Core, Web and the two test projects
43. src/Unayka.Web.Test/ConventionTests.cs - no loose text or logic in .razor markup and token only color classes
44. src/Unayka.Web.Test/StaffTests.cs - administration and counter pages for staff, anonymous block tests
45. src/Unayka.Web.Test/StoreTests.cs - public pages, storefront, checkout flow, contact, downloads and login tests
46. src/Unayka.Web.Test/Unayka.Web.Test.csproj - Web test project
47. src/Unayka.Web.Test/UnaykaWebFactory.cs - test server over a copy of database.sqlite with staff accounts created for the tests
48. src/Unayka.Web/.env.example - documented environment variables
49. src/Unayka.Web/_Imports.razor - the only place for common usings, @inherits and universal injections
50. src/Unayka.Web/Components/App.razor - HTML shell, fonts, Bootstrap Icons from the CDN, Blazor and carousel scripts
51. src/Unayka.Web/Components/Base/UnaykaBase.cs - view helpers (Text, ToUrl, formats, status badges), component and layout bases, and StaffPageBase with the signed in user id, select options and counter links
52. src/Unayka.Web/Components/Checkout/OrderStatusLive.razor - interactive payment status with polling, link to the PaySuite page and retry
53. src/Unayka.Web/Components/Layout/AdminLayout.razor - administration layout with sidebar and sign out
54. src/Unayka.Web/Components/Layout/MainLayout.razor - store layout: announcement, header (navigation, search, account, cart), footer (brand, payment options, social links, sections, help links, locations) and added to cart notice, all from the database
55. src/Unayka.Web/Components/Pages/About.razor - /about with hero, story and values from site blocks
56. src/Unayka.Web/Components/Pages/Account/AccountPage.razor - /conta with greeting, orders with lines and download links, and sign out
57. src/Unayka.Web/Components/Pages/Account/Login.razor - /conta/entrar with Google, Facebook (unavailable message without keys) and staff sections
58. src/Unayka.Web/Components/Pages/Account/Setup.razor - /configurar first administrator screen (email and password typed twice), shown while there are no users
59. src/Unayka.Web/Components/Pages/Admin/_Imports.razor - AdminLayout, AdminPageBase and usings for admin pages
60. src/Unayka.Web/Components/Pages/Admin/AuditPage.razor - /admin/auditoria read only log
61. src/Unayka.Web/Components/Pages/Admin/Categories.razor - /admin/categorias list and inline form
62. src/Unayka.Web/Components/Pages/Admin/CouponsPage.razor - /admin/cupoes list and inline form
63. src/Unayka.Web/Components/Pages/Admin/CustomerDetailPage.razor - /admin/clientes/{id} detail with orders
64. src/Unayka.Web/Components/Pages/Admin/Customers.razor - /admin/clientes list
65. src/Unayka.Web/Components/Pages/Admin/DashboardPage.razor - /admin dashboard with figures, daily sales bars, top products, latest orders and low stock
66. src/Unayka.Web/Components/Pages/Admin/LocationsPage.razor - /admin/lojas locations and contacts administration
67. src/Unayka.Web/Components/Pages/Admin/OrderDetailPage.razor - /admin/pedidos/{number} detail, payments timeline and status actions
68. src/Unayka.Web/Components/Pages/Admin/OrdersPage.razor - /admin/pedidos list with filters
69. src/Unayka.Web/Components/Pages/Admin/PaymentsPage.razor - /admin/pagamentos list with filters and totals
70. src/Unayka.Web/Components/Pages/Admin/ProductEdit.razor - /admin/produtos/novo and /admin/produtos/{id} form
71. src/Unayka.Web/Components/Pages/Admin/Products.razor - /admin/produtos list with tabs, filters, status actions and pager
72. src/Unayka.Web/Components/Pages/Admin/Sections.razor - /admin/seccoes sections administration
73. src/Unayka.Web/Components/Pages/Admin/Tags.razor - /admin/etiquetas tags per section administration
74. src/Unayka.Web/Components/Pages/Cart/CartPage.razor - /carrinho with lines, quantity forms, coupon and summary
75. src/Unayka.Web/Components/Pages/Checkout/CheckoutPage.razor - /checkout single page form with payment options from the database, redirects to the PaySuite page
76. src/Unayka.Web/Components/Pages/Checkout/OrderStatusPage.razor - /pedido/{number} access check, live status and order summary
77. src/Unayka.Web/Components/Pages/Contact.razor - /contacto with hero from site blocks and each location with contacts and map
78. src/Unayka.Web/Components/Pages/Counter/CashShifts.razor - /balcao/caixa open, close and history of cash shifts
79. src/Unayka.Web/Components/Pages/Counter/PointOfSale.razor - /balcao point of sale: product tiles, ticket, counter payment options from the database and receipt
80. src/Unayka.Web/Components/Pages/Counter/PreparationBoardPage.razor - /balcao/preparacao board with order cards per status and 5 second refresh
81. src/Unayka.Web/Components/Pages/Error.razor - generic error page
82. src/Unayka.Web/Components/Pages/Home.razor - home with hero slides, product rows, steps and contact strip, all from site blocks and catalog
83. src/Unayka.Web/Components/Pages/NotFound.razor - not found page
84. src/Unayka.Web/Components/Pages/Store/CatalogPage.razor - /loja/todos, /loja/categoria/{slug} and /loja/{section}: hero, filters (section, category, tags), search, sort, product grid and pagination
85. src/Unayka.Web/Components/Pages/Store/ProductPage.razor - /produto/{slug} gallery, price, order forms, share links, story and ingredient images, facts, related products and photo credits
86. src/Unayka.Web/Components/Pages/Store/Shop.razor - /loja with search, category chips, contained featured marquee, category shortcuts and new arrivals grid
87. src/Unayka.Web/Components/Routes.razor - router with default layout and not found page
88. src/Unayka.Web/Components/Shared/AccessDenied.razor - redirects anonymous users to sign in, shows a message to others
89. src/Unayka.Web/Components/Shared/AdminPager.razor - previous and next buttons for interactive staff lists
90. src/Unayka.Web/Components/Shared/SeoComponent.razor - head title, description, canonical, Open Graph and structured data
91. src/Unayka.Web/Components/Shared/StatusActions.razor - activate, deactivate, archive or delete (with confirm) actions for catalog rows
92. src/Unayka.Web/Components/Store/ProductRail.razor - endless looping product row with arrows when it has more than two items, static row otherwise
93. src/Unayka.Web/Components/Store/StoreProductCard.razor - product card with cross fading images, badges, price, add to cart and share menu
94. src/Unayka.Web/Endpoints/AccountEndpoints.cs - setup gate and POST /configurar, external sign in, staff sign in and sign out
95. src/Unayka.Web/Endpoints/Security.cs - cookie auth, policies, antiforgery, data protection, rate limit policies, external providers, security headers and CSP
96. src/Unayka.Web/Endpoints/StoreEndpoints.cs - cart, checkout (redirect to PaySuite), PaySuite webhook, cookies, downloads, contact redirects and health endpoints
97. src/Unayka.Web/Program.cs - app startup, loads .env first, validates configuration, feature service lines (AddUnayka*), database migrate (SQLite) or create (Postgres) and seed, security pipeline, feature endpoint lines (Map*Endpoints), Razor components
98. src/Unayka.Web/Unayka.Web.csproj - Web project (Blazor only)
99. src/Unayka.Web/wwwroot/images/pay/ - M-Pesa, e-Mola and mKesh logos referenced by payment options in the database
100. src/Unayka.Web/wwwroot/scripts/carousel.js - hero swipe, arrows and autoplay, endless unayka-rail, unayka-fade one at a time crossfade, unayka-collapse and unayka-share elements
101. src/Unayka.Web/wwwroot/styles/globals.css - Tailwind entry with semantic color tokens (accent, surface, ink, brand), fonts and base layer
102. TASKS.md - open work only (In progress, Next, On hold)

## Interactive render modes

No global render mode; AddInteractiveServerComponents is enabled in Program.cs.

103. src/Unayka.Web/Components/Pages/Counter/PointOfSale.razor - /balcao, InteractiveServer: live ticket, payment method and receipt
104. src/Unayka.Web/Components/Pages/Counter/CashShifts.razor - /balcao/caixa, InteractiveServer: live difference while counting the drawer
105. src/Unayka.Web/Components/Pages/Counter/PreparationBoardPage.razor - /balcao/preparacao, InteractiveServer: board refreshed by a timer
106. src/Unayka.Web/Components/Checkout/OrderStatusLive.razor - InteractiveServer: polls payment status every 2 seconds and retries through PaySuite without reload
107. src/Unayka.Web/Components/Pages/Admin/ - InteractiveServer on every administration page for filters, forms and toggles
