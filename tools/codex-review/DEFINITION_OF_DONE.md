# AI Code Review Definition of Done

A pull request passes the automated review only when every criterion scores at least 7/10 and no finding has `critical` or `high` severity.

1. **Implementation correctness** - behavior matches the pull request intent and preserves financial-domain invariants.
2. **Idiomaticity** - the change follows C#/.NET, Blazor, and established repository patterns.
3. **Complexity** - the solution is no more complex than the problem requires.
4. **Test and risk coverage** - tests protect user-visible behavior and important boundaries in proportion to risk.
5. **Security and safety** - household isolation, PIN access, financial integrity, secrets, and injection risks remain protected.

The machine-readable form lives in `review-schema.json`; `review-contract.mjs` validates that the verdict agrees with the scores and findings before the pipeline uses it.
