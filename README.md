# CommerceLens

**E-Commerce Analytics Dashboard**

A cohesive, local full-stack C# learning project: sales analytics, advertising
analytics, product monitoring, a dataset explorer and an ML.NET Prediction Lab.
The React store generates demo orders and application events; the dashboard is
the main product.

Stack: ASP.NET Core 10, C#, EF Core/SQLite, React/TypeScript/Vite, Recharts,
Playwright and ML.NET logistic regression. No microservices, CQRS or real payments.

See [PROJECT_AUDIT_AND_EVALUATION.md](PROJECT_AUDIT_AND_EVALUATION.md) for the
architecture, source-backed feature matrix, C# explanations, measured model
limitations and current test evidence.

**Verified:** 26 xUnit cases and 14 real Chromium end-to-end tests pass, along
with .NET/React builds, TypeScript, lint and migration consistency checks.

## Project scope

| Module | What it provides |
|---|---|
| Overview | Source-aware sales, historical advertising, live events and pipeline status |
| Sales analytics | Demo revenue/orders/AOV, units, top products, categories and UTC sales trends |
| Advertising analytics | Historical CTR, device/position/context breakdowns, missingness and filters; separate live impressions/clicks/CTR on overview |
| Product monitoring | Real rendered product collection, catalog comparisons, signed price deltas, changed availability, missing products and collection history |
| Prediction Lab | Four selectable ad attributes, real ML.NET probability, baseline, confusion matrix, evaluation and retraining |
| Dataset explorer | CSV imports, validation/history, filtering, sorting, pagination and column statistics |

