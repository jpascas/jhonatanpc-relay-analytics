I decided to use Opus 5.5 for explore and clarify the requirements, i believe you need a strong model to do the heavy lifting upfront like this work.

I used Plan mode.

IMPORTANT: to brief the AI properly I used meta-prompting technique to create the prompts first on a different session (Opus 5.5 for same reason as above), specifying what I'm looking for and i feed my set of good practices (in a document) like:
- strive for identifying and replace Vague words (giving examples like fast, clean, simple, properly, a lot of, etc) with specific values and numbers (but i decided a pre-plan phase was needed to discovering those values)
- i specified via a document that a good direction for the AI includes "The Goal", "The Contraints", "The Edge Cases", "The Check" (the Done). This came from a course i took recently.
- "Done" means showing evidence that i could verify. 
- For myself and for the AI, watch for The XY problem (is when someone asks for help with their solution (Y) instead of their problem (X)). In this case, we have to be careful with the Notes from product ("probably involves comparing against some baseline") and focus on the real pain of the user/customer (customer admin can't tell whether last week was normal, or which location to call first). Focus on answering "What problem are we trying to solve?". This good practice was on the course i took as well, but its something that i learn over years of experience aswell (understand the business and the user pain).

For the first promt, first, additionally to reference the README.md, docs/TICKET.md, docs/PRODUCT_BACKGROUND.md, schema.sql to give proper context for the discovery/explore and clarification phase, i added relevant extra context not in the repo: "EXTRA CONTEXT (from the take-home brief, not in the repo)"

focused on clarifying vague terminology in requirements like "normal", "this week", "baseline", "at a glance", "act on it" using information provided in the docs and the schema and after that  asked to verify it by actually querying the data.

I deliberately asked it to reference the source so i can verify and review and specified when to stop for feedback and provided the definition of done for the result of the prompt.

from the raw session, at the proposed checks i made two decisions

1. Cut check # 9 since location is always identified by (account_id, location), so a name reused across accounts can't cause a problem.
2. Every weekly check uses this definition: a week runs Monday 00:00 to the next Monday 00:00 in the account's IANA timezone. Reason: TICKET.md says the admin should "look at this Monday morning and act on it", so they review the previous complete week. A partial week (at the start or end of the data) is never treated as a full week. Flag partial weeks in the output instead of counting them.


During query execution and check It installed a package without asking. It pip-installed tzdata into a temp folder. That's outside the project, so no harm done, but I never approved it. I will create a CLAUDE.md with instructions to avoid it. Since i believe its a good practice to retrofit your CLAUDE.md file with what has been working and what not.

Also during query execution and check It found and fixed a bug in its own run. Its first pass of checks 11 and 12 dropped week 2026-02-02, i saw re-run the output and verified it.
