using HouseholdBudgetMate.Domain.Entities;
using HouseholdBudgetMate.Abstractions.Enums;
using HouseholdBudgetMate.Migrations;
using Microsoft.EntityFrameworkCore;

namespace HouseholdBudgetMate.Tests.Tests.Infrastructure;

public sealed class AccountPersistenceTests
{
    [Fact]
    public async Task Accounts_Query_Should_Include_Archived_Records_By_Default()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        await using (var context = new ApplicationDbContext(options))
        {
            await context.Accounts.AddRangeAsync(
                new Account
                {
                    Name = "Bank",
                    Type = (int)AccountType.Bank
                },
                new Account
                {
                    Name = "Portfel",
                    Type = (int)AccountType.Cash,
                    IsArchived = true,
                    ArchivedAtUtc = DateTime.UtcNow
                });

            await context.SaveChangesAsync();
        }

        await using (var context = new ApplicationDbContext(options))
        {
            var visible = await context.Accounts.ToListAsync();
            Assert.Equal(2, visible.Count);
        }
    }
}

