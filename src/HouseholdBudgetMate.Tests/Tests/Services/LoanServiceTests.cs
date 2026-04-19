using HouseholdBudgetMate.Abstractions.Contracts.Loans.Requests;
using HouseholdBudgetMate.Abstractions.Enums;
using HouseholdBudgetMate.Application.Kernel.Exceptions;
using HouseholdBudgetMate.Application.Services;
using HouseholdBudgetMate.Domain.Entities;
using HouseholdBudgetMate.Tests.Shared;
using Microsoft.EntityFrameworkCore;

namespace HouseholdBudgetMate.Tests.Tests.Services;

public sealed class LoanServiceTests
{
    private readonly string _dbName = Guid.NewGuid().ToString();

    private LoanService CreateService(DateTime? nowUtc = null)
    {
        var factory = TestDbContextFactory.CreateFactory(_dbName);
        var provider = new StaticDateTimeProvider(nowUtc ?? DateTime.UtcNow);
        return new LoanService(factory, provider);
    }

    [Fact]
    public async Task CreateLoanAsync_Should_Generate_Installments_For_Date_Range()
    {
        var service = CreateService();
        var loan = await service.CreateLoanAsync(new CreateLoanRequest
        {
            Name = "Mieszkanie",
            LoanType = LoanType.Mortgage,
            InterestMode = LoanInterestMode.VariableWibor,
            WiborPeriodType = WiborPeriodType.Wibor1M,
            MarginRate = 2m,
            InitialReferenceRate = 5m,
            Principal = 120000m,
            InterestRate = 0,
            StartDate = new DateOnly(2026, 1, 15),
            EndDate = new DateOnly(2026, 12, 15),
            RepaymentDayOfMonth = 15,
            IsActive = true
        }, CancellationToken.None);

        Assert.Equal(12, loan.Installments.Count);
        Assert.Equal(2026, loan.Installments.Min(x => x.Year));
        Assert.Equal(1, loan.Installments.Min(x => x.Month));
        Assert.Equal(12, loan.Installments.Max(x => x.Month));

        await using var verifyContext = TestDbContextFactory.CreateDbContext(_dbName);
        var monthPlans = await verifyContext.MonthPlans.ToListAsync();
        Assert.Empty(monthPlans);
    }

    [Fact]
    public async Task SyncLoanInstallmentsForMonthAsync_Should_Create_Expense_And_SetPaidStatus()
    {
        var service = CreateService();
        var loan = await service.CreateLoanAsync(new CreateLoanRequest
        {
            Name = "Auto",
            LoanType = LoanType.Cash,
            InterestMode = LoanInterestMode.Fixed,
            Principal = 24000m,
            InterestRate = 0m,
            StartDate = new DateOnly(2026, 2, 10),
            EndDate = new DateOnly(2026, 7, 10),
            RepaymentDayOfMonth = 10,
            IsActive = true
        }, CancellationToken.None);

        await using (var context = TestDbContextFactory.CreateDbContext(_dbName))
        {
            context.MonthPlans.Add(new MonthPlan { Year = 2026, Month = 3, IsClosed = false });
            await context.SaveChangesAsync();
        }

        await service.SyncLoanInstallmentsForMonthAsync(2026, 3, CancellationToken.None);

        await using (var verifyContext = TestDbContextFactory.CreateDbContext(_dbName))
        {
            var monthPlan = await verifyContext.MonthPlans.SingleAsync(x => x.Year == 2026 && x.Month == 3);
            var expense = await verifyContext.Expenses.SingleAsync(x => x.MonthPlanId == monthPlan.Id);
            Assert.True(expense.LoanInstallmentId.HasValue);
            Assert.Equal(0m, expense.ActualAmount);

            var installment = await verifyContext.LoanInstallments.SingleAsync(x => x.Id == expense.LoanInstallmentId);
            Assert.False(installment.IsPaid);
        }

        var marchInstallment = loan.Installments.Single(x => x.Year == 2026 && x.Month == 3);
        await service.SetLoanInstallmentPaidAsync(new SetLoanInstallmentPaidRequest
        {
            LoanInstallmentId = marchInstallment.Id,
            IsPaid = true
        }, CancellationToken.None);

        await using var paidContext = TestDbContextFactory.CreateDbContext(_dbName);
        var paidInstallment = await paidContext.LoanInstallments.SingleAsync(x => x.Id == marchInstallment.Id);
        var paidExpense = await paidContext.Expenses.SingleAsync(x => x.LoanInstallmentId == marchInstallment.Id);

        Assert.True(paidInstallment.IsPaid);
        Assert.Equal(paidExpense.PlannedAmount, paidExpense.ActualAmount);
    }

