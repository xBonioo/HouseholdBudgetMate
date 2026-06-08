# GitHub Copilot Instructions

## Context

Before implementing changes, read the relevant canonical context:

- `AGENTS.md` - stable repository guide, commands, and context map.
- `context/foundation/prd.md` - product requirements.
- `context/foundation/roadmap.md` - current product state and roadmap.
- `context/foundation/domain.md` - data model, business rules, and entities.
- `context/foundation/architecture/architecture-guide.md` - architecture rules, layering, and DTO conventions.
- `context/foundation/test-plan.md` - testing strategy and quality Definition of Done.
- `context/foundation/performance/optimization.md` - performance guidance.

## General Rules

- Respect the architecture and coding conventions documented in the repository.
- If task details conflict with domain rules, follow `context/foundation/domain.md` and report the conflict.
- Work in vertical slices unless the requested scope explicitly limits the change to one layer.
- Respect the solution structure and project dependencies described in the architecture guide.
- If a required behavior is not covered by existing product or domain context, ask for clarification instead of guessing.
- Do not generate EF migrations unless explicitly requested.
