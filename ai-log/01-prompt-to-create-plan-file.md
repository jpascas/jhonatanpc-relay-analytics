Using the profiling results from `ai-log/00-explore-and-clarify-1-rawsession.md`, write PLAN.md in the repo root. This is the implementation plan for DASH-247, written before any code. Don't write code or create any other file.

GOAL
A customer admin opens the dashboard on Monday morning and sees, for the last complete week, which of their locations is below or above its own typical activity, with the typical range shown next to the number, so they know which site to call first.

MY DECISIONS (use these; don't invent others)
1. "Now" = the latest occurred_at in the data (check 1), injected through .NET 8 TimeProvider and overridable by config. No code reads DateTime.UtcNow or DateTime.Now.
2. Reported week = the last complete week, Monday 00:00 to Monday 00:00 in the account's IANA timezone (check 2). Partial weeks are never shown or used.
3. Baseline = the 8 complete weeks before the reported week. Weeks with 0 events count as 0.
4. Statistic = median, with typical range P25–P75 (linear interpolation, like Excel PERCENTILE.INC), because of the account 6 spike (check 12).
5. Grain (check 11): per location, a status on total activity only; per-type counts shown as numbers without a status. Per account: status on total, on each event type, and on missed-call rate.
6. Status rule for counts, with value v, median m, range [p25, p75]:
   Below typical if v < p25 AND m − v >= max(3, 0.3·m); Above typical if v > p75 AND v − m >= max(3, 0.3·m); Low volume if m < 5; otherwise Typical.
7. Missed-call rate = missed ÷ calls with a known outcome; only computed for weeks with at least 20 known-outcome calls; flagged if outside [p25, p75] AND at least 10 percentage points from the median.
8. Deduplicate on (account_id, location, event_type, occurred_at, duration_seconds, outcome) through a SQL view (check 18). The seed data is never modified.
9. NULL outcome = unknown: never counted as missed or as connected, and shown as a count (check 16).
10. duration_seconds is not used (check 17).
11. A location with 0 events in the reported week still appears, with 0 (check 13). Account 20 returns an empty result with HTTP 200 (check 19).
12. Locations are identified by (account_id, location).
13. Stack: .NET 8 Web API, EF Core migrations (schema and the dedup view), seed loaded by a separate idempotent step, aggregation in explicit SQL, baseline logic in a pure C# class, SQL Server in Docker (SQLite fallback if setup takes over 30 minutes), Angular with UI state in URL query parameters.
14. Out of scope: auth, alerts, forecasting, visual polish, CI, a week picker.

STRUCTURE OF PLAN.md
1. The problem behind the ticket: who uses it, what they do with the answer.
2. Data findings: one table with the finding, check number, number from the output, and the consequence. Only findings that drive a decision.
3. Questions for product, each with the working assumption I'm proceeding on.
4. Decisions: each with the reason and the trade-off considered.
5. Brief: goal, constraints, edge cases (empty, one, many, enormous, duplicate, malformed, unauthorised, offline, concurrent, each mapped to this data with the required behaviour), and the check.
6. Golden values for tests: run queries now and include (a) account 1, week 2026-07-20, counts per type and total; (b) account 6, Site C, all activity, week 2026-07-20: the value, the 8 baseline weekly values oldest to newest, median, P25, P75, and the status from rule 6. Include the queries from the profiling results.
7. Implementation slices: each with what's built, the command or output that proves it's done, and a time budget, totalling 4–6 hours.
8. Deferred, with reasons.
9. "Plan changes" section at the end, empty, for changes made after coding starts.

CONSTRAINTS
- Every number in PLAN.md must come from a query run in this from the profiling results; cite the check number or show the new query.
- Use only my decisions. If you think one is wrong or the data contradicts it, don't change it: list it in a section called "Challenges" with the evidence.
- If something needs a decision I haven't made, put it in "Open decisions" with the options. Don't choose.
- No vague words (properly, robust, reasonable, clean, appropriate). Every behaviour has to be testable.
- Keep it under 250 lines.

DONE WHEN
PLAN.md exists in the repo root, and you've replied with the golden-value queries and outputs, plus the Challenges and Open decisions lists.