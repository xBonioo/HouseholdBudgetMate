using System.Globalization;
using HouseholdBudgetMate.Abstractions.Contracts.Accounts.Dto;
using HouseholdBudgetMate.Abstractions.Contracts.Categories.Dto;
using HouseholdBudgetMate.Abstractions.Contracts.Expenses.Dto;
using HouseholdBudgetMate.Abstractions.Contracts.Expenses.Requests;
using HouseholdBudgetMate.Abstractions.Contracts.Incomes.Dto;
using HouseholdBudgetMate.Abstractions.Contracts.Incomes.Requests;
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

    #endregion

    #region State - General

    private bool _isLoading;
    private bool _isCreateExpenseFormVisible;
    private bool _isCopyMode;

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
    private bool _isIncomePanelToggleVisible = true;

    private double _incomePanelExpandedWidthPx;
    private ElementReference _incomePanelWrapperRef;
    private DotNetObjectReference<PlanPage>? _incomeToggleViewportRef;

    #endregion

    #region Copy Mode

    private readonly HashSet<int> _selectedExpenseIdsForCopy = [];

    #endregion

    #region Navigation / UX

    private int? _expenseIdPendingScrollIntoView;

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

    private string? CreateExpenseEnvelopeWarning => BuildCreateExpenseEnvelopeWarning();

    private bool IsMonthClosed => _monthPlan?.IsClosed == true;

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

    #endregion

    #region DTOs

    private sealed class EnvelopeProgressItemDto
    {
        public string CategoryName { get; init; } = string.Empty;
        public decimal SpentAmount { get; init; }
        public decimal PlannedAmount { get; init; }
        public decimal LimitAmount { get; init; }
        public double ProgressPercent { get; init; }
        public Color Color { get; init; }
    }

    #endregion
}