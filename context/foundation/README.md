# Foundation Docs

Cross-change living documents that span multiple changes. Each project picks which foundation docs it needs (e.g. product requirements, tech-stack, roadmap, glossary, test-stack). Foundation docs are owned by the skills that read and write them; this README describes the conventions that apply to all of them.

## Current index

- `prd.md` - product requirements.
- `roadmap.md` - roadmap, current product state, and parked/in-progress/done outcomes.
- `tech-stack.md` - selected stack hand-off.
- `architecture/architecture-guide.md` - canonical architecture rules.
- `domain.md` - domain specification and model notes.
- `test-plan.md` - risk-first testing strategy, rollout history, and cookbook patterns.
- `infrastructure.md` - infrastructure research and platform decision.
- `deploy-plan.md` - Render deployment plan and operational gates.
- `performance/optimization.md` - performance reference notes.
- `shape-notes.md` - shaping notes used to produce the PRD.
- `bootstrap-verification.md` - scaffold/bootstrap verification history.

## Update convention

**Edit-in-place.** Foundation docs evolve over the lifetime of the project. When something changes incrementally (a new dependency, a refined product goal, a shifted milestone), edit the existing file. Don't create dated copies.

## Archive convention

When a foundation doc is fully superseded — replaced by a new approach rather than refined — move it to `foundation/archive/YYYY-MM-DD-<doc>.md` and write the replacement at the original path. The archive folder is a historical record; nothing reads from it routinely.

## Anti-pattern

Do **not** put change-scoped docs here. Anything tied to a single change (its plan, its research, its review) belongs under `context/changes/<change-id>/`. Foundation is for what outlives any one change.
