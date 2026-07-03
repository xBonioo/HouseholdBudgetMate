using System.Globalization;
using HouseholdBudgetMate.Abstractions.Contracts.Accounts.Dto;
using HouseholdBudgetMate.Abstractions.Contracts.Categories.Dto;
using HouseholdBudgetMate.Abstractions.Contracts.Expenses.Dto;
using HouseholdBudgetMate.Abstractions.Contracts.Expenses.Requests;
using HouseholdBudgetMate.Abstractions.Contracts.Incomes.Dto;
using HouseholdBudgetMate.Abstractions.Contracts.Incomes.Requests;
using HouseholdBudgetMate.Abstractions.Enums;
using HouseholdBudgetMate.Web.Components.Others;
using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;
using MudBlazor;

namespace HouseholdBudgetMate.Web.Components.Pages.PlanPage;

public partial class PlanPage : ComponentBase
{
    #region Parameters

    [Parameter] public int Year { get; set; }
    [Parameter] public int Month { get; set; }

    [SupplyParameterFromQuery(Name = "editExpenseId")]
    public int? EditExpenseId { get; set; }

    [SupplyParameterFromQuery(Name = "addExpense")]
    public bool AddExpense { get; set; }

    #endregion

    #region State - General

    private bool _isLoading;
    private bool _isCreateExpenseFormVisible;
    private bool _isCopyMode;
    private int? _selectedExpenseCategoryFilterId;
    private ExpensePaymentFilter _selectedExpensePaymentFilter = ExpensePaymentFilter.All;

    #endregion

    #region Data - Main

    private MonthPlanDto? _monthPlan;
    private List<CategoryDto> _categories = [];
    private List<AccountDto> _accounts = [];
    private List<IncomeDto> _incomes = [];
    private Dictionary<int, int> _tagUsageCountByTagId = [];

    private LiveBalanceDto _liveBalance = new();
    private DashboardSummaryDto _dashboardSummary = new();
    private MonthPlanKpiDto _kpi = new();

    #endregion

    #region Savings Transfers

    private readonly CreateMonthSavingsTransferItemRequest _newSavingsTransfer = new()
    {
        TransferDate = DateOnly.FromDateTime(DateTime.Today)
    };

    private UpdateMonthSavingsTransferItemRequest? _editSavingsTransfer;
    private DateOnly _editSavingsTransferDate = DateOnly.FromDateTime(DateTime.Today);

    private string _newSavingsTransferAmountInput = "0,00";
    private string _editSavingsTransferAmountInput = "0,00";

    #endregion

    #region Expenses - Create

    private readonly CreateExpenseRequest _newExpense = new()
    {
        ShowRemainingInUI = false
    };

    private int? _newExpenseRootTagId;
    private string _newExpensePlannedAmountInput = "0,00";
    private string _newExpenseActualAmountInput = "0,00";

    #endregion

    #region Expenses - Edit

    private UpdateExpenseRequest? _editExpense;

    private int? _editExpenseRootTagId;
    private string _editExpensePlannedAmountInput = "0,00";
    private string _editExpenseActualAmountInput = "0,00";

    #endregion

    #region Expenses - Line Items

    private readonly Dictionary<int, LineItemCreateDto> _lineItemCreateModels = new();
    private readonly Dictionary<int, string> _lineItemCreateAmountInputs = new();

    private UpdateExpenseLineItemRequest? _editLineItem;
    private string _editLineItemAmountInput = "0,00";
    private DateOnly _editLineItemDate = DateOnly.FromDateTime(DateTime.Today);

    private int? _editLineItemExpenseId;
    private readonly HashSet<int> _expandedExpenseIds = [];

    #endregion

    #region Incomes - Create

    private readonly CreateIncomeRequest _newIncome = new()
    {
        Name = string.Empty,
        ExpectedDayOfMonth = DateOnly.FromDateTime(DateTime.Today)
    };

    private string _newIncomeAmountInput = "0,00";

    #endregion

    #region Incomes - Edit

