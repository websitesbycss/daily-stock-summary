# IMPLEMENTATION_SPEC — daily-stock-summary

> **Source of truth.** Read this first in any new, compacted, or joined session. Also read `CLAUDE.md` (ground rules).
> **Maintenance rules:** when a phase completes, strike its heading through (`~~...~~`), tick its milestones, and set status to `DONE`. If work stops mid-phase, fill in that phase's **Progress Notes** (done / remaining / next step).

## Status Dashboard

| Phase | Scope | Status |
|---|---|---|
| ~~1~~ | ~~Backend foundation: Yahoo client + aggregation (TDD)~~ | DONE |
| ~~2~~ | ~~Backend API hardening: endpoint, validation, caching, resilience, integration tests~~ | DONE |
| ~~3~~ | ~~Frontend foundation: scaffold, API client, toasts, symbol input, table + chart per symbol~~ | DONE |
| ~~4~~ | ~~Frontend dashboard: multi-symbol, draggable/resizable panels, persistence, polish~~ | DONE |
| 5 | Fixes, testing, deployment, deliverables | IN PROGRESS (built and verified locally; Docker, CI and the manual-changes write-up remain) |

Status values: `NOT STARTED` · `IN PROGRESS` · `DONE`

## 1. Goal and Requirements (from Take-Home Assessment.pdf)

Full-stack app that consumes the public Yahoo Finance chart API and displays intraday market data.

**Backend** (.NET 10, self-hosted): endpoint takes a stock symbol, queries the last month of intraday data (15m interval), groups by day, returns JSON in exactly this shape and precision:

```json
[
  { "day": "2009-01-30", "lowAverage": 40.2958, "highAverage": 49.7534, "volume": 49073348 }
]
```

**Frontend** (React): enter a symbol and view results (table and/or chart); basic error handling for invalid symbols and failed requests.

**Deliverables:** GitHub repo, `README.md` (setup/run), prompt log (see note below), description + reasoning of manual (non-AI) changes.
**Expectations:** production quality, SOLID, maintainable, modern; MVP with requirements expected to grow.

**Our extensions (user-requested):** multiple symbols at once; each has a chart view and a table view; dashboard panels rearranged by dragging a top bar; all notifications (invalid symbol, failed request, etc.) as corner toasts.

> **Naming note:** the prompt log file is `PROMPT_LOG.md`, matching the assessment's deliverable name (renamed by the user from `PROMPTS_LOG.md`).

## 2. Data Semantics (decisions to implement and test)

