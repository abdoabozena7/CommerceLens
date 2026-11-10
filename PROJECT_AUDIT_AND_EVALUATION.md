# CommerceLens - Project Audit and Evaluation

Updated **2026-11-10** after correcting the project scope.
Target: a cohesive, intermediate-level full-stack C#/.NET e-commerce analytics
system, not a collection of integrations or a production payment/ad platform.

## 1. Executive Summary

The dashboard is the main product. Its supporting React store produces demo
orders and application events; Playwright monitors that store's rendered product
information; ML.NET estimates historical advertising click likelihood.

The corrected application has six dashboard navigation entries:

1. Overview
2. Sales analytics
3. Advertising analytics
4. Dataset explorer
5. Product monitoring
6. Prediction Lab

The unrelated social module, its endpoints, model mappings, UI, fixture, analysis,
tests and documentation have been removed. The original external repositories
were not modified. Their role is conceptual learning reference only.

Data preservation was deliberate: old migrations are byte-for-byte unchanged,
obsolete stored tables remain unmapped, and a forward migration performs no
destructive DDL. Tests preserve populated archived rows through upgrade/rollback.
The existing normal database is also byte-for-byte unchanged.

**Current verification:** .NET build: zero warnings/errors; **26 xUnit and
14 Chromium cases passed**, zero failures/skips. React production build,
all TypeScript checks, lint and EF model/migration consistency passed.
Actual runtime checks confirmed removed endpoints return 404, a saved $258 demo
checkout, eight genuinely collected products and unchanged CSV-only model input.

The earlier 46/15 test counts described a different scope and are superseded.
The reduced suite removes 23 unrelated backend cases and two unrelated browser
cases, while adding targeted monitoring/preservation coverage.

**Operational note:** the pre-existing API on port 5080 was not stopped.
Restart that older process normally to load the new source/migrations; do not
delete its database. Verification used separate Release instances and temporary
databases, not the user's saved data.

## 2. Architecture Explanation

```text
Original advertising CSV
  -> startup import / CSV upload
  -> validation, cleaning, content hash, atomic write
  -> ImportBatches + AdImpressions in SQLite
       +-> historical advertising / explorer / missing-value analytics
       +-> user-disjoint ML.NET train/test split
             -> training-fitted encoding -> logistic regression
             -> held-out evaluation + immutable model ZIP
             -> Prediction Lab probability

Independent demo Products in SQLite
  -> product API -> React storefront
       +-> detail views / visible ads / ad clicks -> StoreEvents
       |     -> live advertising metrics
       +-> cart -> authoritative server-price checkout
       |     -> stock + Orders + OrderItems
       |     -> sales analytics
       +-> actual rendered DOM
             -> Playwright collector -> JSON stdout
             -> .NET provenance validation -> ScrapeRuns + ScrapedProducts
             -> Product Monitoring comparisons/history
```

There is **no data-flow arrow from collected products or demo orders into CTR
training**, and no conversion of historical clicks into sales.

### Relevant source and connections

