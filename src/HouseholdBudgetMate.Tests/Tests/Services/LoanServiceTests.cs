using HouseholdBudgetMate.Abstractions.Contracts.Loans.Requests;
using HouseholdBudgetMate.Abstractions.Contracts.Loans.Dto;
using HouseholdBudgetMate.Abstractions.Enums;
using HouseholdBudgetMate.Application.Kernel.Exceptions;
using HouseholdBudgetMate.Application.Services;
using HouseholdBudgetMate.Domain.Entities;
using HouseholdBudgetMate.Migrations;
using HouseholdBudgetMate.Tests.Shared;
using Microsoft.EntityFrameworkCore;
using FluentAssertions;

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

    // ── GetAllAsync ──────────────────────────────────────────────────────────

    /// <summary>
    /// Verifies that GetAllAsync returns an empty list when no loans exist in the database.
    /// </summary>
    [Fact]
    public async Task GetAllAsync_Should_Return_Empty_List_When_No_Loans()
    {
        var service = CreateService();
        var loans = await service.GetAllAsync(CancellationToken.None);
        Assert.Empty(loans);
    }

    /// <summary>
    /// Verifies that GetAllAsync returns active loans before inactive ones and then sorts by name within each group.
    /// </summary>
    [Fact]
    public async Task GetAllAsync_Should_Order_Active_First_Then_By_Name()
    {
        var service = CreateService();

        foreach (var (name, active) in new[] { ("Zebra", false), ("Alpha", true), ("Beta", true) })
        {
            await service.CreateLoanAsync(new CreateLoanRequest
            {
                Name = name,
                LoanType = LoanType.Cash,
                InterestMode = LoanInterestMode.Fixed,
                Principal = 10000m,
                InterestRate = 0m,
                StartDate = new DateOnly(2026, 1, 1),
                EndDate = new DateOnly(2026, 6, 1),
                RepaymentDayOfMonth = 1,
                IsActive = active
            }, CancellationToken.None);
        }

        var loans = await service.GetAllAsync(CancellationToken.None);

        Assert.Equal(3, loans.Count);
        Assert.Equal("Alpha", loans[0].Name);
        Assert.Equal("Beta", loans[1].Name);
        Assert.Equal("Zebra", loans[2].Name);
        Assert.True(loans[0].IsActive);
        Assert.False(loans[2].IsActive);
    }

    // ── CreateLoanAsync ──────────────────────────────────────────────────────

    /// <summary>
    /// Verifies that CreateLoanAsync generates the correct number of installments for a full calendar year
    /// and does not create any MonthPlans as a side effect.
    /// </summary>
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

    /// <summary>
    /// Verifies that interest is calculated using actual days between payment dates (actual/365 convention),
    /// not the number of days in the payment month.
    /// Real mortgage example: 800 000 PLN, WIBOR 1M 3.8% + margin 1.52% = 5.32%, StartDate 19.05.2026,
    /// first payment 15.06.2026, 336 months.
    /// Second installment (15.07.2026): interest period = Jun 15 → Jul 15 = 30 days.
    /// Expected interest ≈ 3493.85 PLN (799 031.20 × 5.32% / 365 × 30).
    /// Fifth installment (15.10.2026): interest period = Sep 15 → Oct 15 = 30 days.
    /// Expected interest ≈ 3480 PLN.
    /// </summary>
    [Fact]
    public async Task CreateLoanAsync_VariableWibor_Should_Use_ActualDays_Between_Due_Dates_For_Interest()
    {
        var service = CreateService();

        var loan = await service.CreateLoanAsync(new CreateLoanRequest
        {
            Name = "Hipoteka test",
            LoanType = LoanType.Mortgage,
            InterestMode = LoanInterestMode.VariableWibor,
            WiborPeriodType = WiborPeriodType.Wibor1M,
            MarginRate = 1.52m,
            InitialReferenceRate = 3.8m,
            Principal = 800_000m,
            InterestRate = 0,
            StartDate = new DateOnly(2026, 6, 15),
            EndDate = new DateOnly(2054, 5, 15),
            RepaymentDayOfMonth = 15,
            IsActive = true
        }, CancellationToken.None);

        AssertInstallmentAmounts(loan, new DateOnly(2026, 6, 15), 968.80m, 3614.68m);
        AssertInstallmentAmounts(loan, new DateOnly(2026, 7, 15), 1089.63m, 3493.85m);
        AssertInstallmentAmounts(loan, new DateOnly(2026, 10, 15), 1102.97m, 3480.51m);
        AssertInstallmentAmounts(loan, new DateOnly(2026, 11, 15), 991.94m, 3591.54m);
        AssertInstallmentAmounts(loan, new DateOnly(2028, 1, 15), 1060.84m, 3522.64m);
        AssertInstallmentAmounts(loan, new DateOnly(2031, 9, 15), 1307.25m, 3276.23m);
        AssertInstallmentAmounts(loan, new DateOnly(2034, 5, 15), 1617.86m, 2965.62m);
        AssertInstallmentAmounts(loan, new DateOnly(2038, 2, 15), 1871.90m, 2711.58m);
        AssertInstallmentAmounts(loan, new DateOnly(2040, 3, 15), 2261.08m, 2322.40m);
        AssertInstallmentAmounts(loan, new DateOnly(2054, 4, 15), 4534.09m, 49.39m);
        AssertInstallmentAmounts(loan, new DateOnly(2054, 5, 15), 6396.52m, 27.97m);
    }

    /// <summary>
    /// Verifies that CreateLoanAsync throws BadRequestException when the specified tag belongs to a category
    /// other than the loan category.
    /// </summary>
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

    /// <summary>
    /// Verifies that a fixed-rate mortgage does not fully amortize during the initial five-year fixed phase
    /// and that no installments are generated with a due date before the loan start date.
    /// </summary>
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

    // ── UpdateLoanAsync ──────────────────────────────────────────────────────

    /// <summary>
    /// Verifies that UpdateLoanAsync throws NotFoundException when the loan does not exist.
    /// </summary>
    [Fact]
    public async Task UpdateLoanAsync_Should_Throw_NotFoundException_When_Loan_Not_Found()
    {
        var service = CreateService();

        await Assert.ThrowsAsync<NotFoundException>(async () =>
            await service.UpdateLoanAsync(new UpdateLoanRequest
            {
                Id = 99999,
                Name = "Ghost",
                LoanType = LoanType.Cash,
                InterestMode = LoanInterestMode.Fixed,
                Principal = 1000m,
                InterestRate = 0m,
                RepaymentDayOfMonth = 10,
                StartDate = new DateOnly(2026, 1, 10),
                EndDate = new DateOnly(2026, 6, 10),
                IsActive = true
            }, CancellationToken.None));
    }

    /// <summary>
    /// Verifies that UpdateLoanAsync rebuilds installments when no payments have been made,
    /// extending the loan end date produces more installments.
    /// </summary>
    [Fact]
    public async Task UpdateLoanAsync_Should_Rebuild_Installments_When_No_Paid_Installments()
    {
        var service = CreateService();
        var loan = await service.CreateLoanAsync(new CreateLoanRequest
        {
            Name = "Kredyt krótki",
            LoanType = LoanType.Cash,
            InterestMode = LoanInterestMode.Fixed,
            Principal = 12000m,
            InterestRate = 0m,
            StartDate = new DateOnly(2026, 1, 10),
            EndDate = new DateOnly(2026, 6, 10),
            RepaymentDayOfMonth = 10,
            IsActive = true
        }, CancellationToken.None);

        Assert.Equal(6, loan.Installments.Count);

        var updated = await service.UpdateLoanAsync(new UpdateLoanRequest
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
            EndDate = new DateOnly(2026, 12, 10),
            TagId = null,
            IsActive = loan.IsActive
        }, CancellationToken.None);

        Assert.Equal(12, updated.Installments.Count);
    }

    /// <summary>
    /// Verifies that UpdateLoanAsync allows changing metadata (name, tag, active flag) when the loan
    /// has paid installments, preserving the existing schedule and paid status.
    /// </summary>
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

    /// <summary>
    /// Verifies that UpdateLoanAsync throws BadRequestException when schedule parameters are changed
    /// but the loan already has paid installments.
    /// </summary>
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

    // ── AddLoanRateEntryAsync ────────────────────────────────────────────────

    /// <summary>
    /// Verifies that AddLoanRateEntryAsync recalculates future unpaid installments when a new WIBOR rate is added.
    /// </summary>
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

        var updated = await service.AddLoanRateEntryAsync(
            await AttachExpectedScheduleVersionAsync(service, new AddLoanRateEntryRequest
            {
                LoanId = loan.Id,
                EffectiveFrom = new DateOnly(2026, 6, 1),
                ReferenceRate = 7m
            }),
            CancellationToken.None);

        var after = updated.Installments.Single(x => x.Month == 6 && x.Year == 2026).Amount;
        Assert.NotEqual(before, after);
    }

    /// <summary>
    /// Verifies a real mortgage-style recalculation when WIBOR changes from 3.80% to 3.75%.
    /// Installments before the effective date stay unchanged, and future installments are rebuilt
    /// from the remaining principal using the lower reference rate.
    /// </summary>
    [Fact]
    public async Task AddLoanRateEntryAsync_VariableWibor_Should_Recalculate_Expected_Installment_Amounts()
    {
        var service = CreateService();

        var loan = await service.CreateLoanAsync(new CreateLoanRequest
        {
            Name = "Hipoteka WIBOR 3.80",
            LoanType = LoanType.Mortgage,
            InterestMode = LoanInterestMode.VariableWibor,
            WiborPeriodType = WiborPeriodType.Wibor1M,
            MarginRate = 1.52m,
            InitialReferenceRate = 3.8m,
            Principal = 800_000m,
            InterestRate = 0m,
            StartDate = new DateOnly(2026, 6, 15),
            EndDate = new DateOnly(2054, 5, 15),
            RepaymentDayOfMonth = 15,
            IsActive = true
        }, CancellationToken.None);

        AssertInstallmentAmounts(loan, new DateOnly(2026, 6, 15), 968.80m, 3614.68m);
        AssertInstallmentAmounts(loan, new DateOnly(2026, 7, 15), 1089.63m, 3493.85m);
        AssertInstallmentAmounts(loan, new DateOnly(2054, 5, 15), 6396.52m, 27.97m);

        var updated = await service.AddLoanRateEntryAsync(
            await AttachExpectedScheduleVersionAsync(service, new AddLoanRateEntryRequest
            {
                LoanId = loan.Id,
                EffectiveFrom = new DateOnly(2026, 6, 16),
                ReferenceRate = 3.73m
            }),
            CancellationToken.None);

        AssertInstallmentAmounts(updated, new DateOnly(2026, 7, 15), 1101.97m, 3447.87m);
        AssertInstallmentAmounts(updated, new DateOnly(2026, 8, 15), 991.95m, 3557.89m);
        AssertInstallmentAmounts(updated, new DateOnly(2026, 10, 15), 1115.30m, 3434.54m);
        AssertInstallmentAmounts(updated, new DateOnly(2028, 1, 15), 1074.59m, 3475.25m);
        AssertInstallmentAmounts(updated, new DateOnly(2031, 9, 15), 1320.22m, 3229.62m);
        AssertInstallmentAmounts(updated, new DateOnly(2034, 5, 15), 1628.24m, 2921.60m);
        AssertInstallmentAmounts(updated, new DateOnly(2038, 2, 15), 1881.10m, 2668.74m);
        AssertInstallmentAmounts(updated, new DateOnly(2040, 3, 15), 2265.47m, 2284.37m);
        AssertInstallmentAmounts(updated, new DateOnly(2054, 4, 15), 4502.92m, 46.92m);
    }

    /// <summary>
    /// Verifies that a WIBOR schedule rebuild keeps existing open future month plans populated
    /// by replacing the deleted old installment expense with the recalculated installment.
    /// </summary>
    [Fact]
    public async Task AddLoanRateEntryAsync_Should_Refresh_Open_Future_Month_Plan_Loan_Expense()
    {
        var service = CreateService(new DateTime(2026, 6, 16, 12, 0, 0, DateTimeKind.Utc));
        var loan = await service.CreateLoanAsync(new CreateLoanRequest
        {
            Name = "Hipoteka przyszly plan WIBOR",
            LoanType = LoanType.Mortgage,
            InterestMode = LoanInterestMode.VariableWibor,
            WiborPeriodType = WiborPeriodType.Wibor1M,
            MarginRate = 1.52m,
            InitialReferenceRate = 3.8m,
            Principal = 800_000m,
            InterestRate = 0m,
            StartDate = new DateOnly(2026, 6, 15),
            EndDate = new DateOnly(2054, 5, 15),
            RepaymentDayOfMonth = 15,
            IsActive = true
        }, CancellationToken.None);

        await using (var setupContext = TestDbContextFactory.CreateDbContext(_dbName))
        {
            setupContext.MonthPlans.Add(new MonthPlan { Year = 2026, Month = 7, IsClosed = false });
            await setupContext.SaveChangesAsync();
        }

        await service.SyncLoanInstallmentsForMonthAsync(2026, 7, CancellationToken.None);

        int oldInstallmentExpenseId;
        await using (var beforeContext = TestDbContextFactory.CreateDbContext(_dbName))
        {
            var julyPlan = await beforeContext.MonthPlans.SingleAsync(x => x.Year == 2026 && x.Month == 7);
            oldInstallmentExpenseId = await beforeContext.Expenses
                .Where(x => x.MonthPlanId == julyPlan.Id)
                .Select(x => x.LoanInstallmentId!.Value)
                .SingleAsync();
        }

        var updated = await service.AddLoanRateEntryAsync(
            await AttachExpectedScheduleVersionAsync(service, new AddLoanRateEntryRequest
            {
                LoanId = loan.Id,
                EffectiveFrom = new DateOnly(2026, 6, 16),
                ReferenceRate = 3.73m
            }),
            CancellationToken.None);

        var julyInstallment = updated.Installments.Single(x => x.DueDate == new DateOnly(2026, 7, 15));
        await using var verifyContext = TestDbContextFactory.CreateDbContext(_dbName);
        var verifyJulyPlan = await verifyContext.MonthPlans.SingleAsync(x => x.Year == 2026 && x.Month == 7);
        var refreshedExpense = await verifyContext.Expenses.SingleAsync(x => x.MonthPlanId == verifyJulyPlan.Id);

        Assert.NotEqual(oldInstallmentExpenseId, refreshedExpense.LoanInstallmentId);
        Assert.Equal(julyInstallment.Id, refreshedExpense.LoanInstallmentId);
        Assert.Equal(julyInstallment.Amount, refreshedExpense.PlannedAmount);
        Assert.Equal(0m, refreshedExpense.ActualAmount);
    }

    /// <summary>
    /// Verifies that AddLoanRateEntryAsync throws BadRequestException when called for a non-mortgage loan.
    /// </summary>
    [Fact]
    public async Task AddLoanRateEntryAsync_Should_Throw_BadRequest_For_Non_Mortgage_Loan()
    {
        var service = CreateService();
        var loan = await service.CreateLoanAsync(new CreateLoanRequest
        {
            Name = "Gotówkowy",
            LoanType = LoanType.Cash,
            InterestMode = LoanInterestMode.Fixed,
            Principal = 10000m,
            InterestRate = 5m,
            StartDate = new DateOnly(2026, 1, 10),
            EndDate = new DateOnly(2026, 12, 10),
            RepaymentDayOfMonth = 10,
            IsActive = true
        }, CancellationToken.None);

        await Assert.ThrowsAsync<BadRequestException>(async () =>
            await service.AddLoanRateEntryAsync(new AddLoanRateEntryRequest
            {
                LoanId = loan.Id,
                EffectiveFrom = new DateOnly(2026, 6, 1),
                ReferenceRate = 6m,
                ExpectedScheduleVersion = "ignored"
            }, CancellationToken.None));
    }

    /// <summary>
    /// Verifies that AddLoanRateEntryAsync throws BadRequestException when a rate entry for the same
    /// effective date already exists.
    /// </summary>
    [Fact]
    public async Task AddLoanRateEntryAsync_Should_Throw_BadRequest_For_Duplicate_EffectiveFrom()
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

        // Add a new entry for June 1
        await service.AddLoanRateEntryAsync(
            await AttachExpectedScheduleVersionAsync(service, new AddLoanRateEntryRequest
            {
                LoanId = loan.Id,
                EffectiveFrom = new DateOnly(2026, 6, 1),
                ReferenceRate = 6m
            }),
            CancellationToken.None);

        // Duplicate: same EffectiveFrom
        await Assert.ThrowsAsync<BadRequestException>(async () =>
            await service.AddLoanRateEntryAsync(new AddLoanRateEntryRequest
            {
                LoanId = loan.Id,
                EffectiveFrom = new DateOnly(2026, 6, 1),
                ReferenceRate = 7m,
                ExpectedScheduleVersion = "ignored"
            }, CancellationToken.None));
    }

    /// <summary>
    /// Verifies that AddLoanRateEntryAsync throws BadRequestException when there is already a paid
    /// installment whose due date falls on or after the requested effective date.
    /// </summary>
    [Fact]
    public async Task AddLoanRateEntryAsync_Should_Throw_BadRequest_When_Paid_Installment_Exists_After_EffectiveFrom()
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

        // Pay March installment (DueDate = 2026-03-15)
        var marchInstallment = loan.Installments.Single(x => x.Year == 2026 && x.Month == 3);
        await service.SetLoanInstallmentPaidAsync(new SetLoanInstallmentPaidRequest
        {
            LoanInstallmentId = marchInstallment.Id,
            IsPaid = true
        }, CancellationToken.None);

        // EffectiveFrom = March 1 → paid March installment (DueDate 2026-03-15) >= 2026-03-01
        await Assert.ThrowsAsync<BadRequestException>(async () =>
            await service.AddLoanRateEntryAsync(new AddLoanRateEntryRequest
            {
                LoanId = loan.Id,
                EffectiveFrom = new DateOnly(2026, 3, 1),
                ReferenceRate = 7m,
                ExpectedScheduleVersion = "ignored"
            }, CancellationToken.None));
    }

    /// <summary>
    /// Verifies that a WIBOR change entered after the paid June installment starts affecting
    /// the July installment rather than rewriting the already paid June row.
    /// </summary>
    [Fact]
    public async Task AddLoanRateEntryAsync_Should_Start_From_Next_Unpaid_Installment_When_Previous_One_Is_Already_Paid()
    {
        var service = CreateService();
        var loan = await service.CreateLoanAsync(new CreateLoanRequest
        {
            Name = "Hipoteka start od lipca",
            LoanType = LoanType.Mortgage,
            InterestMode = LoanInterestMode.VariableWibor,
            WiborPeriodType = WiborPeriodType.Wibor1M,
            MarginRate = 1.52m,
            InitialReferenceRate = 3.8m,
            Principal = 800_000m,
            InterestRate = 0m,
            StartDate = new DateOnly(2026, 6, 15),
            EndDate = new DateOnly(2054, 5, 15),
            RepaymentDayOfMonth = 15,
            IsActive = true
        }, CancellationToken.None);

        var juneInstallment = loan.Installments.Single(x => x.DueDate == new DateOnly(2026, 6, 15));
        var julyBefore = loan.Installments.Single(x => x.DueDate == new DateOnly(2026, 7, 15)).Amount;

        await service.SetLoanInstallmentPaidAsync(new SetLoanInstallmentPaidRequest
        {
            LoanInstallmentId = juneInstallment.Id,
            IsPaid = true
        }, CancellationToken.None);

        var updated = await service.AddLoanRateEntryAsync(
            await AttachExpectedScheduleVersionAsync(service, new AddLoanRateEntryRequest
            {
                LoanId = loan.Id,
                EffectiveFrom = new DateOnly(2026, 6, 16),
                ReferenceRate = 3.73m
            }),
            CancellationToken.None);

        var juneAfter = updated.Installments.Single(x => x.DueDate == new DateOnly(2026, 6, 15));
        var julyAfter = updated.Installments.Single(x => x.DueDate == new DateOnly(2026, 7, 15));

        Assert.True(juneAfter.IsPaid);
        Assert.Equal(juneInstallment.Amount, juneAfter.Amount);
        Assert.NotEqual(julyBefore, julyAfter.Amount);
    }

    /// <summary>
    /// Verifies that AddLoanRateEntryAsync throws NotFoundException when the loan does not exist.
    /// </summary>
    [Fact]
    public async Task AddLoanRateEntryAsync_Should_Throw_NotFoundException_When_Loan_Not_Found()
    {
        var service = CreateService();

        await Assert.ThrowsAsync<NotFoundException>(async () =>
            await service.AddLoanRateEntryAsync(new AddLoanRateEntryRequest
            {
                LoanId = 99999,
                EffectiveFrom = new DateOnly(2026, 6, 1),
                ReferenceRate = 6m,
                ExpectedScheduleVersion = "ignored"
            }, CancellationToken.None));
    }

    /// <summary>
    /// Verifies that the WIBOR preview does not mutate persisted state and matches the confirmed result.
    /// </summary>
    [Fact]
    public async Task PreviewAddLoanRateEntryAsync_Should_Be_SideEffect_Free_And_Match_Confirmed_Result()
    {
        var service = CreateService();
        var loan = await service.CreateLoanAsync(new CreateLoanRequest
        {
            Name = "Hipoteka preview WIBOR",
            LoanType = LoanType.Mortgage,
            InterestMode = LoanInterestMode.VariableWibor,
            WiborPeriodType = WiborPeriodType.Wibor1M,
            MarginRate = 1.52m,
            InitialReferenceRate = 3.8m,
            Principal = 800_000m,
            InterestRate = 0m,
            StartDate = new DateOnly(2026, 6, 15),
            EndDate = new DateOnly(2054, 5, 15),
            RepaymentDayOfMonth = 15,
            IsActive = true
        }, CancellationToken.None);

        await using var beforeContext = TestDbContextFactory.CreateDbContext(_dbName);
        var countsBefore = await GetLoanCollectionCountsAsync(beforeContext);

        var previewRequest = new AddLoanRateEntryRequest
        {
            LoanId = loan.Id,
            EffectiveFrom = new DateOnly(2026, 6, 16),
            ReferenceRate = 3.73m
        };
        var preview = await service.PreviewAddLoanRateEntryAsync(previewRequest, CancellationToken.None);

        await using var afterPreviewContext = TestDbContextFactory.CreateDbContext(_dbName);
        var countsAfterPreview = await GetLoanCollectionCountsAsync(afterPreviewContext);
        Assert.Equal(countsBefore, countsAfterPreview);

        var updated = await service.AddLoanRateEntryAsync(
            new AddLoanRateEntryRequest
            {
                LoanId = loan.Id,
                EffectiveFrom = new DateOnly(2026, 6, 16),
                ReferenceRate = 3.73m,
                ExpectedScheduleVersion = preview.SourceVersion
            },
            CancellationToken.None);

        AssertPreviewMatchesLoanDtos(preview, loan, updated);
    }

    /// <summary>
    /// Verifies that confirming a preview with an obsolete source version is rejected.
    /// </summary>
    [Fact]
    public async Task AddLoanRateEntryAsync_Should_Reject_Stale_Source_Version_Before_Mutation()
    {
        var service = CreateService();
        var loan = await service.CreateLoanAsync(new CreateLoanRequest
        {
            Name = "Hipoteka stale wersja",
            LoanType = LoanType.Mortgage,
            InterestMode = LoanInterestMode.VariableWibor,
            WiborPeriodType = WiborPeriodType.Wibor1M,
            MarginRate = 1.52m,
            InitialReferenceRate = 3.8m,
            Principal = 800_000m,
            InterestRate = 0m,
            StartDate = new DateOnly(2026, 6, 15),
            EndDate = new DateOnly(2054, 5, 15),
            RepaymentDayOfMonth = 15,
            IsActive = true
        }, CancellationToken.None);

        var preview = await service.PreviewAddLoanRateEntryAsync(new AddLoanRateEntryRequest
        {
            LoanId = loan.Id,
            EffectiveFrom = new DateOnly(2026, 6, 16),
            ReferenceRate = 3.73m
        }, CancellationToken.None);

        var firstInstallment = loan.Installments.OrderBy(x => x.DueDate).First();
        await service.SetLoanInstallmentPaidAsync(new SetLoanInstallmentPaidRequest
        {
            LoanInstallmentId = firstInstallment.Id,
            IsPaid = true
        }, CancellationToken.None);

        await Assert.ThrowsAsync<ConflictException>(async () =>
            await service.AddLoanRateEntryAsync(new AddLoanRateEntryRequest
            {
                LoanId = loan.Id,
                EffectiveFrom = new DateOnly(2026, 6, 16),
                ReferenceRate = 3.73m,
                ExpectedScheduleVersion = preview.SourceVersion
            }, CancellationToken.None));
    }

    // ── ApplyLoanPrepaymentAsync ─────────────────────────────────────────────

    /// <summary>
    /// Verifies that ApplyLoanPrepaymentAsync with ReduceInstallment strategy lowers the monthly installment
    /// amount for future periods while keeping the end date unchanged.
    /// </summary>
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

        var updated = await service.ApplyLoanPrepaymentAsync(
            await AttachExpectedScheduleVersionAsync(service, new ApplyLoanPrepaymentRequest
            {
                LoanInstallmentId = targetInstallment.Id,
                Amount = 10000m,
                Strategy = LoanPrepaymentStrategyType.ReduceInstallment
            }),
            CancellationToken.None);

        var after = updated.Installments.Single(x => x.Year == 2027 && x.Month == 2).Amount;
        Assert.True(after < before);
        Assert.Equal(loan.EndDate, updated.EndDate);
    }

    /// <summary>
    /// Verifies that ReduceInstallment applies the prepayment to principal from the selected future
    /// installment onward, keeping earlier installments unchanged and lowering the recalculated payment.
    /// </summary>
    [Fact]
    public async Task ApplyLoanPrepaymentAsync_ReduceInstallment_Should_Subtract_Amount_From_Remaining_Principal()
    {
        var service = CreateService();
        var loan = await service.CreateLoanAsync(new CreateLoanRequest
        {
            Name = "Hipoteka WIBOR 3.80",
            LoanType = LoanType.Mortgage,
            InterestMode = LoanInterestMode.VariableWibor,
            WiborPeriodType = WiborPeriodType.Wibor1M,
            MarginRate = 1.52m,
            InitialReferenceRate = 3.8m,
            Principal = 800_000m,
            InterestRate = 0m,
            StartDate = new DateOnly(2026, 6, 15),
            EndDate = new DateOnly(2054, 5, 15),
            RepaymentDayOfMonth = 15,
            IsActive = true
        }, CancellationToken.None);

        AssertInstallmentAmounts(loan, new DateOnly(2026, 6, 15), 968.80m, 3614.68m);
        AssertInstallmentAmounts(loan, new DateOnly(2026, 7, 15), 1089.63m, 3493.85m);
        AssertInstallmentAmounts(loan, new DateOnly(2054, 5, 15), 6396.52m, 27.97m);

        var updated = await service.AddLoanRateEntryAsync(
            await AttachExpectedScheduleVersionAsync(service, new AddLoanRateEntryRequest
            {
                LoanId = loan.Id,
                EffectiveFrom = new DateOnly(2026, 6, 16),
                ReferenceRate = 3.73m
            }),
            CancellationToken.None);

        AssertInstallmentAmounts(updated, new DateOnly(2026, 7, 15), 1101.97m, 3447.87m);
        AssertInstallmentAmounts(updated, new DateOnly(2026, 8, 15), 991.95m, 3557.89m);
        AssertInstallmentAmounts(updated, new DateOnly(2026, 10, 15), 1115.30m, 3434.54m);
        AssertInstallmentAmounts(updated, new DateOnly(2028, 1, 15), 1074.59m, 3475.25m);
        AssertInstallmentAmounts(updated, new DateOnly(2031, 9, 15), 1320.22m, 3229.62m);
        AssertInstallmentAmounts(updated, new DateOnly(2034, 5, 15), 1628.24m, 2921.60m);
        AssertInstallmentAmounts(updated, new DateOnly(2038, 2, 15), 1881.10m, 2668.74m);
        AssertInstallmentAmounts(updated, new DateOnly(2040, 3, 15), 2265.47m, 2284.37m);
        AssertInstallmentAmounts(updated, new DateOnly(2054, 4, 15), 4502.92m, 46.92m);

        var july2026 = updated.Installments.Single(x => x.DueDate == new DateOnly(2026, 7, 15));
        var june2026 = updated.Installments.Single(x => x.DueDate == new DateOnly(2026, 6, 15));
        var august2026Before = updated.Installments.Single(x => x.DueDate == new DateOnly(2026, 8, 15));
        var principalFromPrepaymentPoint = updated.Installments
            .Where(x => x.DueDate >= july2026.DueDate)
            .Sum(x => x.PrincipalAmount);

        var updated2 = await service.ApplyLoanPrepaymentAsync(
            await AttachExpectedScheduleVersionAsync(service, new ApplyLoanPrepaymentRequest
            {
                LoanInstallmentId = july2026.Id,
                Amount = 100m,
                Strategy = LoanPrepaymentStrategyType.ReduceInstallment
            }),
            CancellationToken.None);

        var juneAfter = updated2.Installments.Single(x => x.DueDate == june2026.DueDate);
        var august2026After = updated2.Installments.Single(x => x.DueDate == new DateOnly(2026, 8, 15));
        var recalculatedPrincipal = updated2.Installments
            .Where(x => x.DueDate >= july2026.DueDate)
            .Sum(x => x.PrincipalAmount);

        Assert.Equal(june2026.Amount, juneAfter.Amount);
        Assert.Equal(june2026.PrincipalAmount, juneAfter.PrincipalAmount);
        Assert.Equal(june2026.InterestAmount, juneAfter.InterestAmount);
        Assert.Equal(decimal.Round(principalFromPrepaymentPoint - 100m, 2, MidpointRounding.AwayFromZero), recalculatedPrincipal);
        Assert.True(august2026After.Amount < august2026Before.Amount);
        Assert.Equal(updated.EndDate, updated2.EndDate);

        AssertInstallmentAmounts(updated2, new DateOnly(2026, 7, 15), 1101.83m, 3447.44m);
        AssertInstallmentAmounts(updated2, new DateOnly(2026, 8, 15), 991.83m, 3557.44m);
        AssertInstallmentAmounts(updated2, new DateOnly(2026, 10, 15), 1115.16m, 3434.11m);
        AssertInstallmentAmounts(updated2, new DateOnly(2028, 1, 15), 1074.45m, 3474.82m);
        AssertInstallmentAmounts(updated2, new DateOnly(2031, 9, 15), 1320.06m, 3229.21m);
        AssertInstallmentAmounts(updated2, new DateOnly(2034, 5, 15), 1628.04m, 2921.23m);
        AssertInstallmentAmounts(updated2, new DateOnly(2038, 2, 15), 1880.86m, 2668.41m);
        AssertInstallmentAmounts(updated2, new DateOnly(2040, 3, 15), 2265.19m, 2284.08m);
        AssertInstallmentAmounts(updated2, new DateOnly(2054, 4, 15), 4502.36m, 46.91m);
    }

    /// <summary>
    /// Verifies that a prepayment schedule rebuild keeps existing open future month plans populated
    /// by replacing the deleted old installment expense with the recalculated installment.
    /// </summary>
    [Fact]
    public async Task ApplyLoanPrepaymentAsync_Should_Refresh_Open_Future_Month_Plan_Loan_Expense()
    {
        var service = CreateService(new DateTime(2026, 7, 16, 12, 0, 0, DateTimeKind.Utc));
        var loan = await service.CreateLoanAsync(new CreateLoanRequest
        {
            Name = "Hipoteka przyszly plan nadplata",
            LoanType = LoanType.Mortgage,
            InterestMode = LoanInterestMode.VariableWibor,
            WiborPeriodType = WiborPeriodType.Wibor1M,
            MarginRate = 1.52m,
            InitialReferenceRate = 3.8m,
            Principal = 800_000m,
            InterestRate = 0m,
            StartDate = new DateOnly(2026, 6, 15),
            EndDate = new DateOnly(2054, 5, 15),
            RepaymentDayOfMonth = 15,
            IsActive = true
        }, CancellationToken.None);

        await using (var setupContext = TestDbContextFactory.CreateDbContext(_dbName))
        {
            setupContext.MonthPlans.Add(new MonthPlan { Year = 2026, Month = 8, IsClosed = false });
            await setupContext.SaveChangesAsync();
        }

        await service.SyncLoanInstallmentsForMonthAsync(2026, 8, CancellationToken.None);

        var augustInstallment = loan.Installments.Single(x => x.DueDate == new DateOnly(2026, 8, 15));
        int oldInstallmentExpenseId;
        await using (var beforeContext = TestDbContextFactory.CreateDbContext(_dbName))
        {
            var augustPlan = await beforeContext.MonthPlans.SingleAsync(x => x.Year == 2026 && x.Month == 8);
            oldInstallmentExpenseId = await beforeContext.Expenses
                .Where(x => x.MonthPlanId == augustPlan.Id)
                .Select(x => x.LoanInstallmentId!.Value)
                .SingleAsync();
        }

        var updated = await service.ApplyLoanPrepaymentAsync(
            await AttachExpectedScheduleVersionAsync(service, new ApplyLoanPrepaymentRequest
            {
                LoanInstallmentId = augustInstallment.Id,
                Amount = 20_000m,
                Strategy = LoanPrepaymentStrategyType.ReduceInstallment
            }),
            CancellationToken.None);

        var updatedAugustInstallment = updated.Installments.Single(x => x.DueDate == new DateOnly(2026, 8, 15));
        await using var verifyContext = TestDbContextFactory.CreateDbContext(_dbName);
        var verifyAugustPlan = await verifyContext.MonthPlans.SingleAsync(x => x.Year == 2026 && x.Month == 8);
        var refreshedExpense = await verifyContext.Expenses.SingleAsync(x => x.MonthPlanId == verifyAugustPlan.Id);

        Assert.NotEqual(oldInstallmentExpenseId, refreshedExpense.LoanInstallmentId);
        Assert.Equal(updatedAugustInstallment.Id, refreshedExpense.LoanInstallmentId);
        Assert.Equal(updatedAugustInstallment.Amount, refreshedExpense.PlannedAmount);
        Assert.Equal(0m, refreshedExpense.ActualAmount);
    }

    /// <summary>
    /// Verifies the ING-style flow where the bank provides the new installment amount and the
    /// last installment date after a prepayment that shortens the loan period.
    /// </summary>
    [Fact]
    public async Task ApplyLoanInstallmentAmountChangeAsync_After_Wibor_Change_Should_Recalculate_Shortened_Schedule_From_Bank_Installment()
    {
        var service = CreateService(new DateTime(2026, 7, 16, 12, 0, 0, DateTimeKind.Utc));
        var loan = await service.CreateLoanAsync(new CreateLoanRequest
        {
            Name = "Hipoteka ING 800000 WIBOR 3.80",
            LoanType = LoanType.Mortgage,
            InterestMode = LoanInterestMode.VariableWibor,
            WiborPeriodType = WiborPeriodType.Wibor1M,
            MarginRate = 1.52m,
            InitialReferenceRate = 3.8m,
            Principal = 800_000m,
            InterestRate = 0m,
            StartDate = new DateOnly(2026, 6, 15),
            EndDate = new DateOnly(2054, 5, 15),
            RepaymentDayOfMonth = 15,
            IsActive = true
        }, CancellationToken.None);

        AssertInstallmentAmounts(loan, new DateOnly(2026, 6, 15), 968.80m, 3614.68m);
        AssertInstallmentAmounts(loan, new DateOnly(2026, 7, 15), 1089.63m, 3493.85m);
        AssertInstallmentAmounts(loan, new DateOnly(2054, 5, 15), 6396.52m, 27.97m);

        var updatedRate = await service.AddLoanRateEntryAsync(
            await AttachExpectedScheduleVersionAsync(service, new AddLoanRateEntryRequest
            {
                LoanId = loan.Id,
                EffectiveFrom = new DateOnly(2026, 6, 16),
                ReferenceRate = 3.73m
            }),
            CancellationToken.None);

        AssertInstallmentAmounts(updatedRate, new DateOnly(2026, 7, 15), 1101.97m, 3447.87m);
        AssertInstallmentAmounts(updatedRate, new DateOnly(2026, 8, 15), 991.95m, 3557.89m);
        AssertInstallmentAmounts(updatedRate, new DateOnly(2028, 1, 15), 1074.59m, 3475.25m);
        AssertInstallmentAmounts(updatedRate, new DateOnly(2054, 4, 15), 4502.92m, 46.92m);

        using var dbContext = TestDbContextFactory.CreateDbContext(_dbName);
        var july2026Id = dbContext.LoanInstallments.Single(x => x.LoanId == loan.Id && x.DueDate == new DateOnly(2026, 7, 15)).Id;

        var updated = await service.ApplyLoanInstallmentAmountChangeAsync(
            await AttachExpectedScheduleVersionAsync(service, new ApplyLoanInstallmentAmountChangeRequest
            {
                LoanInstallmentId = july2026Id,
                InstallmentAmount = 4549.84m,
                LastInstallmentDate = new DateOnly(2045, 5, 15)
            }),
            CancellationToken.None);

        Assert.Equal(new DateOnly(2045, 5, 15), updated.EndDate);
        Assert.Equal(228, updated.Installments.Count);
        Assert.InRange(updated.Installments.Single(x => x.DueDate == new DateOnly(2026, 7, 15)).Amount, 4549.83m, 4549.85m);
        AssertInstallmentAmounts(updated, new DateOnly(2026, 7, 15), 1728.19m, 2821.66m);
        AssertInstallmentAmounts(updated, new DateOnly(2026, 8, 15), 1641.84m, 2908.01m);
        AssertInstallmentAmounts(updated, new DateOnly(2028, 1, 15), 1774.64m, 2775.21m);
        AssertInstallmentAmounts(updated, new DateOnly(2034, 5, 15), 2572.43m, 1977.42m);
        AssertInstallmentAmounts(updated, new DateOnly(2040, 3, 15), 3504.91m, 1044.94m);
        AssertInstallmentAmounts(updated, new DateOnly(2045, 5, 15), 5117.39m, 22.08m);

    }

    [Fact]
    public async Task ApplyLoanInstallmentAmountChangeAsync_Should_Suggest_Earlier_Last_Installment_Date_When_Amount_Is_Too_High()
    {
        var service = CreateService(new DateTime(2026, 7, 16, 12, 0, 0, DateTimeKind.Utc));
        var loan = await service.CreateLoanAsync(new CreateLoanRequest
        {
            Name = "Hipoteka ING 800000 WIBOR 3.80",
            LoanType = LoanType.Mortgage,
            InterestMode = LoanInterestMode.VariableWibor,
            WiborPeriodType = WiborPeriodType.Wibor1M,
            MarginRate = 1.52m,
            InitialReferenceRate = 3.8m,
            Principal = 800_000m,
            InterestRate = 0m,
            StartDate = new DateOnly(2026, 6, 15),
            EndDate = new DateOnly(2054, 5, 15),
            RepaymentDayOfMonth = 15,
            IsActive = true
        }, CancellationToken.None);

        await service.AddLoanRateEntryAsync(
            await AttachExpectedScheduleVersionAsync(service, new AddLoanRateEntryRequest
            {
                LoanId = loan.Id,
                EffectiveFrom = new DateOnly(2026, 6, 16),
                ReferenceRate = 3.73m
            }),
            CancellationToken.None);
        using var dbContext = TestDbContextFactory.CreateDbContext(_dbName);
        var july2026Id = dbContext.LoanInstallments.Single(x => x.LoanId == loan.Id && x.DueDate == new DateOnly(2026, 7, 15)).Id;

        var ex = await Assert.ThrowsAsync<BadRequestException>(async () => await service.ApplyLoanInstallmentAmountChangeAsync(
            new ApplyLoanInstallmentAmountChangeRequest
            {
                LoanInstallmentId = july2026Id,
                InstallmentAmount = 4600m,
                LastInstallmentDate = new DateOnly(2054, 4, 15),
                ExpectedScheduleVersion = "ignored"
            },
            CancellationToken.None));

        ex.Message.Should().Contain("Kwota raty jest zbyt wysoka dla wybranej daty ostatniej raty.");
        ex.Message.Should().Contain("Spróbuj podać wcześniejszą datę ostatniej raty, na przykład");
        ex.Message.Should().Contain("2053-08-15");
    }

    /// <summary>
    /// Verifies that the bank installment preview does not mutate persisted state and matches the confirmed result.
    /// </summary>
    [Fact]
    public async Task PreviewApplyLoanInstallmentAmountChangeAsync_Should_Be_SideEffect_Free_And_Match_Confirmed_Result()
    {
        var service = CreateService(new DateTime(2026, 7, 16, 12, 0, 0, DateTimeKind.Utc));
        var loan = await service.CreateLoanAsync(new CreateLoanRequest
        {
            Name = "Hipoteka preview rata bankowa",
            LoanType = LoanType.Mortgage,
            InterestMode = LoanInterestMode.VariableWibor,
            WiborPeriodType = WiborPeriodType.Wibor1M,
            MarginRate = 1.52m,
            InitialReferenceRate = 3.8m,
            Principal = 800_000m,
            InterestRate = 0m,
            StartDate = new DateOnly(2026, 6, 15),
            EndDate = new DateOnly(2054, 5, 15),
            RepaymentDayOfMonth = 15,
            IsActive = true
        }, CancellationToken.None);

        var ratePreview = await service.PreviewAddLoanRateEntryAsync(new AddLoanRateEntryRequest
        {
            LoanId = loan.Id,
            EffectiveFrom = new DateOnly(2026, 6, 16),
            ReferenceRate = 3.73m
        }, CancellationToken.None);

        var updatedRate = await service.AddLoanRateEntryAsync(
            new AddLoanRateEntryRequest
            {
                LoanId = loan.Id,
                EffectiveFrom = new DateOnly(2026, 6, 16),
                ReferenceRate = 3.73m,
                ExpectedScheduleVersion = ratePreview.SourceVersion
            },
            CancellationToken.None);

        var julyInstallment = updatedRate.Installments.Single(x => x.DueDate == new DateOnly(2026, 7, 15));

        await using var beforeContext = TestDbContextFactory.CreateDbContext(_dbName);
        var countsBefore = await GetLoanCollectionCountsAsync(beforeContext);

        var previewRequest = new ApplyLoanInstallmentAmountChangeRequest
        {
            LoanInstallmentId = julyInstallment.Id,
            InstallmentAmount = 4549.84m,
            LastInstallmentDate = new DateOnly(2045, 5, 15)
        };
        var preview = await service.PreviewApplyLoanInstallmentAmountChangeAsync(previewRequest, CancellationToken.None);

        await using var afterPreviewContext = TestDbContextFactory.CreateDbContext(_dbName);
        var countsAfterPreview = await GetLoanCollectionCountsAsync(afterPreviewContext);
        Assert.Equal(countsBefore, countsAfterPreview);

        var updated = await service.ApplyLoanInstallmentAmountChangeAsync(
            new ApplyLoanInstallmentAmountChangeRequest
            {
                LoanInstallmentId = julyInstallment.Id,
                InstallmentAmount = 4549.84m,
                LastInstallmentDate = new DateOnly(2045, 5, 15),
                ExpectedScheduleVersion = preview.SourceVersion
            },
            CancellationToken.None);

        AssertPreviewMatchesLoanDtos(preview, updatedRate, updated);
    }

    /// <summary>
    /// Verifies that ApplyLoanInstallmentAmountChangeAsync treats a missing previewed installment as stale when loan context is available.
    /// </summary>
    [Fact]
    public async Task ApplyLoanInstallmentAmountChangeAsync_Should_Throw_Conflict_When_Previewed_Installment_Was_Rebuilt()
    {
        var service = CreateService(new DateTime(2026, 6, 19, 12, 0, 0, DateTimeKind.Utc));
        var loan = await service.CreateLoanAsync(new CreateLoanRequest
        {
            Name = "Hipoteka stale rata",
            LoanType = LoanType.Mortgage,
            InterestMode = LoanInterestMode.VariableWibor,
            WiborPeriodType = WiborPeriodType.Wibor1M,
            MarginRate = 1.52m,
            InitialReferenceRate = 3.8m,
            Principal = 800_000m,
            InterestRate = 0m,
            StartDate = new DateOnly(2026, 6, 15),
            EndDate = new DateOnly(2054, 5, 15),
            RepaymentDayOfMonth = 15,
            IsActive = true
        }, CancellationToken.None);
        var julyInstallment = loan.Installments.Single(x => x.DueDate == new DateOnly(2026, 7, 15));
        var preview = await service.PreviewApplyLoanInstallmentAmountChangeAsync(new ApplyLoanInstallmentAmountChangeRequest
        {
            LoanInstallmentId = julyInstallment.Id,
            InstallmentAmount = julyInstallment.Amount + 100m,
            LastInstallmentDate = new DateOnly(2045, 5, 15)
        }, CancellationToken.None);

        await service.AddLoanRateEntryAsync(
            await AttachExpectedScheduleVersionAsync(service, new AddLoanRateEntryRequest
            {
                LoanId = loan.Id,
                EffectiveFrom = new DateOnly(2026, 6, 16),
                ReferenceRate = 3.73m
            }),
            CancellationToken.None);

        await Assert.ThrowsAsync<ConflictException>(async () =>
            await service.ApplyLoanInstallmentAmountChangeAsync(new ApplyLoanInstallmentAmountChangeRequest
            {
                LoanId = loan.Id,
                LoanInstallmentId = julyInstallment.Id,
                InstallmentAmount = julyInstallment.Amount + 100m,
                LastInstallmentDate = new DateOnly(2045, 5, 15),
                ExpectedScheduleVersion = preview.SourceVersion
            }, CancellationToken.None));
    }

    [Fact]
    public async Task ApplyLoanInstallmentAmountChangeAsync_Should_Throw_NotFound_When_Installment_Id_Is_Invalid()
    {
        var service = CreateService();

        await Assert.ThrowsAsync<NotFoundException>(async () =>
            await service.ApplyLoanInstallmentAmountChangeAsync(new ApplyLoanInstallmentAmountChangeRequest
            {
                LoanInstallmentId = 99999,
                InstallmentAmount = 1000m,
                LastInstallmentDate = new DateOnly(2026, 12, 15),
                ExpectedScheduleVersion = "ignored"
            }, CancellationToken.None));
    }

    /// <summary>
    /// Verifies that a percentage-based monthly insurance charge is recalculated from the lower
    /// outstanding balance after a prepayment.
    /// </summary>
    [Fact]
    public async Task ApplyLoanPrepaymentAsync_Should_Lower_Percentage_Based_Insurance_Charge()
    {
        var service = CreateService();
        var loan = await service.CreateLoanAsync(new CreateLoanRequest
        {
            Name = "Hipoteka z ubezpieczeniem od salda",
            LoanType = LoanType.Mortgage,
            InterestMode = LoanInterestMode.VariableWibor,
            WiborPeriodType = WiborPeriodType.Wibor1M,
            MarginRate = 1.52m,
            InitialReferenceRate = 3.8m,
            Principal = 800_000m,
            InterestRate = 0m,
            StartDate = new DateOnly(2026, 6, 15),
            EndDate = new DateOnly(2054, 5, 15),
            RepaymentDayOfMonth = 15,
            IsActive = true
        }, CancellationToken.None);

        await service.CreateLoanChargeAsync(new CreateLoanChargeRequest
        {
            LoanId = loan.Id,
            Name = "Ubezpieczenie od zadluzenia",
            ChargeType = LoanChargeType.Insurance,
            FrequencyType = LoanChargeFrequencyType.Monthly,
            Amount = 0.05m,
            IsPercentageBased = true,
            StartDate = new DateOnly(2026, 6, 1),
            IsActive = true
        }, CancellationToken.None);

        var withCharge = (await service.GetAllAsync(CancellationToken.None)).Single(x => x.Id == loan.Id);
        var january2027Before = withCharge.Installments.Single(x => x.DueDate == new DateOnly(2027, 1, 15));
        var chargeBefore = january2027Before.Amount - january2027Before.PrincipalAmount - january2027Before.InterestAmount;

        var request = new ApplyLoanPrepaymentRequest
        {
            LoanInstallmentId = january2027Before.Id,
            Amount = 20_000m,
            Strategy = LoanPrepaymentStrategyType.ReduceInstallment
        };
        var preview = await service.PreviewApplyLoanPrepaymentAsync(request, CancellationToken.None);
        var previewRow = preview.Rows.Single(x => x.DueDate == january2027Before.DueDate);

        request.ExpectedScheduleVersion = preview.SourceVersion;
        var updated = await service.ApplyLoanPrepaymentAsync(
            request,
            CancellationToken.None);

        var january2027After = updated.Installments.Single(x => x.DueDate == new DateOnly(2027, 1, 15));
        var chargeAfter = january2027After.Amount - january2027After.PrincipalAmount - january2027After.InterestAmount;

        Assert.Equal(396.39m, chargeBefore);
        Assert.Equal(386.39m, chargeAfter);
        Assert.Equal(chargeBefore, previewRow.Before!.ChargesAmount);
        Assert.Equal(chargeAfter, previewRow.After!.ChargesAmount);
        Assert.Equal(january2027Before.PrincipalAmount + january2027Before.InterestAmount + chargeBefore, previewRow.Before.Amount);
        Assert.Equal(january2027After.PrincipalAmount + january2027After.InterestAmount + chargeAfter, previewRow.After.Amount);
        Assert.True(chargeAfter < chargeBefore);
    }

    [Fact]
    public async Task ApplyLoanPrepaymentAsync_Should_Reject_Loan_Metadata_Changes_After_Preview()
    {
        var service = CreateService(new DateTime(2026, 7, 16, 12, 0, 0, DateTimeKind.Utc));
        var loan = await service.CreateLoanAsync(new CreateLoanRequest
        {
            Name = "Hipoteka metadata preview",
            LoanType = LoanType.Mortgage,
            InterestMode = LoanInterestMode.VariableWibor,
            WiborPeriodType = WiborPeriodType.Wibor1M,
            MarginRate = 1.52m,
            InitialReferenceRate = 3.8m,
            Principal = 800_000m,
            InterestRate = 0m,
            StartDate = new DateOnly(2026, 6, 15),
            EndDate = new DateOnly(2054, 5, 15),
            RepaymentDayOfMonth = 15,
            IsActive = true
        }, CancellationToken.None);
        var firstTargetInstallment = loan.Installments.Single(x => x.DueDate == new DateOnly(2026, 8, 15));
        loan = await service.ApplyLoanPrepaymentAsync(
            await AttachExpectedScheduleVersionAsync(service, new ApplyLoanPrepaymentRequest
            {
                LoanId = loan.Id,
                LoanInstallmentId = firstTargetInstallment.Id,
                Amount = 20_000m,
                Strategy = LoanPrepaymentStrategyType.ReduceInstallment
            }),
            CancellationToken.None);

        var targetInstallment = loan.Installments.Single(x => x.DueDate == new DateOnly(2026, 9, 15));
        var request = new ApplyLoanPrepaymentRequest
        {
            LoanId = loan.Id,
            LoanInstallmentId = targetInstallment.Id,
            Amount = 10_000m,
            Strategy = LoanPrepaymentStrategyType.ReduceInstallment
        };
        var preview = await service.PreviewApplyLoanPrepaymentAsync(request, CancellationToken.None);

        await using (var dbContext = TestDbContextFactory.CreateDbContext(_dbName))
        {
            var category = await dbContext.Categories.SingleAsync(x => x.Name == "Kredyt");
            var tag = new Tag
            {
                CategoryId = category.Id,
                Name = "Po preview"
            };
            dbContext.Tags.Add(tag);
            await dbContext.SaveChangesAsync();

            var loanEntity = await dbContext.Loans.SingleAsync(x => x.Id == loan.Id);
            loanEntity.Name = "Hipoteka metadata po preview";
            loanEntity.TagId = tag.Id;
            await dbContext.SaveChangesAsync();
        }

        request.ExpectedScheduleVersion = preview.SourceVersion;

        await Assert.ThrowsAsync<ConflictException>(() =>
            service.ApplyLoanPrepaymentAsync(request, CancellationToken.None));
    }

    [Fact]
    public async Task PreviewApplyLoanPrepaymentAsync_Should_Include_Fixed_And_Percentage_Charges_In_Summary()
    {
        var service = CreateService();
        var loan = await service.CreateLoanAsync(new CreateLoanRequest
        {
            Name = "Hipoteka preview koszty",
            LoanType = LoanType.Mortgage,
            InterestMode = LoanInterestMode.VariableWibor,
            WiborPeriodType = WiborPeriodType.Wibor1M,
            MarginRate = 1.52m,
            InitialReferenceRate = 3.8m,
            Principal = 800_000m,
            InterestRate = 0m,
            StartDate = new DateOnly(2026, 6, 15),
            EndDate = new DateOnly(2054, 5, 15),
            RepaymentDayOfMonth = 15,
            IsActive = true
        }, CancellationToken.None);

        await service.CreateLoanChargeAsync(new CreateLoanChargeRequest
        {
            LoanId = loan.Id,
            Name = "Prowizja miesięczna",
            ChargeType = LoanChargeType.Other,
            FrequencyType = LoanChargeFrequencyType.Monthly,
            Amount = 25m,
            IsActive = true,
            StartDate = new DateOnly(2026, 6, 1)
        }, CancellationToken.None);
        await service.CreateLoanChargeAsync(new CreateLoanChargeRequest
        {
            LoanId = loan.Id,
            Name = "Ubezpieczenie od salda",
            ChargeType = LoanChargeType.Insurance,
            FrequencyType = LoanChargeFrequencyType.Monthly,
            Amount = 0.05m,
            IsPercentageBased = true,
            StartDate = new DateOnly(2026, 6, 1),
            IsActive = true
        }, CancellationToken.None);

        var withCharges = (await service.GetAllAsync(CancellationToken.None)).Single(x => x.Id == loan.Id);
        var targetInstallment = withCharges.Installments.Single(x => x.DueDate == new DateOnly(2027, 1, 15));
        var preview = await service.PreviewApplyLoanPrepaymentAsync(
            new ApplyLoanPrepaymentRequest
            {
                LoanInstallmentId = targetInstallment.Id,
                Amount = 20_000m,
                Strategy = LoanPrepaymentStrategyType.ReduceInstallment
            },
            CancellationToken.None);
        var previewRow = preview.Rows.Single(x => x.DueDate == targetInstallment.DueDate);

        Assert.Equal(421.39m, previewRow.Before!.ChargesAmount);
        Assert.Equal(targetInstallment.PrincipalAmount + targetInstallment.InterestAmount + previewRow.Before.ChargesAmount, previewRow.Before.Amount);
        Assert.Equal(preview.Rows.First(x => !x.BeforeIsPaid).Before!.Amount, preview.BeforeSummary.NextInstallment);
        Assert.True(previewRow.After!.ChargesAmount < previewRow.Before.ChargesAmount);
    }

    /// <summary>
    /// Verifies that a prepayment does not rewrite a paid installment's stored charge history backward.
    /// </summary>
    [Fact]
    public async Task ApplyLoanPrepaymentAsync_Should_Not_Recalculate_Paid_Installment_Charges_Backwards()
    {
        var service = CreateService(new DateTime(2026, 7, 16, 12, 0, 0, DateTimeKind.Utc));
        var loan = await service.CreateLoanAsync(new CreateLoanRequest
        {
            Name = "Hipoteka z zamrożoną historią",
            LoanType = LoanType.Mortgage,
            InterestMode = LoanInterestMode.VariableWibor,
            WiborPeriodType = WiborPeriodType.Wibor1M,
            MarginRate = 1.52m,
            InitialReferenceRate = 3.8m,
            Principal = 800_000m,
            InterestRate = 0m,
            StartDate = new DateOnly(2026, 6, 15),
            EndDate = new DateOnly(2054, 5, 15),
            RepaymentDayOfMonth = 15,
            IsActive = true
        }, CancellationToken.None);

        await service.CreateLoanChargeAsync(new CreateLoanChargeRequest
        {
            LoanId = loan.Id,
            Name = "Ubezpieczenie od salda",
            ChargeType = LoanChargeType.Insurance,
            FrequencyType = LoanChargeFrequencyType.Monthly,
            Amount = 0.05m,
            IsPercentageBased = true,
            StartDate = new DateOnly(2026, 6, 1),
            IsActive = true
        }, CancellationToken.None);

        await using (var setupContext = TestDbContextFactory.CreateDbContext(_dbName))
        {
            setupContext.MonthPlans.Add(new MonthPlan { Year = 2026, Month = 6, IsClosed = false });
            await setupContext.SaveChangesAsync();
        }

        await service.SyncLoanInstallmentsForMonthAsync(2026, 6, CancellationToken.None);

        var loanBeforePayment = (await service.GetAllAsync(CancellationToken.None)).Single();
        var juneInstallmentBeforePayment = loanBeforePayment.Installments
            .Single(x => x.DueDate == new DateOnly(2026, 6, 15));

        await service.SetLoanInstallmentPaidAsync(new SetLoanInstallmentPaidRequest
        {
            LoanInstallmentId = juneInstallmentBeforePayment.Id,
            IsPaid = true
        }, CancellationToken.None);

        var juneBefore = (await service.GetAllAsync(CancellationToken.None)).Single().Installments
            .Single(x => x.DueDate == new DateOnly(2026, 6, 15));
        var juneChargeBefore = juneBefore.Amount - juneBefore.PrincipalAmount - juneBefore.InterestAmount;

        var january2027 = (await service.GetAllAsync(CancellationToken.None)).Single().Installments
            .Single(x => x.DueDate == new DateOnly(2027, 1, 15));

        await service.ApplyLoanPrepaymentAsync(
            await AttachExpectedScheduleVersionAsync(service, new ApplyLoanPrepaymentRequest
            {
                LoanInstallmentId = january2027.Id,
                Amount = 20_000m,
                Strategy = LoanPrepaymentStrategyType.ReduceInstallment
            }),
            CancellationToken.None);

        var juneAfter = (await service.GetAllAsync(CancellationToken.None)).Single().Installments
            .Single(x => x.DueDate == new DateOnly(2026, 6, 15));
        var juneChargeAfter = juneAfter.Amount - juneAfter.PrincipalAmount - juneAfter.InterestAmount;

        Assert.True(juneBefore.IsPaid);
        Assert.True(juneAfter.IsPaid);
        Assert.Equal(juneChargeBefore, juneChargeAfter);
    }

    /// <summary>
    /// Verifies that applying a prepayment creates an unplanned actual expense in the current month,
    /// even when the recalculated schedule starts from a later installment.
    /// </summary>
    [Fact]
    public async Task ApplyLoanPrepaymentAsync_Should_Create_Unplanned_Expense_In_Current_Month()
    {
        var service = CreateService(new DateTime(2026, 7, 16, 12, 0, 0, DateTimeKind.Utc));
        var loan = await service.CreateLoanAsync(new CreateLoanRequest
        {
            Name = "Hipoteka lipcowa",
            LoanType = LoanType.Mortgage,
            InterestMode = LoanInterestMode.VariableWibor,
            WiborPeriodType = WiborPeriodType.Wibor1M,
            MarginRate = 1.52m,
            InitialReferenceRate = 3.8m,
            Principal = 800_000m,
            InterestRate = 0m,
            StartDate = new DateOnly(2026, 6, 15),
            EndDate = new DateOnly(2054, 5, 15),
            RepaymentDayOfMonth = 15,
            IsActive = true
        }, CancellationToken.None);

        var augustInstallment = loan.Installments.Single(x => x.DueDate == new DateOnly(2026, 8, 15));

        await service.ApplyLoanPrepaymentAsync(
            await AttachExpectedScheduleVersionAsync(service, new ApplyLoanPrepaymentRequest
            {
                LoanInstallmentId = augustInstallment.Id,
                Amount = 20_000m,
                Strategy = LoanPrepaymentStrategyType.ReduceInstallment
            }),
            CancellationToken.None);

        await using var verifyContext = TestDbContextFactory.CreateDbContext(_dbName);
        var julyPlan = await verifyContext.MonthPlans.SingleAsync(x => x.Year == 2026 && x.Month == 7);
        var loanCategory = await verifyContext.Categories.SingleAsync(x => x.Name == "Kredyt");
        var expense = await verifyContext.Expenses.SingleAsync(x =>
            x.MonthPlanId == julyPlan.Id
            && x.Name == "Hipoteka lipcowa - nadpłata");

        Assert.Equal("Hipoteka lipcowa - nadpłata", expense.Name);
        Assert.Equal(loanCategory.Id, expense.CategoryId);
        Assert.Null(expense.TagId);
        Assert.Null(expense.LoanInstallmentId);
        Assert.Equal(0m, expense.PlannedAmount);
        Assert.Equal(20_000m, expense.ActualAmount);
        Assert.True(expense.ShowRemainingInUI);
    }

    /// <summary>
    /// Verifies that applying a prepayment fills the actual amount of an existing matching expense
    /// instead of creating a duplicate unplanned row.
    /// </summary>
    [Fact]
    public async Task ApplyLoanPrepaymentAsync_Should_Update_Existing_Prepayment_Expense_ActualAmount()
    {
        var service = CreateService(new DateTime(2026, 7, 16, 12, 0, 0, DateTimeKind.Utc));
        var loan = await service.CreateLoanAsync(new CreateLoanRequest
        {
            Name = "Hipoteka z planowana nadplata",
            LoanType = LoanType.Mortgage,
            InterestMode = LoanInterestMode.VariableWibor,
            WiborPeriodType = WiborPeriodType.Wibor1M,
            MarginRate = 1.52m,
            InitialReferenceRate = 3.8m,
            Principal = 800_000m,
            InterestRate = 0m,
            StartDate = new DateOnly(2026, 6, 15),
            EndDate = new DateOnly(2054, 5, 15),
            RepaymentDayOfMonth = 15,
            IsActive = true
        }, CancellationToken.None);

        await using (var setupContext = TestDbContextFactory.CreateDbContext(_dbName))
        {
            var loanCategory = await setupContext.Categories.SingleAsync(x => x.Name == "Kredyt");
            var julyPlan = new MonthPlan { Year = 2026, Month = 7, IsClosed = false };
            setupContext.MonthPlans.Add(julyPlan);
            setupContext.Expenses.Add(new Expense
            {
                MonthPlan = julyPlan,
                Order = 1,
                Name = "Hipoteka z planowana nadplata - nadpłata",
                CategoryId = loanCategory.Id,
                PlannedAmount = 25_000m,
                ActualAmount = 0m,
                ShowRemainingInUI = true
            });
            await setupContext.SaveChangesAsync();
        }

        var augustInstallment = loan.Installments.Single(x => x.DueDate == new DateOnly(2026, 8, 15));

        await service.ApplyLoanPrepaymentAsync(
            await AttachExpectedScheduleVersionAsync(service, new ApplyLoanPrepaymentRequest
            {
                LoanInstallmentId = augustInstallment.Id,
                Amount = 20_000m,
                Strategy = LoanPrepaymentStrategyType.ReduceInstallment
            }),
            CancellationToken.None);

        await using var verifyContext = TestDbContextFactory.CreateDbContext(_dbName);
        var julyPlanId = await verifyContext.MonthPlans
            .Where(x => x.Year == 2026 && x.Month == 7)
            .Select(x => x.Id)
            .SingleAsync();
        var expenses = await verifyContext.Expenses
            .Where(x => x.MonthPlanId == julyPlanId)
            .Where(x => x.Name == "Hipoteka z planowana nadplata - nadpłata")
            .ToListAsync();

        Assert.Single(expenses);
        Assert.Equal(25_000m, expenses[0].PlannedAmount);
        Assert.Equal(20_000m, expenses[0].ActualAmount);
    }

    /// <summary>
    /// Verifies that ApplyLoanPrepaymentAsync with ShortenPeriod strategy reduces the total number of
    /// installments and moves the end date earlier.
    /// </summary>
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

        var updated = await service.ApplyLoanPrepaymentAsync(
            await AttachExpectedScheduleVersionAsync(service, new ApplyLoanPrepaymentRequest
            {
                LoanInstallmentId = targetInstallment.Id,
                Amount = 10000m,
                Strategy = LoanPrepaymentStrategyType.ShortenPeriod
            }),
            CancellationToken.None);

        Assert.True(updated.Installments.Count < loan.Installments.Count);
        Assert.True(updated.EndDate < loan.EndDate);
    }

    /// <summary>
    /// Verifies that ApplyLoanPrepaymentAsync throws BadRequestException when the target installment
    /// is already paid.
    /// </summary>
    [Fact]
    public async Task ApplyLoanPrepaymentAsync_Should_Throw_BadRequest_When_Installment_Is_Paid()
    {
        var service = CreateService();
        var loan = await service.CreateLoanAsync(new CreateLoanRequest
        {
            Name = "Kredyt nadplata",
            LoanType = LoanType.Cash,
            InterestMode = LoanInterestMode.Fixed,
            Principal = 12000m,
            InterestRate = 0m,
            StartDate = new DateOnly(2026, 1, 10),
            EndDate = new DateOnly(2026, 12, 10),
            RepaymentDayOfMonth = 10,
            IsActive = true
        }, CancellationToken.None);

        var firstInstallment = loan.Installments.OrderBy(x => x.DueDate).First();
        await service.SetLoanInstallmentPaidAsync(new SetLoanInstallmentPaidRequest
        {
            LoanInstallmentId = firstInstallment.Id,
            IsPaid = true
        }, CancellationToken.None);

        await Assert.ThrowsAsync<BadRequestException>(async () =>
            await service.ApplyLoanPrepaymentAsync(
                new ApplyLoanPrepaymentRequest
                {
                    LoanInstallmentId = firstInstallment.Id,
                    Amount = 500m,
                    Strategy = LoanPrepaymentStrategyType.ReduceInstallment,
                    ExpectedScheduleVersion = "ignored"
                },
                CancellationToken.None));
    }

    /// <summary>
    /// Verifies that ApplyLoanPrepaymentAsync throws BadRequestException when the prepayment amount
    /// equals or exceeds the remaining principal.
    /// </summary>
    [Fact]
    public async Task ApplyLoanPrepaymentAsync_Should_Throw_BadRequest_When_Amount_Exceeds_Remaining_Principal()
    {
        var service = CreateService();
        var loan = await service.CreateLoanAsync(new CreateLoanRequest
        {
            Name = "Mały kredyt",
            LoanType = LoanType.Cash,
            InterestMode = LoanInterestMode.Fixed,
            Principal = 1200m,
            InterestRate = 0m,
            StartDate = new DateOnly(2026, 1, 10),
            EndDate = new DateOnly(2026, 12, 10),
            RepaymentDayOfMonth = 10,
            IsActive = true
        }, CancellationToken.None);

        var firstInstallment = loan.Installments.OrderBy(x => x.DueDate).First();

        await Assert.ThrowsAsync<BadRequestException>(async () =>
            await service.ApplyLoanPrepaymentAsync(
                new ApplyLoanPrepaymentRequest
                {
                    LoanInstallmentId = firstInstallment.Id,
                    Amount = 999999m,
                    Strategy = LoanPrepaymentStrategyType.ReduceInstallment,
                    ExpectedScheduleVersion = "ignored"
                },
                CancellationToken.None));
    }

    /// <summary>
    /// Verifies that ApplyLoanPrepaymentAsync treats a missing previewed installment as stale when loan context is available.
    /// </summary>
    [Fact]
    public async Task ApplyLoanPrepaymentAsync_Should_Throw_Conflict_When_Previewed_Installment_Was_Rebuilt()
    {
        var service = CreateService(new DateTime(2026, 6, 19, 12, 0, 0, DateTimeKind.Utc));
        var loan = await service.CreateLoanAsync(new CreateLoanRequest
        {
            Name = "Hipoteka stale nadplata",
            LoanType = LoanType.Mortgage,
            InterestMode = LoanInterestMode.VariableWibor,
            WiborPeriodType = WiborPeriodType.Wibor1M,
            MarginRate = 1.52m,
            InitialReferenceRate = 3.8m,
            Principal = 800_000m,
            InterestRate = 0m,
            StartDate = new DateOnly(2026, 6, 15),
            EndDate = new DateOnly(2054, 5, 15),
            RepaymentDayOfMonth = 15,
            IsActive = true
        }, CancellationToken.None);
        var julyInstallment = loan.Installments.Single(x => x.DueDate == new DateOnly(2026, 7, 15));
        var preview = await service.PreviewApplyLoanPrepaymentAsync(new ApplyLoanPrepaymentRequest
        {
            LoanInstallmentId = julyInstallment.Id,
            Amount = 100m,
            Strategy = LoanPrepaymentStrategyType.ReduceInstallment
        }, CancellationToken.None);

        await service.AddLoanRateEntryAsync(
            await AttachExpectedScheduleVersionAsync(service, new AddLoanRateEntryRequest
            {
                LoanId = loan.Id,
                EffectiveFrom = new DateOnly(2026, 6, 16),
                ReferenceRate = 3.73m
            }),
            CancellationToken.None);

        await Assert.ThrowsAsync<ConflictException>(async () =>
            await service.ApplyLoanPrepaymentAsync(
                new ApplyLoanPrepaymentRequest
                {
                    LoanId = loan.Id,
                    LoanInstallmentId = julyInstallment.Id,
                    Amount = 100m,
                    Strategy = LoanPrepaymentStrategyType.ReduceInstallment,
                    ExpectedScheduleVersion = preview.SourceVersion
                },
                CancellationToken.None));
    }

    [Fact]
    public async Task ApplyLoanPrepaymentAsync_Should_Throw_NotFound_When_Installment_Id_Is_Invalid()
    {
        var service = CreateService();

        await Assert.ThrowsAsync<NotFoundException>(async () =>
            await service.ApplyLoanPrepaymentAsync(
                new ApplyLoanPrepaymentRequest
                {
                    LoanInstallmentId = 99999,
                    Amount = 100m,
                    Strategy = LoanPrepaymentStrategyType.ReduceInstallment,
                    ExpectedScheduleVersion = "ignored"
                },
                CancellationToken.None));
    }

    /// <summary>
    /// Verifies that the prepayment preview does not mutate persisted state and matches the confirmed result.
    /// </summary>
    [Fact]
    public async Task PreviewApplyLoanPrepaymentAsync_Should_Be_SideEffect_Free_And_Match_Confirmed_Result()
    {
        var service = CreateService();
        var loan = await service.CreateLoanAsync(new CreateLoanRequest
        {
            Name = "Hipoteka preview nadplata",
            LoanType = LoanType.Mortgage,
            InterestMode = LoanInterestMode.VariableWibor,
            WiborPeriodType = WiborPeriodType.Wibor1M,
            MarginRate = 1.52m,
            InitialReferenceRate = 3.8m,
            Principal = 800_000m,
            InterestRate = 0m,
            StartDate = new DateOnly(2026, 6, 15),
            EndDate = new DateOnly(2054, 5, 15),
            RepaymentDayOfMonth = 15,
            IsActive = true
        }, CancellationToken.None);

        var targetInstallment = loan.Installments.Single(x => x.DueDate == new DateOnly(2026, 7, 15));

        await using var beforeContext = TestDbContextFactory.CreateDbContext(_dbName);
        var countsBefore = await GetLoanCollectionCountsAsync(beforeContext);

        var previewRequest = new ApplyLoanPrepaymentRequest
        {
            LoanInstallmentId = targetInstallment.Id,
            Amount = 100m,
            Strategy = LoanPrepaymentStrategyType.ReduceInstallment
        };
        var preview = await service.PreviewApplyLoanPrepaymentAsync(previewRequest, CancellationToken.None);

        await using var afterPreviewContext = TestDbContextFactory.CreateDbContext(_dbName);
        var countsAfterPreview = await GetLoanCollectionCountsAsync(afterPreviewContext);
        Assert.Equal(countsBefore, countsAfterPreview);

        var updated = await service.ApplyLoanPrepaymentAsync(
            new ApplyLoanPrepaymentRequest
            {
                LoanInstallmentId = targetInstallment.Id,
                Amount = 100m,
                Strategy = LoanPrepaymentStrategyType.ReduceInstallment,
                ExpectedScheduleVersion = preview.SourceVersion
            },
            CancellationToken.None);

        AssertPreviewMatchesLoanDtos(preview, loan, updated);
    }

    // ── CreateLoanChargeAsync ────────────────────────────────────────────────

    /// <summary>
    /// Verifies that SyncLoanInstallmentsForMonthAsync includes the active loan charge amount
    /// in the planned expense and is idempotent (calling it twice produces only one expense).
    /// </summary>
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
    public async Task SyncLoanInstallmentsForMonthAsync_Should_Not_Double_Count_Charges_After_Manual_Override()
    {
        var service = CreateService();
        var loan = await service.CreateLoanAsync(new CreateLoanRequest
        {
            Name = "Hipoteka override koszty",
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

        var withCharge = (await service.GetAllAsync(CancellationToken.None)).Single(x => x.Id == loan.Id);
        var marchInstallment = withCharge.Installments.Single(x => x.Year == 2026 && x.Month == 3);
        await service.OverrideLoanInstallmentAsync(new OverrideLoanInstallmentRequest
        {
            InstallmentId = marchInstallment.Id,
            PrincipalAmount = marchInstallment.PrincipalAmount,
            InterestAmount = marchInstallment.InterestAmount,
            ChargesAmount = 120m
        }, CancellationToken.None);

        await service.SyncLoanInstallmentsForMonthAsync(2026, 3, CancellationToken.None);

        await using var verifyContext = TestDbContextFactory.CreateDbContext(_dbName);
        var monthPlan = await verifyContext.MonthPlans.SingleAsync(x => x.Year == 2026 && x.Month == 3);
        var expense = await verifyContext.Expenses.SingleAsync(x => x.MonthPlanId == monthPlan.Id);

        Assert.Equal(
            decimal.Round(marchInstallment.PrincipalAmount + marchInstallment.InterestAmount + 120m, 2, MidpointRounding.AwayFromZero),
            expense.PlannedAmount);
    }

    /// <summary>
    /// Verifies that CreateLoanChargeAsync throws NotFoundException when the specified loan does not exist.
    /// </summary>
    [Fact]
    public async Task CreateLoanChargeAsync_Should_Throw_NotFoundException_When_Loan_Not_Found()
    {
        var service = CreateService();

        await Assert.ThrowsAsync<NotFoundException>(async () =>
            await service.CreateLoanChargeAsync(new CreateLoanChargeRequest
            {
                LoanId = 99999,
                Name = "Ubezpieczenie",
                ChargeType = LoanChargeType.Insurance,
                FrequencyType = LoanChargeFrequencyType.Monthly,
                Amount = 50m,
                StartDate = new DateOnly(2026, 1, 1),
                IsActive = true
            }, CancellationToken.None));
    }

    // ── UpdateLoanChargeAsync ────────────────────────────────────────────────

    /// <summary>
    /// Verifies that UpdateLoanChargeAsync persists updated charge fields correctly.
    /// </summary>
    [Fact]
    public async Task UpdateLoanChargeAsync_Should_Update_Charge_Fields()
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

        var charge = await service.CreateLoanChargeAsync(new CreateLoanChargeRequest
        {
            LoanId = loan.Id,
            Name = "Ubezpieczenie",
            ChargeType = LoanChargeType.Insurance,
            FrequencyType = LoanChargeFrequencyType.Monthly,
            Amount = 100m,
            StartDate = new DateOnly(2026, 1, 1),
            IsActive = true
        }, CancellationToken.None);

        var updated = await service.UpdateLoanChargeAsync(new UpdateLoanChargeRequest
        {
            Id = charge.Id,
            Name = "Ubezpieczenie zmienione",
            ChargeType = LoanChargeType.Insurance,
            FrequencyType = LoanChargeFrequencyType.Monthly,
            Amount = 200m,
            StartDate = charge.StartDate,
            IsActive = true
        }, CancellationToken.None);

        Assert.Equal("Ubezpieczenie zmienione", updated.Name);
        Assert.Equal(200m, updated.Amount);
    }

    /// <summary>
    /// Verifies that UpdateLoanChargeAsync throws NotFoundException when the charge does not exist.
    /// </summary>
    [Fact]
    public async Task UpdateLoanChargeAsync_Should_Throw_NotFoundException_When_Charge_Not_Found()
    {
        var service = CreateService();

        await Assert.ThrowsAsync<NotFoundException>(async () =>
            await service.UpdateLoanChargeAsync(new UpdateLoanChargeRequest
            {
                Id = 99999,
                Name = "Ghost",
                ChargeType = LoanChargeType.Insurance,
                FrequencyType = LoanChargeFrequencyType.Monthly,
                Amount = 50m,
                StartDate = new DateOnly(2026, 1, 1),
                IsActive = true
            }, CancellationToken.None));
    }

    // ── DeleteLoanChargeAsync ────────────────────────────────────────────────

    /// <summary>
    /// Verifies that DeleteLoanChargeAsync removes the charge so it no longer appears on the loan.
    /// </summary>
    [Fact]
    public async Task DeleteLoanChargeAsync_Should_Remove_Charge()
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

        var charge = await service.CreateLoanChargeAsync(new CreateLoanChargeRequest
        {
            LoanId = loan.Id,
            Name = "Ubezpieczenie",
            ChargeType = LoanChargeType.Insurance,
            FrequencyType = LoanChargeFrequencyType.Monthly,
            Amount = 100m,
            StartDate = new DateOnly(2026, 1, 1),
            IsActive = true
        }, CancellationToken.None);

        await service.DeleteLoanChargeAsync(new DeleteLoanChargeRequest { Id = charge.Id }, CancellationToken.None);

        var refreshed = (await service.GetAllAsync(CancellationToken.None)).Single(x => x.Id == loan.Id);
        Assert.Empty(refreshed.Charges);
    }

    /// <summary>
    /// Verifies that DeleteLoanChargeAsync throws NotFoundException when the charge does not exist.
    /// </summary>
    [Fact]
    public async Task DeleteLoanChargeAsync_Should_Throw_NotFoundException_When_Charge_Not_Found()
    {
        var service = CreateService();

        await Assert.ThrowsAsync<NotFoundException>(async () =>
            await service.DeleteLoanChargeAsync(new DeleteLoanChargeRequest { Id = 99999 }, CancellationToken.None));
    }

    // ── DeleteLoanAsync ──────────────────────────────────────────────────────

    /// <summary>
    /// Verifies that DeleteLoanAsync removes the loan and all its installments from the database.
    /// </summary>
    [Fact]
    public async Task DeleteLoanAsync_Should_Delete_Loan_And_Installments()
    {
        var service = CreateService();
        var loan = await service.CreateLoanAsync(new CreateLoanRequest
        {
            Name = "Do usunięcia",
            LoanType = LoanType.Cash,
            InterestMode = LoanInterestMode.Fixed,
            Principal = 6000m,
            InterestRate = 0m,
            StartDate = new DateOnly(2026, 1, 10),
            EndDate = new DateOnly(2026, 6, 10),
            RepaymentDayOfMonth = 10,
            IsActive = true
        }, CancellationToken.None);

        Assert.NotEmpty(loan.Installments);

        await service.DeleteLoanAsync(new DeleteLoanRequest { Id = loan.Id }, CancellationToken.None);

        var loans = await service.GetAllAsync(CancellationToken.None);
        Assert.Empty(loans);

        await using var verifyContext = TestDbContextFactory.CreateDbContext(_dbName);
        var installments = await verifyContext.LoanInstallments.ToListAsync();
        Assert.Empty(installments);
    }

    /// <summary>
    /// Verifies that DeleteLoanAsync throws BadRequestException when the loan has paid installments.
    /// </summary>
    [Fact]
    public async Task DeleteLoanAsync_Should_Throw_BadRequest_When_Has_Paid_Installments()
    {
        var service = CreateService();
        var loan = await service.CreateLoanAsync(new CreateLoanRequest
        {
            Name = "Kredyt z zapłatą",
            LoanType = LoanType.Cash,
            InterestMode = LoanInterestMode.Fixed,
            Principal = 6000m,
            InterestRate = 0m,
            StartDate = new DateOnly(2026, 1, 10),
            EndDate = new DateOnly(2026, 6, 10),
            RepaymentDayOfMonth = 10,
            IsActive = true
        }, CancellationToken.None);

        var firstInstallment = loan.Installments.OrderBy(x => x.DueDate).First();
        await service.SetLoanInstallmentPaidAsync(new SetLoanInstallmentPaidRequest
        {
            LoanInstallmentId = firstInstallment.Id,
            IsPaid = true
        }, CancellationToken.None);

        await Assert.ThrowsAsync<BadRequestException>(async () =>
            await service.DeleteLoanAsync(new DeleteLoanRequest { Id = loan.Id }, CancellationToken.None));
    }

    /// <summary>
    /// Verifies that DeleteLoanAsync throws NotFoundException when the loan does not exist.
    /// </summary>
    [Fact]
    public async Task DeleteLoanAsync_Should_Throw_NotFoundException_When_Loan_Not_Found()
    {
        var service = CreateService();

        await Assert.ThrowsAsync<NotFoundException>(async () =>
            await service.DeleteLoanAsync(new DeleteLoanRequest { Id = 99999 }, CancellationToken.None));
    }

    // ── SetLoanInstallmentPaidAsync ──────────────────────────────────────────

    /// <summary>
    /// Verifies that marking an installment as paid sets IsPaid and updates the linked expense ActualAmount
    /// to match PlannedAmount, and that marking it unpaid resets ActualAmount back to zero.
    /// </summary>
    [Fact]
    public async Task SetLoanInstallmentPaidAsync_Should_Reset_Expense_Amount_When_Marking_Unpaid()
    {
        var service = CreateService();
        var loan = await service.CreateLoanAsync(new CreateLoanRequest
        {
            Name = "Auto",
            LoanType = LoanType.Cash,
            InterestMode = LoanInterestMode.Fixed,
            Principal = 6000m,
            InterestRate = 0m,
            StartDate = new DateOnly(2026, 1, 10),
            EndDate = new DateOnly(2026, 6, 10),
            RepaymentDayOfMonth = 10,
            IsActive = true
        }, CancellationToken.None);

        await using (var context = TestDbContextFactory.CreateDbContext(_dbName))
        {
            context.MonthPlans.Add(new MonthPlan { Year = 2026, Month = 2, IsClosed = false });
            await context.SaveChangesAsync();
        }

        await service.SyncLoanInstallmentsForMonthAsync(2026, 2, CancellationToken.None);

        var febInstallment = loan.Installments.Single(x => x.Year == 2026 && x.Month == 2);

        await service.SetLoanInstallmentPaidAsync(new SetLoanInstallmentPaidRequest
        {
            LoanInstallmentId = febInstallment.Id,
            IsPaid = true
        }, CancellationToken.None);

        await service.SetLoanInstallmentPaidAsync(new SetLoanInstallmentPaidRequest
        {
            LoanInstallmentId = febInstallment.Id,
            IsPaid = false
        }, CancellationToken.None);

        await using var verifyContext = TestDbContextFactory.CreateDbContext(_dbName);
        var expense = await verifyContext.Expenses.SingleAsync(x => x.LoanInstallmentId == febInstallment.Id);
        var installment = await verifyContext.LoanInstallments.SingleAsync(x => x.Id == febInstallment.Id);

        Assert.False(installment.IsPaid);
        Assert.Equal(0m, expense.ActualAmount);
    }

    /// <summary>
    /// Verifies that SetLoanInstallmentPaidAsync throws NotFoundException when the installment does not exist.
    /// </summary>
    [Fact]
    public async Task SetLoanInstallmentPaidAsync_Should_Throw_NotFoundException_When_Installment_Not_Found()
    {
        var service = CreateService();

        await Assert.ThrowsAsync<NotFoundException>(async () =>
            await service.SetLoanInstallmentPaidAsync(new SetLoanInstallmentPaidRequest
            {
                LoanInstallmentId = 99999,
                IsPaid = true
            }, CancellationToken.None));
    }

    // ── SyncLoanInstallmentsForMonthAsync ────────────────────────────────────

    /// <summary>
    /// Verifies that SyncLoanInstallmentsForMonthAsync creates an expense linked to the installment
    /// and that SetLoanInstallmentPaidAsync updates both the installment and the expense ActualAmount.
    /// </summary>
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

    /// <summary>
    /// Verifies that SyncLoanInstallmentsForMonthAsync assigns the loan's tag to the generated expense.
    /// </summary>
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

    /// <summary>
    /// Verifies that SyncLoanInstallmentsForMonthAsync is a no-op when no MonthPlan exists for the given month.
    /// </summary>
    [Fact]
    public async Task SyncLoanInstallmentsForMonthAsync_Should_Skip_When_Month_Plan_Does_Not_Exist()
    {
        var service = CreateService();
        await service.CreateLoanAsync(new CreateLoanRequest
        {
            Name = "Kredyt bez planu",
            LoanType = LoanType.Cash,
            InterestMode = LoanInterestMode.Fixed,
            Principal = 6000m,
            InterestRate = 0m,
            StartDate = new DateOnly(2026, 1, 10),
            EndDate = new DateOnly(2026, 6, 10),
            RepaymentDayOfMonth = 10,
            IsActive = true
        }, CancellationToken.None);

        // No MonthPlan created for March — sync should be a no-op
        await service.SyncLoanInstallmentsForMonthAsync(2026, 3, CancellationToken.None);

        await using var verifyContext = TestDbContextFactory.CreateDbContext(_dbName);
        Assert.Empty(await verifyContext.Expenses.ToListAsync());
    }

    /// <summary>
    /// Verifies that SyncLoanInstallmentsForMonthAsync is a no-op when the MonthPlan for the given month
    /// is already closed.
    /// </summary>
    [Fact]
    public async Task SyncLoanInstallmentsForMonthAsync_Should_Skip_When_Month_Plan_Is_Closed()
    {
        var service = CreateService();
        await service.CreateLoanAsync(new CreateLoanRequest
        {
            Name = "Kredyt zamknięty",
            LoanType = LoanType.Cash,
            InterestMode = LoanInterestMode.Fixed,
            Principal = 6000m,
            InterestRate = 0m,
            StartDate = new DateOnly(2026, 1, 10),
            EndDate = new DateOnly(2026, 6, 10),
            RepaymentDayOfMonth = 10,
            IsActive = true
        }, CancellationToken.None);

        await using (var context = TestDbContextFactory.CreateDbContext(_dbName))
        {
            context.MonthPlans.Add(new MonthPlan { Year = 2026, Month = 3, IsClosed = true });
            await context.SaveChangesAsync();
        }

        await service.SyncLoanInstallmentsForMonthAsync(2026, 3, CancellationToken.None);

        await using var verifyContext = TestDbContextFactory.CreateDbContext(_dbName);
        Assert.Empty(await verifyContext.Expenses.ToListAsync());
    }

    // ── RemainingPrincipal ───────────────────────────────────────────────────

    /// <summary>
    /// Verifies that RemainingPrincipal decreases after marking the first installment as paid.
    /// </summary>
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

    /// <summary>
    /// Verifies that debt summary for a selected month does not get rewritten by a later prepayment.
    /// The selected month should reflect only prepayments that already happened by that month.
    /// </summary>
    [Fact]
    public async Task GetDebtSummaryAsync_Should_Not_Apply_Future_Prepayment_To_Past_Months()
    {
        var factory = TestDbContextFactory.CreateFactory(_dbName);
        var service = new LoanService(
            factory,
            new StaticDateTimeProvider(new DateTime(2026, 7, 16, 12, 0, 0, DateTimeKind.Utc)));
        var loan = await service.CreateLoanAsync(new CreateLoanRequest
        {
            Name = "Budowa domu",
            LoanType = LoanType.Mortgage,
            InterestMode = LoanInterestMode.VariableWibor,
            WiborPeriodType = WiborPeriodType.Wibor1M,
            MarginRate = 1.52m,
            InitialReferenceRate = 3.8m,
            Principal = 800_000m,
            InterestRate = 0m,
            StartDate = new DateOnly(2026, 6, 15),
            EndDate = new DateOnly(2054, 5, 15),
            RepaymentDayOfMonth = 15,
            IsActive = true
        }, CancellationToken.None);

        await using (var dbContext = await factory.CreateDbContextAsync())
        {
            var initialRate = await dbContext.LoanRateEntries.SingleAsync(x => x.LoanId == loan.Id);
            initialRate.EffectiveFrom = new DateOnly(2026, 5, 31);
            await dbContext.SaveChangesAsync();
        }

        loan = await service.AddLoanRateEntryAsync(
            await AttachExpectedScheduleVersionAsync(service, new AddLoanRateEntryRequest
            {
                LoanId = loan.Id,
                EffectiveFrom = new DateOnly(2026, 6, 16),
                ReferenceRate = 3.73m
            }),
            CancellationToken.None);

        var mayBefore = await service.GetDebtSummaryAsync(2026, 5, CancellationToken.None);
        var juneBefore = await service.GetDebtSummaryAsync(2026, 6, CancellationToken.None);
        Assert.Equal(0m, mayBefore.ActiveDebt);
        Assert.Equal(0, mayBefore.ActiveLoanCount);
        Assert.True(juneBefore.ActiveDebt > 0);

        var julyInstallment = loan.Installments.Single(x => x.DueDate == new DateOnly(2026, 7, 15));
        await service.ApplyLoanPrepaymentAsync(
            await AttachExpectedScheduleVersionAsync(service, new ApplyLoanPrepaymentRequest
            {
                LoanInstallmentId = julyInstallment.Id,
                Amount = 100m,
                Strategy = LoanPrepaymentStrategyType.ReduceInstallment
            }),
            CancellationToken.None);

        var mayAfter = await service.GetDebtSummaryAsync(2026, 5, CancellationToken.None);
        var juneAfter = await service.GetDebtSummaryAsync(2026, 6, CancellationToken.None);

        Assert.Equal(mayBefore.ActiveDebt, mayAfter.ActiveDebt);
        Assert.Equal(mayBefore.ActiveLoanCount, mayAfter.ActiveLoanCount);
        Assert.Equal(juneBefore.ActiveDebt, juneAfter.ActiveDebt);
    }

    [Fact]
    public async Task GetDebtSummaryAsync_Should_Use_Skipped_Legacy_Prepayment_Expenses_As_Fallback()
    {
        var factory = TestDbContextFactory.CreateFactory(_dbName);
        var service = new LoanService(
            factory,
            new StaticDateTimeProvider(new DateTime(2026, 7, 16, 12, 0, 0, DateTimeKind.Utc)));
        var loan = await service.CreateLoanAsync(new CreateLoanRequest
        {
            Name = "Hipoteka legacy fallback",
            LoanType = LoanType.Mortgage,
            InterestMode = LoanInterestMode.VariableWibor,
            WiborPeriodType = WiborPeriodType.Wibor1M,
            MarginRate = 1.52m,
            InitialReferenceRate = 3.8m,
            Principal = 800_000m,
            InterestRate = 0m,
            StartDate = new DateOnly(2026, 6, 15),
            EndDate = new DateOnly(2054, 5, 15),
            RepaymentDayOfMonth = 15,
            IsActive = true
        }, CancellationToken.None);

        int loanTagId;
        await using (var dbContext = await factory.CreateDbContextAsync())
        {
            var category = await dbContext.Categories.SingleAsync(x => x.Name == "Kredyt");
            var tag = new Tag
            {
                CategoryId = category.Id,
                Name = "Legacy nadplata"
            };
            dbContext.Tags.Add(tag);
            await dbContext.SaveChangesAsync();
            loanTagId = tag.Id;

            var loanEntity = await dbContext.Loans.SingleAsync(x => x.Id == loan.Id);
            loanEntity.TagId = loanTagId;
            await dbContext.SaveChangesAsync();
        }

        var juneBefore = await service.GetDebtSummaryAsync(2026, 6, CancellationToken.None);
        var julyInstallment = loan.Installments.Single(x => x.DueDate == new DateOnly(2026, 7, 15));

        await service.ApplyLoanPrepaymentAsync(
            await AttachExpectedScheduleVersionAsync(service, new ApplyLoanPrepaymentRequest
            {
                LoanId = loan.Id,
                LoanInstallmentId = julyInstallment.Id,
                Amount = 1_000m,
                Strategy = LoanPrepaymentStrategyType.ReduceInstallment
            }),
            CancellationToken.None);

        await using (var dbContext = await factory.CreateDbContextAsync())
        {
            dbContext.LoanPrepayments.RemoveRange(await dbContext.LoanPrepayments.ToListAsync());

            var julyPlan = await dbContext.MonthPlans.SingleAsync(x => x.Year == 2026 && x.Month == 7);
            var legacyExpense = await dbContext.Expenses.SingleAsync(x =>
                x.MonthPlanId == julyPlan.Id
                && x.LoanInstallmentId == null
                && x.ActualAmount == 1_000m);
            legacyExpense.Name = "Hipoteka legacy fallback - nadpłata";
            legacyExpense.TagId = loanTagId;

            await dbContext.SaveChangesAsync();
        }

        var juneAfter = await service.GetDebtSummaryAsync(2026, 6, CancellationToken.None);

        Assert.Equal(juneBefore.ActiveDebt, juneAfter.ActiveDebt);
        Assert.Equal(juneBefore.ActiveLoanCount, juneAfter.ActiveLoanCount);
    }

    [Fact]
    public async Task GetDebtSummaryAsync_Should_Not_Classify_Tagged_Unrelated_Expenses_As_Legacy_Prepayments()
    {
        var factory = TestDbContextFactory.CreateFactory(_dbName);
        var service = new LoanService(
            factory,
            new StaticDateTimeProvider(new DateTime(2026, 7, 16, 12, 0, 0, DateTimeKind.Utc)));
        var loan = await service.CreateLoanAsync(new CreateLoanRequest
        {
            Name = "Hipoteka bez fallbacku",
            LoanType = LoanType.Mortgage,
            InterestMode = LoanInterestMode.VariableWibor,
            WiborPeriodType = WiborPeriodType.Wibor1M,
            MarginRate = 1.52m,
            InitialReferenceRate = 3.8m,
            Principal = 800_000m,
            InterestRate = 0m,
            StartDate = new DateOnly(2026, 6, 15),
            EndDate = new DateOnly(2054, 5, 15),
            RepaymentDayOfMonth = 15,
            IsActive = true
        }, CancellationToken.None);

        int loanTagId;
        await using (var dbContext = await factory.CreateDbContextAsync())
        {
            var category = await dbContext.Categories.SingleAsync(x => x.Name == "Kredyt");
            var tag = new Tag
            {
                CategoryId = category.Id,
                Name = "Tagged unrelated"
            };
            dbContext.Tags.Add(tag);
            await dbContext.SaveChangesAsync();
            loanTagId = tag.Id;

            var loanEntity = await dbContext.Loans.SingleAsync(x => x.Id == loan.Id);
            loanEntity.TagId = loanTagId;
            dbContext.MonthPlans.Add(new MonthPlan
            {
                Year = 2026,
                Month = 7,
                Expenses =
                {
                    new Expense
                    {
                        Order = 1,
                        Name = "Ręczna nadpłata oszczędności",
                        CategoryId = category.Id,
                        TagId = loanTagId,
                        ActualAmount = 1_000m,
                        PlannedAmount = 0m
                    }
                }
            });
            await dbContext.SaveChangesAsync();
        }

        var juneSummary = await service.GetDebtSummaryAsync(2026, 6, CancellationToken.None);

        Assert.Equal(
            loan.Installments.Where(x => x.DueDate > new DateOnly(2026, 6, 30)).Sum(x => x.PrincipalAmount),
            juneSummary.ActiveDebt);
        Assert.Equal(1, juneSummary.ActiveLoanCount);
    }

    [Fact]
    public async Task GetDebtSummaryAsync_Should_Throw_BadRequest_For_Invalid_Month()
    {
        var service = CreateService();

        await Assert.ThrowsAsync<BadRequestException>(() =>
            service.GetDebtSummaryAsync(2026, 13, CancellationToken.None));
    }

    /// <summary>
    /// Verifies that debt summary includes fixed loans that do not have rate entries.
    /// </summary>
    [Fact]
    public async Task GetDebtSummaryAsync_Should_Include_Fixed_Loans_Without_RateEntries()
    {
        var service = CreateService(new DateTime(2026, 6, 19, 12, 0, 0, DateTimeKind.Utc));
        await service.CreateLoanAsync(new CreateLoanRequest
        {
            Name = "Kredyt gotowkowy",
            LoanType = LoanType.Cash,
            InterestMode = LoanInterestMode.Fixed,
            Principal = 12_000m,
            InterestRate = 8m,
            StartDate = new DateOnly(2026, 6, 10),
            EndDate = new DateOnly(2027, 5, 10),
            RepaymentDayOfMonth = 10,
            IsActive = true
        }, CancellationToken.None);

        var summary = await service.GetDebtSummaryAsync(2026, 6, CancellationToken.None);

        Assert.Equal(1, summary.ActiveLoanCount);
        Assert.True(summary.ActiveDebt > 0);
    }

    /// <summary>
    /// Verifies that future prepayment adjustments are tied to the loan identity, not to the display name.
    /// </summary>
    [Fact]
    public async Task GetDebtSummaryAsync_Should_Not_Share_Future_Prepayments_Between_SameNamed_Loans()
    {
        var service = CreateService(new DateTime(2026, 7, 16, 12, 0, 0, DateTimeKind.Utc));
        var firstLoan = await service.CreateLoanAsync(new CreateLoanRequest
        {
            Name = "Wspolna nazwa",
            LoanType = LoanType.Mortgage,
            InterestMode = LoanInterestMode.VariableWibor,
            WiborPeriodType = WiborPeriodType.Wibor1M,
            MarginRate = 1.52m,
            InitialReferenceRate = 3.8m,
            Principal = 120_000m,
            InterestRate = 0m,
            StartDate = new DateOnly(2026, 6, 15),
            EndDate = new DateOnly(2027, 5, 15),
            RepaymentDayOfMonth = 15,
            IsActive = true
        }, CancellationToken.None);
        await service.CreateLoanAsync(new CreateLoanRequest
        {
            Name = "Wspolna nazwa",
            LoanType = LoanType.Mortgage,
            InterestMode = LoanInterestMode.VariableWibor,
            WiborPeriodType = WiborPeriodType.Wibor1M,
            MarginRate = 1.52m,
            InitialReferenceRate = 3.8m,
            Principal = 80_000m,
            InterestRate = 0m,
            StartDate = new DateOnly(2026, 6, 15),
            EndDate = new DateOnly(2027, 5, 15),
            RepaymentDayOfMonth = 15,
            IsActive = true
        }, CancellationToken.None);

        var juneBefore = await service.GetDebtSummaryAsync(2026, 6, CancellationToken.None);
        var augustInstallment = firstLoan.Installments.Single(x => x.DueDate == new DateOnly(2026, 8, 15));

        await service.ApplyLoanPrepaymentAsync(
            await AttachExpectedScheduleVersionAsync(service, new ApplyLoanPrepaymentRequest
            {
                LoanInstallmentId = augustInstallment.Id,
                Amount = 1_000m,
                Strategy = LoanPrepaymentStrategyType.ReduceInstallment
            }),
            CancellationToken.None);

        var juneAfter = await service.GetDebtSummaryAsync(2026, 6, CancellationToken.None);

        Assert.Equal(juneBefore.ActiveDebt, juneAfter.ActiveDebt);
        Assert.Equal(2, juneAfter.ActiveLoanCount);
    }

    /// <summary>
    /// Verifies that multiple prepayments in the same month are preserved as additive history.
    /// </summary>
    [Fact]
    public async Task GetDebtSummaryAsync_Should_Add_All_Future_Prepayments_From_Same_Month()
    {
        var service = CreateService(new DateTime(2026, 7, 16, 12, 0, 0, DateTimeKind.Utc));
        var loan = await service.CreateLoanAsync(new CreateLoanRequest
        {
            Name = "Nadplaty miesieczne",
            LoanType = LoanType.Mortgage,
            InterestMode = LoanInterestMode.VariableWibor,
            WiborPeriodType = WiborPeriodType.Wibor1M,
            MarginRate = 1.52m,
            InitialReferenceRate = 3.8m,
            Principal = 120_000m,
            InterestRate = 0m,
            StartDate = new DateOnly(2026, 6, 15),
            EndDate = new DateOnly(2027, 5, 15),
            RepaymentDayOfMonth = 15,
            IsActive = true
        }, CancellationToken.None);

        var juneBefore = await service.GetDebtSummaryAsync(2026, 6, CancellationToken.None);
        var augustInstallment = loan.Installments.Single(x => x.DueDate == new DateOnly(2026, 8, 15));

        loan = await service.ApplyLoanPrepaymentAsync(
            await AttachExpectedScheduleVersionAsync(service, new ApplyLoanPrepaymentRequest
            {
                LoanInstallmentId = augustInstallment.Id,
                Amount = 1_000m,
                Strategy = LoanPrepaymentStrategyType.ReduceInstallment
            }),
            CancellationToken.None);

        var septemberInstallment = loan.Installments.Single(x => x.DueDate == new DateOnly(2026, 9, 15));
        await service.ApplyLoanPrepaymentAsync(
            await AttachExpectedScheduleVersionAsync(service, new ApplyLoanPrepaymentRequest
            {
                LoanInstallmentId = septemberInstallment.Id,
                Amount = 1_500m,
                Strategy = LoanPrepaymentStrategyType.ReduceInstallment
            }),
            CancellationToken.None);

        var juneAfter = await service.GetDebtSummaryAsync(2026, 6, CancellationToken.None);

        await using var verifyContext = TestDbContextFactory.CreateDbContext(_dbName);
        var prepayments = await verifyContext.LoanPrepayments
            .Where(x => x.LoanId == loan.Id)
            .OrderBy(x => x.Id)
            .ToListAsync();
        var julyPlanId = await verifyContext.MonthPlans
            .Where(x => x.Year == 2026 && x.Month == 7)
            .Select(x => x.Id)
            .SingleAsync();
        var prepaymentExpense = await verifyContext.Expenses.SingleAsync(x =>
            x.MonthPlanId == julyPlanId
            && x.Name == "Nadplaty miesieczne - nadpłata");

        Assert.Equal(juneBefore.ActiveDebt, juneAfter.ActiveDebt);
        Assert.Equal(new[] { 1_000m, 1_500m }, prepayments.Select(x => x.Amount).ToArray());
        Assert.Equal(2_500m, prepaymentExpense.ActualAmount);
    }

    private static void AssertInstallmentAmounts(
        LoanDto loan,
        DateOnly dueDate,
        decimal expectedPrincipal,
        decimal expectedInterest)
    {
        var installment = loan.Installments.Single(x => x.DueDate == dueDate);
        const decimal tolerance = 0.01m;
        Assert.InRange(installment.PrincipalAmount, expectedPrincipal - tolerance, expectedPrincipal + tolerance);
        Assert.InRange(installment.InterestAmount, expectedInterest - tolerance, expectedInterest + tolerance);
    }

    private static async Task<AddLoanRateEntryRequest> AttachExpectedScheduleVersionAsync(
        LoanService service,
        AddLoanRateEntryRequest request)
    {
        request.ExpectedScheduleVersion = (await service.PreviewAddLoanRateEntryAsync(request, CancellationToken.None)).SourceVersion;
        return request;
    }

    private static async Task<ApplyLoanPrepaymentRequest> AttachExpectedScheduleVersionAsync(
        LoanService service,
        ApplyLoanPrepaymentRequest request)
    {
        request.ExpectedScheduleVersion = (await service.PreviewApplyLoanPrepaymentAsync(request, CancellationToken.None)).SourceVersion;
        return request;
    }

    private static async Task<ApplyLoanInstallmentAmountChangeRequest> AttachExpectedScheduleVersionAsync(
        LoanService service,
        ApplyLoanInstallmentAmountChangeRequest request)
    {
        request.ExpectedScheduleVersion = (await service.PreviewApplyLoanInstallmentAmountChangeAsync(request, CancellationToken.None)).SourceVersion;
        return request;
    }

    private static async Task<(int Loans, int LoanRateEntries, int LoanInstallments, int Expenses, int MonthPlans)>
        GetLoanCollectionCountsAsync(ApplicationDbContext context)
    {
        return (
            await context.Loans.CountAsync(),
            await context.LoanRateEntries.CountAsync(),
            await context.LoanInstallments.CountAsync(),
            await context.Expenses.CountAsync(),
            await context.MonthPlans.CountAsync());
    }

    private static void AssertPreviewMatchesLoanDtos(
        LoanScheduleChangePreviewDto preview,
        LoanDto before,
        LoanDto after)
    {
        AssertSummaryEqual(BuildExpectedSummary(before), preview.BeforeSummary);
        AssertSummaryEqual(BuildExpectedSummary(after), preview.AfterSummary);

        var expectedRows = BuildExpectedComparisonRows(before, after);
        Assert.Equal(expectedRows.Count, preview.Rows.Count);

        for (var i = 0; i < expectedRows.Count; i++)
        {
            var expected = expectedRows[i];
            var actual = preview.Rows[i];
            Assert.Equal(expected.DueDate, actual.DueDate);
            Assert.Equal(expected.State, actual.State);
            Assert.Equal(expected.BeforeIsPaid, actual.BeforeIsPaid);
            Assert.Equal(expected.AfterIsPaid, actual.AfterIsPaid);
            Assert.Equal(expected.Before, actual.Before);
            Assert.Equal(expected.After, actual.After);
        }
    }

    private static void AssertSummaryEqual(LoanScheduleSummaryDto expected, LoanScheduleSummaryDto actual)
    {
        Assert.Equal(expected.RemainingPrincipal, actual.RemainingPrincipal);
        Assert.Equal(expected.NextInstallment, actual.NextInstallment);
        Assert.Equal(expected.TotalFutureInterest, actual.TotalFutureInterest);
        Assert.Equal(expected.EndDate, actual.EndDate);
        Assert.Equal(expected.InstallmentCount, actual.InstallmentCount);
    }

    private static LoanScheduleSummaryDto BuildExpectedSummary(LoanDto loan)
    {
        var installments = loan.Installments
            .OrderBy(x => x.DueDate)
            .ThenBy(x => x.Month)
            .ThenBy(x => x.Year)
            .ToList();
        var nextInstallment = installments.FirstOrDefault(x => !x.IsPaid);

        return new LoanScheduleSummaryDto
        {
            RemainingPrincipal = loan.RemainingPrincipal,
            NextInstallment = nextInstallment is null
                ? 0m
                : ToScheduleRow(nextInstallment, installments, loan.Charges).Amount,
            TotalFutureInterest = decimal.Round(
                installments.Where(x => !x.IsPaid).Sum(x => x.InterestAmount),
                2,
                MidpointRounding.AwayFromZero),
            EndDate = loan.EndDate,
            InstallmentCount = loan.Installments.Count
        };
    }

    private static IReadOnlyList<ExpectedComparisonRow> BuildExpectedComparisonRows(LoanDto before, LoanDto after)
    {
        var beforeRows = before.Installments
            .OrderBy(x => x.DueDate)
            .ThenBy(x => x.Id)
            .ToDictionary(x => x.DueDate, x => ToScheduleRow(x, before.Installments, before.Charges));
        var beforePaidByDueDate = before.Installments
            .GroupBy(x => x.DueDate)
            .ToDictionary(x => x.Key, x => x.Any(y => y.IsPaid));

        var afterRows = after.Installments
            .OrderBy(x => x.DueDate)
            .ThenBy(x => x.Id)
            .ToDictionary(x => x.DueDate, x => ToScheduleRow(x, after.Installments, after.Charges));
        var afterPaidByDueDate = after.Installments
            .GroupBy(x => x.DueDate)
            .ToDictionary(x => x.Key, x => x.Any(y => y.IsPaid));

        var rows = new List<ExpectedComparisonRow>(beforeRows.Count + afterRows.Count);
        foreach (var dueDate in beforeRows.Keys.Union(afterRows.Keys).OrderBy(x => x))
        {
            beforeRows.TryGetValue(dueDate, out var beforeRow);
            afterRows.TryGetValue(dueDate, out var afterRow);

            var state = (beforeRow, afterRow) switch
            {
                (null, not null) => LoanScheduleComparisonRowState.Added,
                (not null, null) => LoanScheduleComparisonRowState.Removed,
                (not null, not null) when beforeRow == afterRow => LoanScheduleComparisonRowState.Unchanged,
                _ => LoanScheduleComparisonRowState.Changed
            };

            rows.Add(new ExpectedComparisonRow(
                dueDate,
                state,
                beforePaidByDueDate.GetValueOrDefault(dueDate),
                afterPaidByDueDate.GetValueOrDefault(dueDate),
                beforeRow,
                afterRow));
        }

        return rows;
    }

    private static ScheduleRowDto ToScheduleRow(
        LoanInstallmentDto installment,
        IReadOnlyList<LoanInstallmentDto> installments,
        IReadOnlyList<LoanChargeDto> charges)
    {
        var chargeAmount = CalculateChargeAmount(installment, installments, charges);

        return new ScheduleRowDto(
            installment.Year,
            installment.Month,
            installment.DueDate,
            decimal.Round(installment.PrincipalAmount + installment.InterestAmount + chargeAmount, 2, MidpointRounding.AwayFromZero),
            installment.PrincipalAmount,
            installment.InterestAmount,
            chargeAmount);
    }

    private static decimal CalculateChargeAmount(
        LoanInstallmentDto installment,
        IReadOnlyList<LoanInstallmentDto> installments,
        IReadOnlyList<LoanChargeDto> charges)
    {
        var outstandingBalance = installments
            .Where(x => x.DueDate >= installment.DueDate)
            .Sum(x => x.PrincipalAmount);

        return decimal.Round(
            charges
                .Where(x => x.IsActive)
                .Where(x => IsChargeDueInMonth(x, installment.Year, installment.Month))
                .Sum(x => x.IsPercentageBased
                    ? decimal.Round(outstandingBalance * x.Amount / 100m, 2, MidpointRounding.AwayFromZero)
                    : x.Amount),
            2,
            MidpointRounding.AwayFromZero);
    }

    private static bool IsChargeDueInMonth(LoanChargeDto charge, int year, int month)
    {
        var monthStart = new DateOnly(year, month, 1);
        var monthEnd = new DateOnly(year, month, DateTime.DaysInMonth(year, month));
        if (charge.StartDate > monthEnd || (charge.EndDate.HasValue && charge.EndDate.Value < monthStart))
        {
            return false;
        }

        return charge.FrequencyType switch
        {
            LoanChargeFrequencyType.OneTime => charge.StartDate.Year == year && charge.StartDate.Month == month,
            LoanChargeFrequencyType.Monthly => true,
            LoanChargeFrequencyType.Yearly => charge.StartDate.Month == month && year >= charge.StartDate.Year,
            _ => false
        };
    }

    private sealed record ExpectedComparisonRow(
        DateOnly DueDate,
        LoanScheduleComparisonRowState State,
        bool BeforeIsPaid,
        bool AfterIsPaid,
        ScheduleRowDto? Before,
        ScheduleRowDto? After);
}
