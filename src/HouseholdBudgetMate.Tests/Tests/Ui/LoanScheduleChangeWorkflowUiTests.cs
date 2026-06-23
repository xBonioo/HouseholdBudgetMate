using Bunit;
using FluentAssertions;
using HouseholdBudgetMate.Abstractions.Contracts.Categories.Dto;
using HouseholdBudgetMate.Abstractions.Contracts.Categories.Requests;
using HouseholdBudgetMate.Abstractions.Contracts.Loans.Dto;
using HouseholdBudgetMate.Abstractions.Contracts.Loans.Requests;
using HouseholdBudgetMate.Abstractions.Enums;
using HouseholdBudgetMate.Abstractions.Interfaces;
using HouseholdBudgetMate.Application.Kernel.Exceptions;
using HouseholdBudgetMate.Tests.Shared;
using HouseholdBudgetMate.Web.Components.Pages;
using HouseholdBudgetMate.Web.Components.Pages.LoansPage;
using HouseholdBudgetMate.Web.Services;
using Microsoft.Extensions.DependencyInjection;
using MudBlazor;
using MudBlazor.Services;
using MudBlazor.Extensions;

namespace HouseholdBudgetMate.Tests.Tests.Ui;

public sealed class LoanScheduleChangeWorkflowUiTests : BunitContext, IAsyncLifetime
{
    private readonly RecordingLoanService _loanService = new();

    public LoanScheduleChangeWorkflowUiTests()
    {
        Services.AddMudServices();
        Services.AddSingleton<ILoanService>(_loanService);
        Services.AddSingleton<ICategoryService>(new EmptyCategoryService());
        Services.AddScoped<UnsavedChangesTracker>();
        JSInterop.Mode = JSRuntimeMode.Loose;
    }

    Task IAsyncLifetime.InitializeAsync() => Task.CompletedTask;

    Task IAsyncLifetime.DisposeAsync() => ((IAsyncDisposable)this).DisposeAsync().AsTask();

    [Fact]
    public async Task All_Schedule_Changes_Should_Preview_Before_Write_And_Confirm_The_Reviewed_Version()
    {
        var cut = RenderLoansAndSelectLoan();
        await cut.InvokeAsync(() => cut.FindComponent<LoanWorkspaceTabs>().Instance.ActiveTabChanged.InvokeAsync(LoanWorkspaceTab.Wibor));

        var wibor = cut.FindComponent<LoanWiborPanel>();
        await cut.InvokeAsync(() => wibor.Instance.NewReferenceRateInputChanged.InvokeAsync("3,73"));
        await cut.InvokeAsync(() => wibor.Instance.RateEffectiveFromChanged.InvokeAsync(new DateTime(2026, 7, 1)));
        await cut.InvokeAsync(() => wibor.Instance.OnSubmit.InvokeAsync());
        _loanService.Events.Should().Equal("preview-wibor");

        var previewDialog = cut.FindComponent<LoanScheduleChangePreviewDialog>();
        await cut.InvokeAsync(() => previewDialog.Instance.OnBackToEdit.InvokeAsync());
        cut.FindComponent<LoanWiborPanel>().Instance.NewReferenceRateInput.Should().Be("3,73");

        await cut.InvokeAsync(() => cut.FindComponent<LoanWiborPanel>().Instance.OnSubmit.InvokeAsync());
        await cut.InvokeAsync(() => cut.FindComponent<LoanScheduleChangePreviewDialog>().Instance.OnConfirm.InvokeAsync());
        _loanService.Events.Should().Equal("preview-wibor", "preview-wibor", "write-wibor");
        _loanService.LastExpectedVersion.Should().Be(RecordingLoanService.PreviewVersion);

        await cut.InvokeAsync(() => cut.FindComponent<LoanWorkspaceTabs>().Instance.ActiveTabChanged.InvokeAsync(LoanWorkspaceTab.Schedule));
        var installment = _loanService.Loan.Installments.Single();
        SetPrivateField(cut.Instance, "_prepaymentInstallment", installment);
        SetPrivateField(cut.Instance, "_prepaymentAmountInput", "100");
        await cut.InvokeAsync(() => InvokePrivateAsync(cut.Instance, "ApplyPrepaymentAsync"));
        _loanService.Events.Last().Should().Be("preview-prepayment");

        await cut.InvokeAsync(() => cut.FindComponent<LoanScheduleChangePreviewDialog>().Instance.OnBackToEdit.InvokeAsync());
        GetPrivateField<string>(cut.Instance, "_prepaymentAmountInput").Should().Be("100");
        await cut.InvokeAsync(() => InvokePrivateAsync(cut.Instance, "ApplyPrepaymentAsync"));
        await cut.InvokeAsync(() => cut.FindComponent<LoanScheduleChangePreviewDialog>().Instance.OnConfirm.InvokeAsync());
        _loanService.Events.TakeLast(2).Should().Equal("preview-prepayment", "write-prepayment");
        _loanService.LastExpectedVersion.Should().Be(RecordingLoanService.PreviewVersion);

        SetPrivateField(cut.Instance, "_installmentAmountChangeInstallment", installment);
        SetPrivateField(cut.Instance, "_bankInstallmentAmountInput", "2900");
        SetPrivateField(cut.Instance, "_bankLastInstallmentDate", new DateTime?(new DateTime(2027, 12, 15)));
        await cut.InvokeAsync(() => InvokePrivateAsync(cut.Instance, "ApplyInstallmentAmountChangeAsync"));
        _loanService.Events.Last().Should().Be("preview-bank");

        await cut.InvokeAsync(() => cut.FindComponent<LoanScheduleChangePreviewDialog>().Instance.OnBackToEdit.InvokeAsync());
        GetPrivateField<string>(cut.Instance, "_bankInstallmentAmountInput").Should().Be("2900");
        await cut.InvokeAsync(() => InvokePrivateAsync(cut.Instance, "ApplyInstallmentAmountChangeAsync"));
        await cut.InvokeAsync(() => cut.FindComponent<LoanScheduleChangePreviewDialog>().Instance.OnConfirm.InvokeAsync());
        _loanService.Events.TakeLast(2).Should().Equal("preview-bank", "write-bank");
        _loanService.LastExpectedVersion.Should().Be(RecordingLoanService.PreviewVersion);
    }

