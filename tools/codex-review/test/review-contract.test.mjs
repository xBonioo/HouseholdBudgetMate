import assert from "node:assert/strict";
import test from "node:test";
import { buildReviewPrompt, validateReview } from "../review-contract.mjs";

function review(overrides = {}) {
  return {
    scores: {
      implementationCorrectness: 8,
      idiomaticity: 8,
      complexity: 8,
      testRiskCoverage: 8,
      securitySafety: 8,
    },
    findings: [],
    verdict: "pass",
    summary: "Change meets the review contract.",
    ...overrides,
  };
}

test("accepts a passing review when all five criteria meet the threshold", () => {
  assert.equal(validateReview(review()).valid, true);
});

test("rejects a pass verdict when one score is below the Definition of Done", () => {
  const result = validateReview(
    review({
      scores: {
        implementationCorrectness: 8,
        idiomaticity: 8,
        complexity: 8,
        testRiskCoverage: 6,
        securitySafety: 8,
      },
    }),
  );

  assert.equal(result.valid, false);
  assert.match(result.errors.join(" "), /verdict must be fail/);
});

test("rejects a pass verdict with a high-severity finding", () => {
  const result = validateReview(
    review({
      findings: [
        {
          criterion: "securitySafety",
          severity: "high",
          ruleId: "authorization-boundary",
          location: "src/Service.cs:42",
          detail: "The query is not restricted to the unlocked household.",
        },
      ],
    }),
  );

  assert.equal(result.valid, false);
  assert.match(result.errors.join(" "), /verdict must be fail/);
});

test("includes pull-request intent and diff in the review prompt", () => {
  const prompt = buildReviewPrompt("diff --git a/a.cs b/a.cs", {
    title: "Protect account ordering",
    description: "Keep account rows stable across screens.",
  });

  assert.match(prompt, /Protect account ordering/);
  assert.match(prompt, /Keep account rows stable across screens/);
  assert.match(prompt, /diff --git a\/a\.cs b\/a\.cs/);
});