    private UpdateIncomeRequest? _editIncome;
    private string _editIncomeAmountInput = "0,00";
    private DateOnly _editIncomeDate = DateOnly.FromDateTime(DateTime.Today);

    #endregion

    #region UI - Income Panel

    private bool _isIncomePanelExpanded;
    private bool _isDesktopIncomePanelMode = true;
    private bool _isIncomePanelToggleVisible = true;

    private double _incomePanelExpandedWidthPx;
    private ElementReference _incomePanelWrapperRef;
    private DotNetObjectReference<PlanPage>? _incomeToggleViewportRef;

    #endregion

    #region Copy Mode

    private readonly HashSet<int> _selectedExpenseIdsForCopy = [];
    private int _copyTargetYear;
    private int _copyTargetMonth;

    #endregion

    #region Month Preparation

    private MonthPlanPreparationDto? _monthPlanPreparation;
    private readonly List<MonthPlanSuggestionDraftDto> _monthPlanSuggestionDrafts = [];

    #endregion

    #region Navigation / UX

    private int? _expenseIdPendingScrollIntoView;
    private DirtyStateMonitor? _dirtyStateMonitor;
    private int _dirtyResetVersion;

    #endregion

    #region Constants

    private static readonly CultureInfo Culture = new("pl-PL");

    #endregion

    #region Computed Properties

    private IReadOnlyList<EnvelopeProgressItemDto> EnvelopeProgressItems => BuildEnvelopeProgressItems();

    private IReadOnlyList<ExpenseDto> OrderedExpenses =>
        _monthPlan?.Expenses
            .OrderBy(x => x.Order)
            .ThenBy(x => x.Id)
            .ToList() ?? [];

    private IReadOnlyList<ExpenseDto> FilteredExpenses =>
        OrderedExpenses
            .Where(MatchesExpenseCategoryFilter)
            .Where(MatchesExpensePaymentFilter)
            .ToList();

    private IReadOnlyList<CategoryDto> ExpenseFilterCategories =>
        _monthPlan?.Expenses
            .GroupBy(x => x.CategoryId)
            .Select(group => new
            {
                CategoryId = group.Key,
                CategoryName = group.First().CategoryName
            })
            .OrderBy(x => x.CategoryName)
            .Select(x => new CategoryDto
            {
                Id = x.CategoryId,
                Name = x.CategoryName
            })
            .ToList() ?? [];

    private string? CreateExpenseEnvelopeWarning => BuildCreateExpenseEnvelopeWarning();

    private bool IsMonthClosed => _monthPlan?.IsClosed == true;

    private string BalanceBaseGuidance
    {
        get
        {
            var missingAccounts = _liveBalance.MissingBalanceAccountNames.Count == 0
                ? string.Empty
                : $" Brakuje danych dla: {string.Join(", ", _liveBalance.MissingBalanceAccountNames)}.";

            return $"Uzupełnij i zapisz salda zamknięcia kont za poprzedni miesiąc, aby obliczyć Live balance. Zapisana wartość 0,00 PLN jest poprawnym saldem.{missingAccounts}";
        }
    }

    private decimal UnplannedSpentTotal =>
        _monthPlan?.Expenses
            .Sum(x => x.PlannedAmount <= 0
                ? x.ActualAmount
                : Math.Max(x.ActualAmount - x.PlannedAmount, 0m)) ?? 0m;

    private decimal TotalIncomeAmount => _incomes.Sum(x => x.Amount);

    private decimal RegularIncomeAmount =>
        _incomes.Where(x => x.IsRegular).Sum(x => x.Amount);

    private decimal IrregularIncomeAmount =>
        _incomes.Where(x => !x.IsRegular).Sum(x => x.Amount);

    private int RegularIncomeCount => _incomes.Count(x => x.IsRegular);

    private int IrregularIncomeCount => _incomes.Count - RegularIncomeCount;

    private bool HasMonthPreparation => _monthPlanPreparation is { MonthExists: false } && _monthPlanSuggestionDrafts.Count > 0;

