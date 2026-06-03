using FluentAssertions;

namespace HouseholdBudgetMate.Tests.Tests.Setup;

public sealed class TestPlanQualityGateTests
{
    [Fact]
    public void QualityGates_Should_Name_Shipped_Rollout_Gates()
    {
        var testPlan = ReadTestPlan();

        testPlan.Should().Contain("build/typecheck");
        testPlan.Should().Contain("unit + integration tests");
        testPlan.Should().Contain("targeted monthly-loop contract");
        testPlan.Should().Contain("real-data readiness contract");
        testPlan.Should().Contain("access restore/security regression tests");
        testPlan.Should().Contain("e2e/browser critical flow");
    }

    [Fact]
    public void Cookbook_Should_Record_Shipped_Reference_Tests()
    {
        var testPlan = ReadTestPlan();

        testPlan.Should().Contain("MonthlyBudgetingLoopTests.cs");
        testPlan.Should().Contain("MonthlyBudgetingLoopUiTests.cs");
        testPlan.Should().Contain("MonthlyBudgetingLoopRenderedTests.cs");
        testPlan.Should().Contain("RealDataReadinessGateTests.cs");
        testPlan.Should().Contain("RealDataReadinessServiceTests.cs");
        testPlan.Should().Contain("ReadinessHealthTests.cs");
        testPlan.Should().Contain("RecoveryBoundaryTests.cs");
        testPlan.Should().Contain("AccessHardeningRedirectMiddlewareTests.cs");
    }

    [Fact]
    public void QualityCookbook_Should_Define_Deterministic_Gate_Decision_Rules()
    {
        var testPlan = ReadTestPlan();
        var qualityCookbook = SectionBetween(
            testPlan,
            "### 6.4 Adding a quality gate or selective AI-native review",
            "### 6.5 Per-rollout-phase notes");

        qualityCookbook.Should().NotContain("TBD");
        qualityCookbook.Should().Contain("Promote a gate only after a shipped risk pattern exists");
        qualityCookbook.Should().Contain("owner/location");
        qualityCookbook.Should().Contain("requiredness");
        qualityCookbook.Should().Contain("command or manual evidence");
        qualityCookbook.Should().Contain("regression caught");
        qualityCookbook.Should().Contain("Keep manual evidence explicit");
        qualityCookbook.Should().Contain("Use browser/e2e only when deterministic layers cannot observe the risk");
        qualityCookbook.Should().Contain("Do not use AI-native review as a replacement for deterministic tests");
    }

    [Fact]
    public void RolloutNotes_Should_Be_Parseable()
    {
        var testPlan = ReadTestPlan();
        var notes = SectionBetween(
            testPlan,
            "### 6.5 Per-rollout-phase notes",
            "## 7. What We Deliberately Don't Test");
        var noteLines = notes
            .Split('\n')
            .Select(x => x.Trim())
            .Where(x => x.StartsWith("- Phase ", StringComparison.Ordinal))
            .ToArray();

        noteLines.Should().Contain(x => x.Contains("Phase 1 (`testing-cross-screen-monthly-consistency`)", StringComparison.Ordinal));
        noteLines.Should().Contain(x => x.Contains("Phase 2 (`real-data-readiness-gates`)", StringComparison.Ordinal));
        noteLines.Should().Contain(x => x.Contains("Phase 3 (`recovery-boundary-test`)", StringComparison.Ordinal));
        noteLines.Should().Contain(x => x.Contains("Phase 4 (`quality-cookbook-and-gates`)", StringComparison.Ordinal));

        notes.Should().NotContain("Phase 3 (\n");
        notes.Should().NotContain("Phase 3 (\r\n");
        notes.Should().NotContain("ecovery-boundary-test)");
    }

    [Fact]
    public void Exclusions_Should_Preserve_Negative_Space()
    {
        var testPlan = ReadTestPlan();
        var exclusions = SectionBetween(
            testPlan,
            "## 7. What We Deliberately Don't Test",
            "## 8. Freshness Ledger");

        exclusions.Should().Contain("OCR/file upload paths");
        exclusions.Should().Contain("Full-page visual snapshots everywhere");
        exclusions.Should().Contain("Coverage padding on generated or shape-only contracts");
    }

    private static string ReadTestPlan()
    {
        return NormalizeLineEndings(ReadRepoFile("context/foundation/test-plan.md"));
    }

    private static string SectionBetween(string source, string startMarker, string endMarker)
    {
        var start = source.IndexOf(startMarker, StringComparison.Ordinal);
        start.Should().BeGreaterThanOrEqualTo(0, $"the test plan should include '{startMarker}'");

        var end = source.IndexOf(endMarker, start, StringComparison.Ordinal);
        end.Should().BeGreaterThan(start, $"the test plan should include '{endMarker}' after '{startMarker}'");

        return source[start..end];
    }

    private static string ReadRepoFile(string relativePath)
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            var candidate = Path.Combine(directory.FullName, relativePath);
            if (File.Exists(candidate))
            {
                return File.ReadAllText(candidate);
            }

            directory = directory.Parent;
        }

        throw new FileNotFoundException($"Could not find repository file '{relativePath}'.", relativePath);
    }

    private static string NormalizeLineEndings(string value)
    {
        return value.Replace("\r\n", "\n", StringComparison.Ordinal);
    }
}