[ARACHNE](https://github.com/abdoabozena7/arachne-facebook-crawler) and
[AdClick Prediction System](https://github.com/abdoabozena7/AdClick-Prediction-System)
are **references to learned engineering concepts**, not required integrations.
CommerceLens applies browser lifecycle/DOM extraction and the ML workflow in its
own e-commerce domain. Neither repository is modified; no original Python model
is loaded. The out-of-scope social functionality has been removed.

## Run locally - Windows / PowerShell

Prerequisites: .NET **10.0.403 or a compatible later 10.0.4xx SDK**, and Node
**22.18+**; verified here with Node **26.13.0**, npm **11.20.0**.

If using this machine's existing portable Node:

```powershell
$env:PATH = "$PWD\.tools\node;$env:PATH"
```

Use the system Node installation instead on a fresh machine. Initial setup:

```powershell
dotnet tool restore
dotnet restore CommerceLens.sln
npm.cmd ci --no-audit --no-fund
npx.cmd playwright install chromium
```

API terminal, with Node available on PATH for collection:

```powershell
$env:PATH = "$PWD\.tools\node;$env:PATH"
dotnet run --project backend\CommerceLens.Api --no-launch-profile
```

Frontend terminal:

```powershell
$env:PATH = "$PWD\.tools\node;$env:PATH"
npm.cmd run dev
```

- Dashboard: **http://127.0.0.1:5173/**
- Demo store: **http://127.0.0.1:5173/store**
- Product monitoring: **http://127.0.0.1:5173/monitoring**
- API: **http://127.0.0.1:5080/api/health**

Ports are fixed. Stop your own conflicting process normally rather than switching
ports silently. An already-running older API must be stopped and restarted to
load the changes; **do not delete the database or model files**.
The old `/scraper` bookmark redirects to `/monitoring`.

Use `npm.cmd`/`npx.cmd` where PowerShell blocks `.ps1` execution. No execution
policy or TLS verification was weakened. The existing NuGet configuration uses
Microsoft's public mirror; the existing npm lockfile uses a reachable registry
mirror with integrity hashes.

## Data sources must stay separate

| Source | Stored or returned data | Meaning |
|---|---|---|
| `HistoricalCsv` | Imported observations, original features/label, source hash/row/import time | Historical CTR, not orders or purchases |
| `DemoCatalog` | Eight independent seeded products, prices and stock | Store catalog, never inferred from advertising users |
| `LiveApplication` | Timestamped detail views, visible ad impressions and ad clicks | Actual interactions with the local application |
| `LocalDemoCheckout` | Orders and price/category snapshots | Saved demo transactions; no real money is charged |
| `RenderedStorefront` | Browser-extracted product information, source URL, time and run status | Product monitoring evidence, not sales or ML training rows |
| `ModelPrediction` | ML.NET probability, model time/staleness and engine label | Estimate, not a guaranteed click, purchase or revenue forecast |

Startup applies migrations, imports the original CSV idempotently, seeds the
catalog if empty, and loads or trains the model. Default SQLite/model persistence
is under `backend\CommerceLens.Api\App_Data`. No orders are seeded.

### Original advertising dataset

The untouched [CSV](dataset/ad_click_dataset.csv) has:

- SHA-256: `0C7A029BB57F281BC5C8FCE6940C75B54397F75749BF9A2D5CC1F6151113E78F`
- **10,000 observations / 6,500 positive labels / 65% historical CTR**.
- 4,000 distinct supplied user IDs; 500 repeat. Generated observation keys and
  unique `(ImportBatchId, SourceRow)` identify observations, not the user ID.
- Missing cells: age 4,766; gender 4,693; device type 2,000; ad position 2,000;
  browsing history 4,782; time of day 2,000.
- No original event dates, purchases, prices or ad spend. Import timestamps are
  not original advertising timestamps; no revenue/ROI is invented from clicks.

CSV uploads use the exact ordered nine-column schema, up to 20 MB / 100,000 rows.
Validation finishes before observations are committed; rejected reports persist.
Exact byte hashes prevent reimporting a renamed copy. A different file is a new
batch; repeated users cannot be used for cross-file event deduplication.

### Live advertising

An impression means **at least 50% of a public ad button intersects the viewport
in a foreground tab**, with no dwell-time minimum. Each product/event type is
recorded once per page visit. A client map coalesces requests and a unique
database key makes retries safe. Filtering/cart rerenders do not repeat events;
a new visit or reload can.

Ad clicks first ensure their impression. Product views mean loaded detail pages,
including direct links. `Live CTR = recorded clicks / recorded impressions`;
the API returns a fraction and the dashboard displays a percentage.
Zero impressions means **null / N/A**. Legacy unpaired clicks are preserved and
can exceed newer impression counts. These are demo metrics, not ad billing.

### Product Monitoring & Data Collection

1. The dashboard calls `POST /api/scrapes`.
2. .NET launches the independent TypeScript/Playwright collector.
3. Chromium visits the real React storefront at `/store?collection=1`.
4. The adapter reads **rendered DOM text**: name, price, category, availability
   and optional public ad text. It does not substitute product API payloads.
5. .NET validates IDs, URLs, UTC times and status, then stores the run/records.
6. The dashboard compares saved records against the **current database catalog**.

Price delta is **collected price minus current catalog price**. Missing means a
catalog product absent from a **completed** collection. Changed products include
price, name, category, availability or public-ad differences; price differences
are a subset, not an additional disjoint total. Filters expose matches, changed,
price, missing and extra records.

History shows the latest 20 attempts and failure details. Failed/incomplete runs
have no comparisons and show **N/A**, not a fabricated missing-product alert.
Later catalog edits can change comparisons: this is not a historical price-trend
model. Collector traffic is excluded from customer metrics.

### Prediction Lab

Workflow: **ingestion -> cleaning -> preprocessing -> train/test split ->
training -> evaluation -> prediction**.

ML.NET one-hot encodes device, position, browsing history and time of day, then
fits L-BFGS logistic regression. Missing categories become Unknown. Distinct users
are split approximately 80/20 using seed 42, with **zero overlapping users**.
Encoders fit training data only. Orders and collected products never enter training.

Measured original-data result:

| Metric | Actual value |
|---|---:|
| Training/test observations | 7,760 / 2,240 |
| Training/test user IDs | 3,200 / 800 |
| Accuracy / majority-class test accuracy | **69.42% / 69.42%** |
| ROC-AUC | **0.5270** |
| Precision / recall | 69.42% / 100% |
| TP / FP / TN / FN | 1,555 / 685 / 0 / 0 |

Accuracy has **no baseline improvement**, and ranking is near chance. This is a
working C# learning pipeline, not commercially validated prediction.
Every prediction is labeled `engine: "ML.NET"`. Additional valid CSV imports mark
the model stale; retraining publishes a new immutable artifact/evaluation.

## Database preservation

All historical migration files remain unchanged. The new
[retirement migration](backend/CommerceLens.Api/Migrations/20261110122234_RetireUnusedFeedbackModel.cs)
removes obsolete models from the current EF snapshot **without dropping their
physical tables or stored rows**. They are unmapped and not exposed by the app.
Fresh installations replay the historical migrations and also retain those empty
unused tables. Do not remove historical migrations or purge stored data to tidy
the schema. Migration tests prove populated legacy rows survive upgrade/rollback.

## API surfaces

- Catalog/detail: `GET /api/products`, `GET /api/products/{id}`
- Demo checkout/retrieval: `POST /api/orders`, `GET /api/orders/{id}`
- Tracking: `POST /api/events` with product/type/event key; impressions require a key
- Analytics: `GET /api/analytics/overview`, `/advertising`, `/commerce`, `/options`
- Explorer/imports: `GET /api/data/rows`, `GET/POST /api/data/imports`
- Product monitoring: `GET/POST /api/scrapes`, `GET /api/scrapes/{id}`
- ML: `GET /api/model/status`, `POST /api/model/train`, `POST /api/predictions`

The API is loopback-only by default. CORS/origin checks are not authentication.
Do not expose this learning app or its SQLite data to the public internet.

## Verification

```powershell
$env:PATH = "$PWD\.tools\node;$env:PATH"
dotnet build CommerceLens.sln --configuration Release --no-restore
dotnet test --project tests\CommerceLens.Tests\CommerceLens.Tests.csproj --configuration Release --no-build
npm.cmd run build
npm.cmd run lint
npm.cmd run test:e2e
dotnet ef migrations has-pending-model-changes --project backend\CommerceLens.Api --configuration Release --no-build
```

The .NET 10 Microsoft Testing Platform runner requires `--project`.
Browser tests start their own Release API/preview on **5081/5174**, use a new
temporary database/model directory per run, refuse existing servers and clean
their own data. Build Release and the frontend first.

Final results: **26 xUnit / 14 Chromium passed**, zero failures/skips; builds,
all TypeScript checks, lint and migration consistency passed.
Tests prove actual DOM mutations affect extraction, exact price/missing
comparisons, collection persistence/history/mobile layout, no collector event
contamination, checkout/stock/revenue/AOV/trends, visible-ad deduplication, CSV
identity/validation, saved ML artifact scoring and data-preserving migrations.

Generated local evidence:
[xUnit log](TestResults/audit-xunit.txt),
[browser log](TestResults/audit-e2e.txt),
[migration check](TestResults/audit-migrations.txt),
[runtime assertions](TestResults/audit-runtime.json),
[Playwright report](playwright-report/index.html).
These outputs are ignored. Temporary verification data is not normal-store
performance; the normal database and old migrations were checksum-verified unchanged.

Configuration overrides: `ConnectionStrings__CommerceLens`,
`CommerceLens__DatasetPath`, `CommerceLens__ModelDirectory`,
`CommerceLens__NodeExecutable`, `CommerceLens__StorefrontUrl`; Vite can use
`COMMERCELENS_API_URL`. Keep configured storefront origins local and consistent.
