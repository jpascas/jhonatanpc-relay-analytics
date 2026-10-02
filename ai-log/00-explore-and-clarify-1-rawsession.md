╭─── Claude Code v2.1.119 ─────────────────────────────────────────────────────╮
│                                                    │ Tips for getting        │
│               Welcome back Jhonatan!               │ started                 │
│                                                    │ Run /init to create a … │
│                       ▐▛███▜▌                      │ ─────────────────────── │
│                      ▝▜█████▛▘                     │ Recent activity         │
│                        ▘▘ ▝▝                       │ No recent activity      │
│       claude-opus-5-5 · API Usage Billing ·        │                         │
│       jpascas@gmail.com's Organization             │                         │
│    C:\Qualitara\jhonatanpc-relay-analytics-test    │                         │
╰──────────────────────────────────────────────────────────────────────────────╯

❯                                                                               
                                                                                
  <pasted_content id="9934">                                                    
  Before any code or analysis, I want to work out what we need to learn from    
  the seed data to implement ticket DASH-247.                                   
                                                                                
  GOAL                                                                          
  Produce a list of data-profiling checks. Each check must exist because        
  something in the requirements leaves a decision open that the data can        
  settle.                                                                       
                                                                                
  READ                                                                          
  README.md, docs/TICKET.md, docs/PRODUCT_BACKGROUND.md, schema.sql.            
  Do NOT open or query seed.sql yet. I want checks derived from the             
  requirements, not from what the data happens to contain.                      
                                                                                
  EXTRA CONTEXT (from the take-home brief, not in the repo)                     
  - "Handle the unglamorous parts of real data honestly: empty ranges, odd      
  values, aggregate correctness."                                               
  - "Aggregates that are actually right against the seed data matter more than  
  feature count."                                                               
  - The backend must have at least one endpoint doing real aggregation over the 
   seed data.                                                                   
  - Out of scope: auth, alerting/notifications, ML/forecasting, production      
  infra, visual polish.                                                         
                                                                                
  STEPS                                                                         
  1. List every statement in those files and in the extra context that leaves a 
   design decision open: vague words in the ticket ("normal", "this week",      
  "baseline", "at a glance", "act on it"), facts about the data model (nullable 
   columns, timestamps, timezones, location counts), and anything said about    
  data quality.                                                                 
  2. For each one, write the question the data could answer, and the design     
  decision that answer would change.                                            
  3. Go through every column in schema.sql and ask: can it be NULL, what range  
  could it have, could rows be duplicated, how is it distributed. Add a check   
  only if the answer could change a decision.                                   
  4. Turn each question into a concrete check: what to count or compare, at     
  what grain (overall, per account, per location, per week), and what result    
  would change the design.                                                      
                                                                                
  CONSTRAINTS                                                                   
  - Every check must quote the exact line it comes from, with the file name, or 
   "take-home brief" for the extra context.                                     
  - No check without a decision it informs. If you can't name the decision,     
  drop the check.                                                               
  - Don't propose solutions or a design yet.                                    
  - Stop after step 4 and wait for me to review the list.                       
                                                                                
  DONE WHEN                                                                     
  One table with the columns: # | source (file + quoted line) | question |      
  check (what to measure, at what grain) | decision it informs.                 
  </pasted_content id="9934">                                                   
                                                                                

  Read 4 files (ctrl+o to expand)

● Updated plan
  ⎿  /plan to preview