    [Fact]
    public async Task AddLoanRateEntryAsync_Should_Recalculate_Future_Unpaid_Mortgage_Installments()
    {
        var service = CreateService();
        var loan = await service.CreateLoanAsync(new CreateLoanRequest
        {
            Name = "Hipoteka",
            LoanType = LoanType.Mortgage,
            InterestMode = LoanInterestMode.VariableWibor,
            WiborPeriodType = WiborPeriodType.Wibor1M,
            MarginRate = 2m,
            InitialReferenceRate = 5m,
            Principal = 100000m,
            InterestRate = 0m,
            StartDate = new DateOnly(2026, 1, 15),
            EndDate = new DateOnly(2026, 12, 15),
            RepaymentDayOfMonth = 15,
            IsActive = true
        }, CancellationToken.None);

        var before = loan.Installments.Single(x => x.Month == 6 && x.Year == 2026).Amount;

        var updated = await service.AddLoanRateEntryAsync(new AddLoanRateEntryRequest
        {
            LoanId = loan.Id,
            EffectiveFrom = new DateOnly(2026, 6, 1),
            ReferenceRate = 7m
        }, CancellationToken.None);

        var after = updated.Installments.Single(x => x.Month == 6 && x.Year == 2026).Amount;
        Assert.NotEqual(before, after);
    }

    [Fact]
    public async Task SyncLoanInstallmentsForMonthAsync_Should_Include_LoanCharge_In_Installment_Expense_And_Be_Idempotent()
    {
        var service = CreateService();
        var loan = await service.CreateLoanAsync(new CreateLoanRequest
        {
            Name = "Hipoteka + ubezpieczenie",
            LoanType = LoanType.Mortgage,
            InterestMode = LoanInterestMode.VariableWibor,
            WiborPeriodType = WiborPeriodType.Wibor3M,
            MarginRate = 2m,
            InitialReferenceRate = 5m,
            Principal = 150000m,
            InterestRate = 0m,
            StartDate = new DateOnly(2026, 1, 15),
            EndDate = new DateOnly(2026, 12, 15),
            RepaymentDayOfMonth = 15,
            IsActive = true
        }, CancellationToken.None);

        await using (var context = TestDbContextFactory.CreateDbContext(_dbName))
        {
            context.MonthPlans.Add(new MonthPlan { Year = 2026, Month = 3, IsClosed = false });
            await context.SaveChangesAsync();
        }

        await service.CreateLoanChargeAsync(new CreateLoanChargeRequest
        {
            LoanId = loan.Id,
            Name = "Ubezpieczenie nieruchomosci",
            ChargeType = LoanChargeType.Insurance,
            FrequencyType = LoanChargeFrequencyType.Monthly,
            Amount = 120m,
            StartDate = new DateOnly(2026, 1, 1),
            IsActive = true
        }, CancellationToken.None);

        await service.SyncLoanInstallmentsForMonthAsync(2026, 3, CancellationToken.None);
        await service.SyncLoanInstallmentsForMonthAsync(2026, 3, CancellationToken.None);

        var refreshedLoan = (await service.GetAllAsync(CancellationToken.None)).Single(x => x.Id == loan.Id);
        var marchInstallmentAmount = refreshedLoan.Installments.Single(x => x.Year == 2026 && x.Month == 3).Amount;

        await using var verifyContext = TestDbContextFactory.CreateDbContext(_dbName);
        var monthPlan = await verifyContext.MonthPlans.SingleAsync(x => x.Year == 2026 && x.Month == 3);
        var expenses = await verifyContext.Expenses.Where(x => x.MonthPlanId == monthPlan.Id).ToListAsync();

        Assert.Single(expenses, x => x.LoanInstallmentId.HasValue);
        Assert.Single(expenses);
        Assert.Equal(marchInstallmentAmount, expenses[0].PlannedAmount);
    }

