using FluentAssertions;

namespace HouseholdBudgetMate.Tests.Tests.Setup;

public sealed class RealDataReadinessGateTests
{
    [Fact]
    public void ReadinessEvidence_Should_Not_Read_As_Finally_Approved_While_Manual_Gates_Are_Pending()
    {
        var evidence = ReadRepoFile("context/changes/secure-real-data-readiness/readiness-evidence.md");

        evidence.Should().Contain("external/manual real-data sign-off pending");
        evidence.Should().Contain("Restore smoke test is pending");
        evidence.Should().Contain("Restore smoke-tested: Pending before real-data approval.");
        evidence.Should().Contain("Real-data MVP pilot approved: Pending final human sign-off");

        AssertTableStatus(
            evidence,
            "`/health/ready` with database reachable",
            "Pending manual");
        AssertTableStatus(
            evidence,
            "Render Blueprint health check path",
            "Pending CLI/manual");
        AssertTableStatus(
            evidence,
            "Admin readiness panel",
            "Pending manual");

        evidence.Should().NotContain("Real-data MVP pilot approved: Approved");
        evidence.Should().NotContain("Status: approved");
    }

    [Fact]
    public void DeploymentPlan_Should_Keep_PostgreSql_Rollback_Boundary_Explicit()
    {
        var deployPlan = ReadRepoFile("context/deployment/deploy-plan.md");

        deployPlan.Should().Contain("local `pg_dump` backup before first real data");
        deployPlan.Should().Contain("restore smoke-test notes");
        deployPlan.Should().Contain("Review the migration for destructive operations");
        deployPlan.Should().Contain("rollback or forward-fix notes");
        deployPlan.Should().Contain("App rollback does not roll back PostgreSQL schema or data.");
        deployPlan.Should().Contain("latest database backup/restore point recorded");
    }

    [Fact]
    public void RenderBlueprint_Should_Use_Readiness_Endpoint_And_Safe_Production_Flags()
    {
        var renderYaml = NormalizeLineEndings(ReadRepoFile("render.yaml"));

        renderYaml.Should().Contain("healthCheckPath: /health/ready");
        renderYaml.Should().Contain("      - key: DATABASE_URL\n        fromDatabase:\n          name: household-budget-mate-db\n          property: connectionString");
        renderYaml.Should().Contain("      - key: Blazor__DetailedErrors\n        value: \"false\"");
        renderYaml.Should().Contain("      - key: FileStorage__EnablePublicFileServing\n        value: \"false\"");
        renderYaml.Should().Contain("databases:\n  - name: household-budget-mate-db");
    }

    private static void AssertTableStatus(string markdown, string rowMarker, string expectedStatus)
    {
        var row = markdown
            .Split('\n')
            .Select(x => x.Trim())
            .FirstOrDefault(x => x.Contains(rowMarker, StringComparison.Ordinal));

        row.Should().NotBeNull($"the evidence table should include a row for {rowMarker}");

        var cells = row!
            .Split('|', StringSplitOptions.TrimEntries)
            .Where(x => x.Length > 0)
            .ToArray();

        cells.Should().NotBeEmpty();
        cells[^1].Should().Be(expectedStatus);
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
