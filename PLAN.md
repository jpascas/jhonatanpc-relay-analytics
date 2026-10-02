# PLAN — DASH-247: "Is this normal for us?"

Implementation plan, written before any code. Every number links to its query and output in [EVIDENCE.md](EVIDENCE.md): `E-nn` for the profiling checks, `G-nn` for the golden values. The evidence is pinned to the sha256 of `seed.sql`.

## 1. The problem behind the ticket
- **Who:** a customer admin at a service business (1–15 locations, [E-08](EVIDENCE.md#e-08)). They open the dashboard on Monday morning.
- **What they need:** for last week, is each location's activity normal *for that location*? If not, which site do they call first?
- **What they do with the answer:** call the site manager of a location that is below (or above) its own typical range. They should not need to export data or ask their account manager.
- **Today:** the dashboard shows raw totals per location, with no reference point (PRODUCT_BACKGROUND.md).

## 2. Data findings that drive decisions
| Finding | Evidence | Number | Consequence |
|---|---|---|---|
| Data ends before today | [E-01](EVIDENCE.md#e-01) | last `occurred_at` 2026-07-27 22:20:34 UTC, 66 days before 2026-10-02 | A wall-clock "now" gives an empty week → D1 |
| Last local week is partial for every account | [E-02](EVIDENCE.md#e-02) | week 2026-07-27 partial for 19/19 accounts; last complete week 2026-07-20 | Report the last complete week → D2 |
| Enough history for an 8-week baseline | [E-03](EVIDENCE.md#e-03) | accounts 1–19 have 24–25 complete weeks each | 8 baseline weeks are always available → D3 |
| One outlier week | [E-12](EVIDENCE.md#e-12) | account 6, week 2026-06-01: 881 events vs median 76; 805 on 2026-06-03 across all 15 locations | Mean is inflated (105.1 vs median 76.0) → D4 |
| Small counts at fine grain | [E-11](EVIDENCE.md#e-11) | median per account×location×type×week = 2, 85% of cells <5; per account×week median 20, 1% <5 | Status only where counts allow → D5 |
| Exact duplicates | [E-18](EVIDENCE.md#e-18) | 12 pairs with consecutive ids; 12,626 raw rows → 12,614 deduped [G-03](EVIDENCE.md#g-03) | Dedup view → D8 |
| NULL outcomes | [E-16](EVIDENCE.md#e-16) | 3.1% of calls (240/7,780); never above 5.1% per account | NULL outcome = unknown → D9 |
| Duration does not follow outcome | [E-17](EVIDENCE.md#e-17) | missed calls average 737 s vs connected 765 s | Duration not used → D10 |
| Zero weeks at location grain | [E-13](EVIDENCE.md#e-13) | 4 of 1,672 complete location-weeks have 0 events | Zero is a real value → D3, D11 |
| Account with no events | [E-19](EVIDENCE.md#e-19) | account 20 (Quiet Harbor Spa) has 0 events | Empty response → D11 |
| Location names collide across accounts | [E-08](EVIDENCE.md#e-08) | every account uses `Site A`…; 69 (account, location) pairs | Key = (account_id, location) → D12 |
| Local vs UTC bucketing | [E-06](EVIDENCE.md#e-06) | ≤0.37% of an account's events change week; 1 DST change (2026-03-08) | Local weeks are required for correct edges; the impact is small |
| Timezones are valid IANA names | [E-05](EVIDENCE.md#e-05) | 6 distinct zones, all valid, including `UTC` (account 18) and `America/Phoenix` (no DST) | No fallback zone needed for this seed |

## 3. Questions for product (working assumption in brackets)
1. Does "this week" on a Monday mean the week that just ended? [Yes: the last complete local week, D2.]
2. Is "typical" the location's own recent history, or a comparison with peers? [Own history: the median of the 8 prior weeks with a P25–P75 range, D3/D4.]
3. Was the account 6 spike on 2026-06-03 (805 events) real demand or an import error? [Real; kept in the data and absorbed by the median.]
4. Are rows identical except for `id` double submissions? [Yes: counted once, D8.]
5. What is a call with outcome NULL? [Unknown: shown as a count, excluded from the missed-call rate, D9.]
6. Is `duration_seconds` trustworthy, given that missed calls have durations ([E-17](EVIDENCE.md#e-17))? [No: not used, D10.]
7. What should an account with no data see? [An empty result, D11.]
8. Is "above typical" something to act on, or only "below"? [Both are shown, D6.]

## 4. Decisions
| # | Decision | Reason | Trade-off considered |
|---|---|---|---|
| D1 | "Now" = latest `occurred_at`, injected via .NET 8 `TimeProvider`, overridable by config; no `DateTime.UtcNow`/`Now` | [E-01](EVIDENCE.md#e-01): wall clock gives empty weeks | Demo is pinned to the data, not live time; config override covers live use |
| D2 | Reported week = last complete Mon 00:00→Mon 00:00 week in the account's IANA zone; partial weeks never shown or used | [E-02](EVIDENCE.md#e-02); "Monday morning" in TICKET.md | Current-week activity is not visible |
| D3 | Baseline = 8 complete weeks before the reported week; 0-event weeks count as 0 | [E-03](EVIDENCE.md#e-03) (24–25 weeks available), [E-13](EVIDENCE.md#e-13) | 8 weeks reacts to trends faster than a longer window, but is noisier |
| D4 | Median with P25–P75 range (PERCENTILE.INC, linear interpolation) | [E-12](EVIDENCE.md#e-12) spike | Ignores the size of tail weeks; P25/P75 come from only 8 points |
| D5 | Location: status on total only, per-type counts shown without status. Account: status on total, each type, missed-call rate | [E-11](EVIDENCE.md#e-11) cell sizes | Per-type issues at one location are not flagged |
| D6 | Count status: Below if v<p25 AND m−v ≥ max(3, 0.3m); Above if v>p75 AND v−m ≥ max(3, 0.3m); Low volume if m<5; else Typical | Needs both a range breach and a minimum absolute gap | Thresholds are fixed and not configurable |
| D7 | Missed-call rate = missed ÷ known-outcome calls; computed only in weeks with ≥20 known-outcome calls; flagged if outside [p25,p75] AND ≥10 pp from the median | Avoids rates computed from tiny denominators | Most accounts get no rate (see Challenges C2) |
| D8 | Dedup on (account_id, location, event_type, occurred_at, duration_seconds, outcome) via a SQL view; seed never modified | [E-18](EVIDENCE.md#e-18) | A legitimate repeat event in the same second would be dropped |
| D9 | NULL outcome = unknown, never missed or connected, shown as a count | [E-16](EVIDENCE.md#e-16) | Rate denominators shrink by about 3% |
| D10 | `duration_seconds` unused | [E-17](EVIDENCE.md#e-17) | No call-length insight |
| D11 | A location with 0 events in the reported week still appears, with 0; account 20 → HTTP 200 with an empty result | [E-13](EVIDENCE.md#e-13), [E-19](EVIDENCE.md#e-19) | — |
| D12 | Location key = (account_id, location) | [E-08](EVIDENCE.md#e-08) | — |
| D13 | .NET 8 Web API; EF Core migrations (schema + dedup view); separate idempotent seed step; aggregation in explicit SQL; baseline logic in a pure C# class; SQL Server in Docker (SQLite if setup exceeds 30 min); Angular with UI state in URL query params | Testability: SQL is checked against the golden values, logic is unit-tested without a DB | SQL Server cannot convert IANA zones natively (see O1) |
| D14 | Out of scope: auth, alerts, forecasting, visual polish, CI, week picker | Ticket and brief | — |

## 5. Brief
**Goal:** one API call and one page. For an account and its reported week, show each location's total versus its baseline median and P25–P75 range, with a status. Show account-level statuses for the total, each event type, and the missed-call rate.

**Constraints:** D1–D14. No new NuGet or npm packages without approval (CLAUDE.md; list in O2). `seed.sql` and `schema.sql` are untouched.

**Edge cases mapped to this data**
| Case | In this data | Required behaviour (testable) |
|---|---|---|
| Empty | Account 20, 0 events ([E-19](EVIDENCE.md#e-19)) | `GET` returns 200 with `locations: []` and no statuses; UI shows "No activity recorded" |
| Empty location-week | 4 location-weeks with 0 ([E-13](EVIDENCE.md#e-13)); none in week 2026-07-20 ([E-13](EVIDENCE.md#e-13)) | A 0 counts as a baseline value; a location with 0 in the reported week is listed with `total: 0` |
| One | Single-location accounts 8, 13, 16, 19 ([E-08](EVIDENCE.md#e-08)) | Response has exactly 1 location row; account status is still computed |
| Many | Account 6, 15 locations ([E-08](EVIDENCE.md#e-08)) | Response has 15 location rows, one SQL round trip for counts |
| Enormous | Account 6 week 2026-06-01: 881 events ([E-12](EVIDENCE.md#e-12)) | Inside the baseline for week 2026-07-20; median and P25/P75 for account 6 Site C equal §6(b) |
| Duplicate | 12 pairs ([E-18](EVIDENCE.md#e-18)); account 1 Site C week 2026-07-06: raw 5 → 4 [G-03](EVIDENCE.md#g-03) | All counts read the dedup view; the baseline value for that week is 4 |
| Malformed | 240 calls with NULL outcome ([E-16](EVIDENCE.md#e-16)); missed calls with durations ([E-17](EVIDENCE.md#e-17)) | NULLs are counted in `unknownOutcomeCalls` and excluded from the rate; duration never read. Non-integer account id → 400 (model binding) |
| Unauthorised | Auth out of scope (D14) | Any caller can read any account; documented in README as a known gap |
| Offline | API unreachable from Angular | See O5 |
| Concurrent | Read-only endpoint; seed step may be re-run | Running the seed step twice leaves 20 accounts and 12,626 events (idempotent) |
| Unknown account | id not in `accounts` (e.g. 999) | See O4 |

**The check:** API output for account 1 and for account 6 Site C equals §6. Unit tests on the baseline class pass with the §6(b) and §10 vectors. The page at `?account=6` lists 15 locations, each with value, range and status.

## 6. Golden values for tests (dedup applied; raw = dedup in both cases)
[G-01](EVIDENCE.md#g-01) **(a) Account 1 (America/Chicago), week 2026-07-20 = [2026-07-20 05:00, 2026-07-27 05:00) UTC:**
calls **34**, leads **12**, appointments **7**, total **53**.

[G-02](EVIDENCE.md#g-02) **(b) Account 6 (America/New_York), Site C, all activity, week 2026-07-20 = [07-20 04:00, 07-27 04:00) UTC:**
- value **6**
- baseline weeks 2026-05-25 → 2026-07-13, oldest to newest: **4, 67, 11, 3, 6, 6, 8, 3**
- median **6.0**, P25 **3.75**, P75 **8.75**, threshold max(3, 0.3·6) = 3
- status **Typical**: 6 is not below 3.75 and not above 8.75, and m ≥ 5

Queries and outputs: [G-01](EVIDENCE.md#g-01), [G-02](EVIDENCE.md#g-02). Week edges use fixed summer offsets, which are valid because all weeks from 2026-05-25 onward are after the 2026-03-08 DST change ([E-06](EVIDENCE.md#e-06)).

## 7. Implementation slices (total 5 h 25 min)
| # | Built | Done when | Budget |
|---|---|---|---|
| S0 | Approval of the package list (O2); solution skeleton `api/`, `api.tests/`, `web/` | `dotnet build` succeeds; `ng version` runs | 20 min |
| S1 | SQL Server in Docker; EF Core migration for both tables and view `activity_events_dedup` | `dotnet ef database update` succeeds; if the container isn't serving within 30 min, switch to SQLite and record it under Plan changes | 45 min |
| S2 | Idempotent seed step (runs `seed.sql` only when `accounts` is empty) | Run twice → `SELECT COUNT(*)` gives 20 accounts, 12,626 events, 12,614 rows in the view | 30 min |
| S3 | `TimeProvider` registration (config `Clock:NowUtc`, default = MAX(occurred_at)); week calculator (IANA → UTC edges) | Unit tests: now 2026-07-27 22:20:34 → reported week 2026-07-20 for all 19 accounts; account 18 edges 2026-07-20 00:00/07-27 00:00 UTC, account 1 05:00/05:00 ([E-22](EVIDENCE.md#e-22)) | 30 min |
| S4 | Pure `BaselineEvaluator`: PERCENTILE.INC, D6, D7 | Unit tests pass for §6(b) and every row in §10 | 45 min |
| S5 | Explicit SQL weekly counts (9 weeks × location × type, zero-filled) + `GET /api/accounts/{id}/weekly-status` | `curl` for account 1 returns 34/12/7/53; account 6 Site C returns value 6, range 3.75–8.75, Typical; account 20 returns 200 with `[]` | 60 min |
| S6 | Angular page: account from `?account=`, table of locations (value, range, status, per-type counts), account summary row | Load `?account=6` → 15 rows; reload keeps the account; account 20 shows the empty message | 75 min |
| S7 | README: run steps, assumptions (§3), known gaps | Following the README on a clean clone reaches S6's result | 20 min |

## 8. Deferred
- Auth, alerts/notifications, forecasting, CI, visual polish, week picker: out of scope (D14, ticket, brief).
- Per-type status at location grain: 85% of those cells are <5 ([E-11](EVIDENCE.md#e-11)).
- Call duration metrics: duration does not depend on outcome ([E-17](EVIDENCE.md#e-17)).
- Hour-of-day views: the UTC hour profile is the same in every timezone ([E-06](EVIDENCE.md#e-06)), so local hours are not credible.
- Peer or industry comparison: the ticket says "typical for them".

## 9. Challenges (decisions kept; evidence for review)
- **C1, D6 flags a third of locations.** In week 2026-07-20, D6 gives 15 Above, 8 Below, 6 Low volume and 40 Typical out of 69 locations [G-04](EVIDENCE.md#g-04). Above flags fire on gaps as small as 3 events (e.g. 1/Site C: 9 vs median 5.5). "Which site to call first" may get lost among 23 flags.
- **C2, D7 rarely produces a rate.** Only 5 of 19 accounts have ≥20 known-outcome calls in week 2026-07-20. 11 of 19 have 0 qualifying baseline weeks, and only 3 have all 8 [G-05](EVIDENCE.md#g-05). Most accounts get no missed-call status.
- **C3, account-level appointment status is mostly Low volume.** 14 of 19 accounts are Low volume for appointments in week 2026-07-20 [G-05](EVIDENCE.md#g-05) ([E-11](EVIDENCE.md#e-11): mean 3.8 appointments per account-week).
- **C4, the D6 order is ambiguous.** Low volume is listed after Below/Above. For 6/Site M (v=7, m=3.5) and 6/Site O (v=8, m=4.5), Above-first gives "Above" and Low-first gives "Low volume" [G-04](EVIDENCE.md#g-04). See O3.

## 10. Extra test vectors (rule D6; dedup; week 2026-07-20; baseline sorted; source [G-04](EVIDENCE.md#g-04))
| Location | v | Baseline (sorted) | Status |
|---|---|---|---|
| 1 / Site C | 9 | 4,4,5,5,6,8,8,11 | Above |
| 5 / Site B | 3 | 3,4,6,6,7,10,11,14 | Below |
| 8 / Site A | 7 | 8,8,10,10,10,11,12,13 | Below |
| 18 / Site A | 3 | 1,2,2,3,4,5,7,8 | Low volume |
| 6 / Site M | 7 | 1,2,3,3,4,5,6,50 | depends on O3 |

## 11. Open decisions (not chosen)
- **O1, IANA week edges with SQL Server** (its `AT TIME ZONE` takes Windows zone names). (a) C# computes UTC edges per account with `TimeZoneInfo` and passes them to SQL as parameters; (b) map IANA to Windows names and bucket in SQL.
- **O2, packages needing approval (CLAUDE.md).** EF Core provider + Design (+ the SQLite provider for the fallback), the `dotnet-ef` tool, a test framework (xUnit or MSTest), and the Angular CLI/runtime npm packages.
- **O3, D6 precedence when m < 5.** (a) Below/Above win over Low volume; (b) Low volume wins.
- **O4, account id not in `accounts`.** (a) 404; (b) 200 empty, the same as account 20.
- **O5, Angular when the API is unreachable.** (a) error message with no numbers; (b) retry button plus error message.
- **O6, row order on the page** (affects "which site to call first"). (a) Below first, then by m − v; (b) alphabetical; (c) by gap size, regardless of direction.
- **O7, missed-call baseline when fewer than 8 weeks qualify (C2).** (a) use only the qualifying weeks, with a minimum N; (b) no rate status unless all 8 qualify.
- **O8, location list source** (there is no locations table). (a) locations with any event in the 9-week window; (b) locations with any event ever. [E-10](EVIDENCE.md#e-10) shows no location starts or stops mid-range, so the seed gives the same result either way.

## Plan changes
<!-- Changes made after coding starts: date, what changed, why. -->
