using FluentAssertions;

namespace HouseholdBudgetMate.Tests.Tests.Setup;

public sealed class SessionCookieHardeningTests
{
    [Fact]
    public void AppCookieScript_Should_Use_Strict_SameSite_And_Https_Secure_Flag()
    {
        var appRazor = File.ReadAllText(FindRepoFile("src/HouseholdBudgetMate.Web/Components/App.razor"));

        appRazor.Should().Contain("SameSite=Strict");
        appRazor.Should().Contain("window.location.protocol === 'https:' ? ';Secure' : ''");
    }

    private static string FindRepoFile(string relativePath)
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            var candidate = Path.Combine(directory.FullName, relativePath);
            if (File.Exists(candidate))
            {
                return candidate;
            }

            directory = directory.Parent;
        }

        throw new FileNotFoundException($"Could not find repo file '{relativePath}'.");
    }
}
