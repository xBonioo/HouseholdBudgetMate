const FAILING_REVIEW = {
  scores: {
    implementationCorrectness: 3,
    idiomaticity: 6,
    complexity: 8,
    testRiskCoverage: 2,
    securitySafety: 2,
  },
  findings: [
    {
      criterion: "securitySafety",
      severity: "critical",
      ruleId: "sql-injection",
      location: "LoanService.cs:44",
      detail: "Raw user input is interpolated into SQL.",
    },
    {
      criterion: "securitySafety",
      severity: "critical",
      ruleId: "authorization-boundary",
      location: "LoanService.cs:47",
      detail: "Query filters are disabled.",
    },
    {
      criterion: "testRiskCoverage",
      severity: "high",
      ruleId: "missing-tests",
      location: "LoanService.cs:40",
      detail: "No tests cover the new behavior.",
    },
  ],
  verdict: "fail",
  summary: "Unsafe change.",
};

const PASSING_REVIEW = {
  scores: {
    implementationCorrectness: 8,
    idiomaticity: 8,
    complexity: 9,
    testRiskCoverage: 8,
    securitySafety: 8,
  },
  findings: [],
  verdict: "pass",
  summary: "Narrow change with focused coverage.",
};

export default class ContractProvider {
  id() {
    return "household-budget-mate-review-contract";
  }

  async callApi(prompt) {
    return { output: prompt.includes("FromSqlRaw") ? FAILING_REVIEW : PASSING_REVIEW };
  }
}
