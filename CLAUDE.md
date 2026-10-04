# CLAUDE.md

Project: **daily-stock-summary** — take-home assessment (.NET 10 API + React frontend).
**Read `IMPLEMENTATION_SPEC.md` first** in every new, compacted, or joined session. It is the source of truth for scope, architecture, and progress.

## Ground Rules

1. **Never add, commit, or push any changes.** The user does all git work manually (`git add`, `git commit`, `git push`, and anything that stages or publishes).
2. **Never expose API keys.** `.env` and `.env.local` are git-ignored, but as an additional safeguard never include any API key or secret in thinking text, output, comments, or code. Read secrets only via environment/config at runtime.
3. **Verify pivotal changes.** After a pivotal change, check for issues with eslint and `npm run build` (frontend) and `dotnet build` / `dotnet test` (backend). Fix any issues immediately, before moving to the next task.
4. **Plan before pivotal changes.** Enter Plan mode before making a pivotal change so the user can collaborate on what will be done.
5. **Use subagents** for parallelizable work (research, independent files, reviews) to move faster.
6. **Log every prompt.** After every prompt the user sends, append it to `PROMPT_LOG.md` using the existing template. Put the prompt in quotations and leave `Reasoning:` and `Adjustments:` blank (the user fills them in). Leave a blank line after each {prompt, reasoning, adjustments} set.

## Skills

- `test-driven-development` (in `.claude/skills/`): use for **backend** work (API logic, services, parsing/aggregation). **Do not use it for frontend development.**
- `frontend-design` and `pr-review-toolkit` are being added by the user as plugins; use them for frontend UI work and review respectively once available.

## Spec Maintenance

- Keep `IMPLEMENTATION_SPEC.md` current. When a phase is complete, strike it through (`~~...~~`) and mark its status `DONE`.
- If work stops mid-phase, add a note in that phase's **Progress Notes** stating what is done and what remains.
- Update the spec whenever a decision changes the plan, not only at the end of a phase.
