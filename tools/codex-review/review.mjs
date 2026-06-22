import { Codex } from "@openai/codex-sdk";
import { fileURLToPath } from "node:url";
import { buildReviewPrompt, REVIEW_SCHEMA, validateReview } from "./review-contract.mjs";

const DEMO_DIFF = `diff --git a/src/Calculator.cs b/src/Calculator.cs
index 1111111..2222222 100644
--- a/src/Calculator.cs
+++ b/src/Calculator.cs
@@ -1,4 +1,4 @@
 public decimal Divide(decimal amount, decimal divisor)
 {
-    return amount / divisor;
+    return divisor == 0 ? 0 : amount / divisor;
 }`;

async function readStdin() {
  const chunks = [];
  for await (const chunk of process.stdin) chunks.push(chunk);
  return Buffer.concat(chunks).toString("utf8").trim();
}

const useDemo = process.argv.includes("--demo");
const diff = useDemo ? DEMO_DIFF : await readStdin();
if (!diff) {
  console.error("No diff received. Pipe git diff to this command or run npm run review:demo.");
  process.exitCode = 1;
} else {
  const repositoryRoot = fileURLToPath(new URL("../..", import.meta.url));
  const codex = new Codex();
  const thread = codex.startThread({
    workingDirectory: repositoryRoot,
    model: process.env.REVIEW_MODEL,
    sandboxMode: "read-only",
    approvalPolicy: "never",
    networkAccessEnabled: false,
    webSearchMode: "disabled",
  });

  const result = await thread.run(
    buildReviewPrompt(diff, {
      title: process.env.PR_TITLE,
      description: process.env.PR_DESCRIPTION,
    }),
    { outputSchema: REVIEW_SCHEMA },
  );
  const review = JSON.parse(result.finalResponse);
  const validation = validateReview(review);
  if (!validation.valid) {
    console.error(`Invalid review result: ${validation.errors.join("; ")}`);
    process.exitCode = 2;
  } else {
    console.log(JSON.stringify(review, null, 2));
  }

  if (result.usage) {
    console.error(
      `tokens: ${result.usage.input_tokens} input, ${result.usage.cached_input_tokens} cached, ${result.usage.output_tokens} output`,
    );
  }
}