    [Fact]
    public async Task Stale_Confirmation_Should_Preserve_Wibor_Input_And_Not_Report_A_Write_Success()
    {
        var cut = RenderLoansAndSelectLoan();
        await cut.InvokeAsync(() => cut.FindComponent<LoanWorkspaceTabs>().Instance.ActiveTabChanged.InvokeAsync(LoanWorkspaceTab.Wibor));
        var wibor = cut.FindComponent<LoanWiborPanel>();
        await cut.InvokeAsync(() => wibor.Instance.NewReferenceRateInputChanged.InvokeAsync("3,73"));
        await cut.InvokeAsync(() => wibor.Instance.OnSubmit.InvokeAsync());

        _loanService.ThrowConflictOnWrite = true;
        await cut.InvokeAsync(() => cut.FindComponent<LoanScheduleChangePreviewDialog>().Instance.OnConfirm.InvokeAsync());

        _loanService.Events.Should().Equal("preview-wibor", "write-wibor-conflict");
        cut.FindComponent<LoanWiborPanel>().Instance.NewReferenceRateInput.Should().Be("3,73");
        cut.FindComponent<LoanScheduleChangePreviewDialog>().Instance.Preview.Should().BeNull();
    }

    private IRenderedComponent<Loans> RenderLoansAndSelectLoan()
    {
        Render<MudPopoverProvider>();
        var cut = Render<Loans>();
        cut.FindAll("button").Single(x => x.TextContent.Contains("Zarz")).Click();
        cut.FindComponent<LoanWorkspaceTabs>();
        return cut;
    }

    private static Task InvokePrivateAsync(Loans component, string methodName)
    {
        var method = typeof(Loans).GetMethod(
            methodName,
            System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)
            ?? throw new InvalidOperationException($"Method '{methodName}' was not found.");
        return (Task)(method.Invoke(component, null)
            ?? throw new InvalidOperationException($"Method '{methodName}' returned no task."));
    }

    private static void SetPrivateField<T>(Loans component, string fieldName, T value)
    {
        var field = typeof(Loans).GetField(
            fieldName,
            System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)
            ?? throw new InvalidOperationException($"Field '{fieldName}' was not found.");
        field.SetValue(component, value);
    }

    private static T GetPrivateField<T>(Loans component, string fieldName)
    {
        var field = typeof(Loans).GetField(
            fieldName,
            System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)
            ?? throw new InvalidOperationException($"Field '{fieldName}' was not found.");
        return (T)(field.GetValue(component)
            ?? throw new InvalidOperationException($"Field '{fieldName}' has no value."));
    }

    private sealed class RecordingLoanService : NoOpLoanService
    {
        public const string PreviewVersion = "schedule-version-1";

        public LoanDto Loan { get; } = BuildLoan();
        public List<string> Events { get; } = [];
        public string? LastExpectedVersion { get; private set; }
        public bool ThrowConflictOnWrite { get; set; }

        public override Task<IReadOnlyList<LoanDto>> GetAllAsync(CancellationToken cancellationToken)
            => Task.FromResult<IReadOnlyList<LoanDto>>([Loan]);

        public override Task<LoanScheduleChangePreviewDto> PreviewAddLoanRateEntryAsync(
            AddLoanRateEntryRequest request,
            CancellationToken cancellationToken)
        {
            Events.Add("preview-wibor");
            return Task.FromResult(BuildPreview("WIBOR"));
        }

        public override Task<LoanScheduleChangePreviewDto> PreviewApplyLoanPrepaymentAsync(
            ApplyLoanPrepaymentRequest request,
            CancellationToken cancellationToken)
        {
            Events.Add("preview-prepayment");
            return Task.FromResult(BuildPreview("Nadpłata"));
        }