    private bool IsCopyTargetSameAsSource => _copyTargetYear == Year && _copyTargetMonth == Month;

    private bool HasActiveExpenseFilters =>
        _selectedExpenseCategoryFilterId.HasValue
        || _selectedExpensePaymentFilter != ExpensePaymentFilter.All;

    #endregion

    private void MarkDirtyStatePristine()
    {
        _dirtyResetVersion++;
        _dirtyStateMonitor?.Reset(GetDirtyState());
    }

    private object GetDirtyState() => new
    {
        SavingsTransfer = new
        {
            _newSavingsTransfer.TransferDate,
            Amount = _newSavingsTransferAmountInput
        },
        EditSavingsTransfer = _editSavingsTransfer is null
            ? null
            : new
            {
                _editSavingsTransfer.Id,
                _editSavingsTransferDate,
                Amount = _editSavingsTransferAmountInput
            },
        NewIncome = new
        {
            _newIncome.Name,
            _newIncome.AccountId,
            Amount = _newIncomeAmountInput,
            _newIncome.ExpectedDayOfMonth
        },
        EditIncome = _editIncome is null
            ? null
            : new
            {
                _editIncome.Id,
                _editIncome.Name,
                _editIncome.AccountId,
                Amount = _editIncomeAmountInput,
                _editIncomeDate
            },
        NewExpense = new
        {
            _newExpense.Name,
            _newExpense.CategoryId,
            _newExpenseRootTagId,
            _newExpense.TagId,
            PlannedAmount = _newExpensePlannedAmountInput,
            ActualAmount = _newExpenseActualAmountInput,
            _newExpense.ShowRemainingInUI
        },
        EditExpense = _editExpense is null
            ? null
            : new
            {
                _editExpense.Id,
                _editExpense.Name,
                _editExpense.CategoryId,
                _editExpenseRootTagId,
                _editExpense.TagId,
                PlannedAmount = _editExpensePlannedAmountInput,
                ActualAmount = _editExpenseActualAmountInput,
                _editExpense.ShowRemainingInUI
            },
        NewLineItems = _lineItemCreateModels
            .OrderBy(kvp => kvp.Key)
            .Select(kvp => new
            {
                ExpenseId = kvp.Key,
                kvp.Value.Description,
                Amount = _lineItemCreateAmountInputs.GetValueOrDefault(kvp.Key),
                kvp.Value.OccurredAt,
                kvp.Value.TagId
            })
            .ToList(),
        EditLineItem = _editLineItem is null
            ? null
            : new
            {
                ExpenseId = _editLineItemExpenseId,
                _editLineItem.Id,
                _editLineItem.Description,
                Amount = _editLineItemAmountInput,
                _editLineItemDate,
                _editLineItem.TagId
            },
        MonthPreparation = HasMonthPreparation
            ? _monthPlanSuggestionDrafts
                .OrderBy(x => x.Suggestion.SourceExpenseId)
                .Select(x => new
                {
                    x.Suggestion.SourceExpenseId,
                    x.IsSelected,
                    x.PlannedAmountInput
                })
                .ToList()
            : null
    };

    private void ResetCopyTargetToNextMonth()
    {
        var nextMonth = new DateTime(Year, Month, 1).AddMonths(1);
        _copyTargetYear = nextMonth.Year;
        _copyTargetMonth = nextMonth.Month;
    }

    private void SetMonthPreparation(MonthPlanPreparationDto preparation)
    {
        _monthPlanPreparation = preparation;
        _monthPlanSuggestionDrafts.Clear();
        _monthPlanSuggestionDrafts.AddRange(
            preparation.Suggestions.Select(suggestion => new MonthPlanSuggestionDraftDto(suggestion, FormatDecimalInput(suggestion.SuggestedPlannedAmount)) { IsSelected = false }));
        MarkDirtyStatePristine();
    }

    private void ClearMonthPreparation()
    {
        _monthPlanPreparation = null;
        _monthPlanSuggestionDrafts.Clear();
        MarkDirtyStatePristine();
    }
}