| Component | Responsibility and actual connection |
|---|---|
| [Program.cs](backend/CommerceLens.Api/Program.cs) | DI, loopback/CORS configuration, controllers, explicit error handler, startup initialization |
| [DatabaseInitializer](backend/CommerceLens.Api/Data/DatabaseInitializer.cs) | Applies migrations, idempotently imports the original file, seeds eight independent demo products, records interrupted collection failures |
| [Entities](backend/CommerceLens.Api/Models/Entities.cs) and [CommerceDbContext](backend/CommerceLens.Api/Data/CommerceDbContext.cs) | Nine active EF entity types, keys, FKs, meaningful indexes and UTC round trips |
| [CsvImportService.ImportAsync](backend/CommerceLens.Api/Services/CsvImportService.cs) | Entire-file validation/cleaning, hash deduplication, rejected reports, transactional insertion |
| [DataController](backend/CommerceLens.Api/Controllers/DataController.cs) | `/api/data/rows` and imports; [DataExplorer](frontend/src/pages/DataExplorer.tsx) connects filtering/paging/upload |
| [AdQueries](backend/CommerceLens.Api/Services/AdQueries.cs) and [AnalyticsService](backend/CommerceLens.Api/Services/AnalyticsService.cs) | Composable filters and SQL LINQ aggregations; [AnalyticsController](backend/CommerceLens.Api/Controllers/AnalyticsController.cs) serves overview/advertising/commerce/options |
| [StoreController.cs](backend/CommerceLens.Api/Controllers/StoreController.cs) | Product, order and event controllers; connected to [Storefront](frontend/src/pages/Storefront.tsx) and [CartDrawer](frontend/src/components/CartDrawer.tsx) |
| [OrderService.CheckoutAsync](backend/CommerceLens.Api/Services/OrderService.cs) | Authoritative prices, integer cents, stock checks, order snapshots, transaction and retry-key/cart-hash identity |
| [StoreEventService](backend/CommerceLens.Api/Services/StoreEventService.cs) and [useStoreTracking](frontend/src/services/useStoreTracking.ts) | Validated/keyed detail-view, impression and click records; client request coalescing and database retry safety |
| [StorefrontScraperService](backend/CommerceLens.Api/Services/StorefrontScraperService.cs) | Runs Node, handles timeout/pipes/failures, validates collection provenance, persists evidence, compares current catalog |
| [adapter.ts](scraper/src/adapter.ts) and [cli.ts](scraper/src/cli.ts) | Fresh local Chromium, actual visible product text, bounded local-origin traffic, typed JSON output |
| [ScrapesController](backend/CommerceLens.Api/Controllers/ScrapesController.cs) and [ProductMonitoring](frontend/src/pages/ProductMonitoring.tsx) | `/api/scrapes` creation/history/detail -> prices, signed deltas, availability, difference filters, status/history |
| [ClickPredictionService](backend/CommerceLens.Api/Services/ClickPredictionService.cs) and [prediction controllers](backend/CommerceLens.Api/Controllers/PredictionsController.cs) | CSV-only training/evaluation/artifact persistence and request-owned ML.NET scoring -> [PredictionLab](frontend/src/pages/PredictionLab.tsx) |
| [api.ts](frontend/src/services/api.ts), [useResource](frontend/src/services/useResource.ts), [Vite config](frontend/vite.config.ts) | Typed relative `/api` requests/proxy, visible failures, same-resource refresh retention and cross-tab notifications |

Simple read actions query EF directly; business rules live in services.
There is no repository framework, CQRS, microservice or event bus.

### Active database design

| Table | Purpose / invariant |
|---|---|
| `ImportBatches` | Unique content SHA-256, source, import time, counts and validation history |
| `AdImpressions` | Historical observations, generated PK and unique `(ImportBatchId, SourceRow)`; repeated `OriginalUserId` is not a PK |
| `Products` | Independent eight-product demo catalog, unique slug, integer price cents and stock |
| `Orders` | Unique retry key and cart hash, UTC order time, source and exact total |
| `OrderItems` | Order/product FKs and price/name/category snapshots with quantities |
| `StoreEvents` | Product FK, event/source/UTC time and optional unique event key |
| `ScrapeRuns` | Collection URL, start/end, status and explicit failure text |
| `ScrapedProducts` | Run FK, rendered fields, source/time; product ID is comparison metadata, not a restrictive catalog FK |
| `ModelEvaluations` | Training time, CSV/pipeline fingerprint, evaluation and artifact identity |

Stored money is integer cents; DTO money is decimal. Checkout transactions
protect multi-line stock/order consistency. Read-only queries use
`AsNoTracking`, projection, SQL grouping and stable ID-tiebreak pagination.

### Non-destructive retirement

The existing
[initial migration](backend/CommerceLens.Api/Migrations/20261110095150_InitialCreate.cs)
and [subsequent migration](backend/CommerceLens.Api/Migrations/20261110112212_LiveTrackingAndSocialFeedback.cs),
including their designers, remain byte-for-byte unchanged.
The new
[RetireUnusedFeedbackModel migration](backend/CommerceLens.Api/Migrations/20261110122234_RetireUnusedFeedbackModel.cs)
has deliberately empty `Up`/`Down` operations: its target snapshot stops mapping
unused tables, without dropping, recreating or overwriting their rows.

The two legacy tables can remain physically present, including empty tables
after a fresh replay of migration history. They are not active application
models, endpoints or features. Future EF migrations compare against the updated
core-only snapshot. Do not roll back older creation migrations or purge data to
make the schema look smaller.

