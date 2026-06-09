module.exports = {
  forbidden: [
    {
      name: "not-to-unresolvable",
      severity: "error",
      comment:
        "Imported modules must resolve on disk. Add missing npm packages to package.json or fix local paths.",
      from: {},
      to: {
        couldNotResolve: true,
      },
    },
    {
      name: "no-circular",
      severity: "error",
      comment: "Circular JavaScript/TypeScript dependencies make changes harder to reason about.",
      from: {},
      to: {
        circular: true,
      },
    },
    {
      name: "no-deprecated-core",
      severity: "error",
      comment: "Do not depend on deprecated Node.js core modules.",
      from: {},
      to: {
        dependencyTypes: ["core"],
        path: "^(?:punycode|domain|constants|sys|_linklist|_stream_wrap)$",
      },
    },
    {
      name: "no-non-package-json",
      severity: "error",
      comment: "All npm imports used by analyzed code must be declared in package.json.",
      from: {},
      to: {
        dependencyTypes: ["npm-no-pkg", "npm-unknown"],
      },
    },
    {
      name: "not-to-deprecated",
      severity: "warn",
      comment: "Avoid deprecated npm packages.",
      from: {},
      to: {
        dependencyTypes: ["deprecated"],
      },
    },
  ],
  options: {
    doNotFollow: {
      path: "node_modules",
      dependencyTypes: [
        "npm",
        "npm-dev",
        "npm-optional",
        "npm-peer",
        "npm-bundled",
        "npm-no-pkg",
      ],
    },
    exclude: {
      path: [
        "^node_modules/",
        "^artifacts/",
        "^playwright-report/",
        "^test-results/",
        "^src/HouseholdBudgetMate\\.Web/wwwroot/js/chart\\.umd\\.min\\.js$",
      ].join("|"),
    },
    reporterOptions: {
      archi: {
        collapsePattern:
          "^(e2e|src/HouseholdBudgetMate\\.Web/(?:wwwroot/js|Components/[^/]+))/",
      },
      dot: {
        collapsePattern:
          "^(e2e|src/HouseholdBudgetMate\\.Web/(?:wwwroot/js|Components/[^/]+))/",
      },
    },
  },
};
