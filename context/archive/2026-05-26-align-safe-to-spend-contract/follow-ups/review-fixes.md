# Implementation Review Follow-ups

## Resolved

- F2: Keep the exact immediately preceding-month balance requirement and align contract wording and test names.
- F3: Determine archived-account applicability from its archive timestamp, excluding accounts archived before or during the selected month and retaining accounts archived only after that month ended; distinguish an explicit stored zero balance from a missing balance row; calculate closed historical months from stored prior data without blocking on retroactive gaps.

## Accepted Risk

- F1: Keep `Safe to spend` as tooltip-only content rather than a separately visible KPI.

## Pending Decision

- F4: Decide whether unrelated solution/page-title, dashboard navigation, and income-edit submission changes should remain in this feature or be separated/documented.