● User approved Claude's plan
  ⎿  Plan saved to: 
     ~\.claude\plans\pasted-content-id-9934-before-any-compiled-dream.md · /plan
      to edit
     DASH-247 — Data-profiling checks (derived from requirements only)

     Context

     I need to decide what to learn from seed.sql before designing DASH-247 ("is
      this normal for us?"). These checks come only from README.md,
     docs/TICKET.md, docs/PRODUCT_BACKGROUND.md, schema.sql, and the take-home
     brief. I have not opened seed.sql. Each check names the open decision it
     settles. This list contains no design or solutions. The next step is your
     review.

     Checks

     #: 1
     Source (file + quoted line): TICKET.md: "can't tell whether this week's
     numbers
       are good, bad, or typical"
     Question: What date does "this week" refer to? Does the data reach the
     present,
       or does it stop in the past?
     Check (what to measure, at what grain): Overall MIN/MAX(occurred_at), and
     MAX
       compared with today (2026-10-02).
     Decision it informs: Whether "now" is the wall clock or the latest data
     point
       (an "as-of" anchor). If the data stops in the past, a wall-clock "this
     week"
       is always empty.
     ────────────────────────────────────────
     #: 2
     Source (file + quoted line): TICKET.md: "look at this Monday morning and
     act on
       it"
     Question: Is the last week of data complete or partial?
     Check (what to measure, at what grain): Event count per UTC day for the
     final
       14 days, overall and per account. Look for a drop-off or a truncated last

       day.
     Decision it informs: Whether to compare the last complete week or
     week-to-date.
        Also whether the latest week counts as part of the dataset.
     ────────────────────────────────────────
     #: 3
     Source (file + quoted line): TICKET.md: "probably involves comparing
     against
       some baseline"
     Question: How much history does each account have?
     Check (what to measure, at what grain): Per account: first and last event
     date
       and the number of distinct weeks with events. Show the distribution
     across
       the 20 accounts.
     Decision it informs: Baseline window length (N trailing weeks). Also the
       minimum history needed before showing a comparison, and what to show when

       there is too little.
     ────────────────────────────────────────
     #: 4
     Source (file + quoted line): schema.sql: "created_at TIMESTAMP NOT NULL -- 
     UTC"
     Question: Do events fall before the account was created, or does history
     start
       well after creation?
     Check (what to measure, at what grain): Per account: count of events with
       occurred_at < created_at, plus the gap between created_at and the first
       event.
     Decision it informs: Whether created_at or the first event marks the start
     of
       the baseline. Also whether pre-creation events are excluded or kept.
     ────────────────────────────────────────
     #: 5
     Source (file + quoted line): README.md: "All occurred_at values are stored
     in
       UTC. Each account has an IANA timezone."
     Question: Which timezones exist, and are they all valid IANA names?
     Check (what to measure, at what grain): Distinct accounts.timezone values
     with
       account counts. Flag non-IANA strings ('EST', 'UTC+3', blanks, bad
     casing).
     Decision it informs: Whether week and day bucketing must be per-account
     local
       time. Also the fallback rule for invalid or unknown zones.
     ────────────────────────────────────────
     #: 6
     Source (file + quoted line): README.md: "stored in UTC. Each account has an

       IANA timezone" + TICKET.md: "this week"
     Question: Does local versus UTC bucketing change the weekly numbers in a
       material way?
     Check (what to measure, at what grain): Per account: % of events whose week

       (and day) bucket differs between UTC and local time. Also count of DST
       transitions in the data range for each zone.
     Decision it informs: Whether local-time bucketing is required for correct
       aggregates or is only cosmetic. It also tells me whether DST-aware date
     math
       is needed.
     ────────────────────────────────────────
     #: 7
     Source (file + quoted line): TICKET.md: "is this normal for us"
     Question: Do weekday and time-of-day patterns matter enough to affect what
       "normal" means?
     Check (what to measure, at what grain): Event share by local day-of-week,
     per
       account (and by hour if useful).
     Decision it informs: Whether the baseline compares whole weeks or
       same-weekday/partial-week-to-same-point. That matters for a
     Monday-morning
       view with little current-week data.
     ────────────────────────────────────────
     #: 8
     Source (file + quoted line): README.md: "accounts range from single-site to
      15
       locations"
     Question: Does the data match this, or are location names fragmented?
     Check (what to measure, at what grain): Distinct location per account. Find

       near-duplicates: case, whitespace, punctuation variants such as
     'Downtown'
       and 'downtown '.
     Decision it informs: Location normalization rule, and whether the
     per-location
       view uses raw or cleaned keys.
     ────────────────────────────────────────
     #: 9
     Source (file + quoted line): schema.sql: "location VARCHAR(80) NOT NULL -- 
     the 
       account's site/branch"
     Question: Are location names reused across accounts, for example 'Main'?
     Check (what to measure, at what grain): Count of location strings that
     appear
       under more than one account_id.
     Decision it informs: Whether the location key must be (account_id,
     location)
       everywhere.
     ────────────────────────────────────────
     #: 10
     Source (file + quoted line): PRODUCT_BACKGROUND.md: "customers with
     multiple
       locations struggle to spot which location needs attention"
     Question: Do locations start or stop partway through the data range?
     Check (what to measure, at what grain): Per (account, location): first and
     last
       week seen, and the number of active weeks.
     Decision it informs: How to treat a new location with no baseline, and how
     to
       treat a location that went silent (a real drop or a closed site).
     ────────────────────────────────────────
     #: 11
     Source (file + quoted line): TICKET.md: "multi-location customers matter"
     Question: Are weekly per-location counts large enough for comparisons to be

       stable?
     Check (what to measure, at what grain): Distribution (min/median/p90) of
     weekly
       events per (account, location, event_type). Count of cells under 5 or 10.
     Decision it informs: Whether to use relative (%) or absolute deltas, the
       minimum-volume threshold before flagging, and the lowest grain the
     comparison
        runs at.
     ────────────────────────────────────────
     #: 12
     Source (file + quoted line): TICKET.md: "good, bad, or typical for them"
     Question: How noisy is weekly volume, and are there outlier weeks?
     Check (what to measure, at what grain): Per account and per location:
       weekly-count coefficient of variation. Find weeks that are more than 3×
     or
       less than ⅓ of the median, and check whether they cluster in the same
       calendar week across accounts.
     Decision it informs: Baseline statistic (mean or median/robust) and the
     band
       definition for "typical". Also whether outlier weeks are excluded from
     the
       baseline.
     ────────────────────────────────────────
     #: 13
     Source (file + quoted line): take-home brief: "Handle the unglamorous parts
      of
       real data honestly: empty ranges"
     Question: Are there zero-event weeks or days inside an active period?
     Check (what to measure, at what grain): Per (account, location): weeks with
      0
       events between first and last seen week. Also runs of consecutive zero
     days.
     Decision it informs: Whether gap periods count as zeros in the baseline or
     as
       missing data. Also how empty ranges are shown.
     ────────────────────────────────────────
     #: 14
     Source (file + quoted line): schema.sql: "event_type ... -- 'call_received'
      | 
       'lead_created' | 'appointment_set'"
     Question: Is event_type limited to these 3 values?
     Check (what to measure, at what grain): Distinct event_type with counts.
     Flag
       casing/whitespace variants and unknown values.
     Decision it informs: The metric list and the normalization or exclusion
     rule
       for unexpected types.
     ────────────────────────────────────────
     #: 15
     Source (file + quoted line): PRODUCT_BACKGROUND.md: "track inbound customer

       activity — calls, leads, appointments"
     Question: Does every account and location produce all 3 types?
     Check (what to measure, at what grain): Event-type mix per account and per
       location. Count of accounts or locations with 0 of a given type.
     Decision it informs: Whether metrics are shown per type or as a total, and
     how
       a metric the account doesn't use is displayed.
     ────────────────────────────────────────
     #: 16
     Source (file + quoted line): schema.sql: "outcome ... -- e.g. 'connected' |
      
       'missed' | 'voicemail' | 'converted' | 'no_show'; may be NULL"
     Question: What outcome values exist, how often are they NULL, and do they
     pair
       with event types in a consistent way?
     Check (what to measure, at what grain): Distinct outcome with counts. NULL
     rate
       per event_type. Cross-tab of event_type × outcome, flagging pairs like
       'no_show' on a call.
     Decision it informs: Whether rate metrics (missed-call rate, conversion,
       no-show rate) are reliable enough to include. Also how NULL outcomes
     enter
       denominators.
     ────────────────────────────────────────
     #: 17
     Source (file + quoted line): schema.sql: "duration_seconds INTEGER NULL -- 
     only
       meaningful for calls; may be NULL"
     Question: How complete and how sane is duration on calls?
     Check (what to measure, at what grain): For calls: NULL rate, count of 0
     and
       negative values, min/median/p99/max, and values over 4h. For non-calls:
     count
        of non-NULL durations.
     Decision it informs: Whether call duration becomes a metric, plus
       exclusion/trimming rules for bad values.
     ────────────────────────────────────────
     #: 18
     Source (file + quoted line): take-home brief: "Aggregates that are actually

       right against the seed data"
     Question: Are some events duplicated under different ids?
     Check (what to measure, at what grain): Count of groups with identical
       (account_id, location, event_type, occurred_at, duration_seconds,
     outcome)
       and more than one row. Also bursts of more than N events at the same
       timestamp per account.
     Decision it informs: Whether aggregation dedupes, and on which key. This
       directly affects count correctness.
     ────────────────────────────────────────
     #: 19
     Source (file + quoted line): schema.sql: "account_id INTEGER NOT NULL 
       REFERENCES accounts(id)"
     Question: Does every event map to an existing account, and does every
     account
       have events?
     Check (what to measure, at what grain): Events whose account_id is not in
       accounts. Accounts with 0 events.
     Decision it informs: Whether to drop or report orphan events (FKs may not
     be
       enforced on load), and the empty-account state.
     ────────────────────────────────────────
     #: 20
     Source (file + quoted line): take-home brief: "odd values" + schema.sql:
       "occurred_at TIMESTAMP NOT NULL -- UTC"
     Question: Are there implausible timestamps?
     Check (what to measure, at what grain): Count of events in the future
     relative
       to the data's end and to today. Also sentinel or epoch dates (e.g. 1970,
       1900) and exact-midnight clustering per account.
     Decision it informs: Timestamp exclusion filters, and whether midnight
       clustering points to date-only data that local-time bucketing would put
     in
       the wrong day.
     ────────────────────────────────────────
     #: 21
     Source (file + quoted line): TICKET.md: "this week's numbers"
     Question: Do some accounts have no recent activity at all?
     Check (what to measure, at what grain): Per account and per location: days
       between its last event and the dataset's max occurred_at.
     Decision it informs: Whether a zero current week means "dropped, needs
       attention" or "dormant/churned", which changes how it is presented.
     ────────────────────────────────────────
     #: 22
     Source (file + quoted line): take-home brief: "at least one endpoint doing
     real
       aggregation" + "Aggregates that are actually right"
     Question: What are the reference totals that any aggregation must
     reproduce?
     Check (what to measure, at what grain): An independent count of events per
       account × event_type × local week, with a check that location subtotals
     sum
       to the account total.
     Decision it informs: Golden values for tests. Also confirms that the
     bucketing
       rules chosen above give consistent totals.

     Checks considered and dropped (no decision found)

     - accounts.name and accounts.industry distributions. The ticket says
     "typical for them", which points to self-comparison. No requirement asks
     for peer or industry baselines.
     - activity_events.id / accounts.id uniqueness. Both are primary keys and
     the schema enforces this.

     Next step

     Wait for your review of this list. After that, run the checks against
     seed.sql. No design work until then.

