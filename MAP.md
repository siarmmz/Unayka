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
9. Dockerfile - multi stage build (Tailwind styles, .NET publish, ASP.NET runtime) on port 10000
10. MAP.md - this file map, one line per file, updated whenever a file is added, removed, renamed or changes purpose
11. PLAN.md - context between sessions (ignored by Git)
12. README.md - product description, stack, configuration, development, phases and open decisions
13. TASKS.md - open work only (In progress, Next, On hold)
14. src/Unayka.Core.Test/Accounts/AccountServiceTests.cs - external sign in, account linking and staff sign in tests
15. src/Unayka.Core.Test/Accounts/PhoneNumberTests.cs - phone normalization tests
16. src/Unayka.Core.Test/Administration/AdminRulesTests.cs - status rules, dashboard, slug, money, coupon and segment tests
17. src/Unayka.Core.Test/Administration/AdminServicesTests.cs - order cancel, product save, coupon revenue, customers and low stock tests
18. src/Unayka.Core.Test/Administration/ArchiveRuleTests.cs - archive instead of delete rule for referenced catalog items and locations
19. src/Unayka.Core.Test/Administration/CatalogCrudTests.cs - section, category, tag and product CRUD validation and digital file tests
20. src/Unayka.Core.Test/Administration/InMemoryDigitalFileStore.cs - in memory IDigitalFileStore for tests
21. src/Unayka.Core.Test/Administration/TestContextFactory.cs - IDbContextFactory over the in memory test database
22. src/Unayka.Core.Test/Audit/AuditLogTests.cs - audit insert only tests
23. src/Unayka.Core.Test/Carts/CartServiceTests.cs - cart totals, stock and coupon tests
24. src/Unayka.Core.Test/Catalog/CatalogQueryTests.cs - filtering, sorting, paging and detail tests
25. src/Unayka.Core.Test/Catalog/PublicVisibilityTests.cs - archived, inactive and hidden products stay out of public queries
26. src/Unayka.Core.Test/Catalog/StockLevelsTests.cs - stock level thresholds tests
27. src/Unayka.Core.Test/Configuration/AppEnvConfigTests.cs - environment configuration reading and production validation tests
28. src/Unayka.Core.Test/Counter/CashMathTests.cs - cash math, money input, wallet rule and preparation flow tests
29. src/Unayka.Core.Test/Counter/CounterServicesTests.cs - sale registration, shift close and board advance tests
30. src/Unayka.Core.Test/Coupons/CouponEvaluatorTests.cs - coupon rule tests
31. src/Unayka.Core.Test/Data/ManualTimeProvider.cs - controllable clock for tests
32. src/Unayka.Core.Test/Data/SeedTests.cs - seed idempotency, catalog, staff and demo data tests
33. src/Unayka.Core.Test/Data/TestDatabase.cs - in memory SQLite context for tests
34. src/Unayka.Core.Test/Data/TestStock.cs - test helper to seed stock per location
35. src/Unayka.Core.Test/Locations/LocationDirectoryTests.cs - location directory and stock allocation tests
36. src/Unayka.Core.Test/Orders/CheckoutFixture.cs - seeded in memory services for checkout tests
37. src/Unayka.Core.Test/Orders/DigitalDownloadTests.cs - download allowed for the buyer, refused for other users, anonymous and unpaid
38. src/Unayka.Core.Test/Orders/OrderPlacementTests.cs - placement integration tests on SQLite
39. src/Unayka.Core.Test/Orders/PickupLocationTests.cs - per line pickup location validation, mixed locations and digital only carts
40. src/Unayka.Core.Test/Payments/PaymentTests.cs - mock gateway and payment transition tests
41. src/Unayka.Core.Test/Presentation/FormattersTests.cs - money, date and phone formatting tests
42. src/Unayka.Core.Test/Presentation/SafeTextTests.cs - text escaping and inline tag allowlist tests
43. src/Unayka.Core.Test/Presentation/SafeUrlTests.cs - URL safety and link target tests
44. src/Unayka.Core.Test/SharedEnvironment.cs - serializes tests that change environment variables
45. src/Unayka.Core.Test/Unayka.Core.Test.csproj - Core test project
46. src/Unayka.Core/Accounts/AccountService.cs - external (Google, Facebook) customer sign in, staff password sign in, lockout, bootstrap administrator
47. src/Unayka.Core/Accounts/AccountServiceExtensions.cs - services.AddUnaykaAccounts()
48. src/Unayka.Core/Accounts/ExternalProviders.cs - Google and Facebook provider names, configured or locally mocked
49. src/Unayka.Core/Accounts/IAccountService.cs - sign in contract, outcomes, result and ExternalIdentity
50. src/Unayka.Core/Accounts/IpHash.cs - one way hash of the client address
51. src/Unayka.Core/Accounts/LoginAttempt.cs - sign in attempt entity (identifier and IP hash)
52. src/Unayka.Core/Accounts/PhoneNumber.cs - normalizes +258 phones to the nine digit national number
53. src/Unayka.Core/Accounts/UnaykaPolicies.cs - authorization policy names (Staff, Management, Administration)
54. src/Unayka.Core/Accounts/User.cs - user entity (customer or staff)
55. src/Unayka.Core/Accounts/UserRole.cs - Customer, Cashier, Manager, Administrator
56. src/Unayka.Core/Administration/AdminAuditService.cs - paged read only audit log
57. src/Unayka.Core/Administration/AdminCatalogModels.cs - product and category rows and edit forms
58. src/Unayka.Core/Administration/AdminCatalogService.cs - product and category queries and audited commands
59. src/Unayka.Core/Administration/AdminCommon.cs - paging, result, local day, money input, slug, search pattern and audit name helpers
60. src/Unayka.Core/Administration/AdminCouponsService.cs - coupon list with revenue, form conversions and audited commands
61. src/Unayka.Core/Administration/AdminCustomersService.cs - customers with totals, recurrence segment and detail
62. src/Unayka.Core/Administration/AdminDashboardService.cs - dashboard data query (figures, top products, latest orders, low stock)
63. src/Unayka.Core/Administration/AdministrationServiceExtensions.cs - services.AddUnaykaAdministration()
64. src/Unayka.Core/Administration/AdminLocationsModels.cs - location and contact admin forms and rows
65. src/Unayka.Core/Administration/AdminLocationsService.cs - IAdminLocations: location and contact CRUD with validation, archive rule and audit
66. src/Unayka.Core/Administration/AdminOrdersService.cs - order list, detail and audited status changes with stock restore
67. src/Unayka.Core/Administration/AdminPaymentsService.cs - payment list with filters and totals per status
68. src/Unayka.Core/Administration/AdminTaxonomyModels.cs - section, category and tag admin forms and rows
69. src/Unayka.Core/Administration/AdminTaxonomyService.cs - IAdminTaxonomy: section, category and tag CRUD with archive rule and audit
70. src/Unayka.Core/Administration/CatalogValidator.cs - product and category validation
71. src/Unayka.Core/Administration/DashboardCalculator.cs - pure dashboard figures from order samples
72. src/Unayka.Core/Administration/LocationValidator.cs - validation of +258 phones, emails, coordinates and map URL
73. src/Unayka.Core/Administration/OrderStatusRules.cs - allowed order status transitions
74. src/Unayka.Core/Audit/AuditEntry.cs - insert only audit entry entity
75. src/Unayka.Core/Audit/AuditLog.cs - audit log implementation
76. src/Unayka.Core/Audit/IAuditLog.cs - audit log contract
77. src/Unayka.Core/Carts/Cart.cs - server cart entity
78. src/Unayka.Core/Carts/CartCalculator.cs - pure totals from lines and coupon
79. src/Unayka.Core/Carts/CartItem.cs - cart line entity
80. src/Unayka.Core/Carts/CartModels.cs - cart line and cart view records, change and coupon apply results
81. src/Unayka.Core/Carts/CartService.cs - cart service with stock limits and totals recalculated from current prices
82. src/Unayka.Core/Carts/CartServiceExtensions.cs - services.AddUnaykaCarts()
83. src/Unayka.Core/Carts/CartSummary.cs - default ICartSummary reading the cart table
84. src/Unayka.Core/Carts/ICartService.cs - server cart contract (add, quantity, remove, clear, coupon)
85. src/Unayka.Core/Carts/ICartSummary.cs - cart cookie name and ICartSummary (item count)
86. src/Unayka.Core/Carts/PickupLocations.cs - default and validated pickup location per cart line
87. src/Unayka.Core/Catalog/CatalogEnums.cs - ProductKind, CatalogStatus and ProductMediaKind
88. src/Unayka.Core/Catalog/CatalogModels.cs - ProductSearch, ProductPage, CategorySummary, ProductDetail and related read models
89. src/Unayka.Core/Catalog/CatalogQuery.cs - EF implementation with filters, text search, sorting and paging of 12
90. src/Unayka.Core/Catalog/CatalogServiceExtensions.cs - services.AddUnaykaCatalog()
91. src/Unayka.Core/Catalog/Category.cs - category entity inside a section, with status and sort order
92. src/Unayka.Core/Catalog/DigitalAsset.cs - private downloadable file of a digital product (storage key, name, type, size)
93. src/Unayka.Core/Catalog/ICatalogQuery.cs - catalog read contract (featured, best sellers, new arrivals, categories, search, detail, related)
94. src/Unayka.Core/Catalog/IDigitalFileStore.cs - private digital file storage contract (save, open, delete)
95. src/Unayka.Core/Catalog/LocalDigitalFileStore.cs - local folder implementation of IDigitalFileStore under the uploads path
96. src/Unayka.Core/Catalog/Product.cs - product entity: prices in minor units, kind (physical or digital), status, featured and new flags, tags, sales count
97. src/Unayka.Core/Catalog/ProductCardModel.cs - card record (images, new flag, kind) and the EF projection FromProduct
98. src/Unayka.Core/Catalog/ProductFact.cs - product information row (nutrition table, composition)
99. src/Unayka.Core/Catalog/ProductMedia.cs - product image entity (Prepared, Package, Elements, Gallery)
100. src/Unayka.Core/Catalog/ProductStock.cs - total stock of a product across active locations, unlimited for digital
101. src/Unayka.Core/Catalog/ProductVisibility.cs - public visibility rule: product, category and section all active
102. src/Unayka.Core/Catalog/Section.cs - section entity (for example Chás, Cosméticos, Livros), top level of the catalog
103. src/Unayka.Core/Catalog/SectionSlugs.cs - seeded section slugs (chas, cosmeticos, livros)
104. src/Unayka.Core/Catalog/StockLevel.cs - stock state (in stock, low stock, out of stock) from quantity
105. src/Unayka.Core/Catalog/StoreUrls.cs - shared route constants and URL builders for store pages
106. src/Unayka.Core/Catalog/Tag.cs - tag entity scoped to a section, many to many with products
107. src/Unayka.Core/Configuration/AppEnvConfig.cs - single typed reader of environment variables with production validation
108. src/Unayka.Core/Counter/CashMath.cs - pure cash shift totals, expected cash, difference and change
109. src/Unayka.Core/Counter/CashShift.cs - cash shift entity
110. src/Unayka.Core/Counter/CounterPreparation.cs - board query and status advance with audit
111. src/Unayka.Core/Counter/CounterSales.cs - validates the ticket, creates paid counter order, payment, stock movement and audit
112. src/Unayka.Core/Counter/CounterServiceExtensions.cs - services.AddUnaykaCounter()
113. src/Unayka.Core/Counter/CounterShifts.cs - shift implementation with audit
114. src/Unayka.Core/Counter/CounterTimes.cs - Maputo time formatting, start of day and age in minutes
115. src/Unayka.Core/Counter/CounterWallet.cs - counter mock wallet provider names, delay and approval rule (phone ending in 0 fails)
116. src/Unayka.Core/Counter/ICounterPreparation.cs - preparation board contract and records
117. src/Unayka.Core/Counter/ICounterSales.cs - catalog and sale registration contract, requests, receipt and errors
118. src/Unayka.Core/Counter/ICounterShifts.cs - shift contract (open, summarize, close, history) and records
119. src/Unayka.Core/Counter/MoneyInput.cs - parses typed MZN amounts into minor units
120. src/Unayka.Core/Counter/PreparationFlow.cs - preparation status transitions and initial status of a counter order
121. src/Unayka.Core/Counter/TicketLine.cs - counter ticket line record and total
122. src/Unayka.Core/Coupons/Coupon.cs - coupon entity and CouponKind
123. src/Unayka.Core/Coupons/CouponEvaluator.cs - pure coupon rules returning a discount or a rejection reason
124. src/Unayka.Core/Data/DataServiceExtensions.cs - services.AddUnaykaData(), provider selection, database create and seed
125. src/Unayka.Core/Data/DesignTimeDbContextFactory.cs - DbContext factory for dotnet ef migrations
126. src/Unayka.Core/Data/Migrations/ - EF Core SQLite migrations and model snapshot
127. src/Unayka.Core/Data/Seed/CatalogSeed.cs - seed data for 3 sections, 8 categories, 7 tags and 29 products (one digital e-book)
128. src/Unayka.Core/Data/Seed/DatabaseSeeder.cs - idempotent seeding of catalog, coupons and users
129. src/Unayka.Core/Data/Seed/DemoOrdersSeed.cs - demo customers, orders and payments over the last 30 days
130. src/Unayka.Core/Data/Seed/DigitalSeedFiles.cs - writes the placeholder e-book file into local private storage
131. src/Unayka.Core/Data/Seed/LocationSeed.cs - seed for Vilanculos and Maputo Polana with contacts and stock
132. src/Unayka.Core/Data/Seed/SeedOptions.cs - seed switches from environment (staff password, demo data)
133. src/Unayka.Core/Data/Seed/SiteImages.cs - Unsplash URL helper and site wide hero images
134. src/Unayka.Core/Data/UnaykaDbContext.cs - EF Core context, indexes, insert only audit guard
135. src/Unayka.Core/Data/UtcTicksConverter.cs - stores every DateTimeOffset as UTC ticks (sortable on SQLite and PostgreSQL)
136. src/Unayka.Core/Locations/ILocationDirectory.cs - read contract for active locations, contacts and stock per location
137. src/Unayka.Core/Locations/Location.cs - physical store location entity with address, hours, coordinates and map
138. src/Unayka.Core/Locations/LocationContact.cs - location contact entity (phone, WhatsApp, email) and ContactKind
139. src/Unayka.Core/Locations/LocationDirectory.cs - EF implementation of ILocationDirectory
140. src/Unayka.Core/Locations/LocationServiceExtensions.cs - services.AddUnaykaLocations()
141. src/Unayka.Core/Locations/LocationStock.cs - stock quantity of a product at a location
142. src/Unayka.Core/Locations/StockAllocator.cs - allocates and returns stock across locations in sort order
143. src/Unayka.Core/Orders/DigitalDownloadGrant.cs - download access of a user to a paid digital order line
144. src/Unayka.Core/Orders/Downloads/DigitalDownloads.cs - authorizes and serves digital downloads to the buyer, counts and audits
145. src/Unayka.Core/Orders/Downloads/IDigitalDownloads.cs - digital download contract
146. src/Unayka.Core/Orders/IOrderNumberGenerator.cs - next order number contract (UN-000123)
147. src/Unayka.Core/Orders/IOrderPlacement.cs - online order placement contract
148. src/Unayka.Core/Orders/IOrderQueries.cs - customer order list and detail records and contract
149. src/Unayka.Core/Orders/Order.cs - order entity
150. src/Unayka.Core/Orders/OrderAccess.cs - who may view an order (staff, owner, placing browser)
151. src/Unayka.Core/Orders/OrderEnums.cs - OrderChannel, FulfillmentKind, OrderStatus
152. src/Unayka.Core/Orders/OrderItem.cs - order line entity with snapshot of name, image, price, kind and pickup location
153. src/Unayka.Core/Orders/OrderNumberGenerator.cs - order number generator from the highest stored number
154. src/Unayka.Core/Orders/OrderPayments.cs - order payment state helpers (paid check for downloads)
155. src/Unayka.Core/Orders/OrderPlacement.cs - validates checkout, allocates stock per location, creates order and pending payment, audits
156. src/Unayka.Core/Orders/OrderPlacementModels.cs - place order request, outcomes, delivery fee constant
157. src/Unayka.Core/Orders/OrderQueries.cs - order list and detail queries
158. src/Unayka.Core/Orders/OrderServiceExtensions.cs - services.AddUnaykaOrders()
159. src/Unayka.Core/Payments/IPaymentGateway.cs - provider gateway contract and request/status records
160. src/Unayka.Core/Payments/IPaymentService.cs - payment start, refresh and retry contract
161. src/Unayka.Core/Payments/MockPaymentGateway.cs - mock wallet push (paid after 5 seconds, phone ending in 0 fails)
162. src/Unayka.Core/Payments/OnlineMethods.cs - methods accepted online (wallets and card) and IsOnline
163. src/Unayka.Core/Payments/Payment.cs - provider agnostic payment entity
164. src/Unayka.Core/Payments/PaymentEnums.cs - PaymentMethod and PaymentStatus
165. src/Unayka.Core/Payments/PaymentEvent.cs - raw payment event entity
166. src/Unayka.Core/Payments/PaymentService.cs - creates payments, resolves status, marks order paid, records events and audit
167. src/Unayka.Core/Payments/WalletMethods.cs - mobile wallet methods (M-Pesa, e-Mola, mKesh) and IsWallet
168. src/Unayka.Core/Presentation/Formatters.cs - MZN money, Maputo date and +258 phone formatting
169. src/Unayka.Core/Presentation/SafeText.cs - HTML escaping of visible text with a small inline tag allowlist
170. src/Unayka.Core/Presentation/SafeUrl.cs - URL safety (rejects javascript: and data:) and link target choice
171. src/Unayka.Core/Presentation/StatusLabels.cs - Portuguese labels and tones for order and payment statuses
172. src/Unayka.Core/Unayka.Core.csproj - Core project (rules and implementations by feature)
173. src/Unayka.slnx - solution with Core, Web and the two test projects
174. src/Unayka.Web.Test/Conventions/RazorTextRuleTests.cs - enforces no loose text in .razor markup
175. src/Unayka.Web.Test/Conventions/TokenColorRuleTests.cs - fails on non token color classes in any .razor
176. src/Unayka.Web.Test/Pages/AdminCatalogPagesTests.cs - admin catalog, sections, tags and locations pages return 200 for staff
177. src/Unayka.Web.Test/Pages/AdminPagesTests.cs - staff access and 200 tests for every admin page
178. src/Unayka.Web.Test/Pages/CheckoutFlowTests.cs - cart to order flow tests over HTTP
179. src/Unayka.Web.Test/Pages/ContactAndDownloadTests.cs - contact page, contact redirects, CSP frame-src and anonymous download
180. src/Unayka.Web.Test/Pages/CounterPagesTests.cs - staff access and anonymous block tests for the counter pages
181. src/Unayka.Web.Test/Pages/LoginFlowTests.cs - login page, antiforgery and sign in flow tests
182. src/Unayka.Web.Test/Pages/PublicPagesTests.cs - public pages, health and SEO tests
183. src/Unayka.Web.Test/Pages/StorefrontTests.cs - 200, 404, search and product form tests
184. src/Unayka.Web.Test/Unayka.Web.Test.csproj - Web test project
185. src/Unayka.Web.Test/UnaykaWebFactory.cs - in memory test server factory
186. src/Unayka.Web/.env.example - documented environment variables
187. src/Unayka.Web/_Imports.razor - the only place for common usings, @inherits and universal injections
188. src/Unayka.Web/Components/Admin/_Imports.razor - usings for admin components
189. src/Unayka.Web/Components/Admin/AdminOptions.cs - select options for statuses, channels, kinds
190. src/Unayka.Web/Components/Admin/AdminPageBase.cs - base for admin pages (actor user id, date and channel helpers)
191. src/Unayka.Web/Components/Admin/AdminPageHeader.razor - page title with eyebrow and actions
192. src/Unayka.Web/Components/Admin/AdminPager.razor - previous and next buttons for interactive lists
193. src/Unayka.Web/Components/Admin/AdminTabs.razor - tab links for admin list subpages
194. src/Unayka.Web/Components/Admin/CatalogStatusBadge.razor - Active, Inactive or Archived badge
195. src/Unayka.Web/Components/Admin/ErrorList.razor - validation messages
196. src/Unayka.Web/Components/Admin/SalesChart.razor - Tailwind bar chart of daily sales
197. src/Unayka.Web/Components/Admin/StatTile.razor - KPI tile
198. src/Unayka.Web/Components/Admin/StatusActions.razor - activate, deactivate, archive or delete (with confirm) actions
199. src/Unayka.Web/Components/Admin/ThumbnailImage.razor - small product image
200. src/Unayka.Web/Components/App.razor - HTML shell, head assets, Blazor and carousel scripts
201. src/Unayka.Web/Components/Base/UnaykaComponentBase.cs - base class for components exposing the view helpers
202. src/Unayka.Web/Components/Base/UnaykaLayoutBase.cs - base class for layouts exposing the same helpers
203. src/Unayka.Web/Components/Base/UnaykaView.cs - shared static view helpers (Text, ToUrl, formats)
204. src/Unayka.Web/Components/Checkout/OrderLinesList.razor - order lines grouped by pickup location with digital download links
205. src/Unayka.Web/Components/Checkout/OrderStatusLive.razor - interactive payment status with polling and retry
206. src/Unayka.Web/Components/Checkout/OrderSummaryCard.razor - order lines, totals and fulfillment information
207. src/Unayka.Web/Components/Checkout/PaymentChoices.cs - payment method labels and icons (wwwroot/images/pay)
208. src/Unayka.Web/Components/Counter/CounterNav.razor - pill links between sale, cash shift and preparation pages
209. src/Unayka.Web/Components/Counter/CounterPageBase.cs - base for counter pages exposing the signed in user id
210. src/Unayka.Web/Components/Counter/CounterTile.razor - touch friendly product tile for the sale grid
211. src/Unayka.Web/Components/Counter/PreparationColumn.razor - preparation board column with order cards
212. src/Unayka.Web/Components/Counter/ReceiptPanel.razor - receipt shown after a sale with the new sale button
213. src/Unayka.Web/Components/Counter/TicketRow.razor - ticket line with quantity buttons and remove
214. src/Unayka.Web/Components/Layout/AdminLayout.razor - administration layout with sidebar and sign out
215. src/Unayka.Web/Components/Layout/CartAddedNotice.razor - added to cart confirmation with Ver carrinho and Continuar a comprar
216. src/Unayka.Web/Components/Layout/MainLayout.razor - store layout (announcement bar, SiteHeader, main, SiteFooter)
217. src/Unayka.Web/Components/Layout/SiteFooter.razor - footer with section links from data, contact, hours and wallet note
218. src/Unayka.Web/Components/Layout/SiteHeader.razor - sticky header with nav, search, account menu and cart count
219. src/Unayka.Web/Components/Locations/ContactLinks.cs - tel:, wa.me and mailto: link builders for location contacts
220. src/Unayka.Web/Components/Locations/LocationCard.razor - location address, hours, contacts and OpenStreetMap iframe
221. src/Unayka.Web/Components/Pages/About.razor - about page
222. src/Unayka.Web/Components/Pages/Account/AccountPage.razor - /conta with greeting, orders list and sign out
223. src/Unayka.Web/Components/Pages/Account/Login.razor - /conta/entrar with Google, Facebook and staff sections
224. src/Unayka.Web/Components/Pages/Admin/_Imports.razor - AdminLayout, AdminPageBase and usings for admin pages
225. src/Unayka.Web/Components/Pages/Admin/AuditPage.razor - /admin/auditoria read only log
226. src/Unayka.Web/Components/Pages/Admin/Categories.razor - /admin/categorias list and inline form
227. src/Unayka.Web/Components/Pages/Admin/CouponsPage.razor - /admin/cupoes list and inline form
228. src/Unayka.Web/Components/Pages/Admin/CustomerDetailPage.razor - /admin/clientes/{id} detail with orders
229. src/Unayka.Web/Components/Pages/Admin/Customers.razor - /admin/clientes list
230. src/Unayka.Web/Components/Pages/Admin/DashboardPage.razor - /admin dashboard
231. src/Unayka.Web/Components/Pages/Admin/LocationsPage.razor - /admin/lojas locations and contacts administration
232. src/Unayka.Web/Components/Pages/Admin/OrderDetailPage.razor - /admin/pedidos/{number} detail, payments timeline and status actions
233. src/Unayka.Web/Components/Pages/Admin/OrdersPage.razor - /admin/pedidos list with filters
234. src/Unayka.Web/Components/Pages/Admin/PaymentsPage.razor - /admin/pagamentos list with filters and totals
235. src/Unayka.Web/Components/Pages/Admin/ProductEdit.razor - /admin/produtos/novo and /admin/produtos/{id} form
236. src/Unayka.Web/Components/Pages/Admin/Products.razor - /admin/produtos list with active toggle
237. src/Unayka.Web/Components/Pages/Admin/Sections.razor - /admin/seccoes sections administration
238. src/Unayka.Web/Components/Pages/Admin/Tags.razor - /admin/etiquetas tags per section administration
239. src/Unayka.Web/Components/Pages/Cart/CartPage.razor - /carrinho with lines, quantity forms, coupon and summary
240. src/Unayka.Web/Components/Pages/Checkout/CheckoutPage.razor - /checkout single page form (contact, fulfillment, wallet payment, summary)
241. src/Unayka.Web/Components/Pages/Checkout/OrderStatusPage.razor - /pedido/{number} access check, summary and live status
242. src/Unayka.Web/Components/Pages/Contact.razor - contact page (placeholder until channels are defined)
243. src/Unayka.Web/Components/Pages/Counter/CashShifts.razor - /balcao/caixa open, close and history of cash shifts
244. src/Unayka.Web/Components/Pages/Counter/PointOfSale.razor - /balcao point of sale
245. src/Unayka.Web/Components/Pages/Counter/PreparationBoardPage.razor - /balcao/preparacao board with 5 second refresh
246. src/Unayka.Web/Components/Pages/Error.razor - generic error page
247. src/Unayka.Web/Components/Pages/Home.razor - home with rotating hero carousel, Destaques and one row per active section, how it works, natural ingredients and contact strip
248. src/Unayka.Web/Components/Pages/NotFound.razor - not found page
249. src/Unayka.Web/Components/Pages/Store/AllProducts.razor - /loja/todos full catalog with filters and search
250. src/Unayka.Web/Components/Pages/Store/CategoryPage.razor - /loja/categoria/{slug} catalog with category hero
251. src/Unayka.Web/Components/Pages/Store/ProductPage.razor - /produto/{slug} narrative page with order forms and mobile bottom bar
252. src/Unayka.Web/Components/Pages/Store/SectionShop.razor - /loja/{section} catalog of one section with banner and tag chips
253. src/Unayka.Web/Components/Pages/Store/Shop.razor - /loja with search, category chips, contained featured marquee, category shortcuts and new arrivals grid
254. src/Unayka.Web/Components/Routes.razor - router with default layout and not found page
255. src/Unayka.Web/Components/Shared/AccessDenied.razor - redirects anonymous users to sign in, shows a message to others
256. src/Unayka.Web/Components/Shared/EmptyState.razor - empty list message with optional action
257. src/Unayka.Web/Components/Shared/PageHero.razor - full width image header for content pages
258. src/Unayka.Web/Components/Shared/Pagination.razor - page links for listings
259. src/Unayka.Web/Components/Shared/PriceTag.razor - price with optional compare at price and discount
260. src/Unayka.Web/Components/Shared/SectionHeading.razor - eyebrow, title and subtitle
261. src/Unayka.Web/Components/Shared/SeoComponent.razor - head title, description, canonical, Open Graph and structured data
262. src/Unayka.Web/Components/Shared/StatusBadge.razor - colored order or payment status label
263. src/Unayka.Web/Components/Store/CartNotice.cs - query flag for the added to cart confirmation
264. src/Unayka.Web/Components/Store/CatalogLinks.cs - catalog query string parsing and URL building, sort options and price ranges
265. src/Unayka.Web/Components/Store/CatalogView.razor - filters (section, category, tag chips), search and sort form, product grid, pagination
266. src/Unayka.Web/Components/Store/ProductGallery.razor - CSS only product gallery with thumbnails
267. src/Unayka.Web/Components/Store/ProductRail.razor - auto scrolling, swipeable product row (unayka-rail)
268. src/Unayka.Web/Components/Store/ProductRowSection.razor - titled product row with "Ver mais" link, shared by home and /loja
269. src/Unayka.Web/Components/Store/ShareButton.razor - Partilhar with Web Share API and WhatsApp, Facebook, copy link fallbacks
270. src/Unayka.Web/Components/Store/StoreIcon.razor - inline SVG icon by name
271. src/Unayka.Web/Components/Store/StoreIcons.cs - SVG path data for store icons
272. src/Unayka.Web/Components/Store/StoreProductCard.razor - product card with cross fading images, Novo and Destaque badges, share and add to cart
273. src/Unayka.Web/Components/Store/StoreStructuredData.cs - schema.org Product and Store JSON-LD builders
274. src/Unayka.Web/Endpoints/Account/AccountEndpoints.cs - external sign in start and completion (mocked locally), staff sign in and sign out
275. src/Unayka.Web/Endpoints/Cart/CartCookies.cs - unayka_cart cookie read/write and user id claim helper
276. src/Unayka.Web/Endpoints/Cart/CartEndpoints.cs - POST /carrinho/adicionar, comprar-agora, atualizar, remover, cupao
277. src/Unayka.Web/Endpoints/Checkout/CheckoutEndpoints.cs - MapCheckoutEndpoints and POST /checkout/confirmar
278. src/Unayka.Web/Endpoints/Checkout/PlacedOrdersCookie.cs - protected cookie with the order numbers placed in this browser
279. src/Unayka.Web/Endpoints/Downloads/DownloadEndpoints.cs - GET /conta/descargas/{orderItemId} for signed in buyers
280. src/Unayka.Web/Endpoints/HealthEndpoint.cs - /health reporting active database and storage
281. src/Unayka.Web/Endpoints/Locations/LocationEndpoints.cs - /contact, /location and /localizacao 301 redirects to /contacto
282. src/Unayka.Web/Endpoints/Security/ExternalAuthentication.cs - external cookie scheme and Google/Facebook handlers when keys exist
283. src/Unayka.Web/Endpoints/Security/RateLimitPolicies.cs - rate limit policy names (login, checkout, webhook)
284. src/Unayka.Web/Endpoints/Security/SecurityExtensions.cs - cookie auth, policies, antiforgery, data protection, rate limiting, security headers
285. src/Unayka.Web/Program.cs - app startup, loads .env first, validates configuration, feature service lines (AddUnayka*), database migrate (SQLite) or create (Postgres) and seed, security pipeline, feature endpoint lines (Map*Endpoints), Razor components
286. src/Unayka.Web/Unayka.Web.csproj - Web project (Blazor only)
287. src/Unayka.Web/wwwroot/images/pay/ - M-Pesa, e-Mola, mKesh, Visa and Mastercard icons
288. src/Unayka.Web/wwwroot/scripts/carousel.js - hero swipe and autoplay, unayka-rail, unayka-fade shared timer, unayka-collapse and unayka-share elements
289. src/Unayka.Web/wwwroot/styles/globals.css - Tailwind entry with semantic color tokens (accent, surface, ink, brand), fonts and base layer

## Interactive render modes

No global render mode; AddInteractiveServerComponents is enabled in Program.cs.

1. src/Unayka.Web/Components/Pages/Counter/PointOfSale.razor - /balcao, InteractiveServer: live ticket, payment status and receipt
2. src/Unayka.Web/Components/Pages/Counter/CashShifts.razor - /balcao/caixa, InteractiveServer: live difference while counting the drawer
3. src/Unayka.Web/Components/Pages/Counter/PreparationBoardPage.razor - /balcao/preparacao, InteractiveServer: board refreshed by a timer
4. src/Unayka.Web/Components/Checkout/OrderStatusLive.razor - InteractiveServer: polls payment status every 2 seconds and retries without reload
5. src/Unayka.Web/Components/Pages/Admin/ - InteractiveServer on every administration page for filters, forms and toggles
