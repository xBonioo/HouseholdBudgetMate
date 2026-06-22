# AI Code Review Implementation Plan

## Overview

Add a structured AI code-review agent to GitHub Actions and a repeatable Promptfoo evaluation harness. The pipeline must receive pull-request intent and diff, enforce a machine-readable Definition of Done, and retain workflow artifacts suitable for course evidence.

## Progress

### Phase 1: Reviewer and evaluation pipeline

#### Automated

- [x] 1.1 Define the five review criteria and structured output contract. — 88cfded
- [x] 1.2 Add the pull-request review workflow and verdict gate. — 88cfded
- [x] 1.3 Add Promptfoo scenarios, model comparison, and regression workflow. — 88cfded
- [x] 1.4 Verify contract tests, deterministic evals, and YAML parsing. — 88cfded

#### Manual

- [ ] 1.5 Run the three-model eval in GitHub Actions with `OPENAI_API_KEY` configured.
- [ ] 1.6 Confirm a pull request receives the structured review comment and enforced status check.

## Phase 1: Reviewer and Evaluation Pipeline

### Changes Required

- `.github/workflows/ai-code-review.yml` - run structured review for pull requests to `main`, publish the result, upload evidence, and enforce the verdict.
- `.github/workflows/ai-review-evals.yml` - run deterministic contract checks and the three-model Promptfoo comparison when reviewer files change.
- `tools/codex-review/DEFINITION_OF_DONE.md` - document five acceptance criteria and the pass threshold.
- `tools/codex-review/review-schema.json` - define machine-readable scores, findings, verdict, and summary.
- `tools/codex-review/review-contract.mjs` - build the PR-aware prompt and validate verdict consistency.
- `tools/codex-review/review.mjs` - execute the read-only Codex review.
- `tools/codex-review/gate.mjs` - convert the structured verdict into a pipeline exit code.
- `tools/codex-review/promptfooconfig.yaml` - compare three models against the same controlled diffs.
- `tools/codex-review/evals/` - keep one unsafe and one acceptable diff as regression scenarios.
- `tools/codex-review/test/review-contract.test.mjs` - protect the output contract and PR context.

### Success Criteria

#### Automated Verification

- [x] `npm ci`
- [x] `npm test`
- [x] `npm run eval:contract`
- [x] Parse `.github/workflows/ai-code-review.yml`, `.github/workflows/ai-review-evals.yml`, and `promptfooconfig.yaml` as YAML.

#### Manual Verification

- [ ] Configure `OPENAI_API_KEY` in GitHub Actions and retain the model-comparison artifact.
- [ ] Open a pull request to `main` and confirm the reviewer comment and merge gate.

## What We're NOT Doing

- Storing API keys or generated model credentials in the repository.
- Treating the local deterministic provider as evidence of a completed three-model API run.
- Making the path-filtered eval workflow a globally required check for unrelated pull requests.

## References

- Lesson evidence: `context/changes/ai-code-review/evidence.md`
- Implementation commit: `88cfded`
