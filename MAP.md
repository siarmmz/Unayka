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
24. src/Unayka.Core/Administration/AdminCommon.cs - paging, result, local day, money input, slug and audit helpers, location administration and services.AddUnaykaAdministration()
25. src/Unayka.Core/Administration/AdminSales.cs - orders, payments, customers, coupons, dashboard and audit log administration services
26. src/Unayka.Core/Audit/Audit.cs - insert only audit entry, contract and implementation
27. src/Unayka.Core/Carts/Carts.cs - server cart entities, models, totals, pickup locations, cart service and registration
28. src/Unayka.Core/Catalog/Catalog.cs - catalog entities (section with preparation flag, category, tag, product, media with credit, facts, digital asset), read models, stock, visibility, store URLs and contracts
29. src/Unayka.Core/Catalog/CatalogQuery.cs - EF catalog query, local digital file store and services.AddUnaykaCatalog()
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
43. src/Unayka.Web.Test/ConventionTests.cs - no loose text in .razor markup and token only color classes
44. src/Unayka.Web.Test/StaffTests.cs - administration and counter pages for staff, anonymous block tests
45. src/Unayka.Web.Test/StoreTests.cs - public pages, storefront, checkout flow, contact, downloads and login tests
46. src/Unayka.Web.Test/Unayka.Web.Test.csproj - Web test project
47. src/Unayka.Web.Test/UnaykaWebFactory.cs - test server over a copy of database.sqlite with staff accounts created for the tests
48. src/Unayka.Web/.env.example - documented environment variables
49. src/Unayka.Web/_Imports.razor - the only place for common usings, @inherits and universal injections
50. src/Unayka.Web/Components/Admin/_Imports.razor - usings for admin components
51. src/Unayka.Web/Components/Admin/AdminOptions.cs - select options for statuses, channels, kinds
52. src/Unayka.Web/Components/Admin/AdminPageBase.cs - base for admin pages (actor user id, date and channel helpers)
53. src/Unayka.Web/Components/Admin/AdminPageHeader.razor - page title with eyebrow and actions
54. src/Unayka.Web/Components/Admin/AdminPager.razor - previous and next buttons for interactive lists
55. src/Unayka.Web/Components/Admin/AdminTabs.razor - tab links for admin list subpages
56. src/Unayka.Web/Components/Admin/CatalogStatusBadge.razor - Active, Inactive or Archived badge
57. src/Unayka.Web/Components/Admin/ErrorList.razor - validation messages
58. src/Unayka.Web/Components/Admin/SalesChart.razor - Tailwind bar chart of daily sales
59. src/Unayka.Web/Components/Admin/StatTile.razor - KPI tile
60. src/Unayka.Web/Components/Admin/StatusActions.razor - activate, deactivate, archive or delete (with confirm) actions
61. src/Unayka.Web/Components/Admin/ThumbnailImage.razor - small product image
62. src/Unayka.Web/Components/App.razor - HTML shell, fonts, Bootstrap Icons from the CDN, Blazor and carousel scripts
63. src/Unayka.Web/Components/Base/UnaykaComponentBase.cs - base class for components exposing the view helpers
64. src/Unayka.Web/Components/Base/UnaykaLayoutBase.cs - base class for layouts exposing the same helpers
65. src/Unayka.Web/Components/Base/UnaykaView.cs - shared static view helpers (Text, ToUrl, formats)
66. src/Unayka.Web/Components/Checkout/OrderLinesList.razor - order lines grouped by pickup location with digital download links
67. src/Unayka.Web/Components/Checkout/OrderStatusLive.razor - interactive payment status with polling, link to the PaySuite page and retry
68. src/Unayka.Web/Components/Checkout/PaymentChoices.cs - payment method labels and icons (wwwroot/images/pay)
69. src/Unayka.Web/Components/Counter/CounterNav.razor - pill links between sale, cash shift and preparation pages
70. src/Unayka.Web/Components/Counter/CounterPageBase.cs - base for counter pages exposing the signed in user id
71. src/Unayka.Web/Components/Counter/CounterTile.razor - touch friendly product tile for the sale grid
72. src/Unayka.Web/Components/Counter/PreparationColumn.razor - preparation board column with order cards
73. src/Unayka.Web/Components/Counter/ReceiptPanel.razor - receipt shown after a sale with the new sale button
74. src/Unayka.Web/Components/Counter/TicketRow.razor - ticket line with quantity buttons and remove
75. src/Unayka.Web/Components/Layout/AdminLayout.razor - administration layout with sidebar and sign out
76. src/Unayka.Web/Components/Layout/CartAddedNotice.razor - added to cart confirmation with Ver carrinho and Continuar a comprar
77. src/Unayka.Web/Components/Layout/MainLayout.razor - store layout (announcement bar, SiteHeader, main, SiteFooter)
78. src/Unayka.Web/Components/Layout/SiteFooter.razor - footer with section links from data, contact, hours and wallet note
79. src/Unayka.Web/Components/Layout/SiteHeader.razor - sticky header with nav, search, account menu and cart count
80. src/Unayka.Web/Components/Pages/About.razor - /about with hero, story and values from site blocks
81. src/Unayka.Web/Components/Pages/Account/AccountPage.razor - /conta with greeting, orders list and sign out
82. src/Unayka.Web/Components/Pages/Account/Login.razor - /conta/entrar with Google, Facebook (unavailable message without keys) and staff sections
83. src/Unayka.Web/Components/Pages/Account/Setup.razor - /configurar first administrator screen (email and password typed twice), shown while there are no users
84. src/Unayka.Web/Components/Pages/Admin/_Imports.razor - AdminLayout, AdminPageBase and usings for admin pages
85. src/Unayka.Web/Components/Pages/Admin/AuditPage.razor - /admin/auditoria read only log
86. src/Unayka.Web/Components/Pages/Admin/Categories.razor - /admin/categorias list and inline form
87. src/Unayka.Web/Components/Pages/Admin/CouponsPage.razor - /admin/cupoes list and inline form
88. src/Unayka.Web/Components/Pages/Admin/CustomerDetailPage.razor - /admin/clientes/{id} detail with orders
89. src/Unayka.Web/Components/Pages/Admin/Customers.razor - /admin/clientes list
90. src/Unayka.Web/Components/Pages/Admin/DashboardPage.razor - /admin dashboard
91. src/Unayka.Web/Components/Pages/Admin/LocationsPage.razor - /admin/lojas locations and contacts administration
92. src/Unayka.Web/Components/Pages/Admin/OrderDetailPage.razor - /admin/pedidos/{number} detail, payments timeline and status actions
93. src/Unayka.Web/Components/Pages/Admin/OrdersPage.razor - /admin/pedidos list with filters
94. src/Unayka.Web/Components/Pages/Admin/PaymentsPage.razor - /admin/pagamentos list with filters and totals
95. src/Unayka.Web/Components/Pages/Admin/ProductEdit.razor - /admin/produtos/novo and /admin/produtos/{id} form
96. src/Unayka.Web/Components/Pages/Admin/Products.razor - /admin/produtos list with active toggle
97. src/Unayka.Web/Components/Pages/Admin/Sections.razor - /admin/seccoes sections administration
98. src/Unayka.Web/Components/Pages/Admin/Tags.razor - /admin/etiquetas tags per section administration
99. src/Unayka.Web/Components/Pages/Cart/CartPage.razor - /carrinho with lines, quantity forms, coupon and summary
100. src/Unayka.Web/Components/Pages/Checkout/CheckoutPage.razor - /checkout single page form with payment options from the database, redirects to the PaySuite page
101. src/Unayka.Web/Components/Pages/Checkout/OrderStatusPage.razor - /pedido/{number} access check, live status and order summary
102. src/Unayka.Web/Components/Pages/Contact.razor - /contacto with hero from site blocks and each location with contacts and map
103. src/Unayka.Web/Components/Pages/Counter/CashShifts.razor - /balcao/caixa open, close and history of cash shifts
104. src/Unayka.Web/Components/Pages/Counter/PointOfSale.razor - /balcao point of sale with counter payment options from the database
105. src/Unayka.Web/Components/Pages/Counter/PreparationBoardPage.razor - /balcao/preparacao board with 5 second refresh
106. src/Unayka.Web/Components/Pages/Error.razor - generic error page
107. src/Unayka.Web/Components/Pages/Home.razor - home with hero slides, product rows, steps and contact strip, all from site blocks and catalog
108. src/Unayka.Web/Components/Pages/NotFound.razor - not found page
109. src/Unayka.Web/Components/Pages/Store/AllProducts.razor - /loja/todos full catalog with filters and search
110. src/Unayka.Web/Components/Pages/Store/CategoryPage.razor - /loja/categoria/{slug} catalog with category hero
111. src/Unayka.Web/Components/Pages/Store/ProductPage.razor - /produto/{slug} narrative page with order forms and mobile bottom bar
112. src/Unayka.Web/Components/Pages/Store/SectionShop.razor - /loja/{section} catalog of one section with banner and tag chips
113. src/Unayka.Web/Components/Pages/Store/Shop.razor - /loja with search, category chips, contained featured marquee, category shortcuts and new arrivals grid
114. src/Unayka.Web/Components/Routes.razor - router with default layout and not found page
115. src/Unayka.Web/Components/Shared/AccessDenied.razor - redirects anonymous users to sign in, shows a message to others
116. src/Unayka.Web/Components/Shared/EmptyState.razor - empty list message with optional action
117. src/Unayka.Web/Components/Shared/PageHero.razor - full width image header for content pages
118. src/Unayka.Web/Components/Shared/Pagination.razor - page links for listings
119. src/Unayka.Web/Components/Shared/PriceTag.razor - price with optional compare at price and discount
120. src/Unayka.Web/Components/Shared/SectionHeading.razor - eyebrow, title and subtitle
121. src/Unayka.Web/Components/Shared/SeoComponent.razor - head title, description, canonical, Open Graph and structured data
122. src/Unayka.Web/Components/Shared/StatusBadge.razor - colored order or payment status label
123. src/Unayka.Web/Components/Shared/SwipeHint.razor - animated swipe icon shown on hover and on load over moving carousels
124. src/Unayka.Web/Components/Store/CartNotice.cs - query flag for the added to cart confirmation
125. src/Unayka.Web/Components/Store/CatalogLinks.cs - catalog query string parsing and URL building, sort options and price ranges
126. src/Unayka.Web/Components/Store/CatalogView.razor - filters (section, category, tag chips), search and sort form, product grid, pagination
127. src/Unayka.Web/Components/Store/ProductGallery.razor - CSS only product gallery with thumbnails
128. src/Unayka.Web/Components/Store/ProductRail.razor - endless looping product row with arrows when it has more than two items, static row otherwise
129. src/Unayka.Web/Components/Store/ProductRowSection.razor - titled product row with "Ver mais" link, shared by home and /loja
130. src/Unayka.Web/Components/Store/ShareButton.razor - Partilhar with Web Share API and WhatsApp, Facebook, copy link fallbacks
131. src/Unayka.Web/Components/Store/StoreIcon.razor - inline SVG icon by name
132. src/Unayka.Web/Components/Store/StoreIcons.cs - SVG path data for store icons
133. src/Unayka.Web/Components/Store/StoreProductCard.razor - product card with cross fading images, Novo and Destaque badges, share and add to cart
134. src/Unayka.Web/Components/Store/StoreStructuredData.cs - schema.org Product and Store JSON-LD builders
135. src/Unayka.Web/Endpoints/AccountEndpoints.cs - setup gate and POST /configurar, external sign in, staff sign in and sign out
136. src/Unayka.Web/Endpoints/Security.cs - cookie auth, policies, antiforgery, data protection, rate limit policies, external providers, security headers and CSP
137. src/Unayka.Web/Endpoints/StoreEndpoints.cs - cart, checkout (redirect to PaySuite), PaySuite webhook, cookies, downloads, contact redirects and health endpoints
138. src/Unayka.Web/Program.cs - app startup, loads .env first, validates configuration, feature service lines (AddUnayka*), database migrate (SQLite) or create (Postgres) and seed, security pipeline, feature endpoint lines (Map*Endpoints), Razor components
139. src/Unayka.Web/Unayka.Web.csproj - Web project (Blazor only)
140. src/Unayka.Web/wwwroot/images/pay/ - M-Pesa, e-Mola, mKesh, Visa and Mastercard icons
141. src/Unayka.Web/wwwroot/scripts/carousel.js - hero swipe, arrows and autoplay, endless unayka-rail, unayka-fade one at a time crossfade, unayka-collapse and unayka-share elements
142. src/Unayka.Web/wwwroot/styles/globals.css - Tailwind entry with semantic color tokens (accent, surface, ink, brand), fonts and base layer
143. TASKS.md - open work only (In progress, Next, On hold)

## Interactive render modes

No global render mode; AddInteractiveServerComponents is enabled in Program.cs.

144. src/Unayka.Web/Components/Pages/Counter/PointOfSale.razor - /balcao, InteractiveServer: live ticket, payment method and receipt
145. src/Unayka.Web/Components/Pages/Counter/CashShifts.razor - /balcao/caixa, InteractiveServer: live difference while counting the drawer
146. src/Unayka.Web/Components/Pages/Counter/PreparationBoardPage.razor - /balcao/preparacao, InteractiveServer: board refreshed by a timer
147. src/Unayka.Web/Components/Checkout/OrderStatusLive.razor - InteractiveServer: polls payment status every 2 seconds and retries through PaySuite without reload
148. src/Unayka.Web/Components/Pages/Admin/ - InteractiveServer on every administration page for filters, forms and toggles
