using FluentAssertions;
using HouseholdBudgetMate.Abstractions.Contracts.Expenses.Dto;
using HouseholdBudgetMate.Web.Services;

namespace HouseholdBudgetMate.Tests.Tests.Ui;

public sealed class ArchiveMonthsCacheServiceTests
{
    [Fact]
    public void TryGetCache_Should_Not_Return_Months_For_Different_BudgetOwner()
    {
        var cache = new ArchiveMonthsCacheService();
        var kamilMonths = new List<AvailableMonthDto>
        {
            new() { Year = 2026, Month = 5, IsClosed = true }
        };

        cache.UpdateCache("kamil", kamilMonths);

        cache.TryGetCache("kuba", out var kubaMonths).Should().BeFalse();
        kubaMonths.Should().BeEmpty();

        cache.TryGetCache("kamil", out var cachedKamilMonths).Should().BeTrue();
        cachedKamilMonths.Should().BeEquivalentTo(kamilMonths);
    }

    [Fact]
    public void Invalidate_Should_Clear_Only_Matching_BudgetOwner()
    {
        var cache = new ArchiveMonthsCacheService();
        cache.UpdateCache("kamil",
        [
            new AvailableMonthDto { Year = 2026, Month = 4, IsClosed = false }
        ]);

        cache.Invalidate("kuba");
        cache.TryGetCache("kamil", out _).Should().BeTrue();

        cache.Invalidate("kamil");
        cache.TryGetCache("kamil", out var months).Should().BeFalse();
        months.Should().BeEmpty();
    }
}
