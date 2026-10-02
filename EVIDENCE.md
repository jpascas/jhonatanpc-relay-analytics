# EVIDENCE — DASH-247

The source for every number in [PLAN.md](PLAN.md). Each entry gives the query exactly as it was run and the output it returned.
- `E-nn` entries are the profiling checks (numbered as in `ai-log/00-explore-and-clarify-1-rawsession.md`; check 9 was cut).
- `G-nn` entries are the golden values and rule evaluations used by tests and by the plan's Challenges.

**Data the queries ran against**
| File | sha256 |
|---|---|
| `seed.sql` | `40e60ee81d999eb32057b4437bc84e9ec197265d4e58c13c0bfbad150e6eaea2` |
| `schema.sql` | `348912f4fd6dade1728058a4f666780c60b94578f4276135583f502616e51d3d` |

If either hash changes, every entry below is stale.

**Environment:** Python 3.14 `sqlite3` (standard library), with `schema.sql` then `seed.sql` loaded into a throwaway file DB with `PRAGMA foreign_keys=OFF`. Run on 2026-10-02.
- **E-entries** that use local time need the helpers below. They use `zoneinfo` with the `tzdata` package (installed outside the repo, tzdata 2026d). It is approved as a dev-only tool in [PLAN.md](PLAN.md) D16.
- **G-01 to G-06** use only the standard library. They rely on fixed summer UTC offsets, which are exact for weeks from 2026-05-25 onward ([E-06](#e-06): the only DST change in range is 2026-03-08).
- **G-07 to G-09** cover weeks before or across the DST change, so they use the E-entry helpers (`aw`, `ev`, IANA zones via `tzdata`).

## Helpers used by E-entries

These are registered SQLite functions, plus temp tables built after loading. A week is Mon 00:00 → Mon 00:00 in the account's IANA zone.

```python
local_ts(utc, tz)      # 'YYYY-MM-DD HH:MM:SS' in the account's zone
week_start(utc, tz)    # local Monday (date) of the week containing utc
week_start_utc(wk, tz) # local Monday 00:00 of wk, as UTC
week_end_utc(wk, tz)   # next local Monday 00:00, as UTC
utc_week_start(utc)    # Monday of the UTC week
is_iana(tz)            # 1 if tz in zoneinfo.available_timezones()
```
```sql
CREATE TEMP TABLE ev AS
SELECT e.*, a.timezone AS tz, a.created_at,
       local_ts(e.occurred_at, a.timezone) AS lts, week_start(e.occurred_at, a.timezone) AS wk
FROM activity_events e JOIN accounts a ON a.id = e.account_id;
```
`aw(account_id, wk, complete)` and `lw(account_id, location, wk, complete)` list every week from the entity's first event week up to the week containing cutoff = `MAX(occurred_at)` = `2026-07-27 22:20:34`.
- `complete = 1` iff `week_start_utc(wk) >= entity's first occurred_at` AND `week_end_utc(wk) <= cutoff`.
- The start edge is per entity (per account for `aw`, per location for `lw`).

---

## E-01
**Does the data reach today?**
```sql
SELECT MIN(occurred_at) AS min_utc, MAX(occurred_at) AS max_utc,
       CAST(julianday('2026-10-02') - julianday(MAX(occurred_at)) AS INT) AS days_before_today, COUNT(*) AS n
FROM activity_events
```
```
2026-02-01 10:57:44 | 2026-07-27 22:20:34 | 66 | 12626
```

## E-02
**Is the last week complete?**
```sql
SELECT complete, COUNT(*) AS account_weeks, COUNT(DISTINCT wk) AS distinct_week_starts FROM aw GROUP BY 1;
SELECT wk, complete, COUNT(*) AS accounts FROM aw WHERE complete=0 GROUP BY 1,2 ORDER BY 1;
```
```
0 | 38 | 3
1 | 464 | 25
2026-01-26 | 0 | 8
2026-02-02 | 0 | 11
2026-07-27 | 0 | 19
```
Week 2026-07-27 is partial for all 19 accounts with events, which makes 2026-07-20 the last complete week. The data ends on Monday 2026-07-27, with 79 events that UTC day.

## E-03
**History per account**
```sql
SELECT a.id AS account_id, MIN(ev.lts) AS first_local, MAX(ev.lts) AS last_local, COUNT(ev.id) AS events,
       (SELECT SUM(complete) FROM aw WHERE aw.account_id=a.id) AS complete_weeks,
       (SELECT COUNT(*) FROM aw WHERE aw.account_id=a.id AND complete=0) AS partial_weeks,
       (SELECT COUNT(*) FROM aw WHERE aw.account_id=a.id AND complete=1
          AND NOT EXISTS (SELECT 1 FROM ev x WHERE x.account_id=a.id AND x.wk=aw.wk)) AS complete_weeks_with_0_events
FROM accounts a LEFT JOIN ev ON ev.account_id=a.id GROUP BY a.id ORDER BY a.id
```
Accounts 1–19 have `complete_weeks` of 24 or 25, `partial_weeks` = 2 and `complete_weeks_with_0_events` = 0. Events range from 167 (account 16) to 2,641 (account 6). Account 20 has 0 events.

## E-05
**Timezones**
```sql
SELECT timezone, is_iana(timezone) AS valid_iana, COUNT(*) AS accounts, GROUP_CONCAT(id) AS account_ids FROM accounts GROUP BY 1 ORDER BY 1
```
```
America/Chicago     | 1 | 6 | 1,4,8,10,13,16
America/Denver      | 1 | 2 | 3,9
America/Los_Angeles | 1 | 4 | 5,12,17,20
America/New_York    | 1 | 5 | 2,6,11,14,19
America/Phoenix     | 1 | 2 | 7,15
UTC                 | 1 | 1 | 18
```

## E-06
**Local vs UTC bucketing, DST, and hour profile**
```sql
SELECT account_id, tz, COUNT(*) AS events,
       SUM(wk <> utc_week_start(occurred_at)) AS week_differs,
       ROUND(100.0*SUM(wk <> utc_week_start(occurred_at))/COUNT(*),2) AS pct_week_differs,
       SUM(date(lts) <> date(occurred_at)) AS day_differs,
       ROUND(100.0*SUM(date(lts) <> date(occurred_at))/COUNT(*),1) AS pct_day_differs
FROM ev GROUP BY 1,2 ORDER BY 1
```
- **Week bucket:** max `pct_week_differs` is 0.37 (account 9, 2 events); 11 accounts have 0.
- **Day bucket:** max `pct_day_differs` is 1.9 (account 17).

**DST transitions** were found by stepping hourly over 2026-02-01 → 2026-07-28 UTC and comparing `utcoffset()`. Chicago, Denver, Los Angeles and New York each change once, on 2026-03-08. Phoenix and UTC have none.
```sql
SELECT COUNT(*) FROM ev WHERE date(lts)='2026-03-08' AND strftime('%H',lts)='02'   -- 0
```
**Hour profile by timezone:**
```sql
SELECT tz, COUNT(*) AS n,
  ROUND(AVG(CAST(strftime('%H',lts) AS INT)),1) AS mean_local_hour,
  ROUND(AVG(CAST(strftime('%H',occurred_at) AS INT)),1) AS mean_utc_hour
FROM ev GROUP BY tz ORDER BY tz
```
The mean UTC hour is 15.3–15.6 in every zone (UTC included), while the mean local hour runs from 8.7 (Los Angeles) to 11.3 (New York).

## E-08
**Locations**
```sql
SELECT account_id, COUNT(DISTINCT location), COUNT(DISTINCT lower(trim(location))),
       COUNT(DISTINCT lower(replace(replace(replace(replace(trim(location),' ',''),'-',''),'.',''),'_','')))
FROM activity_events GROUP BY 1 ORDER BY 1;
SELECT COUNT(*) AS location_total FROM (SELECT DISTINCT account_id, location FROM activity_events);
```
- The three counts are equal for every account, and the variant query returns 0 rows.
- Names are `Site A`…`Site O`, reused across accounts.
- Locations per account: 1 for accounts 8, 13, 16 and 19; 15 for account 6.
- `location_total` = **69**.

## E-10
**Do locations start or stop mid-range?**
```sql
WITH l AS (SELECT account_id, location, MIN(occurred_at) f, MAX(occurred_at) t FROM activity_events GROUP BY 1,2),
     a AS (SELECT account_id, MIN(occurred_at) f, MAX(occurred_at) t FROM activity_events GROUP BY 1)
SELECT MAX(ROUND(julianday(l.f)-julianday(a.f),1)), MAX(ROUND(julianday(a.t)-julianday(l.t),1)),
       SUM(julianday(l.f)-julianday(a.f) > 14), SUM(julianday(a.t)-julianday(l.t) > 14)
FROM l JOIN a USING(account_id)
```
```
4.4 | 5.0 | 0 | 0
```

## E-11
**Weekly cell sizes** (complete weeks only, zero-filled)
```sql
-- location grain (uses lw)
CREATE TEMP TABLE cell AS SELECT lw.account_id, lw.location, t.event_type, lw.wk,
  (SELECT COUNT(*) FROM ev WHERE ev.account_id=lw.account_id AND ev.location=lw.location AND ev.event_type=t.event_type AND ev.wk=lw.wk) AS n
FROM lw CROSS JOIN (SELECT DISTINCT event_type FROM activity_events) t WHERE lw.complete=1;
-- account grain (uses aw)
CREATE TEMP TABLE acell AS SELECT aw.account_id, t.event_type, aw.wk,
  (SELECT COUNT(*) FROM ev WHERE ev.account_id=aw.account_id AND ev.event_type=t.event_type AND ev.wk=aw.wk) AS n
FROM aw CROSS JOIN (SELECT DISTINCT event_type FROM activity_events) t WHERE aw.complete=1;
```
Distributions were computed in Python over `SELECT SUM(n) ... GROUP BY <grain>`:
```
account x week                     (acell) cells=464  median=20 <5=3 (1%)     <10=71 (15%)    zeros=0
account x event_type x week        (acell) cells=1392 median=5  <5=589 (42%)  <10=980 (70%)   zeros=62
account x location x week          (cell)  cells=1672 median=7  <5=398 (24%)  <10=1358 (81%)  zeros=4
account x location x type x week   (cell)  cells=5016 median=2  <5=4253 (85%) <10=4954 (99%)  zeros=1042
```
```sql
SELECT event_type, ROUND(AVG(n),1) AS mean_per_acct_week, MIN(n), SUM(n<5), SUM(n=0), COUNT(*) FROM acell GROUP BY 1
```
```
appointment_set | 3.8  | 0 | 332 | 52 | 464
call_received   | 16.4 | 1 | 46  | 0  | 464
lead_created    | 6.4  | 0 | 211 | 10 | 464
```
A first pass built account totals from `cell` (location grids). That was wrong, because it drops week 2026-02-02 for most locations; the numbers above come from the corrected rerun.

## E-12
**Noise and outlier weeks**

Per account, using `SELECT account_id, wk, SUM(n) FROM acell GROUP BY 1,2`:
- Coefficient of variation (CV) is 0.16–0.30, except account 6 at 1.51 (0.18 with outliers excluded).
- Account 6: mean **105.1**, median **76.0**. The only week >3× or <⅓ of its median is `2026-06-01: 881`.
```sql
SELECT date(lts) AS local_day, event_type, COUNT(*) AS n, COUNT(DISTINCT location) AS locs
FROM ev WHERE account_id=6 AND wk='2026-06-01' GROUP BY 1,2 ORDER BY 1,2
```
On 2026-06-03 there are 110 appointments, 485 calls and 210 leads, which totals **805**. Each type is spread across all 15 locations.
```sql
SELECT COUNT(*), SUM(EXISTS (SELECT 1 FROM activity_events y WHERE y.id<>x.id AND y.account_id=x.account_id
         AND y.location=x.location AND y.event_type=x.event_type AND y.occurred_at=x.occurred_at))
FROM activity_events x WHERE account_id=6 AND date(local_ts(occurred_at,'America/New_York'))='2026-06-03'
```
```
805 | 2
```
Only 2 of those rows have a twin, so the spike is not made of duplicates.

## E-13
**Zero location-weeks**
```sql
SELECT lw.account_id, lw.location, lw.wk FROM lw WHERE complete=1
  AND NOT EXISTS (SELECT 1 FROM ev WHERE ev.account_id=lw.account_id AND ev.location=lw.location AND ev.wk=lw.wk)
```
```
6 | Site G | 2026-04-13
6 | Site G | 2026-06-29
6 | Site N | 2026-02-23
18 | Site C | 2026-03-23
```
That is 4 of 1,672 complete location-weeks ([E-11](#e-11)), and none of them is in 2026-07-20.

## E-16
**Outcomes**
```sql
SELECT event_type, COALESCE(outcome,'<NULL>'), COUNT(*),
       ROUND(100.0*COUNT(*)/SUM(COUNT(*)) OVER (PARTITION BY event_type),1)
FROM activity_events GROUP BY 1,2 ORDER BY 1, 3 DESC
```
```
appointment_set | completed 1439 (79.9) | no_show 305 (16.9) | NULL 58 (3.2)
call_received   | connected 4669 (60.0) | missed 1937 (24.9) | voicemail 934 (12.0) | NULL 240 (3.1)
lead_created    | open 1871 (61.5)      | converted 1073 (35.2) | NULL 100 (3.3)
```
```sql
SELECT account_id, ROUND(100.0*SUM(outcome IS NULL)/COUNT(*),1) FROM activity_events
WHERE event_type='call_received' GROUP BY 1 ORDER BY 2 DESC LIMIT 5
```
The highest per-account NULL rate for calls is 5.1% (account 15).

## E-17
**Duration**
```sql
SELECT COALESCE(outcome,'<NULL>'), COUNT(*), SUM(duration_seconds IS NULL), MIN(duration_seconds),
       MAX(duration_seconds), ROUND(AVG(duration_seconds))
FROM activity_events WHERE event_type='call_received' GROUP BY 1 ORDER BY 2 DESC
```
```
connected | 4669 | 185 | 20 | 1500 | 765
missed    | 1937 | 76  | 20 | 1500 | 737
voicemail | 934  | 46  | 21 | 1499 | 768
<NULL>    | 240  | 6   | 26 | 1495 | 766
```
Leads and appointments have NULL duration on every row.

## E-18
**Exact duplicates**
```sql
SELECT COUNT(*) AS dup_groups, SUM(cnt) AS rows_in_groups, SUM(cnt-1) AS extra_rows FROM (
  SELECT COUNT(*) cnt FROM activity_events
  GROUP BY account_id, location, event_type, occurred_at, duration_seconds IS NULL,
           COALESCE(duration_seconds,-1), COALESCE(outcome,'<NULL>')
  HAVING COUNT(*) > 1)
```
```
12 | 24 | 12
```
Every pair has consecutive ids (e.g. 11266/11267, account 1 Site C 2026-07-07 20:26:04).

## E-19
**Orphans and empty accounts**
```sql
SELECT (SELECT COUNT(*) FROM activity_events e WHERE NOT EXISTS (SELECT 1 FROM accounts a WHERE a.id=e.account_id)),
       (SELECT COUNT(*) FROM accounts a WHERE NOT EXISTS (SELECT 1 FROM activity_events e WHERE e.account_id=a.id)),
       (SELECT GROUP_CONCAT(id || ':' || name) FROM accounts a WHERE NOT EXISTS (SELECT 1 FROM activity_events e WHERE e.account_id=a.id))
```
```
0 | 1 | 20:Quiet Harbor Spa
```

## E-22
**Reconciliation and week edges**
```sql
WITH acct AS (SELECT account_id, event_type, wk, COUNT(*) n FROM ev GROUP BY 1,2,3),
     loc  AS (SELECT account_id, event_type, wk, SUM(n) n FROM
              (SELECT account_id, location, event_type, wk, COUNT(*) n FROM ev GROUP BY 1,2,3,4) GROUP BY 1,2,3)
SELECT COUNT(*), SUM(acct.n), SUM(loc.n), SUM(acct.n <> loc.n) FROM acct JOIN loc USING(account_id, event_type, wk)
```
```
1405 | 12626 | 12626 | 0
```
```sql
SELECT ev.account_id, week_start_utc('2026-07-20', tz), week_end_utc('2026-07-20', tz) FROM ev WHERE wk='2026-07-20' GROUP BY ev.account_id
```
- Account 1 (Chicago): `2026-07-20 05:00:00` → `2026-07-27 05:00:00`
- Account 6 (New York): `04:00` → `04:00`
- Account 18 (UTC): `2026-07-20 00:00:00` → `2026-07-27 00:00:00`

---

## Golden values

G-01 to G-06 share the dedup CTE below, which mirrors D8:
```sql
dedup AS (SELECT account_id, location, event_type, occurred_at, duration_seconds, outcome
          FROM activity_events GROUP BY account_id, location, event_type, occurred_at, duration_seconds, outcome)
```
Local Monday 00:00 in UTC uses fixed summer offsets: New York +4h, Chicago +5h, Denver +6h, Los Angeles and Phoenix +7h, UTC +0h.

## G-01
**Account 1, week 2026-07-20, counts per type**
```sql
WITH dedup AS (...)
SELECT 'dedup', SUM(event_type='call_received'), SUM(event_type='lead_created'), SUM(event_type='appointment_set'), COUNT(*)
FROM dedup WHERE account_id=1 AND occurred_at >= '2026-07-20 05:00:00' AND occurred_at < '2026-07-27 05:00:00'
UNION ALL
SELECT 'raw', SUM(event_type='call_received'), SUM(event_type='lead_created'), SUM(event_type='appointment_set'), COUNT(*)
FROM activity_events WHERE account_id=1 AND occurred_at >= '2026-07-20 05:00:00' AND occurred_at < '2026-07-27 05:00:00'
```
```
dedup | 34 | 12 | 7 | 53
raw   | 34 | 12 | 7 | 53
```

## G-02
**Account 6, Site C, baseline and status**
```sql
WITH RECURSIVE dedup AS (...),
weeks(wk) AS (SELECT '2026-05-25' UNION ALL SELECT date(wk,'+7 days') FROM weeks WHERE wk < '2026-07-20')
SELECT w.wk,
  (SELECT COUNT(*) FROM dedup d WHERE d.account_id=6 AND d.location='Site C'
     AND d.occurred_at >= datetime(w.wk,'+4 hours') AND d.occurred_at < datetime(w.wk,'+7 days','+4 hours')) AS n_dedup,
  (SELECT COUNT(*) FROM activity_events d WHERE d.account_id=6 AND d.location='Site C'
     AND d.occurred_at >= datetime(w.wk,'+4 hours') AND d.occurred_at < datetime(w.wk,'+7 days','+4 hours')) AS n_raw
FROM weeks w ORDER BY w.wk
```
```
2026-05-25 4 | 2026-06-01 67 | 2026-06-08 11 | 2026-06-15 3 | 2026-06-22 6 | 2026-06-29 6 | 2026-07-06 8 | 2026-07-13 3 | 2026-07-20 6
(n_raw = n_dedup in every week)
```
**Percentiles:** PERCENTILE.INC, computed in Python as `h = p*(n-1); s[floor(h)] + (h-floor(h))*(s[floor(h)+1]-s[floor(h)])` over the 8 baseline weeks.
- Median 6.0, P25 3.75, P75 8.75; threshold max(3, 0.3·6) = 3.
- v=6 → v<p25 False, v>p75 False, m<5 False → **Typical**.

## G-03
**Dedup totals and a duplicate inside a baseline week**
```sql
SELECT (SELECT COUNT(*) FROM activity_events),
       (SELECT COUNT(*) FROM (SELECT 1 FROM activity_events GROUP BY account_id, location, event_type, occurred_at, duration_seconds, outcome))
```
```
12626 | 12614
```
```sql
-- account 1 (UTC-5), Site C, local week 2026-07-06
SELECT COUNT(*),
  (SELECT COUNT(*) FROM (SELECT 1 FROM activity_events WHERE account_id=1 AND location='Site C'
     AND occurred_at >= '2026-07-06 05:00:00' AND occurred_at < '2026-07-13 05:00:00'
     GROUP BY event_type, occurred_at, duration_seconds, outcome))
FROM activity_events WHERE account_id=1 AND location='Site C'
  AND occurred_at >= '2026-07-06 05:00:00' AND occurred_at < '2026-07-13 05:00:00'
```
```
5 | 4
```

## G-04
**Rule D6 across all 69 locations, week 2026-07-20**
```sql
WITH RECURSIVE dedup AS (...),
off(tz, h) AS (VALUES ('America/New_York',4),('America/Chicago',5),('America/Denver',6),
                      ('America/Los_Angeles',7),('America/Phoenix',7),('UTC',0)),
weeks(wk) AS (SELECT '2026-05-25' UNION ALL SELECT date(wk,'+7 days') FROM weeks WHERE wk < '2026-07-20'),
locs AS (SELECT DISTINCT d.account_id, d.location, off.h FROM dedup d JOIN accounts a ON a.id=d.account_id JOIN off ON off.tz=a.timezone)
SELECT l.account_id, l.location, w.wk, COUNT(d.occurred_at) AS n
FROM locs l CROSS JOIN weeks w LEFT JOIN dedup d ON d.account_id=l.account_id AND d.location=l.location
  AND d.occurred_at >= datetime(w.wk, '+'||l.h||' hours') AND d.occurred_at < datetime(w.wk,'+7 days','+'||l.h||' hours')
GROUP BY 1,2,3 ORDER BY 1,2,3
```
D6 is applied in Python, with the last week as v and the 8 prior weeks as the baseline:
```
order Below,Above,Low -> typical 40, above 15, below 8, low 6
order Low,Below,Above -> typical 40, above 13, below 8, low 8
baseline median < 5: 8 of 69 locations
status depends on order: (6,'Site M') v=7 m=3.5; (6,'Site O') v=8 m=4.5
```
Selected rows (v, sorted baseline, status with order Below,Above,Low):
```
(1,'Site C')  9  [4,4,5,5,6,8,8,11]     above
(5,'Site B')  3  [3,4,6,6,7,10,11,14]   below
(8,'Site A')  7  [8,8,10,10,10,11,12,13] below
(18,'Site A') 3  [1,2,2,3,4,5,7,8]      low
(6,'Site M')  7  [1,2,3,3,4,5,6,50]     above
```

## G-05
**Account-level statuses and missed-call eligibility, week 2026-07-20**

This uses the same query as G-04, with the location collapsed to `'ALL'` and these columns:
```sql
SUM(d.event_type='call_received') calls, SUM(d.event_type='lead_created') leads, SUM(d.event_type='appointment_set') appts,
COUNT(d.occurred_at) total, SUM(d.event_type='call_received' AND d.outcome IS NOT NULL) known_calls,
SUM(d.event_type='call_received' AND d.outcome='missed') missed
```
```
calls  status (19 accounts): typical 16, low 2, above 1
leads  status: typical 6, above 5, low 7, below 1
appts  status: typical 3, low 14, above 2
total  status: typical 16, below 1, above 2
reported-week known-outcome calls >= 20: 5 of 19
baseline weeks with >= 20 known calls: all 8 for 3 accounts; 0 for 11 accounts
(acct, qualifying baseline weeks, reported-week known calls):
(1,8,32) (2,3,20) (3,0,14) (4,5,21) (5,5,18) (6,8,48) (7,0,8) (8,0,4) (9,0,11) (10,0,6)
(11,0,10) (12,8,30) (13,0,4) (14,2,15) (15,0,9) (16,0,6) (17,0,5) (18,1,14) (19,0,8)
```

## G-06
**Account 1, missed-call rate, week 2026-07-20 (D7, D9, D21)**
```sql
-- account 1 (America/Chicago, CDT = UTC-5), weeks 2026-05-25..2026-07-20, dedup
WITH RECURSIVE dedup AS (...),
weeks(wk) AS (SELECT '2026-05-25' UNION ALL SELECT date(wk,'+7 days') FROM weeks WHERE wk < '2026-07-20')
SELECT w.wk AS local_week_start,
  SUM(d.event_type='call_received') AS calls,
  SUM(d.event_type='call_received' AND d.outcome IS NOT NULL) AS known_calls,
  SUM(d.event_type='call_received' AND d.outcome IS NULL) AS unknown_calls,
  SUM(d.event_type='call_received' AND d.outcome='missed') AS missed
FROM weeks w LEFT JOIN dedup d ON d.account_id=1
  AND d.occurred_at >= datetime(w.wk,'+5 hours') AND d.occurred_at < datetime(w.wk,'+7 days','+5 hours')
GROUP BY w.wk ORDER BY w.wk
```
```
wk         | calls | known | unknown | missed | rate_pct (missed/known, computed in Python)
2026-05-25 | 25    | 25    | 0       | 5      | 20.0
2026-06-01 | 23    | 22    | 1       | 8      | 36.36
2026-06-08 | 33    | 32    | 1       | 9      | 28.12
2026-06-15 | 32    | 31    | 1       | 11     | 35.48
2026-06-22 | 25    | 25    | 0       | 2      | 8.0
2026-06-29 | 37    | 37    | 0       | 10     | 27.03
2026-07-06 | 23    | 22    | 1       | 5      | 22.73
2026-07-13 | 28    | 28    | 0       | 5      | 17.86
2026-07-20 | 34    | 32    | 2       | 8      | 25.0
```
- All 8 baseline weeks have ≥20 known calls, and the reported week has 32 (matches [G-05](#g-05)). Calls 34 match [G-01](#g-01).
- PERCENTILE.INC over the 8 baseline rates (percent): median **24.8771**, P25 **19.4643**, P75 **29.9647**.
- v = 8/32 = **25.0**. Inside [P25, P75] and |v − m| = 0.1229 < 10 pp → **typical**.
- Unknown-outcome calls in the reported week: **2**.

## G-07
**First and last complete week per account (bounds for the `week` parameter, D29)**

Uses the E-entry helpers (`aw`, IANA zones via `tzdata`).
```sql
SELECT account_id, MIN(wk) AS first_complete_week, MAX(wk) AS last_complete_week, COUNT(*) AS complete_weeks
FROM aw WHERE complete=1 GROUP BY 1 ORDER BY 1
```
- First complete week **2026-02-02**: accounts 1, 4, 5, 6, 7, 12, 14, 18 (25 complete weeks each).
- First complete week **2026-02-09**: accounts 2, 3, 8, 9, 10, 11, 13, 15, 16, 17, 19 (24 complete weeks each).
- Last complete week **2026-07-20** for all 19 accounts. Account 20 has no rows.

## G-08
**Account 1, week 2026-03-02: short baseline and DST-week edges**

Uses the E-entry helpers (`aw`, `ev`, IANA zones via `tzdata`); dedup by grouping on the D8 key within the account.
```sql
SELECT (SELECT COUNT(*) FROM aw WHERE account_id=1 AND complete=1 AND wk < '2026-03-02') AS complete_weeks_before,
  (SELECT COUNT(*) FROM (SELECT 1 FROM ev WHERE account_id=1 AND wk='2026-03-02'
     GROUP BY location, event_type, occurred_at, duration_seconds, outcome)) AS total_dedup,
  week_start_utc('2026-03-02','America/Chicago') AS start_utc, week_end_utc('2026-03-02','America/Chicago') AS end_utc
```
```
4 | 51 | 2026-03-02 06:00:00 | 2026-03-09 05:00:00
```
The week is 167 hours long: it contains the 2026-03-08 DST change ([E-06](#e-06)).

## G-09
**Account 1, total, reported week 2026-03-30 (baseline crosses DST)**

Uses the E-entry helpers (`aw`, `ev`, IANA zones via `tzdata`); dedup by grouping on the D8 key within the account.
```sql
WITH weeks AS (SELECT wk FROM aw WHERE account_id=1 AND complete=1 AND wk BETWEEN '2026-02-02' AND '2026-03-30')
SELECT w.wk, week_start_utc(w.wk,'America/Chicago') AS start_utc, week_end_utc(w.wk,'America/Chicago') AS end_utc,
  (SELECT COUNT(*) FROM (SELECT 1 FROM ev WHERE account_id=1 AND ev.wk=w.wk
     GROUP BY location, event_type, occurred_at, duration_seconds, outcome)) AS total_dedup,
  (SELECT COUNT(*) FROM ev WHERE account_id=1 AND ev.wk=w.wk) AS total_raw
FROM weeks w ORDER BY w.wk
```
```
2026-02-02 | 2026-02-02 06:00:00 | 2026-02-09 06:00:00 | 43 | 43
2026-02-09 | 2026-02-09 06:00:00 | 2026-02-16 06:00:00 | 51 | 51
2026-02-16 | 2026-02-16 06:00:00 | 2026-02-23 06:00:00 | 40 | 40
2026-02-23 | 2026-02-23 06:00:00 | 2026-03-02 06:00:00 | 50 | 50
2026-03-02 | 2026-03-02 06:00:00 | 2026-03-09 05:00:00 | 51 | 51
2026-03-09 | 2026-03-09 05:00:00 | 2026-03-16 05:00:00 | 52 | 52
2026-03-16 | 2026-03-16 05:00:00 | 2026-03-23 05:00:00 | 36 | 36
2026-03-23 | 2026-03-23 05:00:00 | 2026-03-30 05:00:00 | 45 | 45
2026-03-30 | 2026-03-30 05:00:00 | 2026-04-06 05:00:00 | 61 | 61
```
- D6 in Python: baseline sorted [36, 40, 43, 45, 50, 51, 51, 52], v = 61.
- Median **47.5**, P25 **42.25**, P75 **51.0**; gap threshold max(3, 0.3·47.5) = **14.25**.
- v > P75, but v − m = 13.5 < 14.25 → **typical**. This is a test for "outside the range but under the gap".