        public override Task<LoanScheduleChangePreviewDto> PreviewApplyLoanInstallmentAmountChangeAsync(
            ApplyLoanInstallmentAmountChangeRequest request,
            CancellationToken cancellationToken)
        {
            Events.Add("preview-bank");
            return Task.FromResult(BuildPreview("Zmiana raty z banku"));
        }

        public override Task<LoanDto> AddLoanRateEntryAsync(
            AddLoanRateEntryRequest request,
            CancellationToken cancellationToken)
        {
            LastExpectedVersion = request.ExpectedScheduleVersion;
            if (ThrowConflictOnWrite)
            {
                Events.Add("write-wibor-conflict");
                throw new ConflictException("Podgląd jest nieaktualny.");
            }

            Events.Add("write-wibor");
            return Task.FromResult(Loan);
        }

        public override Task<LoanDto> ApplyLoanPrepaymentAsync(
            ApplyLoanPrepaymentRequest request,
            CancellationToken cancellationToken)
        {
            Events.Add("write-prepayment");
            LastExpectedVersion = request.ExpectedScheduleVersion;
            return Task.FromResult(Loan);
        }

        public override Task<LoanDto> ApplyLoanInstallmentAmountChangeAsync(
            ApplyLoanInstallmentAmountChangeRequest request,
            CancellationToken cancellationToken)
        {
            Events.Add("write-bank");
            LastExpectedVersion = request.ExpectedScheduleVersion;
            return Task.FromResult(Loan);
        }

        private static LoanDto BuildLoan()
        {
            var dueDate = new DateOnly(2026, 7, 15);
            return new LoanDto
            {
                Id = 1,
                Name = "Hipoteka testowa",
                LoanType = LoanType.Mortgage,
                InterestMode = LoanInterestMode.VariableWibor,
                WiborPeriodType = WiborPeriodType.Wibor1M,
                Principal = 800_000m,
                RemainingPrincipal = 800_000m,
                MarginRate = 1.52m,
                CurrentReferenceRate = 3.8m,
                RepaymentDayOfMonth = 15,
                StartDate = new DateOnly(2026, 6, 15),
                EndDate = new DateOnly(2054, 5, 15),
                IsActive = true,
                Installments =
                [
                    new LoanInstallmentDto
                    {
                        Id = 10,
                        LoanId = 1,
                        Year = dueDate.Year,
                        Month = dueDate.Month,
                        DueDate = dueDate,
                        Amount = 3_000m,
                        PrincipalAmount = 2_000m,
                        InterestAmount = 1_000m
                    }
                ]
            };
        }

        private static LoanScheduleChangePreviewDto BuildPreview(string label)
        {
            var dueDate = new DateOnly(2026, 7, 15);
            return new LoanScheduleChangePreviewDto
            {
                LoanId = 1,
                LoanName = "Hipoteka testowa",
                ChangeType = label,
                ChangeLabel = label,
                AffectedFrom = dueDate,
                SourceVersion = PreviewVersion,
                BeforeSummary = new LoanScheduleSummaryDto { EndDate = new DateOnly(2054, 5, 15), InstallmentCount = 1 },
                AfterSummary = new LoanScheduleSummaryDto { EndDate = new DateOnly(2054, 5, 15), InstallmentCount = 1 },
                Rows =
                [
                    new LoanScheduleComparisonRowDto
                    {
                        DueDate = dueDate,
                        State = LoanScheduleComparisonRowState.Changed,
                        Before = new ScheduleRowDto(2026, 7, dueDate, 3_000m, 2_000m, 1_000m),
                        After = new ScheduleRowDto(2026, 7, dueDate, 2_990m, 2_010m, 980m)
                    }
                ]
            };
        }
    }

    private sealed class EmptyCategoryService : ICategoryService
    {
        public Task<IReadOnlyList<CategoryDto>> GetAllAsync(CancellationToken cancellationToken)
            => Task.FromResult<IReadOnlyList<CategoryDto>>([]);

        public Task<CategoryDto> CreateCategoryAsync(CreateCategoryRequest request, CancellationToken cancellationToken)
            => throw new NotSupportedException();

        public Task<TagDto> CreateTagAsync(CreateTagRequest request, CancellationToken cancellationToken)
            => throw new NotSupportedException();

        public Task<CategoryDto> UpdateCategoryAsync(UpdateCategoryRequest request, CancellationToken cancellationToken)
            => throw new NotSupportedException();

        public Task<TagDto> UpdateTagAsync(UpdateTagRequest request, CancellationToken cancellationToken)
            => throw new NotSupportedException();

        public Task DeleteCategoryAsync(DeleteCategoryRequest request, CancellationToken cancellationToken)
            => throw new NotSupportedException();

        public Task DeleteTagAsync(DeleteTagRequest request, CancellationToken cancellationToken)
            => throw new NotSupportedException();
    }
}
