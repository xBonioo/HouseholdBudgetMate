# Context

This directory is the canonical home for AI-readable project context. Keep root files concise and link here instead of duplicating requirements, plans, research, decisions, or testing strategy.

## Directory Map

- `foundation/` - durable project knowledge that spans many changes: PRD, roadmap, architecture, tech stack, deployment, test strategy, and long-lived reference docs.
- `changes/` - in-flight change folders. Each normal change should have a `change.md` identity file plus any research, plan, review, or evidence artifacts.
- `archive/` - completed change folders moved out of `changes/`. Treat archived artifacts as historical records.

## Canonical References

- Product: `foundation/prd.md`
- Roadmap: `foundation/roadmap.md`
- Architecture: `foundation/architecture/architecture-guide.md`
- Domain notes: `foundation/domain.md`
- Test strategy: `foundation/test-plan.md`
- Infrastructure research: `foundation/infrastructure.md`
- Deployment plan: `foundation/deploy-plan.md`
- Active change index: `changes/README.md`
- Archive convention: `archive/README.md`

## Maintenance Rules

- Edit foundation docs in place when the underlying project knowledge evolves.
- Put change-scoped work under `changes/<change-id>/`.
- Archive completed changes under `archive/`.
- Prefer moving existing documents to the right context location over copying their content.
- Avoid duplicate explanations across root docs and context docs; keep one source of truth and link to it.