● I've written 22 data-profiling checks, all taken from the four files and the
  brief. I haven't opened or queried seed.sql yet. Approving the plan only took
  us out of plan mode. You asked me to stop for your review, so I'm stopping
  here. The full table, with the quoted source line for each check, is in the
  plan file: C:\Users\jpasc\.claude\plans\pasted-content-id-9934-before-any-comp
  iled-dream.md.

  Area: What "this week" is
  #: 1, 2, 21
  What the data settles: Whether "now" is today's date or the last date in the
    data (they may differ), whether the last week is complete, and whether a
    quiet account is dormant or has dropped
  ────────────────────────────────────────
  Area: How much baseline history exists
  #: 3, 4
  What the data settles: How many weeks the baseline window covers and the
    minimum history needed; whether the baseline starts at created_at or at the
    first event
  ────────────────────────────────────────
  Area: Timezones and week edges
  #: 5, 6, 7, 20
  What the data settles: Whether timezone values are valid IANA names, whether
    counting weeks in local time instead of UTC changes the totals, weekday
    patterns (which matter for a Monday-morning view), and timestamps that are
  in
     the future, sentinel dates, or pile up at midnight
  ────────────────────────────────────────
  Area: Locations
  #: 8, 9, 10
  What the data settles: Location names that differ only by case or spacing,
    names reused across accounts (so the key is account plus location), and
    locations that start or stop partway through
  ────────────────────────────────────────
  Area: How stable the comparisons are
  #: 11, 12, 13
  What the data settles: Weekly counts too small for percentage changes, how
    noisy the weeks are and which are outliers (mean vs. median baseline), and
    zero-event weeks (real zero or missing data)
  ────────────────────────────────────────
  Area: Event types and the nullable columns
  #: 14, 15, 16, 17
  What the data settles: Whether event_type holds only the three listed values,
    which types each account actually uses, whether outcome is reliable enough
    for rate metrics, and how complete and sensible call duration_seconds is
  ────────────────────────────────────────
  Area: Whether aggregates are right
  #: 18, 19, 22
  What the data settles: Duplicate events with different ids, events whose
    account doesn't exist, and reference totals to test against

  I dropped two candidates because I couldn't name a decision they would change:
  - Industry and name distributions: the ticket says "typical for them", which
  means each account is compared against itself, not against peers.
  - Primary-key uniqueness: the schema already enforces it.

  Tell me what to add, cut or rescope. Once you've signed off, I'll run the
  checks against seed.sql.

