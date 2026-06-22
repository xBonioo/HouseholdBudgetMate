import { readFileSync } from "node:fs";
import { validateReview } from "./review-contract.mjs";

const resultPath = process.argv[2];
if (!resultPath) {
  console.error("Usage: node gate.mjs <review-result.json>");
  process.exit(2);
}

let review;
try {
  review = JSON.parse(readFileSync(resultPath, "utf8"));
} catch (error) {
  console.error(`Cannot read review result: ${error.message}`);
  process.exit(2);
}

const validation = validateReview(review);
if (!validation.valid) {
  console.error(`Review contract failed: ${validation.errors.join("; ")}`);
  process.exit(2);
}

console.log(`AI review verdict: ${review.verdict}`);
process.exit(review.verdict === "pass" ? 0 : 1);