    [Fact]
    public async Task CreateLoanAsync_FixedMortgage_Should_Not_Fully_Amortize_During_First_Five_Years_And_Should_Not_Create_Installments_Before_StartDate()
    {
        var service = CreateService();
        var loan = await service.CreateLoanAsync(new CreateLoanRequest
        {
            Name = "Hipoteka 5Y stale potem WIBOR",
            LoanType = LoanType.Mortgage,
            InterestMode = LoanInterestMode.Fixed,
            WiborPeriodType = WiborPeriodType.Wibor3M,
            MarginRate = 2m,
            InitialReferenceRate = 5m,
            Principal = 400000m,
            InterestRate = 7m,
            StartDate = new DateOnly(2026, 1, 20),
            EndDate = new DateOnly(2035, 12, 20),
            RepaymentDayOfMonth = 5,
            IsActive = true
        }, CancellationToken.None);

        Assert.DoesNotContain(loan.Installments, x => x.DueDate < new DateOnly(2026, 1, 20));

        var fixedPhaseEnd = new DateOnly(2031, 1, 20);
        var principalPaidInFixedPhase = loan.Installments
            .Where(x => x.DueDate < fixedPhaseEnd)
            .Sum(x => x.PrincipalAmount);

        Assert.True(principalPaidInFixedPhase < loan.Principal);
        Assert.Contains(loan.Installments, x => x.DueDate >= fixedPhaseEnd && x.PrincipalAmount > 0);
    }

    [Fact]
    public async Task SyncLoanInstallmentsForMonthAsync_Should_Assign_Loan_Tag_To_Expense()
    {
        int loanTagId;
        await using (var context = TestDbContextFactory.CreateDbContext(_dbName))
        {
            var category = new Category
            {
                Name = "Kredyt",
                Color = "#000000",
                SupportsLineItems = false,
                IsDeleted = false
            };
            context.Categories.Add(category);
            await context.SaveChangesAsync();

            var tag = new Tag
            {
                CategoryId = category.Id,
                Name = "Hipoteka"
            };
            context.Tags.Add(tag);
            await context.SaveChangesAsync();
            loanTagId = tag.Id;

            context.MonthPlans.Add(new MonthPlan { Year = 2026, Month = 3, IsClosed = false });
            await context.SaveChangesAsync();
        }

        var service = CreateService();
        var loan = await service.CreateLoanAsync(new CreateLoanRequest
        {
            Name = "Hipoteka",
            LoanType = LoanType.Mortgage,
            InterestMode = LoanInterestMode.VariableWibor,
            WiborPeriodType = WiborPeriodType.Wibor1M,
            MarginRate = 2m,
            InitialReferenceRate = 5m,
            Principal = 100000m,
            InterestRate = 0m,
            StartDate = new DateOnly(2026, 1, 15),
            EndDate = new DateOnly(2026, 12, 15),
            RepaymentDayOfMonth = 15,
            TagId = loanTagId,
            IsActive = true
        }, CancellationToken.None);

        await service.SyncLoanInstallmentsForMonthAsync(2026, 3, CancellationToken.None);

        await using var verifyContext = TestDbContextFactory.CreateDbContext(_dbName);
        var monthPlan = await verifyContext.MonthPlans.SingleAsync(x => x.Year == 2026 && x.Month == 3);
        var expense = await verifyContext.Expenses.SingleAsync(x => x.MonthPlanId == monthPlan.Id);
        Assert.Equal(loanTagId, expense.TagId);
        Assert.Equal(loanTagId, loan.TagId);
    }

    [Fact]
    public async Task CreateLoanAsync_Should_Reject_Tag_From_Different_Category()
    {
        int wrongTagId;
        await using (var context = TestDbContextFactory.CreateDbContext(_dbName))
        {
            context.Categories.Add(new Category
            {
                Name = "Kredyt",
                Color = "#000000",
                SupportsLineItems = false,
                IsDeleted = false
            });

            var otherCategory = new Category
            {
                Name = "Inne",
                Color = "#FFFFFF",
                SupportsLineItems = false,
                IsDeleted = false
            };
            context.Categories.Add(otherCategory);
            await context.SaveChangesAsync();

            var tag = new Tag
            {
                CategoryId = otherCategory.Id,
                Name = "Niepoprawny"
            };
            context.Tags.Add(tag);
            await context.SaveChangesAsync();
            wrongTagId = tag.Id;
        }

        var service = CreateService();

        await Assert.ThrowsAsync<BadRequestException>(async () =>
            await service.CreateLoanAsync(new CreateLoanRequest
            {
                Name = "Gotowkowy",
                LoanType = LoanType.Cash,
                InterestMode = LoanInterestMode.Fixed,
                Principal = 10000m,
                InterestRate = 8m,
                StartDate = new DateOnly(2026, 1, 10),
                EndDate = new DateOnly(2026, 12, 10),
                RepaymentDayOfMonth = 10,
                TagId = wrongTagId,
                IsActive = true
            }, CancellationToken.None));
    }