❯                                                                               
                                                                                
  <pasted_content id="9934">                                                    
  I've reviewed the checks.                                                     
                                                                                
  Have the following considerations:                                            
                                                                                
  1. Cut check # 9 since location is always identified by (account_id,          
  location), so a name reused across accounts can't cause a problem.            
  2. Every weekly check uses this definition: a week runs Monday 00:00 to the   
  next Monday 00:00 in the account's IANA timezone. Reason: TICKET.md says the  
  admin should "look at this Monday morning and act on it", so they review the  
  previous complete week. A partial week (at the start or end of the data) is   
  never treated as a full week. Flag partial weeks in the output instead of     
  counting them.                                                                
                                                                                
                                                                                
  After that:                                                                   
  Load schema.sql and seed.sql into a throwaway SQLite database in /tmp using   
  Python's built-in sqlite3. Don't add anything to the project.                 
                                                                                
  For each check, show the query and its output, then one line on what it means 
   for the decision it informs. Every number you state must come from a query   
  run in this session; otherwise write "not checked". Don't recommend a design  
  yet. Stop when all checks are done.                                           
  </pasted_content id="9934">                                                   
                                                                                

● Bash(python --version; python -c "import zoneinfo;
      print(zoneinfo.ZoneInfo('America/Chicago'))"; ls -la /tmp | head -3;
      cygpath -w /tmp; wc -c seed.sql; head -c 1500…)
  ⎿  Python 3.14.7
     Traceback (most recent call last):
       File "C:\Users\jpasc\AppData\Local\Python\pythoncore-3.14-64\Lib\zon
     … +60 lines (ctrl+o to expand)

● Bash(ls /usr/share/zoneinfo 2>&1 | head; cygpath -w /usr/share/zoneinfo; ls
      /usr/share/zoneinfo/America 2>&1 | head -3)
  ⎿  ls: cannot access '/usr/share/zoneinfo': No such file or directory
     C:\Program Files\Git\usr\share\zoneinfo
     ls: cannot access '/usr/share/zoneinfo/America': No such file or direc
     tory