[MigrationTests](tests/CommerceLens.Tests/MigrationTests.cs) verifies the complete
original 10,000-row import plus orders/items/stock/events/collection survive
upgrade. A separate preservation case snapshots every column of populated
legacy rows, upgrades, rolls back the retirement and reapplies it without changes.

## 3. Feature Completion Matrix

| Feature | Current status | Data / verification |
|---|---|---|
| Dashboard-centered navigation | Fully implemented and verified | Exact six-entry navigation asserted; no unrelated module routes or links |
| Revenue/orders/AOV/units | Fully implemented and verified for demo checkout | Saved orders only; exact-cent calculations, API assertions and real cart/browser flow |
| Top products/category performance | Fully implemented and verified | Saved line quantities/category/price snapshots; no use of historical click labels |
| Sales trends/date filters | Fully implemented and verified | Actual order UTC dates, recent orders and summed daily revenue; honest empty periods |
| Historical CTR and context breakdowns | Fully implemented and verified | 10,000 observations / 6,500 clicks; device, position, time and browsing groups with Unknown |
| Live impressions/clicks/CTR | Fully implemented and verified | Actual store events, explicit visibility definition, deduplication and separate denominator |
| Dataset explorer | Fully implemented and verified | CSV-only rows, filters/search/sort, stable pagination, import validation and missing-value statistics |
| Product DOM extraction | Fully implemented and verified | Name, price, category and availability are independently mutated in the DOM; extractor follows the DOM, not the unchanged API |
| Collection persistence/history | Fully implemented and verified | Actual second-browser collection -> .NET -> SQLite -> UI/reload; latest 20 attempts and failure details |
| Price differences/changed information | Fully implemented and verified | Exact positive/negative deltas, name/category/availability before/after, price-specific counts and filters |
| Missing/extra products | Fully implemented and verified | Real DOM removal changes extraction count; known comparator fixtures prove missing/extra classification |
| Failed collection semantics | Fully implemented and verified | Failed/incomplete runs produce no comparisons; UI shows N/A, not a false missing-product alert |
| Monitoring/mobile/old bookmark | Fully implemented and verified | 390px page without overflow, filters/history, `/scraper` -> `/monitoring` redirect |
| ML.NET prediction/evaluation | Fully implemented and verified, predictive value limited | Real artifact parity, user-disjoint holdout, majority baseline, confusion matrix and retraining |
| Data and migration preservation | Fully implemented and verified | Up/down populated-row tests; old migrations and normal DB checksum unchanged |
| Production payments/authentication | Intentionally outside scope | Local demo, no real payments or public-service claim |

## 4. Previous Project References

These are references to engineering concepts the developer learned, not required
data/code integrations:

| Reference | Concepts applied here | CommerceLens implementation |
|---|---|---|
| [ARACHNE](https://github.com/abdoabozena7/arachne-facebook-crawler) | Browser/context lifecycle, DOM extraction, adapter boundary, durable collection evidence and explicit failure handling | Independent local storefront collector and Product Monitoring |
| [AdClick Prediction System](https://github.com/abdoabozena7/AdClick-Prediction-System) | Ingestion, cleaning, preprocessing, separation of train/test data, training, evaluation and probability scoring | Independent ML.NET/C# categorical logistic-regression pipeline |

No external crawler selectors, authentication, comment exports, Python model
weights or Python preprocessing artifacts are required or loaded.
Neither original repository was modified. Comparing old Python scores from
another dataset/evaluation procedure would not demonstrate improvement here.

## 5. Dashboard Evaluation

### Source correctness

- **Sales:** saved `LocalDemoCheckout` orders only. The isolated runtime check
  saved two $129 products as a $258 demo order, with two units and $258 AOV.
  That is test activity, not real merchant income or a normal-workspace seeded sale.
- **Advertising history:** original CSV observations/labels only. No original
  event dates, prices, purchases or spend exist; no historical revenue/ROI is invented.
- **Live advertising:** actual detail-page loads and public ad interactions.
  Historical observations never become live impressions.
- **Product monitoring:** source/time-stamped rendered records, not purchases
  or training inputs. Repeated collections add evidence, not catalog product count.
- **Prediction:** a model estimate with explicit engine, baseline and staleness;
  scoring does not mutate live events, orders or historical labels.

An impression is at least **50% viewport intersection in a foreground tab**,
without a dwell minimum, once per product/event type per visit. Clicks ensure
their impression first. Reload/new visit can record another event. The API
returns a CTR fraction; UI formats a percentage. No impressions means null/N/A.
Legacy unpaired/keyless clicks remain and can exceed newer impression counts;
there is no fabricated backfill or ratio clamp.

### Product monitoring interpretation

`PriceDifference = CollectedPrice - CurrentCatalogPrice`.
Price mismatch is a subset of changed products. Availability is shown on both
sides. A missing row means absent from a **completed** collection, not an
unsuccessful attempt. Failed runs remain in history with their error, with
comparisons unavailable.

The baseline is the current database catalog, not a historical catalog snapshot.
Later catalog edits can change an old run's comparison. This feature monitors
data consistency; it does not claim a historical price-trend or competitor platform.
The underlying `/api/scrapes` route remains stable while the UI is `/monitoring`.

### Dataset quality

The [original CSV](dataset/ad_click_dataset.csv) remains unchanged:

| Measure | Verified value |
|---|---:|
| Observations / positive labels | 10,000 / 6,500 |
| Historical CTR | 65% |
| Distinct original IDs / IDs that repeat | 4,000 / 500 |
| Missing age / gender | 4,766 / 4,693 |
| Missing device / position / time | 2,000 / 2,000 / 2,000 |
| Missing browsing history | 4,782 |

Names are not returned by the explorer. Missing source values remain nullable;
analytics/ML explicitly represent missing categories as Unknown. Import time is
metadata, not an original ad-event timestamp.

### UX

Sales cards lead overview; navigation uses domain names. Loading, errors,
rejections, empty periods and collection failures are explicit. Successful writes
refresh relevant queries and notify other tabs; focus/manual refresh handles
external changes. Event notifications do not refetch the catalog unnecessarily.

Desktop and 390px dashboard/monitoring layouts were tested. Table overflow stays
inside its scroll container. Light/dark KPI explanatory-text contrast is asserted
at >=4.5:1. This is not a complete WCAG/screen-reader certification; some existing
small labels and chart nonvisual alternatives can still improve.

## 6. Machine Learning Evaluation

Source: [ClickPredictionService](backend/CommerceLens.Api/Services/ClickPredictionService.cs).

| Stage | Actual behavior |
|---|---|
| Ingestion/cleaning | Accepted CSV observations; missing raw features preserved and normalized explicitly |
| Preprocessing | One-hot device type, ad position, browsing history and time of day; training-fitted encoders |
| Split | Approximately 80/20 of distinct user IDs, seed 42; all a user's rows stay in one partition |
| Training | ML.NET L-BFGS logistic regression; no IDs, names, demographics, orders, prices or collected products in Features |
| Evaluation | Same held-out users for model and training-selected majority baseline |
| Prediction | Saved evaluated transformer, per-request PredictionEngine; explicit `engine: "ML.NET"` |

Current original-data results were reproduced after the scope correction:

| Metric | Actual result |
|---|---:|
| Training/test rows | 7,760 / 2,240 |
| Training/test distinct user IDs | 3,200 / 800 |
| User overlap | **0** |
| Historical full-data CTR | 65.00% |
| Accuracy / majority test accuracy | **69.42% / 69.42%** |
| Precision / recall | 69.42% / 100% |
| ROC-AUC | **0.5269899312319571** |
| F1 | 0.8194993412384717 |
| ML.NET-reported log loss | 0.9008172066274225 |
| TP / FP / TN / FN | **1,555 / 685 / 0 / 0** |

All held-out rows are classified as click at threshold 0.5. This explains high
recall/F1 without useful negative-class recognition. Accuracy gain over baseline
is **zero percentage points**; AUC is only slightly above chance. The 65% whole-file
CTR and 69.42% held-out accuracy have different denominators, not an improvement.

The runtime check created an order and collected products, then retrained:
**exactly 10,000 training+test rows, the same CSV fingerprint and the same four
features** remained. This directly verifies source separation. Mobile / Top /
Shopping / Morning returned probability **0.6116299033164978** from ML.NET.
Unit tests independently load the artifact and require exact API-score parity.

The evaluated training-partition transformer is saved/served; held-out rows are
not silently used in a final refit. Additional accepted CSV files mark the model
stale; retraining publishes new immutable metadata/artifact.

Limits: one dataset/split, weak signal, no cross-validation, uncertainty,
calibration study, threshold search, temporal generalization or production drift
monitoring. A genuine probability is not necessarily a well-calibrated probability.
This demonstrates the C# workflow, **not commercially successful prediction**.

## 7. Code Quality and C# Learning

The active design stays intermediate: controllers, services/DI, EF entities/DTOs,
SQLite, LINQ, async I/O and REST. No new architecture framework was introduced.

| Component/concept | Problem, approach and interview explanation |
|---|---|
| Classes/objects | [Entities](backend/CommerceLens.Api/Models/Entities.cs) represent mutable persisted objects. DTO records in [Contracts](backend/CommerceLens.Api/DTOs/Contracts.cs) expose only required input/output; clients cannot choose stored order prices. |
| Interfaces and DI | [Program](backend/CommerceLens.Api/Program.cs) registers scoped contexts/services and singleton ML. Framework interfaces such as `ILogger<T>`, `IWebHostEnvironment`, `IServiceScopeFactory` and `IExceptionHandler` provide contracts without artificial custom wrappers. |
| Controllers/routing/REST | [StoreController.cs](backend/CommerceLens.Api/Controllers/StoreController.cs) binds typed requests and returns resource/status contracts. Checkout creates an order with a Location; errors use appropriate 400/404/409/413/422/502/504 responses. |
| Validation | `[ApiController]`, range/length attributes and service guards reject invalid input. [CsvImportService](backend/CommerceLens.Api/Services/CsvImportService.cs) validates the entire file before commit rather than inserting partial data. |
| EF relationships | [CommerceDbContext](backend/CommerceLens.Api/Data/CommerceDbContext.cs) maps order/items/product, import/observations and run/records. Keys, FKs and unique constraints express database invariants beyond UI checks. |
| LINQ | [AdQueries](backend/CommerceLens.Api/Services/AdQueries.cs) composes `IQueryable` filters; [AnalyticsService](backend/CommerceLens.Api/Services/AnalyticsService.cs) translates grouping/projection into SQL. Materialization such as `ToListAsync` executes the query. |
| Transactions/money | [OrderService](backend/CommerceLens.Api/Services/OrderService.cs) conditionally decrements stock and saves line snapshots atomically. Integer cents persist exactly; decimal DTO money divides by `100m`. |
| Retry safety | Event keys and checkout keys/hashes prevent duplicate writes. Reusing an order key for a different cart conflicts; repeated user IDs are not mistaken for duplicated observations. |
| Async/await | Database, stream and process I/O are awaited; cancellation propagates where supported. Async is not automatic parallelism or free CPU training. |
| Exceptions | [ApiExceptionHandler](backend/CommerceLens.Api/Services/ApiException.cs) implements the framework interface, logs unexpected failures and returns explicit problem responses. Failed collection never returns synthetic success data. |
| Process lifecycle | [StorefrontScraperService](backend/CommerceLens.Api/Services/StorefrontScraperService.cs) reads stdout/stderr concurrently to avoid pipe deadlock, enforces timeout and validates provenance before persistence. |
| Nullable arithmetic/DTOs | [ScrapeContracts](backend/CommerceLens.Api/DTOs/ScrapeContracts.cs) computes decimal price delta; missing/extra records yield null, not an invented zero. Price counts derive from actual comparison differences. |
| ML.NET/lifetimes | [ClickPredictionService](backend/CommerceLens.Api/Services/ClickPredictionService.cs) retains an evaluated transformer, creates DB scopes rather than storing a scoped context, and owns a new PredictionEngine per request. |
| Basic tests | SQLite service tests check real relational behavior; WebApplicationFactory tests use isolated files; Playwright exercises actual HTTP/browser/process flows with assertions, not just endpoint existence. |

Strengths: clear source identity, exact money, practical service boundaries,
explicit failure handling, ordinary code and meaningful tests.
Remaining tradeoffs: string statuses, separate aggregate queries, a heavily
minified existing stylesheet and no load-tested production lifecycle.
Do not add CQRS/repository/event infrastructure to hide these simple workflows.

## 8. Testing Results

### Commands actually executed

```powershell
$env:PATH = "$PWD\.tools\node;$env:PATH"
dotnet build CommerceLens.sln --configuration Release --no-restore
dotnet test --project tests\CommerceLens.Tests\CommerceLens.Tests.csproj --configuration Release --no-build
npm.cmd run build
npm.cmd run lint
npm.cmd run test:e2e
dotnet ef migrations has-pending-model-changes --project backend\CommerceLens.Api --configuration Release --no-build
Get-FileHash -Algorithm SHA256 dataset\ad_click_dataset.csv
```

The forward migration was scaffolded with `dotnet ef migrations add
RetireUnusedFeedbackModel --project backend\CommerceLens.Api --configuration Release`.
The generated destructive operations were deliberately replaced with no-op
retirement **before any application/test migration ran**.

| Check | Actual result |
|---|---|
| .NET solution Release build | Passed, **0 warnings/errors** |
| xUnit cases | **26 passed / 0 failed / 0 skipped** |
| Real Chromium E2E cases | **14 passed / 0 failed / 0 skipped**, 44.9 seconds |
| React production build | Passed |
| React/scraper/E2E TypeScript | Passed |
| Oxlint | Passed |
| EF model consistency | No pending model changes |
| Previous migration files | All four main/designer files checksum-identical |
| Existing normal database | Checksum-identical; no audit writes |
| Removed endpoint HTTP checks | All four checked method/routes returned **404** |
| Actual collection | **8 collected / 8 matched**, no price/missing differences, zero added customer events |
| Demo checkout | Saved $258 order; retry same ID; correct revenue/AOV/units |
| Retraining after checkout/collection | Still exactly 10,000 CSV rows, same fingerprint/features |
| Editor diagnostics | No errors in changed source/test folders |

Original CSV SHA-256:
`0C7A029BB57F281BC5C8FCE6940C75B54397F75749BF9A2D5CC1F6151113E78F`.
Preserved normal database SHA-256:
`AA49642649CA2E1FEEA3DA72672501B132E43C136AA127619445D606161689E5`.

### Test inventory

| Test group | Cases / relevant evidence |
|---|---|
| [ImportTests](tests/CommerceLens.Tests/ImportTests.cs) | 6: exact hash/counts, repeated IDs, missingness, idempotence and invalid CSV |
| [AnalyticsTests](tests/CommerceLens.Tests/AnalyticsTests.cs) | 6: independent CTR math, filters, explorer, validation history, order-date/money analytics and upload limits |
| [StoreTests](tests/CommerceLens.Tests/StoreTests.cs) | 5: products, saved/retried orders, key conflict, stock rollback and invalid/null carts |
| [PredictionTests](tests/CommerceLens.Tests/PredictionTests.cs) | 2: user-disjoint split, real artifact parity/reload, metrics, missing inputs, staleness and retraining |
| [LiveTrackingTests](tests/CommerceLens.Tests/LiveTrackingTests.cs) | 1 multi-assertion case: five concurrent retries -> one row, CTR and validation/source separation |
| [MigrationTests](tests/CommerceLens.Tests/MigrationTests.cs) | 2: original populated schema upgrade and complete legacy-row preservation in both directions |
| [ScrapeTests](tests/CommerceLens.Tests/ScrapeTests.cs) | 2: matching/missing/extra/changed classification; signed prices and name/category/availability before/after |
| [ScrapeFailureTests](tests/CommerceLens.Tests/ScrapeFailureTests.cs) | 1: actual missing executable -> saved Failed status, no fake records/comparisons |
| [ProductMonitoringTests](tests/CommerceLens.Tests/ProductMonitoringTests.cs) | 1: persisted comparison API fixture with $1.25 delta, six missing rows and unchanged order/advertising/event metrics |

The fixture-based comparison case is not represented as a real scrape. Actual
collection is separately proved by [scraping.spec.ts](tests/e2e/scraping.spec.ts):
mutated DOM name/price/category/availability and removed DOM card, actual
second-browser persistence, filters/history/reload, old URL and mobile layout.

Seven [dashboard cases](tests/e2e/dashboard.spec.ts), three
[monitoring cases](tests/e2e/scraping.spec.ts) and four
[storefront cases](tests/e2e/storefront.spec.ts) cover both requested workflows:
store -> backend -> monitoring -> dashboard, and historical CSV -> ML.NET ->
Prediction Lab. Checkout checks revenue/AOV/top products/categories/trends;
prediction checks the actual response and explicit ML.NET engine.

[playwright.config.ts](playwright.config.ts) runs one worker on **5081/5174**, a
fresh temporary database/model directory per run and no server reuse.
Normal development remains **5080/5173**. Tests/cleanup never reset normal data.

Generated local evidence:
[xUnit output](TestResults/audit-xunit.txt),
[browser output](TestResults/audit-e2e.txt),
[migration check](TestResults/audit-migrations.txt),
[runtime assertions](TestResults/audit-runtime.json),
[Playwright report](playwright-report/index.html).
These are ignored generated outputs; no coverage percentage or CI result is implied.

### Versions used

.NET SDK **10.0.403**; ASP.NET/runtime/EF Core/MVC Testing **10.0.14**;
CsvHelper **34.0.0**; ML.NET **5.0.1**; xunit.v3 **4.0.2**;
Node **26.13.0**, npm **11.20.0**; React **19.3.1**;
TypeScript **7.0.2**, Vite **8.3.7**, Recharts **3.10.1**;
Playwright **1.65.0**, Chromium **158.0.8127.18**, Oxlint **1.93.0**.
No new dependency or testing framework was installed for scope correction.

## 9. Changes Made

- Removed the unrelated controller/service/DTOs/models, client upload/types,
  page/navigation, analysis code/styles, downloadable fixture and feature tests.
- Kept historical migrations unchanged; added model-only retirement and updated
  [EF snapshot](backend/CommerceLens.Api/Migrations/CommerceDbContextModelSnapshot.cs).
- Replaced the old scraper page with
  [ProductMonitoring.tsx](frontend/src/pages/ProductMonitoring.tsx), explicit
  price/missing/changed KPIs, availability values, signed deltas, filters/history.
- Added `/monitoring` and preserved the old `/scraper` bookmark redirect in
  [CommerceApp](frontend/src/CommerceApp.tsx).
- Refocused [DashboardShell](frontend/src/components/DashboardShell.tsx),
  [Overview](frontend/src/pages/Overview.tsx) and
  [Sales analytics](frontend/src/pages/Commerce.tsx) on coherent e-commerce modules.
- Corrected failed/incomplete collection semantics in
  [StorefrontScraperService](backend/CommerceLens.Api/Services/StorefrontScraperService.cs).
- Extended backend/real-browser monitoring and sales assertions; preserved
  advertising, event, checkout and ML core behavior.
- Replaced the obsolete README/report with this scope-aligned documentation and
  fresh runtime evidence. Original CSV and external repositories were untouched.

## 10. Remaining Limitations

1. Local learning application: no authentication, real payments, production
   deployment/operations, billing-grade advertising or fraud defense.
2. ML is real but weak; no accuracy lift or commercial-performance claim.
3. Monitoring compares saved evidence to the current catalog, not historical
   snapshots; only the local owned storefront is allowed.
4. A completed full collection can establish missing products; a failed/partial
   attempt cannot. Collector traffic is explicitly opted out, not classified by
   a sophisticated bot detector.
5. CSV file-hash identity does not establish semantic cross-file event identity.
   There are no original timestamps or spend for historical ROI/trend analysis.
6. Legacy unmapped tables remain intentionally to preserve data/history. There
   is no automatic archive purge.
7. Browser coverage is Chromium desktop/resized-mobile, not all browsers or full
   accessibility certification. Cart state is in memory.
8. No soak/load/concurrent multi-instance testing, model-retention policy,
   cross-validation or production monitoring is claimed.
9. Restart the older normal API before demonstrating new UI/contracts. The
   original running process was deliberately preserved rather than silently killed.

## 11. Interview Presentation

### 30-second pitch

> CommerceLens is an e-commerce analytics dashboard built with ASP.NET Core,
> EF Core/SQLite and React. The store generates saved demo orders and live ad
> events, Playwright monitors rendered product information, and ML.NET estimates
> advertising click likelihood. I focused on correct source boundaries, exact
> order totals, idempotent imports/events and real end-to-end tests. Its 26 .NET
> and 14 browser cases verify the workflows, while the model's weak baseline
> comparison is displayed honestly.

### Two-minute technical explanation

> The system has two independent starting data sources: 10,000 historical ad
> observations and an eight-product demo catalog. I never infer prices or sales
> from user IDs or click labels. CSV imports validate completely and use file
> hashes and source rows, because user IDs repeat.
>
> The React store reads products through the API. Checkout uses server prices,
> saves price/category snapshots and updates stock in a transaction. The sales
> dashboard aggregates those orders for revenue, AOV, categories and trends.
> Visible public ads generate deduplicated impressions/clicks, separate from
> historical CTR.
>
> Product Monitoring launches a real Playwright browser, extracts rendered text
> and sends JSON to .NET for validation/persistence. It detects price and product
> information differences against the current catalog. A failed attempt cannot
> establish missing products. A DOM-mutation test proves the extractor does not
> simply return product API values.
>
> ML.NET fits a four-category logistic regression using a user-disjoint split.
> It reports real probabilities, but accuracy equals its majority baseline and
> AUC is only 0.527. Collected products/orders never enter that training dataset.
> I applied scraping and ML concepts from earlier work without making this
> application depend on unrelated portfolio functionality.

### Five challenging questions

**Why is user ID not the observation primary key?**
There are 4,000 distinct IDs among 10,000 rows. A generated observation key and
import/source-row identity preserve repeated observations without corrupting CTR.

**How do you prove collection is rendered DOM, not an API shortcut?**
The test changes visible name, price, category and availability while the API
stays unchanged; extraction follows the DOM. Removing a card reduces collected
records. A separate real browser run persists evidence through .NET.

**Why is 69.42% accuracy not commercially successful?**
The model predicts click for every held-out row, and 69.42% of those labels are
click. It ties the training-selected majority baseline; AUC 0.527 is weak ranking.

**How do retries avoid duplicate orders/events?**
Checkout uses a unique key and canonical cart hash; same-cart retries return the
existing order, changed-cart reuse conflicts. Event identities are coalesced in
the client and unique in SQLite. Transactions protect order/stock consistency.

**How did you remove models without deleting existing data?**
I kept old migrations, removed active mappings, and added a no-op retirement
migration with a core-only snapshot. Populated-row tests prove both directions
preserve every column. No current endpoint reads or exposes those archived tables.

### Key lessons

Typed models/DTOs, appropriate DI lifetimes, relational keys/constraints,
SQL-translatable LINQ, integer-cent money, transactions/idempotency, async I/O,
explicit errors, observable browser assertions and honest model evaluation
support one understandable application. Adding unrelated features or advanced
patterns would not make that engineering stronger.

## 12. Final Scores

Scores assess the stated local/intermediate project, not production readiness.

| Area | Score | Justification |
|---|---:|---|
| ASP.NET Core | 8/10 | Connected services/controllers, validation/errors and real integration tests; local-only operating model |
| C# quality | 8/10 | Typed contracts, ordinary LINQ/async, restrained design; string statuses and some larger service methods remain |
| Database | 8/10 | Correct observation identity, exact money, snapshots/transactions and proven preservation; no accounting/retention platform |
| React | 7.5/10 | Cohesive navigation, connected states/refresh and tested layouts; small labels and incomplete nonvisual chart coverage |
| Dashboard analytics | 8/10 | Clear sales/history/live/monitoring boundaries and relevant filters; no attribution/spend/business-wide dataset |
| Product monitoring | 8/10 | Proven DOM extraction, persistence, price/availability comparisons and honest failures/history; local/current-catalog scope |
| ML | 6.5/10 | Genuine leakage-conscious pipeline/artifact parity; weak predictive value and limited evaluation |
| Testing/reliability | 8/10 | 26 core .NET + 14 browser cases and preservation assertions; not exhaustive coverage/load/cross-browser/CI evidence |
| Portfolio readiness | 8/10 | One technically defensible project with clear demonstrations and limitations, rather than unrelated integrations |
| Overall | 7.8/10 | A coherent, verified educational e-commerce analytics system; not a production platform or validated commercial predictor |
