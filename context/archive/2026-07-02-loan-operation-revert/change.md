---
change_id: loan-operation-revert
title: Revert loan prepayment and WIBOR operations
status: archived
created: 2026-07-02
updated: 2026-07-03
archived_at: 2026-07-03T17:35:59Z
---

## Notes

Add revert for loan operations, scoped to loan prepayment and WIBOR/rate changes. Revert should be available from audit entries, undo all effects of the original operation, create an audit trace for the revert, block stale reverts when the loan changed later, and be available to non-admin users who have access to the budget.