    [Fact]
    public async Task ApplyLoanPrepaymentAsync_ReduceInstallment_Should_Lower_Future_Installment_Amount()
    {
        var service = CreateService();
        var loan = await service.CreateLoanAsync(new CreateLoanRequest
        {
            Name = "Nadplata test",
            LoanType = LoanType.Cash,
            InterestMode = LoanInterestMode.Fixed,
            Principal = 120000m,
            InterestRate = 8m,
            StartDate = new DateOnly(2026, 1, 15),
            EndDate = new DateOnly(2030, 12, 15),
            RepaymentDayOfMonth = 15,
            IsActive = true
        }, CancellationToken.None);

        var targetInstallment = loan.Installments.Single(x => x.Year == 2027 && x.Month == 1);
        var before = loan.Installments.Single(x => x.Year == 2027 && x.Month == 2).Amount;

        var updated = await service.ApplyLoanPrepaymentAsync(new ApplyLoanPrepaymentRequest
        {
            LoanInstallmentId = targetInstallment.Id,
            Amount = 10000m,
            Strategy = LoanPrepaymentStrategyType.ReduceInstallment
        }, CancellationToken.None);

        var after = updated.Installments.Single(x => x.Year == 2027 && x.Month == 2).Amount;
        Assert.True(after < before);
        Assert.Equal(loan.EndDate, updated.EndDate);
    }

    [Fact]
    public async Task ApplyLoanPrepaymentAsync_ShortenPeriod_Should_Reduce_Number_Of_Installments()
    {
        var service = CreateService();
        var loan = await service.CreateLoanAsync(new CreateLoanRequest
        {
            Name = "Nadplata test okres",
            LoanType = LoanType.Cash,
            InterestMode = LoanInterestMode.Fixed,
            Principal = 120000m,
            InterestRate = 8m,
            StartDate = new DateOnly(2026, 1, 15),
            EndDate = new DateOnly(2030, 12, 15),
            RepaymentDayOfMonth = 15,
            IsActive = true
        }, CancellationToken.None);

        var targetInstallment = loan.Installments.Single(x => x.Year == 2027 && x.Month == 1);

        var updated = await service.ApplyLoanPrepaymentAsync(new ApplyLoanPrepaymentRequest
        {
            LoanInstallmentId = targetInstallment.Id,
            Amount = 10000m,
            Strategy = LoanPrepaymentStrategyType.ShortenPeriod
        }, CancellationToken.None);

        Assert.True(updated.Installments.Count < loan.Installments.Count);
        Assert.True(updated.EndDate < loan.EndDate);
    }