Upstream: `GET https://query1.finance.yahoo.com/v8/finance/chart/{SYMBOL}?interval=15m&range=1mo` with header `User-Agent: Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36`. (`range=1mo` keeps within Yahoo's 15m limit of ~60 days.) No API key is needed; do not add any.

Response shape used: `chart.result[0].timestamp[]` (unix seconds), `chart.result[0].indicators.quote[0].{high[], low[], volume[]}`, `chart.result[0].meta.exchangeTimezoneName` / `gmtoffset`. Errors: `chart.error` (e.g. `{code:"Not Found"}`) with HTTP 404 for unknown symbols.

| Field | Definition |
|---|---|
| `day` | `yyyy-MM-dd` of the bar's timestamp **in the exchange's timezone** (`exchangeTimezoneName`, fallback to `gmtoffset`), not UTC |
| `lowAverage` | arithmetic mean of the `low` values of that day's 15m bars, rounded to **4 decimals** (`MidpointRounding.AwayFromZero`) |
| `highAverage` | arithmetic mean of the `high` values of that day's bars, rounded to **4 decimals** |
| `volume` | **sum** of that day's bar volumes, integer (`long`) |

Rules (as implemented): the parser drops any bar whose `low` or `high` is `null` (Yahoo emits nulls for missing bars), including that bar's volume; a kept bar with `null` `volume` counts as 0; a day with no kept bars does not appear; a response with a missing or empty `timestamp` array yields `[]`; quote arrays whose length differs from `timestamp` are an upstream failure; output sorted by `day` **ascending** (oldest first; **decided by user**); JSON property names camelCase; numbers serialized as JSON numbers (not strings). Symbols are normalized to upper-case. The frontend presents the same data newest-first in the table (default sort `day` descending, most recent day at the top) and oldest-to-newest left-to-right on charts (most recent day at the right edge).

## 3. Architecture

### Repo layout (target)

```
/ (repo root)
├─ CLAUDE.md, IMPLEMENTATION_SPEC.md, PROMPT_LOG.md, README.md
├─ backend/
│  ├─ DailyStockSummary.slnx                # + global.json, Directory.Build.props, Directory.Packages.props, .editorconfig
│  ├─ src/DailyStockSummary.Api/            # Minimal API host, DI, middleware, config (empty stub until Phase 2)
│  ├─ src/DailyStockSummary.Core/           # Domain models, interfaces, aggregation (no I/O)
│  ├─ src/DailyStockSummary.Infrastructure/ # Yahoo client, caching decorator
│  └─ tests/DailyStockSummary.Tests/        # xUnit (unit + integration)
└─ frontend/                                # Vite + React + TypeScript
```

### Backend design (SOLID)

- `IMarketDataProvider` (Core) — `Task<IntradaySeries> GetIntradayAsync(Symbol, ct)`; implemented by `YahooFinanceClient` (typed `HttpClient`). Swappable provider = open/closed.
- `IDailySummaryCalculator` (Core) — pure function: `IntradaySeries -> IReadOnlyList<DailySummary>`. No I/O, fully unit-tested.
- `IStockSummaryService` (Core) — orchestrates provider + calculator.
- `CachingMarketDataProvider` (Infrastructure) — decorator over `IMarketDataProvider` using `IMemoryCache` (short TTL, configurable) to avoid hammering Yahoo.
- `Symbol` value object — validates the trimmed ASCII input, then upper-cases: `^(?=.*[A-Za-z0-9])[A-Za-z0-9.\-^=]{1,15}$` (at least one letter/digit, so `.`/`..` cannot alter the upstream URL path; non-ASCII letters are rejected). **The frontend validation in Phase 3 must mirror this regex.** Note Yahoo's own spelling: Berkshire is `BRK-B` (`BRK.B` is "not found"), while `VOD.L`, `^GSPC`, `EURUSD=X`, `BTC-USD` work as typed.
- Typed exceptions (`InvalidSymbolException`, `SymbolNotFoundException`, `UpstreamUnavailableException`) mapped to RFC 7807 `ProblemDetails` by a global exception handler (`IExceptionHandler`): 400 / 404 / 502 (or 504 on timeout) / 500.
- Resilience: `Microsoft.Extensions.Http.Resilience` (retry w/ jitter, timeout, circuit breaker) on the Yahoo `HttpClient`.
- Options pattern (`YahooOptions`: base URL, user agent, interval, range, timeout, cache TTL); CORS policy from config (allowed origins); `/health` endpoint; structured logging; OpenAPI doc.
- Endpoint: **`GET /api/stocks/{symbol}/daily-summary`** → `200 DailySummary[]`.

### Frontend design

- Vite + React + TypeScript (strict). **TanStack Query** for server state (one query per symbol; caching, retries, error callbacks → toasts). Thin typed API client (`fetch`), base URL from `VITE_API_BASE_URL` (non-secret).
- **Toasts:** `sonner` (or equivalent), anchored to a corner (bottom-right). Every user-facing error/notice goes through one `notify` helper — no inline error banners.
- **Charts:** Recharts (high/low average as lines/area, volume as bars on a secondary axis). Accessible colors, light/dark friendly.
- **Dashboard:** `react-grid-layout` (or `dnd-kit` if it fits better) — each symbol is a panel with a **drag-handle title bar**, resizable, with fluid reflow. Panel has a **Chart / Table toggle**, refresh, and remove actions. Layout and symbol list persisted to `localStorage` (guarded with try/catch).
- Symbol input: add multiple symbols, normalize to upper-case, ignore duplicates, client-side format check mirroring the backend regex.
- Table: sortable columns, **default sort newest day first (top)**, number formatting (prices to 4 dp, volume grouped), keyboard accessible.
- States: loading skeletons, empty state, per-panel error state with retry.
- Skill usage: backend uses `test-driven-development` (red → green → refactor). **Frontend does not use the TDD skill**; add focused component/hook tests with Vitest + React Testing Library after implementation.

## 4. Environment Prerequisites

- .NET SDK 10.0.401 installed and verified (`global.json` pins `10.0.401` with `rollForward: latestFeature`). In a freshly opened terminal `dotnet` may need a new session to appear on PATH.
- Node.js 24.x present (v24.14.0 at spec creation).
- `.gitignore` now covers `.env.local`, `bin/`, `obj/`, `node_modules/`, `dist/` (fixed by the user). IDE folders (`.vs/`, `.idea/`) and `coverage/` are optional additions.
- Tooling quirk: a very large inline heredoc in the Bash tool can fail to parse; write scripts to a file (scratchpad) and run them instead.

---

## ~~Phase 1 — Backend Foundation (TDD)~~

**Goal:** a tested core that turns Yahoo intraday data into the exact daily-summary JSON, independent of HTTP hosting concerns.
**Status:** DONE (2026-10-04)

Tasks
1. Install/verify .NET 10 SDK; create solution + projects per layout (Api, Core, Infrastructure, Tests); central package management and `Directory.Build.props` (nullable on, warnings-as-errors, analyzers).
2. **TDD** (use `test-driven-development` skill): `Symbol` value object (valid/invalid/normalization).
3. **TDD:** `DailySummaryCalculator` — averages, 4-dp rounding, volume sum, timezone-correct day bucketing (incl. a bar near midnight UTC that belongs to a different exchange day), null-bar skipping, empty input, single bar, sort order.
4. **TDD:** Yahoo response DTO parsing + mapping to `IntradaySeries` (fixture JSON files from real responses stored under `tests/.../Fixtures/`; no network in tests).
5. **TDD:** `YahooFinanceClient` using a fake `HttpMessageHandler`: sends required `User-Agent`, correct URL/query, maps 404/`chart.error` → `SymbolNotFoundException`, non-2xx/timeout/malformed → `UpstreamUnavailableException`.
6. `StockSummaryService` wiring provider + calculator (TDD with fake provider).

Milestones (all must pass before Phase 2)
- [x] `dotnet build` clean with zero warnings; `dotnet test` green (64 tests after the test-quality review).
- [x] Calculator tests prove exact output for a hand-computed fixture (values to 4 dp, volume as integer).
- [x] Timezone bucketing test passes for a non-UTC exchange (Tokyo) and for DST-sensitive New York instants.
- [x] Client tests cover success, unknown symbol, 5xx, timeout, caller cancellation, malformed JSON.
- [x] One manual smoke check (throwaway console app in the scratchpad, not in the repo) against live Yahoo: `TSLA` and `^GSPC` returned 21 days, `lowAverage < highAverage` on every day; `BRK-B`, `VOD.L`, `EURUSD=X`, `BTC-USD` also work; `ZZZZZZZZ` → `SymbolNotFoundException`.

**Progress Notes:**
- Built per the approved plan: `Symbol`, `DailySummaryCalculator`, Yahoo DTOs + `YahooChartParser` (internal, tested via `InternalsVisibleTo`), `YahooFinanceClient`, `YahooOptions`, `StockSummaryService`; all test-first (red verified, then green).
- Real-data fixture `tests/.../Fixtures/tsla-15m-two-days.json` (2 trading days sliced from a live response); expected daily values were computed independently in Node, not by the code under test.
- Deviations from the original spec: solution file is `.slnx`; null-bar handling lives in the parser (calculator only sees complete bars); `Api` project is an empty stub (host is Phase 2); `Microsoft.Extensions.Http` / logging packages deferred to Phase 2 (not needed yet); `CA1707` (underscores in names) is disabled for `tests/` only via `tests/.editorconfig`.
- Review pass (pr-review-toolkit code-reviewer, silent-failure-hunter, pr-test-analyzer) found and we fixed: dot-only symbols (`.`/`..`) altering the upstream URL, non-ASCII letters normalizing into valid symbols, `"result":[null]` crashing, any HTTP 404 being reported as "symbol not found". Added tests for those plus token forwarding, 5xx-with-valid-body, bad `gmtoffset`, out-of-range timestamps, longer-than-timestamps arrays, empty `timestamp`, DST bucketing, digit symbols, default options. A mutation check (6 mutations: token dropped, status check removed, DST ignored, length check loosened, digits removed from regex, empty-timestamp boundary) was caught by the suite each time.
- Deliberate decision kept: a response with a missing/empty `timestamp` yields `[]` (a valid but illiquid symbol may legitimately have no bars) rather than a 502.
- Test-quality review (user request, same day): removed coverage-driven or fabricated tests (impossible `gmtoffset`/timestamp values, 5xx-with-valid-body, a stub-rethrows-what-it-is-told cancellation test, a record-equality test, mock-call assertions) and the two defensive branches that only they covered. Strengthened: fallback timezone now tested with a real half-hour offset (India, 19800s), timeouts use a real `HttpClient` timeout, service tests check behavior (right symbol's data, cancellation) not recorded calls. Added `Pipeline/RealDataPipelineTests`: a full real month of TSLA (547 bars, `Fixtures/tsla-15m-one-month.json`) through the real client, parser, calculator and service, asserted against values computed independently in Node, including the Labor Day gap, no weekend days, a 27-bar day (Yahoo appends the closing print to the latest day) and total volume. The two-day fixture was replaced by this single real fixture. Suite is now 64 tests; a 13-mutation check was caught every time. Shared helpers live in `tests/.../TestSupport/` (`StubHttpMessageHandler`, `TestFixtures`).
- Test rule going forward: expected values come from independent derivation or real captured data; no assertions that only check non-emptiness or that a mock was called; no tests for inputs the real world cannot produce; every test names a real scenario (a user symbol, a Yahoo response, an operational failure).
- Carried forward (not done in Phase 1, see Phase 2/5 tasks): logging when the `gmtoffset` timezone fallback is used; Polly exception mapping; options validation; tzdata in Docker; sanity filters for garbage bars.

---

## ~~Phase 2 — Backend API & Hardening~~

**Goal:** a production-grade HTTP API exposing the service, runnable locally, covered by integration tests.
**Status:** DONE (2026-10-04)

Tasks
1. Minimal API host: `GET /api/stocks/{symbol}/daily-summary`, `GET /health`, DI registrations, options binding + validation on start.
2. Global `IExceptionHandler` → `ProblemDetails` (400/404/502/504/500) with stable `type`/`title` and a correlation/trace id; no stack traces or upstream bodies leaked.
3. `CachingMarketDataProvider` decorator (configurable TTL; cache key = normalized symbol; do not cache failures).
4. Resilience pipeline on the Yahoo `HttpClient` (retry w/ jitter on transient errors, timeout, circuit breaker).
5. CORS (config-driven origins; default `http://localhost:5173`), rate limiting (per-IP fixed window), ~~response compression~~ (dropped by decision: payload is ~2 KB; nginx can compress in Phase 5), OpenAPI JSON document in Development only (~~viewer~~ dropped by decision: no UI package), structured logging.
6. **TDD** with `WebApplicationFactory`: integration tests replacing `IMarketDataProvider` with a fake.
7. **Carried over from the Phase 1 review:**
   - Register the Yahoo client with `AddHttpClient` (needs `Microsoft.Extensions.Http`), set a sane `MaxResponseContentBufferSize` (a few MB; default is 2 GB) and validate `YahooOptions` on start (base URL ends with `/`, non-empty interval/range, valid user agent: `ParseAdd` throws `FormatException` on a bad one).
   - Resilience handlers surface Polly exceptions (e.g. `TimeoutRejectedException`, `BrokenCircuitException`) instead of `HttpRequestException`/`TaskCanceledException`; verify actual behavior and map them in the client (timeout → a distinguishable timeout failure so the handler can return 504; open circuit → 502/503). Today `UpstreamUnavailableException` cannot distinguish timeout from other failures.
   - Add structured logging (inject `ILogger`): warn when the `gmtoffset` timezone fallback is used, when bars are dropped, and for every upstream failure (the exception handler logs details but returns only fixed titles; never echo `ex.Message` of `UpstreamUnavailableException` or upstream bodies in `ProblemDetails`).
   - The exception handler must treat `OperationCanceledException` with `HttpContext.RequestAborted` set as a quiet client abort, not a 500, and `Symbol.Parse` must run inside the request pipeline so `InvalidSymbolException` → 400.
   - Consider sanity filters in the parser for garbage bars (`low > high`, non-positive prices, negative volume, duplicate timestamps); decide behavior and test it.

Milestones
- [x] `dotnet run` serves the API (`http://localhost:5241` with `--no-launch-profile --urls`); `curl .../api/stocks/TSLA/daily-summary`, `BTC-USD` and `^GSPC` return the exact JSON shape from live Yahoo.
- [x] `/api/stocks/!!!/daily-summary` → 400 ProblemDetails; `ZZZZZZZZ` → 404 ProblemDetails; simulated upstream failure → 502, stalled upstream → 504, rate limit → 429; no stack traces or upstream text in any body.
- [x] Second request for the same symbol within the TTL does not call upstream (live: 1.3 s then 5 ms; tests assert upstream call counts, expiry via `FakeTimeProvider`, and the TTL setting through real DI).
- [x] CORS preflight from the Vite origin succeeds; unlisted origin is refused; error responses (400/404/502/429) still carry the CORS header.
- [x] Test suite green (178 tests); `dotnet build --no-incremental` zero warnings; `dotnet list package --vulnerable` clean.
- [x] `/health` returns 200 without touching Yahoo; OpenAPI document served in Development only.

**Progress Notes:**
- Built per the approved plan, test-first (red verified, then green): `YahooOptions` + `YahooOptionsValidator` (fail-fast at startup), `UpstreamFailureKind` (502 vs 504), Polly pipeline (total timeout → retry → circuit breaker → per-attempt timeout; retries 5xx/408/network/timeouts, never 404/429; `RetryCount = 0` skips the strategy because Polly rejects zero), `CachingMarketDataProvider`, `AddStockSummary` DI extension, `GlobalExceptionHandler` + `Problems` (stable `urn:daily-stock-summary:problem:*` types), `StocksEndpoints`, CORS, per-client rate limiting (IPv4-mapped IPv6 normalized), `/health`, OpenAPI (Development only), structured logging (parser and client warnings, handler logs by severity; JSON console outside Development).
- Test approach: `WebApplicationFactory<Program>` with only Yahoo's `HttpMessageHandler` replaced (`TestSupport/ApiTestApp`), so every API test runs the real stack: routing, validation, rate limiter, CORS, cache, resilience, client, parser, calculator. Added a second real fixture (`btc-usd-15m-one-month.json`, 24-hour UTC market: a 1-bar first day and a 97-bar last day) with independently computed expectations.
- Review pass (code-reviewer, silent-failure-hunter, pr-test-analyzer) findings, fixed test-first: cache replaced `MemoryCache` with a small `TimeProvider`-based store (silent refusal at the size limit, two clocks, single-flight lost when the TTL passed mid-fetch, empty glitch responses cached for a minute); two cache tests that could not fail for their stated reason were rewritten (token-observing upstream, truly concurrent callers with a start delay); IPv4-mapped clients got a second rate-limit allowance; no log trail for non-Yahoo 404s/429s or empty responses; `Yahoo:Interval`/`Range` values like `banana` passed startup; null `BaseUrl` crashed the validator. Reviewers suspected CORS headers were lost on error responses; a test proved they are not (verified for 400/404/502/429).
- Mutation checks: 20 new mutations (cache lock, token forwarding, empty/failed caching, eviction order, TTL binding, CORS, rate-limit keys, handler log levels, validator, resilience wiring) plus the earlier set were all caught. One equivalent mutant remains: removing `ValidateOnStart` for the CORS settings still fails at startup because the CORS middleware resolves the options while the pipeline is built.
- Decisions: no response compression; OpenAPI document only; `traceId` in problem bodies comes from ASP.NET's defaults (Activity id or `TraceIdentifier`); an empty-but-valid response yields `200 []` but is never cached; a response with absurd timestamps or `gmtoffset` is allowed to surface as a 500 (the real world does not send them; no test or code for it by design).
- Known and accepted (not done): the shared fetch keeps running if every waiting caller disconnects (bounded by `TotalTimeout` and the rate limiter); cache expiry counts from fetch start; `OperationCanceledException` not from the caller's token is reported as a timeout; `HEAD` is not routed (405).
- Carried to Phase 5: forwarded-headers handling when deployed behind a reverse proxy (rate limiting keys on the client address, which would otherwise be the proxy's), tzdata in the container image, an `Origin`-less/same-origin CORS setup if the frontend is served behind the same host.

---

## ~~Phase 3 — Frontend Foundation~~

**Goal:** a working single-symbol experience wired to the real backend, with toast-based error handling.
**Status:** DONE (2026-10-04)

Tasks
1. Scaffold Vite + React + TS (strict) in `frontend/`; ESLint (typescript-eslint, react-hooks), Prettier; `VITE_API_BASE_URL` in `.env.example` (no secrets); dev proxy or CORS to the backend.
2. Typed API client + `DailySummary` types; TanStack Query provider; error normalization (ProblemDetails → user-friendly message).
3. Toast system in a corner (`sonner`), single `notify` helper (success/info/error), used for invalid symbol, 404, network failure, 5xx, rate-limit.
4. Symbol input form (validation, upper-casing, Enter to submit, disabled while pending).
5. Table view and chart view for one symbol (components reusable by Phase 4 panels).
6. Loading skeleton, empty state, error state with retry.

Milestones
- [x] `npm run lint` and `npm run build` pass with zero errors/warnings.
- [x] Entering `TSLA` shows a table and a chart matching the backend JSON (spot-check 2+ days against `curl`).
- [x] Invalid symbol (`!!!`) → client-side toast, no request sent; unknown symbol (`ZZZZZZZZ`) → 404 toast; backend stopped → network-failure toast. Toasts appear in a corner and auto-dismiss.
- [x] Table defaults to newest day at the top and columns sort; chart x-axis runs oldest → newest (latest day at the right); numbers formatted (4 dp prices, grouped volume).
- [x] Basic Vitest tests for API client error mapping and symbol validation pass.

**Progress Notes:**
- Built per the approved plan in `frontend/`: Vite 8 + React 19 + TypeScript (strict), TanStack Query, sonner (bottom-right, follows system theme), Recharts, ESLint (typescript-eslint + react-hooks, replacing the template's oxlint) + Prettier, Vitest + React Testing Library. `VITE_API_BASE_URL` documented in `.env.example` (`.env.local` is git-ignored). No dev proxy: the API's CORS allows the Vite origin.
- Layout: `api/` (types, `ApiError` mapping by ProblemDetails `type` slug with HTTP-status fallback, fetch client), `lib/` (symbol regex mirror, date, number formatting), `notify.ts` (the only module that raises toasts; `describeApiError` also feeds the panel error state), `queries/` (one query per symbol; toasts fire from the QueryCache `onError`, once per failed fetch), `components/` (`SymbolForm`, `StockView` with Chart/Table toggle, `SummaryTable`, `SummaryChart`, `Notifications`). `StockView` (replaced by `Panel` in Phase 4) took only a `symbol`.
- Date rule (user decision): the frontend never converts time zones and never calls `new Date('yyyy-MM-dd')` (UTC midnight, shows the previous day in the US). `lib/date.ts` works on numeric year/month/day parts; table, chart axis and tooltip share it. Vitest runs under `America/Los_Angeles` (set in `vite.config.ts`) so a regression shows up.
- Design (frontend-design skill, user chose clean editorial light): cool paper ground, Newsreader (display) + Hanken Grotesk (UI), the symbol field as the large serif centerpiece, no cards or shadows; blue/amber lines for high/low (colour-blind safe), pale slate volume bars; dark mode via `prefers-color-scheme` tokens.
- Verified: `npm run lint`, `npm run build`, `npm test` (51 tests) clean, `npm audit` 0 vulnerabilities. Driven in headless Chrome against the live backend: `!!!` toast with no request; `ZZZZZZZZ` 404 toast + panel retry; TSLA chart (21 days, Sep 3 to Oct 2, latest right) and table (newest first, 4 dp prices, grouped volume) match the API JSON; browser-offline gives the network toast; toaster at bottom-right; no horizontal scroll at 390 px.
- Found while verifying: TanStack Query's default `networkMode: 'online'` paused fetches when the browser was offline (skeleton forever, no toast); set `networkMode: 'always'`.
- Carried forward, resolved in Phase 4: the 670 KB single chunk (the chart is now lazy-loaded) and the missing component tests for panel states.


---

## ~~Phase 4 — Frontend Dashboard (multi-symbol, draggable)~~

**Goal:** the dashboard experience: many symbols, each with chart + table views, freely rearrangeable.
**Status:** DONE (2026-10-05)

Tasks
1. Dashboard state (symbols + layout) via a reducer/store; persisted to `localStorage` with safe fallback.
2. Panel component per symbol: **drag-handle top bar** (symbol title), Chart/Table toggle, refresh, remove, resize handle.
3. Grid layout with fluid drag-to-rearrange and responsive breakpoints; new panels placed in the first free slot.
4. Duplicate-symbol handling (toast + focus/scroll to the existing panel); per-panel loading/error isolation (one failing symbol never affects others).
5. Visual polish via the `frontend-design` skill: typography, color tokens, light/dark, keyboard + screen-reader accessibility (drag handle keyboard operable, aria-labels, focus management).
6. Charts: shared tooltip styling, readable axes at small panel sizes, volume on secondary axis.

Milestones
- [x] Add 3+ symbols (e.g. `TSLA`, `AAPL`, `MSFT`); each panel independently toggles Chart ↔ Table.
- [x] Dragging a panel by its top bar rearranges the grid smoothly; resize works; layout survives page refresh.
- [x] One invalid/failed symbol shows a toast and an error state in its panel only; others unaffected; retry works.
- [x] Usable at phone width (no horizontal page scroll) and desktop; lighthouse/axe accessibility check has no critical issues.
- [x] `npm run lint` + `npm run build` clean; component tests for panel actions and layout persistence pass.

**Progress Notes:**
- Built per the approved plan. Grid: `react-grid-layout` v2 (`Responsive`, drag handle = the panel top bar, buttons excluded, `se` resize handle, minimum panel size 3 columns x 9 rows). Breakpoints by container width: lg >900 px (12 columns, 2 panels per row), md >600 px (8 columns, 2 per row), below that 1 column. Limits: 12 panels (keeps within the backend's 60 requests/minute).
- State is a pure reducer (`dashboard/dashboardReducer.ts`: add, remove, setView, layoutChange, move) with layout math in `dashboard/layout.ts` (first free slot, reading order, `breakpointForWidth`) and persistence in `dashboard/storage.ts` (key `dss.dashboard.v1`, validated on load; unusable data is copied to `dss.dashboard.v1.bak` and the user is told once; a failed save is reported once). Panels render in reading order so Tab and screen readers follow what is on screen.
- Layout is saved only from the grid's `onDragStop`/`onResizeStop`, for the breakpoint we pass to the grid (derived from the container width). The grid's `onLayoutChange` is deliberately not used: during unusual viewport jumps it reports layouts it recomputes itself, and a one-column layout ended up stored under `lg` (reproduced with a full-page browser capture, not with ordinary window resizing, which was checked at 1280/800/500/620/1000 px).
- Keyboard: the grid cannot be dragged by keyboard, so each panel has Move earlier / Move later buttons (`aria-disabled`, so focus stays on the button), refresh and remove; a polite live region announces moves, additions and removals. After a removal focus returns to the symbol field. A duplicate symbol shows a toast, scrolls to and flashes the existing panel, and sends no request.
- Resilience (from a review pass: code-reviewer, silent-failure-hunter, pr-test-analyzer): error boundary around each panel view (reset by switching view) and one around the app; a failed lazy chart chunk or render error no longer blanks the page; requests time out after 20 s (`timeout` error kind); a 200 that is not an array is an `unexpected` error; 503 and 408 are treated as transient; failures are logged to the console with the status, backend `traceId` and cause; a failed refresh keeps showing the data already loaded (the toast reports it).
- Fixes found while testing in the browser: a Recharts "Maximum update depth" crash caused by inline object props (hoisted to constants in `SummaryChart`); the Chart/Table toggle losing its base styles (caught by axe in dark mode); focus lost when a move button became disabled; TanStack Query pausing fetches while the browser is offline (`networkMode: 'always'`).
- Carry-forward from Phase 3 resolved: the chart is lazy-loaded (main bundle 374 kB, chart chunk 375 kB, no size warning); component tests now cover the panel states, retries, refresh, persistence and reordering.
- Verified: `npm run lint`, `npm run build`, `npm test` (175 tests), `npm audit` (0 vulnerabilities), prettier clean. A mutation spot-check of 7 behaviours (breakpoint, DOM order, error-over-data, array check, slug mapping, duplicate layout items, save-failure notice) is caught by the suite. Driven in headless Chrome against the live backend in light and dark: add TSLA/AAPL/MSFT, per-panel Chart/Table, drag by the title bar, resize (552x582 to 741x674), reload restores order/size/views, one failing symbol isolated with Try again, duplicate handling, keyboard move, removal, axe (no violations), 390 px phone width without horizontal scroll.
- Known limits: jsdom has no layout engine, so the grid is stubbed in the component tests (drag/resize/reflow are verified in the browser only); rapid repeated browser runs hit the backend's rate limit (429), which is expected; in React StrictMode (dev only) each symbol is fetched twice, the first request being cancelled.

---

## Phase 5 — Fixes, Testing, Deployment

**Goal:** release-quality repo a reviewer can clone, run, and trust.
**Status:** IN PROGRESS (2026-10-05)

Tasks
1. Full bug sweep from manual end-to-end testing; run `pr-review-toolkit` review passes and `/code-review`; fix findings.
2. Test hardening: backend coverage review (edge cases: holidays/weekends, symbols with `.`/`^`/`=`, thin-volume days, Yahoo schema drift); frontend tests for critical paths; an end-to-end smoke (Playwright, optional) for add symbol → toggle view → drag.
3. Security pass: no secrets in repo or logs, dependency audit (`dotnet list package --vulnerable`, `npm audit`), input validation review, security headers, CORS locked to configured origins.
4. Deployment (behind a reverse proxy, add `UseForwardedHeaders` with known proxies before the rate limiter so limits apply per real client): multi-stage Dockerfiles (ensure the runtime image has tzdata/ICU so IANA exchange timezones resolve; otherwise bucketing silently falls back to a fixed offset, so add a startup check; API: `dotnet publish` on the ASP.NET 10 runtime image, non-root; frontend: static build served by nginx), `docker-compose.yml` running both, config via environment variables, CI workflow (GitHub Actions: backend build+test, frontend lint+build+test).
5. Deliverables: `README.md` (prereqs, run backend, run frontend, run tests, Docker, API reference, design decisions, future work); finalize `PROMPT_LOG.md`; document manual (non-AI) changes and reasoning (user-authored section).
6. `.gitignore` finalized (`.env.local`, `bin/`, `obj/`, `node_modules/`, `dist/`, IDE files).

Milestones
- [x] README-only run of both services works locally: the documented commands (`dotnet run`, `npm ci` + `npm run dev`) were run and the full flow verified in a browser. (Not done from a fresh clone.)
- [ ] `docker compose up` brings up both services and the UI works against the containerized API. **Not verified: Docker is not installed on the authoring machine.** The files are written and statically reviewed; the CI `containers` job builds both images and smoke-tests them through nginx on the first push.
- [ ] CI green (backend tests, frontend lint/build/tests, container smoke test). **Workflow written and its YAML validated; it has not run yet** (nothing has been pushed).
- [x] `dotnet list package --vulnerable --include-transitive` and `npm audit`: no vulnerabilities. Repo scanned for secrets: none.
- [ ] Prompt log complete; manual-changes write-up present. `PROMPT_LOG.md` has every prompt, but the Reasoning/Adjustments fields and `manual-changes.txt` (empty) are the author's to fill in.

**Progress Notes:**
- Done, backend (test-first; 224 to 229 tests): `ForwardedHeaders` settings (`Enabled` default false, CIDR `KnownNetworks`, `ForwardLimit`) validated at startup and wired before the rate limiter, trusting only the listed networks (the framework's default loopback trust is cleared) and reading only the nearest proxy hop; `TimeZoneDataStartupCheck` (host fails to start when `America/New_York`, `Europe/London` or `Asia/Tokyo` is missing); security headers on every API response including errors (set in `OnStarting` so the exception handler cannot strip them); IPv6 clients rate-limited per /64 (found in review: rotating low address bits earned a fresh allowance).
- Decision (sanity filters, carried from Phase 1): the parser now drops bars with a non-positive price, a low above the high, or a negative volume, and counts them in the existing dropped-bars warning. Negative volume silently corrupted day totals; zero-price bars occur on thin FX/illiquid instruments. Flat bars and zero-volume bars are real and kept. Duplicate or unsorted timestamps are left as they are (harmless to averages). A bar with a null low/high but real volume still loses its volume (deliberate, documented in the README).
- Done, tests from the backend audit: schema drift (null/missing `indicators`, `quote`, series, `result: [null]`, unknown extra fields, decimal volume = 502), HTTP-level 200 + `chart.error` (404) and 200 + empty result (502), raw and escaped route forms for `^GSPC`, `EURUSD=X`, `VOD.L`, `BRK-B` asserted against the real upstream path, calculator cases (weekend gap, short trading day, New York fall-back day, zero-volume day), and an IPv4-mapped proxy peer. A mutation check of 9 behaviours (UTC bucketing, unescaped symbols, flat-bar filter, negative volume, loopback trust, trusting everyone, forward limit, header timing, tz check) is caught by the suite; two initial survivors (loopback `::1`, `ForwardLimit`) got tests.
- Done, deployment files: `backend/Dockerfile` (multi-stage, non-root), `frontend/Dockerfile` (node build, `nginx-unprivileged`), `frontend/nginx.conf` + `security-headers.conf` (SPA fallback, gzip, immutable hashed assets, `no-cache` index, `/api/` proxy with `X-Forwarded-For` appended, DNS re-resolution), `docker-compose.yml` (only `web` published on 8080; `ForwardedHeaders__*` set for the private ranges; `cap_drop: ALL`), `.dockerignore` files, `.github/workflows/ci.yml` (backend, frontend, containers jobs), `.gitattributes` (LF), rewritten `.gitignore`.
- Done, same-origin and CSP check without Docker: the Release build was published and run in Production mode with the compose environment (JSON logs, tz check passing, per-client rate limiting over real HTTP with `X-Forwarded-For`, spoofed prefixes ignored); the SPA was built with an empty `VITE_API_BASE_URL` and served behind a proxy with the exact CSP and headers from `security-headers.conf`, then driven in headless Chrome. This found that the first CSP broke the toast library's injected stylesheet and inlined `data:` fonts; `style-src` now allows inline styles and `font-src` allows `data:`, with `script-src 'self'` kept strict. Zero CSP violations afterwards.
- Reviews (code-reviewer on the backend changes, a static review of Docker/nginx/compose/CI files): fixed the README being saved as UTF-16 (would render as binary on GitHub), the CI race (waiting on nginx instead of the API), a CI assertion that used `HEAD`, a redundant `ASPNETCORE_URLS`, nginx caching the `api` address, and a wrong base-image comment. The static review could not execute any container tooling.
- Remaining (for the author): (1) push and confirm the CI run is green, in particular the `containers` job, which is the first real build of the Dockerfiles, nginx config and compose file; (2) fill in `manual-changes.txt` and the Reasoning/Adjustments fields in `PROMPT_LOG.md`; (3) run `git add --renormalize .` once so existing files get the LF line endings that `.gitattributes` now asks for; (4) consider `git rm temp.txt` (the session hand-off note is committed but not part of the deliverable). When (1) and (2) are done, strike this phase through and set it to DONE.
- Decisions and non-goals: nginx serves the app and proxies `/api/` (same origin, no CORS needed in containers); Playwright end-to-end tests were skipped (optional in the plan; browser flows were verified with scripted headless-Chrome runs that are not part of the repo); the SDK image tag floats (`10.0`) while `global.json` pins `10.0.401` with feature roll-forward.

---

## Change Log (spec)

- 2026-10-04 — Spec created. No code written yet. Open prerequisite: install .NET 10 SDK.
- 2026-10-04 — Ascending day order decided by user (table shows newest first on the frontend).
- 2026-10-04 — Phase 1 complete (64 tests, zero warnings, live smoke check passed). Symbol regex tightened; Phase 2 gained carried-over review items; Phase 5 gained a tzdata check.
- 2026-10-04 — Phase 2 complete (178 tests, zero warnings, live check passed). Compression and OpenAPI viewer dropped by decision; cache rewritten without MemoryCache after review; Phase 5 gained forwarded-headers handling.
- 2026-10-04 — Phase 3 complete (51 frontend tests, lint/build clean, live browser check passed). Date rule recorded: frontend never converts time zones or parses day strings with `new Date`.
- 2026-10-05 — Phase 4 complete (175 frontend tests, lint/build/audit clean, live browser check in light and dark). Layout is persisted from drag/resize stops only; panels are wrapped in error boundaries; 12-panel cap.
- 2026-10-05 — Phase 5 built and verified as far as the authoring machine allows (229 backend tests, 175 frontend tests, audits clean). Docker/CI unverified until the first push; manual-changes write-up and PROMPT_LOG fields are the author's. Phase 5 stays IN PROGRESS.
