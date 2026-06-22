# AI Code Review Lesson Evidence

## Lesson

- Module 5, lesson 3: Code Review in the AI Era - standards, Definition of Done, and an agent in the pipeline.
- Local verification date: 2026-06-19.

## Requirement Matrix

| Practical task | Repository evidence | Verification |
| --- | --- | --- |
| Define five review criteria | `tools/codex-review/DEFINITION_OF_DONE.md` | Human-readable criteria and a mechanical 7/10 threshold. |
| Enforce structured output | `tools/codex-review/review-schema.json`, `review-contract.mjs` | `npm test` validates scores, severity, verdict consistency, and PR context. |
| Put the reviewer in CI/CD | `.github/workflows/ai-code-review.yml` | PR title, description, and diff feed a read-only Codex thread; the job posts a comment, stores an artifact, and enforces the verdict. |
| Compare 2-3 models with evals | `tools/codex-review/promptfooconfig.yaml` | Three OpenAI Responses models evaluate the same prompt, schema, and two controlled diffs. |
| Keep evals as a regression gate | `.github/workflows/ai-review-evals.yml` | Runs when reviewer or eval files change and requires a 100% pass rate. |

## Controlled Scenarios

1. `evals/insecure-loan-query.diff` must fail and identify SQL injection, household authorization bypass, and missing tests.
2. `evals/safe-account-ordering.diff` must pass because it is a narrow repository-aligned change with a focused test.

Promptfoo records pass/fail, latency, token usage, and provider cost through its native OpenAI Responses providers. CI invokes `promptfoo@0.121.17` and preserves the complete JSON result as the `ai-review-evals-<run-id>` artifact.

## Reproduction

From `tools/codex-review`:

```powershell
npm ci
npm test
npm run eval:contract
npm run eval:ci
```

The model comparison and PR workflow require the repository secret `OPENAI_API_KEY`. No key is stored in source control.

## Verification Status

- [x] Reviewer dependency installation is reproducible with `npm ci`.
- [x] Deterministic review-contract tests pass (4/4 on 2026-06-19).
- [x] Promptfoo configuration and assertions pass with the deterministic contract provider (2/2, 100% on 2026-06-19).
- [x] A local Codex demo run returned a schema-valid `fail` verdict for a silent divide-by-zero regression and identified missing tests.
- [ ] Three-model eval completed with a 100% pass rate in GitHub Actions.
- [ ] Pull-request review posted a structured comment and enforced its verdict.

The final two checks require a pushed branch, GitHub Actions, and `OPENAI_API_KEY`; mark them only after linked workflow runs exist.

## Evidence To Submit

1. Link to a pull request showing the `AI Code Review` comment and status check.
2. Link to the `AI Review Evals` workflow run.
3. Downloaded `ai-review-evals-<run-id>` artifact containing `promptfoo-results.json`.
4. Screenshot of the Actions run summary with three model labels and both controlled scenarios.
5. This file at the commit used for the run.