    [Fact]
    public async Task UpdateLoanAsync_WithPaidInstallments_Should_Allow_Metadata_Changes_Like_Tag()
    {
        var service = CreateService();
        var loan = await service.CreateLoanAsync(new CreateLoanRequest
        {
            Name = "Kredyt do tagowania",
            LoanType = LoanType.Cash,
            InterestMode = LoanInterestMode.Fixed,
            Principal = 20000m,
            InterestRate = 8m,
            StartDate = new DateOnly(2026, 1, 10),
            EndDate = new DateOnly(2026, 12, 10),
            RepaymentDayOfMonth = 10,
            IsActive = true
        }, CancellationToken.None);

        var paidInstallment = loan.Installments.OrderBy(x => x.DueDate).First();
        await service.SetLoanInstallmentPaidAsync(new SetLoanInstallmentPaidRequest
        {
            LoanInstallmentId = paidInstallment.Id,
            IsPaid = true
        }, CancellationToken.None);

        int tagId;
        await using (var context = TestDbContextFactory.CreateDbContext(_dbName))
        {
            var category = await context.Categories.FirstAsync(x => x.Name == "Kredyt");

            var tag = new Tag
            {
                CategoryId = category.Id,
                Name = "Nadplacany"
            };
            context.Tags.Add(tag);
            await context.SaveChangesAsync();
            tagId = tag.Id;
        }

        var updated = await service.UpdateLoanAsync(new UpdateLoanRequest
        {
            Id = loan.Id,
            Name = "Kredyt do tagowania - update",
            LoanType = loan.LoanType,
            InterestMode = loan.InterestMode,
            WiborPeriodType = loan.WiborPeriodType,
            Principal = loan.Principal,
            InterestRate = loan.InterestRate,
            MarginRate = loan.MarginRate,
            InitialReferenceRate = null,
            RepaymentDayOfMonth = loan.RepaymentDayOfMonth,
            StartDate = loan.StartDate,
            EndDate = loan.EndDate,
            TagId = tagId,
            IsActive = false
        }, CancellationToken.None);

        Assert.Equal("Kredyt do tagowania - update", updated.Name);
        Assert.Equal(tagId, updated.TagId);
        Assert.False(updated.IsActive);
        Assert.Equal(loan.Installments.Count, updated.Installments.Count);
        Assert.Contains(updated.Installments, x => x.Id == paidInstallment.Id && x.IsPaid);
    }

    [Fact]
    public async Task UpdateLoanAsync_WithPaidInstallments_Should_Block_Schedule_Changes()
    {
        var service = CreateService();
        var loan = await service.CreateLoanAsync(new CreateLoanRequest
        {
            Name = "Kredyt z blokada",
            LoanType = LoanType.Cash,
            InterestMode = LoanInterestMode.Fixed,
            Principal = 20000m,
            InterestRate = 8m,
            StartDate = new DateOnly(2026, 1, 10),
            EndDate = new DateOnly(2026, 12, 10),
            RepaymentDayOfMonth = 10,
            IsActive = true
        }, CancellationToken.None);

        var paidInstallment = loan.Installments.OrderBy(x => x.DueDate).First();
        await service.SetLoanInstallmentPaidAsync(new SetLoanInstallmentPaidRequest
        {
            LoanInstallmentId = paidInstallment.Id,
            IsPaid = true
        }, CancellationToken.None);

        await Assert.ThrowsAsync<BadRequestException>(async () =>
            await service.UpdateLoanAsync(new UpdateLoanRequest
            {
                Id = loan.Id,
                Name = loan.Name,
                LoanType = loan.LoanType,
                InterestMode = loan.InterestMode,
                WiborPeriodType = loan.WiborPeriodType,
                Principal = loan.Principal,
                InterestRate = loan.InterestRate,
                MarginRate = loan.MarginRate,
                InitialReferenceRate = null,
                RepaymentDayOfMonth = loan.RepaymentDayOfMonth,
                StartDate = loan.StartDate,
                EndDate = loan.EndDate.AddMonths(1),
                IsActive = loan.IsActive
            }, CancellationToken.None));
    }

    [Fact]
    public async Task RemainingPrincipal_Should_Decrease_After_Paying_Installment()
    {
        var service = CreateService();
        var loan = await service.CreateLoanAsync(new CreateLoanRequest
        {
            Name = "Kapital pozostaly",
            LoanType = LoanType.Cash,
            InterestMode = LoanInterestMode.Fixed,
            Principal = 30000m,
            InterestRate = 8m,
            StartDate = new DateOnly(2026, 1, 10),
            EndDate = new DateOnly(2026, 12, 10),
            RepaymentDayOfMonth = 10,
            IsActive = true
        }, CancellationToken.None);

        var before = loan.RemainingPrincipal;
        var firstInstallment = loan.Installments.OrderBy(x => x.DueDate).First();

        await service.SetLoanInstallmentPaidAsync(new SetLoanInstallmentPaidRequest
        {
            LoanInstallmentId = firstInstallment.Id,
            IsPaid = true
        }, CancellationToken.None);

        var refreshed = (await service.GetAllAsync(CancellationToken.None)).Single(x => x.Id == loan.Id);
        Assert.True(refreshed.RemainingPrincipal < before);
    }
}