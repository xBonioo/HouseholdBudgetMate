using FluentAssertions;
using HouseholdBudgetMate.Abstractions.Enums;
using HouseholdBudgetMate.Domain.Entities;
using HouseholdBudgetMate.Domain.Infrastructure;
using HouseholdBudgetMate.Migrations;
using Microsoft.EntityFrameworkCore;

namespace HouseholdBudgetMate.Tests.Tests.Services;

public sealed class UserScopingTests
{
    [Fact]
    public async Task SaveChanges_Should_Stamp_New_Entities_With_Current_User_And_Filter_By_User()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        await using (var setupContext = new ApplicationDbContext(options, CreateCurrentUserContext("user-a")))
        {
            setupContext.Users.AddRange(
                new User { Id = "user-a", Username = "user-a", PasswordHash = "11111111" },
                new User { Id = "user-b", Username = "user-b", PasswordHash = "22222222" });
            await setupContext.SaveChangesAsync();
        }

        await using (var userAContext = new ApplicationDbContext(options, CreateCurrentUserContext("user-a")))
        {
            userAContext.Accounts.Add(new Account
            {
                Name = "A bank",
                Type = (int)AccountType.Bank,
                Order = 1
            });

            userAContext.Loans.Add(CreateLoan("A loan"));
            await userAContext.SaveChangesAsync();
        }

        await using (var userBContext = new ApplicationDbContext(options, CreateCurrentUserContext("user-b")))
        {
            var visibleAccounts = await userBContext.Accounts.ToListAsync();
            visibleAccounts.Should().BeEmpty();

            userBContext.Accounts.Add(new Account
            {
                Name = "B bank",
                Type = (int)AccountType.Bank,
                Order = 1
            });

            userBContext.Loans.Add(CreateLoan("B loan"));
            await userBContext.SaveChangesAsync();
        }

        await using (var verificationContext = new ApplicationDbContext(options, CreateCurrentUserContext("user-a")))
        {
            var userAAccounts = await verificationContext.Accounts.ToListAsync();
            userAAccounts.Should().ContainSingle();
            userAAccounts[0].UserId.Should().Be("user-a");

            var allAccounts = await verificationContext.Accounts
                .IgnoreQueryFilters()
                .OrderBy(x => x.UserId)
                .ToListAsync();
            allAccounts.Select(x => x.UserId).Should().Equal("user-a", "user-b");

            var userALoans = await verificationContext.Loans.ToListAsync();
            userALoans.Should().ContainSingle();
            userALoans[0].UserId.Should().Be("user-a");

            var allLoans = await verificationContext.Loans
                .IgnoreQueryFilters()
                .OrderBy(x => x.UserId)
                .ToListAsync();
            allLoans.Select(x => x.UserId).Should().Equal("user-a", "user-b");
        }
    }

    [Fact]
    public async Task Shared_Budget_User_Should_Read_And_Write_Data_For_Budget_Owner()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        await using (var setupContext = new ApplicationDbContext(options, CreateCurrentUserContext("user-a")))
        {
            setupContext.Users.AddRange(
                new User { Id = "user-a", Username = "user-a", PasswordHash = "hash-a" },
                new User
                {
                    Id = "user-b",
                    Username = "user-b",
                    PasswordHash = "hash-b",
                    HouseholdMode = (int)HouseholdMode.SharedBudget,
                    BudgetOwnerUserId = "user-a"
                });

            setupContext.Accounts.Add(new Account
            {
                Name = "Shared bank",
                Type = (int)AccountType.Bank,
                Order = 1
            });

            setupContext.Loans.Add(CreateLoan("Shared loan"));

            await setupContext.SaveChangesAsync();
        }

        await using (var spouseContext = new ApplicationDbContext(
                         options,
                         CreateCurrentUserContext("user-b", "user-a")))
        {
            var visibleAccounts = await spouseContext.Accounts.ToListAsync();
            visibleAccounts.Should().ContainSingle(x => x.Name == "Shared bank");

            var visibleLoans = await spouseContext.Loans.ToListAsync();
            visibleLoans.Should().ContainSingle(x => x.Name == "Shared loan");

            spouseContext.Accounts.Add(new Account
            {
                Name = "Added by spouse",
                Type = (int)AccountType.Bank,
                Order = 2
            });

            spouseContext.Loans.Add(CreateLoan("Loan added by spouse"));

            await spouseContext.SaveChangesAsync();
        }

        await using (var verificationContext = new ApplicationDbContext(options, CreateCurrentUserContext("user-a")))
        {
            var accounts = await verificationContext.Accounts
                .OrderBy(x => x.Order)
                .ToListAsync();

            accounts.Should().HaveCount(2);
            accounts.Select(x => x.UserId).Should().OnlyContain(x => x == "user-a");

            var loans = await verificationContext.Loans
                .OrderBy(x => x.Name)
                .ToListAsync();

            loans.Should().HaveCount(2);
            loans.Select(x => x.UserId).Should().OnlyContain(x => x == "user-a");
        }
    }

    private static Loan CreateLoan(string name)
    {
        return new Loan
        {
            Name = name,
            LoanType = 1,
            InterestMode = 1,
            Principal = 1000,
            InterestRate = 5,
            RepaymentDayOfMonth = 10,
            StartDate = new DateOnly(2026, 1, 1),
            EndDate = new DateOnly(2026, 12, 1),
            IsActive = true
        };
    }

    private static CurrentUserContext CreateCurrentUserContext(string userId, string? budgetOwnerUserId = null)
    {
        return new CurrentUserContext
        {
            UserId = userId,
            BudgetOwnerUserId = budgetOwnerUserId ?? userId
        };
    }
}
