import { readFileSync } from "node:fs";
import { fileURLToPath } from "node:url";

export const REVIEW_SCHEMA = JSON.parse(
  readFileSync(fileURLToPath(new URL("review-schema.json", import.meta.url)), "utf8"),
);

export const REVIEW_CRITERIA = [
  "implementationCorrectness",
  "idiomaticity",
  "complexity",
  "testRiskCoverage",
  "securitySafety",
];

const CRITERIA_TEXT = `
1. implementationCorrectness: behavior matches the PR intent and preserves domain invariants.
2. idiomaticity: the change follows C#/.NET, Blazor, and repository patterns.
3. complexity: the solution is no more complex than the problem requires.
4. testRiskCoverage: tests protect user-visible behavior and important boundaries in proportion to risk.
5. securitySafety: household isolation, PIN access, financial data integrity, secrets, and injection risks are protected.`;

export function buildReviewPrompt(diff, pullRequest = {}) {
  const title = pullRequest.title?.trim() || "Not provided";
  const description = pullRequest.description?.trim() || "Not provided";

  return `You are the CI code reviewer for Household Budget Mate.
Review only the supplied diff. Do not modify files.

Pull request title:
${title}

Pull request description:
${description}

Definition of Done criteria (score each from 1 to 10):
${CRITERIA_TEXT}

Return JSON matching the supplied schema. Findings must be concrete and actionable.
Use stable ruleId values such as sql-injection, authorization-boundary, missing-tests,
behavior-regression, unnecessary-complexity, or project-pattern.

The verdict is pass only when every criterion scores at least 7 and there are no
critical or high findings. Otherwise the verdict is fail.

Diff:
${diff}`;
}

export function validateReview(review) {
  const errors = [];
  if (!review || typeof review !== "object" || Array.isArray(review)) {
    return { valid: false, errors: ["result must be an object"] };
  }

  for (const criterion of REVIEW_CRITERIA) {
    const score = review.scores?.[criterion];
    if (!Number.isInteger(score) || score < 1 || score > 10) {
      errors.push(`${criterion} must be an integer from 1 to 10`);
    }
  }

  if (!Array.isArray(review.findings)) errors.push("findings must be an array");
  if (!["pass", "fail"].includes(review.verdict)) errors.push("verdict must be pass or fail");
  if (typeof review.summary !== "string" || review.summary.trim() === "") {
    errors.push("summary must be a non-empty string");
  }

  const findings = Array.isArray(review.findings) ? review.findings : [];
  const hasBlockingFinding = findings.some((finding) =>
    ["critical", "high"].includes(finding?.severity),
  );
  const scoresPass = REVIEW_CRITERIA.every((criterion) => review.scores?.[criterion] >= 7);
  const expectedVerdict = scoresPass && !hasBlockingFinding ? "pass" : "fail";
  if (review.verdict !== expectedVerdict) {
    errors.push(`verdict must be ${expectedVerdict} for the supplied scores and findings`);
  }

  return { valid: errors.length === 0, errors };
}
