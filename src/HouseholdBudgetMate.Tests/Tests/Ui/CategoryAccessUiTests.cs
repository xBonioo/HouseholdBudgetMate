using FluentAssertions;

namespace HouseholdBudgetMate.Tests.Tests.Ui;

public sealed class CategoryAccessUiTests
{
    [Fact]
    public void CategoriesPage_Should_Gate_Category_And_Tag_Management_To_Admins()
    {
        var markup = ReadRepoFile("src/HouseholdBudgetMate.Web/Components/Pages/Categories.razor");

        markup.Should().Contain("@inject IUserSessionService UserSessionService");
        markup.Should().Contain("private bool IsAdminSession => UserSessionService.CurrentUser?.IsAdmin == true;");
        markup.Should().Contain("Enabled=\"@(!_isLoading && IsAdminSession)\"");
        markup.Should().Contain("@if (IsAdminSession)");
        markup.Should().Contain("Kategorie i tagi może zmieniać tylko administrator.");
        markup.Should().Contain("private bool EnsureAdminCanManageCategories()");
        markup.Should().Contain("_editCategory.Id == category.Id && IsAdminSession");
        markup.Should().Contain("_editTag.Id == rootTag.Id && IsAdminSession");
        markup.Should().Contain("_editTag.Id == childTag.Id && IsAdminSession");
        markup.Should().Contain("Usuń tag");
        markup.Should().Contain("Czy na pewno chcesz usunąć tag");
    }

    private static string ReadRepoFile(string relativePath)
    {
        var baseDirectory = AppContext.BaseDirectory;
        var current = new DirectoryInfo(baseDirectory);

        while (current is not null && !File.Exists(Path.Combine(current.FullName, "HouseholdBudgetMate.slnx")))
        {
            current = current.Parent;
        }

        if (current is null)
        {
            throw new InvalidOperationException($"Could not locate repository root from {baseDirectory}.");
        }

        return File.ReadAllText(Path.Combine(current.FullName, relativePath));
    }
}
