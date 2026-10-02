I decided to use Opus 5.5 for explore and clarify the requirements, i belive you need a strong model to do the heavy lifting upfront like this work.

I used Plan mode.

from the rawsession, at the proposed checks i made two decisions

1. Cut check # 9 since location is always identified by (account_id, location), so a name reused across accounts can't cause a problem.
2. Every weekly check uses this definition: a week runs Monday 00:00 to the next Monday 00:00 in the account's IANA timezone. Reason: TICKET.md says the admin should "look at this Monday morning and act on it", so they review the previous complete week. A partial week (at the start or end of the data) is never treated as a full week. Flag partial weeks in the output instead of counting them.


During query execution and check It installed a package without asking. It pip-installed tzdata into a temp folder. That's outside the project, so no harm done, but I never approved it. I will create a CLAUDE.md with instructions to avoid it.

Also during query execution and check It found and fixed a bug in its own run. Its first pass of checks 11 and 12 dropped week 2026-02-02, i saw re-run the output and verified.