● Bash(rm -f /tmp/dash247.db && python -c "
      import sqlite3…)
  ⎿  accounts 20
     activity_events 12626
     [('America/Chicago', 6), ('America/Denver', 2), ('America/Los_Angeles'
     … +6 lines (ctrl+o to expand)
● Bash(python -m pip install --quiet --target
      "C:/Users/jpasc/AppData/Local/Temp/dash247_deps" tzdata 2>&1 | tail -3;
      PYTHONPATH="C:/Users/jpasc/AppData/Local/Temp/das…)
  ⎿  2026d America/Chicago

● Bash(mkdir -p /tmp/dash247 && cat > /tmp/dash247/q.py <<'EOF'
      import sqlite3, sys…)
  ⎿  ## Check 1
     ```sql
     SELECT MIN(occurred_at) AS min_utc, MAX(occurred_at) AS max_utc,
     … +26 lines (ctrl+o to expand)

● Checks 1–2 are done: the data runs from 2026-02-01 to Monday 2026-07-27,
  ending 66 days before today. Next I'm building the local-time week tables and
  running the rest of the checks.

● Bash(cat >> /tmp/dash247/q.py <<'EOF'
      def prep(c):…)
  ⎿  cutoff 2026-07-27 22:20:34
     ## Check 2 (per account)
     ```sql
     … +51 lines (ctrl+o to expand)

● Bash(cd /tmp/dash247 && python - <<'EOF'
      from q import *…)
  ⎿  ## Check 3
     ```sql
     SELECT a.id AS account_id, MIN(ev.lts) AS first_local, MAX(ev.lts) AS
     … +96 lines (ctrl+o to expand)

● Bash(cd /tmp/dash247 && python - <<'EOF'
      from q import *…)
  ⎿  ## Check 6
     ```sql
     SELECT account_id, tz, COUNT(*) AS events,
     … +91 lines (ctrl+o to expand)

● Bash(cd /tmp/dash247 && python - <<'EOF'
      from q import *…)
  ⎿  ## Check 6/7 follow-up: does the UTC claim hold? Hour-of-day by tz, lo
     cal vs UTC
     ```sql
     … +34 lines (ctrl+o to expand)

● Bash(cd /tmp/dash247 && python - <<'EOF'
      from q import *…)
  ⎿  ## Check 8
     ```sql
     SELECT account_id, COUNT(DISTINCT location) AS raw_locations,
     … +30 lines (ctrl+o to expand)

● Bash(cd /tmp/dash247 && python - <<'EOF'
      from q import *…)
  ⎿  ## Check 8 (all names, for non-mechanical near-duplicates)
     ```sql
     SELECT account_id, GROUP_CONCAT(location || ' (' || n || ')', ' | ') A
     … +151 lines (ctrl+o to expand)

● Checks 1–8 and 10 are done. Locations look clean ("Site A"–"Site O", with no
  name variants). Now I'm running the volume, noise, and zero-week checks
  (11–13).

● Bash(cd /tmp/dash247 && python - <<'EOF'
      from q import *…)  ⎿  ## Check 10 (cleaner: days between location first/last event and its a
     ccount first/last event)
     ```sql
     … +45 lines (ctrl+o to expand)

● Bash(cd /tmp/dash247 && python - <<'EOF'
      from q import *…)
  ⎿  ## Check 11
     ```sql
     -- cell = complete local weeks only, zero-filled
     … +67 lines (ctrl+o to expand)

● Bash(cd /tmp/dash247 && python - <<'EOF'
      from q import *…)
  ⎿  ## Check 11 (account grain, corrected)
     ```sql
     CREATE TEMP TABLE acell AS SELECT aw.account_id, t.event_type, aw.wk,
     … +73 lines (ctrl+o to expand)

● Bash(cd /tmp/dash247 && python - <<'EOF'
      from q import *…)
  ⎿  ## Check 12 follow-up
     ```sql
     WITH t AS (SELECT account_id, wk, COUNT(*) n FROM ev GROUP BY 1,2)
     … +60 lines (ctrl+o to expand)

● Bash(cd /tmp/dash247 && python - <<'EOF'
      from q import *…)
  ⎿  ## Check 16
     ```sql
     SELECT event_type, COALESCE(outcome,'<NULL>') AS outcome, '[' || COALE
     … +67 lines (ctrl+o to expand)

● Bash(cd /tmp/dash247 && python - <<'EOF'
      from q import *…)
  ⎿  ## Check 18
     ```sql
     SELECT COUNT(*) AS dup_groups, SUM(cnt) AS rows_in_groups, SUM(cnt-1)
     … +83 lines (ctrl+o to expand)

● Bash(cd /tmp/dash247 && python - <<'EOF'
      from q import *…)
  ⎿  ## Check 19
     ```sql
     SELECT (SELECT COUNT(*) FROM activity_events e WHERE NOT EXISTS (SELEC
     … +77 lines (ctrl+o to expand)

● Bash(cd /tmp/dash247 && python - <<'EOF'
      from q import *…)
  ⎿  <stdin>:24: SyntaxWarning: "\d" is an invalid escape sequence. Such se
     quences will not work in the future. Did you mean "\\d"? A raw string
     is also an option.
     … +62 lines (ctrl+o to expand)

● All 21 checks have run (9 is cut). Every number below comes from a query run
  in this session, against a throwaway database at %TEMP%\dash247.db (Git Bash's
   /tmp). Nothing was added to the project.

  Before the results

  - I installed one package outside the project. Python on this machine has no
  timezone data, so I installed tzdata into %TEMP%\dash247_deps. Converting to
  local time doesn't work without it.
  - Week rule as you defined it. A week runs Monday 00:00 to the next Monday
  00:00 in the account's IANA timezone. A week is "complete" only if it starts
  at or after the entity's first event and ends at or before the last event in
  the data (cutoff 2026-07-27 22:20:34 UTC). Everything else is flagged partial.
  - One open question about the start edge. "Start of the data" could mean each
  account's or location's first event, or the first event in the whole dataset.
  With the per-entity reading, week 2026-02-02 is partial for 11 of 19 accounts
  and 53 of 69 locations. With the dataset-wide reading (first event 2026-02-01 
  10:57:44 UTC; the earliest local Monday 2026-02-02 00:00 in UTC is 2026-02-02 
  00:00:00), it would be complete for all of them. I used per-entity below.
  - I fixed a bug in my own run. My first pass of checks 11 and 12 built account
   totals from location-level week grids, which dropped week 2026-02-02 for most
   locations. The account-level numbers below are from the rerun on the
  account-level grid.

  ---
  1. Does the data reach today?

  SELECT MIN(occurred_at), MAX(occurred_at),
         CAST(julianday('2026-10-02') - julianday(MAX(occurred_at)) AS INT) AS
  days_before_today, COUNT(*)
  FROM activity_events
  2026-02-01 10:57:44 | 2026-07-27 22:20:34 | 66 | 12626
  All 12,626 occurred_at values are 19-character text.
  Meaning: a "this week" based on today's date has 0 events. "Now" has to be
  anchored to the end of the data, or every view is empty.

  2. Is the last week complete?

  SELECT date(occurred_at), strftime('%w',occurred_at), COUNT(*), COUNT(DISTINCT
   account_id), MAX(time(occurred_at))
  FROM activity_events WHERE occurred_at >= date((SELECT MAX(occurred_at) FROM
  activity_events), '-13 days') GROUP BY 1
  2026-07-20 Mon 95 | ... | 2026-07-24 Fri 78 | 07-25 Sat 25 | 07-26 Sun 23 |
  2026-07-27 Mon 79 (last ts 22:20:34)
  The local week starting 2026-07-27 is partial for all 19 accounts that have
  events. The last complete week is 2026-07-20 for all 19. Across the grid there
   are 464 complete and 38 partial account-weeks, and the partial ones fall only
   in weeks 2026-01-26 (8 accounts), 2026-02-02 (11) and 2026-07-27 (19).
  Meaning: the data ends on a Monday afternoon (local), which matches your
  Monday-morning framing. The latest complete week is 2026-07-20, and week
  2026-07-27 must be flagged partial.

  3. How much history does each account have?

  SELECT a.id, MIN(ev.lts), MAX(ev.lts), COUNT(ev.id),
    (SELECT SUM(complete) FROM aw WHERE aw.account_id=a.id) complete_weeks,
    (SELECT COUNT(*) FROM aw WHERE aw.account_id=a.id AND complete=0)
  partial_weeks, ...
  FROM accounts a LEFT JOIN ev ON ev.account_id=a.id GROUP BY a.id
  Accounts 1–19 have 24 or 25 complete weeks and 2 partial weeks each, and no
  complete week with 0 events. Event totals range from 167 (account 16) to 2,641
   (account 6). Account 20 has 0 events.
  Meaning: every account with data has about 24 weeks of baseline history, so a
  lack of history is a problem for only one account (20).

  4. How does created_at compare with the first event?

  SELECT a.id, a.created_at, MIN(e.occurred_at),
         ROUND(julianday(MIN(e.occurred_at)) - julianday(a.created_at),1),
  SUM(e.occurred_at < a.created_at)
  FROM accounts a LEFT JOIN activity_events e ON e.account_id=a.id GROUP BY a.id
  No events fall before created_at, for any account. The gap between creation
  and first event is 60.9 to 393.8 days. Every account's first event is between
  2026-02-01 and 2026-02-03.
  Meaning: created_at has nothing to do with when the history starts. The data
  window starts around 2026-02-01 for everyone, so the baseline can only start
  at the first event.

  5. Are the timezones valid?

  SELECT timezone, is_iana(timezone), COUNT(*), GROUP_CONCAT(id) FROM accounts
  GROUP BY 1
  America/Chicago 1 6 | America/Denver 1 2 | America/Los_Angeles 1 4 |
  America/New_York 1 5 | America/Phoenix 1 2 | UTC 1 1 (acct 18)
  Meaning: all 6 values are valid IANA names, so the seed data never hits an
  invalid-zone fallback. Phoenix (no DST) and UTC are both present.

  6. Does local versus UTC bucketing change the numbers?

  SELECT account_id, tz, COUNT(*), SUM(wk <> utc_week_start(occurred_at)),
  ROUND(100.0*...,2),
         SUM(date(lts) <> date(occurred_at)), ROUND(100.0*...,1) FROM ev GROUP
  BY 1,2
  - Week bucket: at most 2 events per account change week (max 0.37%,
  account 9); 11 accounts have 0.
  - Day bucket: up to 17 events (account 12), max 1.9% (account 17).
  - DST: one transition in range, on 2026-03-08, for Chicago, Denver, Los
  Angeles and New York; none for Phoenix or UTC. 0 events fall in the 02:xx
  local hour that day.
  - Odd value: the mean UTC hour is 15.3–15.6 in every timezone, including the
  UTC account, while the mean local hour ranges from 8.7 (Los Angeles) to 11.3
  (New York).

  Meaning: local and UTC bucketing barely change the weekly totals in this seed.
   Local time still matters for correct edges, and DST affects only one week.
  The hour-of-day pattern doesn't follow local business hours, so hourly local
  patterns aren't reliable.

  7. Are there weekday patterns?

  SELECT account_id, ROUND(100.0*SUM(strftime('%w',lts)='1')/COUNT(*),1) AS mon,
   ... AS sun FROM ev GROUP BY 1
  Weekdays are about 13–22% each and Saturday/Sunday 0.5–6.0% each, in every
  account. Account 6 has Wednesday at 42.2%, which is explained by a single day
  (see 12). By local hour, 0–1 holds 0.5–0.8% and 10 holds 9.6%.
  Meaning: weekday shape is strong, so comparing a partial week against full
  weeks would be biased. Whole complete weeks avoid that.

  8. Are location names fragmented?

  SELECT account_id, COUNT(DISTINCT location), COUNT(DISTINCT
  lower(trim(location))), COUNT(DISTINCT <alnum-normalized>)
  FROM activity_events GROUP BY 1
  The three counts are identical for every account, and the variant query
  returned 0 rows. Names are Site A … Site O. Location counts per account are 1
  (accounts 8, 13, 16, 19) up to 15 (account 6), for 69 locations total.
  Meaning: no normalization is needed in this seed. The range of 1 to 15
  locations matches the README.

  9. (Cut, as you asked.)

  10. Do locations start or stop partway through?

  WITH l AS (... MIN/MAX per account,location), a AS (... MIN/MAX per account)
  SELECT MAX(julianday(l.f)-julianday(a.f)), MAX(julianday(a.t)-julianday(l.t)),
         SUM(julianday(l.f)-julianday(a.f) > 14),
  SUM(julianday(a.t)-julianday(l.t) > 14) FROM l JOIN a USING(account_id)
  4.4 | 5.0 | 0 | 0
  Meaning: no location opens or closes mid-range (all are within 5 days of their
   account's edges), so new or closed locations never come up in this seed.

  11. Are weekly counts large enough for stable comparisons?

  Counts are zero-filled, complete weeks only.

  ┌─────────────────────────┬───────┬────────┬──────────┬──────────┬───────┐
  │          Grain          │ Cells │ Median │ Under 5  │ Under 10 │ Zeros │
  ├─────────────────────────┼───────┼────────┼──────────┼──────────┼───────┤
  │ account × week          │ 464   │ 20     │ 3 (1%)   │ 71 (15%) │ 0     │
  ├─────────────────────────┼───────┼────────┼──────────┼──────────┼───────┤
  │ account × type × week   │ 1,392 │ 5      │ 589      │ 980      │ 62    │
  │                         │       │        │ (42%)    │ (70%)    │       │
  ├─────────────────────────┼───────┼────────┼──────────┼──────────┼───────┤
  │ account × location ×    │ 1,672 │ 7      │ 398      │ 1,358    │ 4     │
  │ week                    │       │        │ (24%)    │ (81%)    │       │
  ├─────────────────────────┼───────┼────────┼──────────┼──────────┼───────┤
  │ account × location ×    │ 5,016 │ 2      │ 4,253    │ 4,954    │ 1,042 │
  │ type × week             │       │        │ (85%)    │ (99%)    │       │
  └─────────────────────────┴───────┴────────┴──────────┴──────────┴───────┘

  By type at account × week, the means are calls 16.4, leads 6.4 and
  appointments 3.8. Appointments have 52 zero weeks.
  Meaning: at location × type the counts are mostly 0 to 5, so percentage
  changes are unstable there. Account × week is the only grain where almost
  every cell is 10 or more.

  12. How noisy is weekly volume, and are there outliers?

  SELECT account_id, wk, SUM(n) FROM acell GROUP BY 1,2   -- per account: CV, 
  median, weeks >3x or <1/3 median
  - Normal noise: account-level coefficient of variation (CV) is 0.16–0.30,
  except account 6 at 1.51 (0.18 once outliers are excluded). Location-level CV
  across 69 locations is 0.18–1.78, median 0.39.
  - Big spike: account 6, week 2026-06-01, has 881 events against a median
  of 76. 805 of them are on 2026-06-03 (local), spread across all 15 locations
  and all 3 types, from 00:00 to 19:00 local. Only 2 of those rows have an exact
   twin.
  - Small dips: account 16 has 2 events in each of weeks 2026-03-23 and
  2026-04-06, against a median of 7.
  - No shared outlier weeks: in the same-week query, 2026-06-01 is the only week
   with any accounts high (2) and every week has at most 2 accounts low.

  Meaning: a mean-based baseline for account 6 is inflated by about 38% (mean
  105.1 vs median 76.0). Outlier handling and the choice of baseline statistic
  both affect the result.

  13. Are there zero weeks or empty days?

  SELECT lw.account_id, lw.location, lw.wk FROM lw WHERE complete=1 AND NOT
  EXISTS (SELECT 1 FROM ev WHERE ... ev.wk=lw.wk)
  6 Site G 2026-04-13 | 6 Site G 2026-06-29 | 6 Site N 2026-02-23 | 18 Site C
  2026-03-23   (4 of 1,672 complete location-weeks)
  No account has a complete week with 0 events. The longest run of zero-event
  local days is 4 days (accounts 16, 13 and 8).
  Meaning: empty ranges are rare but real at location grain, so the
  zero-versus-missing rule only matters for those 4 location-weeks. Zero days
  mostly fall on weekends.

  14. Is event_type limited to the 3 values?

  SELECT event_type, '['||event_type||']', length(event_type), COUNT(*) FROM
  activity_events GROUP BY 1
  call_received 7780 | lead_created 3044 | appointment_set 1802
  Meaning: only the 3 documented values appear, with no variants, so no
  normalization rule is needed.

  15. Does every account and location produce all 3 types?

  SELECT account_id, SUM(event_type='call_received'), SUM(...='lead_created'),
  SUM(...='appointment_set') FROM activity_events GROUP BY 1
  Every account has all 3 types (minimum: 18 appointments, account 16). The
  query for locations missing a type returned 0 rows.
  Meaning: no account or location lacks a type, so "metric not used" never comes
   up. Low appointment volume is the constraint instead (see 11).

  16. Are the outcomes reliable enough for rate metrics?

  SELECT event_type, COALESCE(outcome,'<NULL>'), COUNT(*),
  ROUND(100.0*COUNT(*)/SUM(COUNT(*)) OVER (PARTITION BY event_type),1)
  FROM activity_events GROUP BY 1,2
  appointment_set: completed 1439 (79.9) | no_show 305 (16.9) | NULL 58 (3.2)
  call_received:   connected 4669 (60.0) | missed 1937 (24.9) | voicemail 934
  (12.0) | NULL 240 (3.1)
  lead_created:    open 1871 (61.5) | converted 1073 (35.2) | NULL 100 (3.3)
  - No mismatches: no outcome appears under the wrong event type.
  - Undocumented values: completed and open aren't in the schema's example list.
  - NULL rate: 2.0–4.4% per month for each type, and at most 5.1% per account
  for calls.

  Meaning: outcomes are clean enough for rate metrics, since the NULL rate is
  small and steady. How NULLs enter the denominator still has to be decided.

  17. Is call duration usable?

  SELECT event_type, COUNT(*), SUM(duration_seconds IS NULL), SUM(=0), SUM(<0),
  SUM(>14400), MIN, MAX, SUM(typeof NOT IN ('integer','null'))
  FROM activity_events GROUP BY 1
  call_received 7780 | null 313 | zero 0 | neg 0 | >4h 0 | min 20 | max 1500
  leads/appts: duration NULL on every row
  For non-NULL calls, percentiles are p1 37, median 754, p99 1484, max 1500.
  Missed calls average 737 s, close to connected calls (765 s).
  Meaning: the values are within range, but duration doesn't vary with outcome.
  A missed call lasting about 12 minutes is implausible, which weakens any
  duration metric.

  18. Are events duplicated?

  SELECT COUNT(*), SUM(cnt), SUM(cnt-1) FROM (SELECT COUNT(*) cnt FROM
  activity_events
    GROUP BY account_id, location, event_type, occurred_at,
  COALESCE(duration_seconds,-1), COALESCE(outcome,'<NULL>') HAVING COUNT(*)>1)
  12 groups | 24 rows | 12 extra rows
  Every duplicate pair has consecutive ids (e.g. 11266/11267). They are spread
  over 7 accounts, up to 4 in account 6. The looser key (no duration or outcome)
   also finds 12. Same-second events within one account: 19 pairs, none larger.
  Meaning: deduplicating changes complete-week totals from 12,361 to 12,349.
  That decides whether aggregates are exactly right, though the effect on any
  single week is at most 1.

  19. Do all events map to an account, and does every account have events?

  SELECT (orphan events), (accounts without events), (which)
  0 | 1 | 20:Quiet Harbor Spa:America/Los_Angeles:2025-12-04 12:00:00
  Meaning: there are no orphan events, but account 20 needs an empty state with
  no history and no current week.

  20. Are there implausible timestamps?

  SELECT SUM(occurred_at > '2026-10-02'), SUM(< '2000-01-01'), SUM(bad format),
  SUM(unparseable),
         SUM(time(occurred_at)='00:00:00'), SUM(time(lts)='00:00:00'), SUM(on
  the hour) FROM ev
  All 7 counts are 0. All 20 accounts.created_at values are at 12:00:00.
  Meaning: no timestamp filters are needed, and nothing suggests date-only
  values being bucketed into the wrong day.

  21. Are any accounts or locations inactive?

  SELECT account_id, MAX(occurred_at), ROUND(julianday(<data end>) -
  julianday(MAX(occurred_at)),1) FROM activity_events GROUP BY 1
  The most stale account is 8, at 3.2 days. The most stale location is 11/Site
  A, at 5.3 days. In the last complete week (2026-07-20), all 19 accounts have
  events: 7 for account 8 against a mean of 10.5, and 87 for account 6 against a
   mean of 105.1.
  Meaning: nothing is dormant, so a zero "this week" never needs to be read as
  churn in this seed (account 20 aside).

  22. Reference totals

  WITH acct AS (... per account,type,wk), loc AS (... location subtotals rolled
  up)
  SELECT COUNT(*), SUM(acct.n), SUM(loc.n), SUM(acct.n <> loc.n), (SELECT
  COUNT(*) FROM activity_events) FROM acct JOIN loc USING(...)
  1405 cells | 12626 | 12626 | 0 mismatches | 12626 raw
  - Split: complete weeks hold 12,361 raw events (12,349 deduped), partial
  weeks 265.
  - Golden totals, week 2026-07-20: account 1 = 34/12/7
  (calls/leads/appointments), 53 total; account 6 = 87; account 18 (UTC) runs
  from 2026-07-20 00:00 to 2026-07-27 00:00 UTC. That week contains no
  duplicates, so raw and deduped totals match.
  - File: full counts per account × location × type × week are in
  %TEMP%\dash247\golden_account_location_type_week.csv (4,173 rows).

  Meaning: location subtotals reconcile exactly with account totals. The golden
  values let us test any endpoint, raw or deduped.

  ---
  Not checked:
  - Whether occurred_at is really UTC. The flat UTC hour profile in check 6 is
  consistent with timestamps generated without regard to local time, but the
  data can't prove it either way.
  - Hour-level patterns per location.