using System.Globalization;
using HouseholdBudgetMate.Abstractions.Contracts.Accounts.Dto;
using HouseholdBudgetMate.Abstractions.Contracts.Accounts.Requests;
using HouseholdBudgetMate.Abstractions.Contracts.Categories.Dto;
using HouseholdBudgetMate.Abstractions.Contracts.Common.Dto;
using HouseholdBudgetMate.Abstractions.Contracts.Expenses.Dto;
using HouseholdBudgetMate.Abstractions.Contracts.Incomes.Dto;
using HouseholdBudgetMate.Abstractions.Enums;
using HouseholdBudgetMate.Abstractions.Extensions;
using HouseholdBudgetMate.Abstractions.Interfaces;
using HouseholdBudgetMate.Abstractions.Parsing;
using HouseholdBudgetMate.Application.Kernel.Timing;
using HouseholdBudgetMate.Web.Components.Dialogs;
using HouseholdBudgetMate.Web.Components.Others;
using Microsoft.AspNetCore.Components;
using MudBlazor;

namespace HouseholdBudgetMate.Web.Components.Pages;

public partial class Accounts
{
    [Inject] private IDateTimeProvider DateTimeProvider { get; set; } = default!;
    [Inject] private IAccountService AccountService { get; set; } = default!;
    [Inject] private ICategoryService CategoryService { get; set; } = default!;
    [Inject] private IExpenseService ExpenseService { get; set; } = default!;
    [Inject] private IIncomeService IncomeService { get; set; } = default!;
    [Inject] private IDialogService DialogService { get; set; } = default!;
    [Inject] private ISnackbar Snackbar { get; set; } = default!;

    private readonly CultureInfo _culture = new("pl-PL");
    private readonly AccountType[] _accountTypes = Enum.GetValues<AccountType>();
    private readonly IReadOnlyList<MonthOptionDto> _monthOptions = Enumerable.Range(1, 12)
        .Select(month => new MonthOptionDto(
            month,
            new DateTime(2000, month, 1).ToString("MMMM", new CultureInfo("pl-PL")),
            new DateTime(2000, month, 1).ToString("MMM", new CultureInfo("pl-PL"))))
        .ToList();

    private bool _isInitialLoading = true;
    private bool _hasLoaded;
    private bool _isBusy;
    private bool _canEditSelectedMonth = true;
    private bool _hasSelectedMonthPlan;
    private string? _loadError;

    private List<AccountDto> _accounts = [];
    private IReadOnlyList<CategoryDto> _categories = [];
    private IReadOnlyList<AccountBalanceRowDto> _accountRows = [];
    private IReadOnlyList<AccountBalanceRowDto> _balanceRows = [];
    private IReadOnlyList<BudgetHealthItemDto> _overspentCategories = [];
    private IReadOnlyList<BudgetHealthItemDto> _safeCategories = [];
    private IReadOnlyList<AvailableMonthDto> _availablePlanMonths = [];
    private MonthPlanDto? _selectedMonthPlan;
    private LiveBalanceDto _liveBalance = new();
    private AccountsOverviewDto _overview = new();
    private SavingsTransferSummaryDto _savingsSummary = new();
    private decimal _savedAmountSelectedMonth;

    private UpdateAccountRequest? _editModel;
    private string? _editNameError;
    private DirtyStateMonitor? _dirtyStateMonitor;
    private int _dirtyResetVersion;

    private int _selectedYear;
    private int _selectedMonth;
    private List<int> _availableYears = [];
    private List<int> _availableMonthsForSelectedYear = [];
    private readonly Dictionary<int, decimal> _selectedMonthAmounts = new();
    private readonly Dictionary<int, string> _selectedMonthAmountInputs = new();
    private readonly Dictionary<int, string> _balanceInputErrors = new();

    private string SelectedMonthPlanHref => $"/plan/{_selectedYear}/{_selectedMonth}";
    private string SelectedPeriodLabel => $"{GetMonthLabel(_selectedMonth)} {_selectedYear}";
    private string BalanceBaseGuidance
    {
        get
        {
            var missingAccounts = _overview.MissingBalanceAccountNames.Count == 0
                ? string.Empty
                : $" Brakuje danych dla: {string.Join(", ", _overview.MissingBalanceAccountNames)}.";

            return $"Uzupełnij i zapisz salda zamknięcia kont za poprzedni miesiąc, aby obliczyć Live balance. Zapisana wartość 0,00 PLN jest poprawnym saldem.{missingAccounts}";
        }
    }

    private string AttentionKpiClass => _overview.OverspentCategoryCount > 0
        ? "hb-kpi-card accounts-kpi-card accounts-kpi-attention"
        : "hb-kpi-card accounts-kpi-card";
    private static InputMode DecimalInputMode => InputMode.@decimal;

    protected override async Task OnInitializedAsync()
    {
        var today = DateTimeProvider.GetLocalDateOnly();
        _selectedYear = today.Year;
        _selectedMonth = today.Month;
        _availableYears = [today.Year];

        await LoadAsync();
    }

    private async Task LoadAsync()
    {
        _loadError = null;
        _isInitialLoading = !_hasLoaded;
        _isBusy = _hasLoaded;

        try
        {
            _accounts = (await AccountService.GetAllAsync(CancellationToken.None))
                .OrderBy(x => x.Order)
                .ThenBy(x => x.Name)
                .ToList();

            _categories = await CategoryService.GetAllAsync(CancellationToken.None);
            _availablePlanMonths = await ExpenseService.GetAvailableMonthsAsync(CancellationToken.None);
            SyncAvailableYears();
            SyncAvailableMonthsForSelectedYear();
            EnsureSelectedPeriodIsSelectable();

            _hasSelectedMonthPlan = _availablePlanMonths.Any(x => x.Year == _selectedYear && x.Month == _selectedMonth);
            _canEditSelectedMonth = CanSelectAccountPeriod(_selectedYear, _selectedMonth) && !IsSelectedMonthClosed();
            await LoadSelectedMonthFinancialPictureAsync();

            SyncSelectedMonthAmounts();
            RebuildPresentationModels();
            _hasLoaded = true;
            MarkDirtyStatePristine();
        }
        catch (Exception ex)
        {
            _loadError = ex.Message;
            Snackbar.Add(ex.Message, Severity.Error);
        }
        finally
        {
            _isInitialLoading = false;
            _isBusy = false;
        }
    }

    private void SyncAvailableYears()
    {
        var today = DateTimeProvider.GetLocalDateOnly();
        var knownPeriods = _availablePlanMonths
            .Select(x => new YearMonthKeyDto(x.Year, x.Month))
            .Concat(_accounts.SelectMany(account => account.MonthBalances.Select(balance => new YearMonthKeyDto(balance.Year, balance.Month))))
            .Append(new YearMonthKeyDto(today.Year, today.Month))
            .ToList();

        var earliestKnownPeriod = knownPeriods
            .OrderBy(x => ToMonthKey(x.Year, x.Month))
            .First();
        var firstBalanceSetupPeriod = new DateOnly(earliestKnownPeriod.Year, earliestKnownPeriod.Month, 1).AddMonths(-1);
        var startYear = firstBalanceSetupPeriod.Year;
        var endYear = knownPeriods
            .Select(x => x.Year)
            .Append(today.Year)
            .Max();

        var years = Enumerable.Range(startYear, (endYear - startYear) + 1)
            .OrderByDescending(x => x)
            .ToList();

        _availableYears = years.Count == 0 ? [today.Year] : years;

        if (!_availableYears.Contains(_selectedYear))
        {
            _selectedYear = _availableYears.Contains(today.Year) ? today.Year : _availableYears[0];
        }
    }

    private void SyncAvailableMonthsForSelectedYear()
    {
        _availableMonthsForSelectedYear = Enumerable.Range(1, 12)
            .Where(month => CanSelectAccountPeriod(_selectedYear, month))
            .ToList();

        if (_selectedMonth is < 1 or > 12)
        {
            _selectedMonth = DateTimeProvider.GetLocalDateOnly().Month;
        }
    }

    private async Task MoveSelectedPeriodAsync(int direction)
    {
        var nextPeriod = FindAdjacentSelectablePeriod(direction);
        if (nextPeriod is null)
        {
            Snackbar.Add("Brak dostępnego miesiąca w tym kierunku.", Severity.Info);
            return;
        }

        await SelectPeriodAsync(nextPeriod.Year, nextPeriod.Month);
    }

    private async Task LoadSelectedMonthFinancialPictureAsync()
    {
        if (_hasSelectedMonthPlan)
        {
            var monthlyPicture = await ExpenseService.GetMonthlyFinancialPictureAsync(_selectedYear, _selectedMonth, CancellationToken.None);
            _selectedMonthPlan = monthlyPicture.MonthPlan;
            _liveBalance = monthlyPicture.LiveBalance;
            return;
        }

        _selectedMonthPlan = null;
        _liveBalance = await IncomeService.GetLiveBalanceAsync(_selectedYear, _selectedMonth, CancellationToken.None);
    }

    private async Task OpenPeriodDialogAsync()
    {
        var parameters = new DialogParameters
        {
            [nameof(AccountPeriodDialog.SelectedYear)] = _selectedYear,
            [nameof(AccountPeriodDialog.SelectedMonth)] = _selectedMonth,
            [nameof(AccountPeriodDialog.AvailableYears)] = _availableYears,
            [nameof(AccountPeriodDialog.AvailablePlanMonths)] = _availablePlanMonths,
            [nameof(AccountPeriodDialog.CurrentYear)] = DateTimeProvider.GetLocalDateOnly().Year,
            [nameof(AccountPeriodDialog.CurrentMonth)] = DateTimeProvider.GetLocalDateOnly().Month
        };

        var options = new DialogOptions
        {
            MaxWidth = MaxWidth.Small,
            FullWidth = true,
            CloseButton = true
        };

        var dialog = await DialogService.ShowAsync<AccountPeriodDialog>("Wybierz miesiąc", parameters, options);
        var result = await dialog.Result;
        if (result is null || result.Canceled || result.Data is not AccountPeriodSelectionDto period)
        {
            return;
        }

        await SelectPeriodAsync(period.Year, period.Month);
    }

    private async Task SelectPeriodAsync(int year, int month)
    {
        if (!CanSelectAccountPeriod(year, month))
        {
            Snackbar.Add("Nie można przejść do przyszłego miesiąca bez planu.", Severity.Warning);
            return;
        }

        _selectedYear = year;
        _selectedMonth = month;
        EnsureSelectedYearIsAvailable();
        await LoadSelectedMonthDetailsAsync();
    }

    private void EnsureSelectedYearIsAvailable()
    {
        if (_availableYears.Contains(_selectedYear))
        {
            return;
        }

        _availableYears = _availableYears.Append(_selectedYear).Distinct().OrderByDescending(x => x).ToList();
    }

    private void EnsureSelectedPeriodIsSelectable()
    {
        if (CanSelectAccountPeriod(_selectedYear, _selectedMonth))
        {
            return;
        }

        var selectedPeriodKey = ToMonthKey(_selectedYear, _selectedMonth);
        var fallback = _availableYears
            .SelectMany(year => Enumerable.Range(1, 12).Select(month => new YearMonthKeyDto(year, month)))
            .Where(period => CanSelectAccountPeriod(period.Year, period.Month))
            .OrderBy(x => Math.Abs(ToMonthKey(x.Year, x.Month) - selectedPeriodKey))
            .ThenByDescending(x => ToMonthKey(x.Year, x.Month))
            .First();

        _selectedYear = fallback.Year;
        _selectedMonth = fallback.Month;
        SyncAvailableMonthsForSelectedYear();
    }

    private async Task LoadSelectedMonthDetailsAsync()
    {
        _loadError = null;
        _isBusy = true;

        try
        {
            _hasSelectedMonthPlan = _availablePlanMonths.Any(x => x.Year == _selectedYear && x.Month == _selectedMonth);
            _canEditSelectedMonth = CanSelectAccountPeriod(_selectedYear, _selectedMonth) && !IsSelectedMonthClosed();
            await LoadSelectedMonthFinancialPictureAsync();
            SyncSelectedMonthAmounts();
            RebuildPresentationModels();
            MarkDirtyStatePristine();
        }
        catch (Exception ex)
        {
            _loadError = ex.Message;
            Snackbar.Add(ex.Message, Severity.Error);
        }
        finally
        {
            _isBusy = false;
        }
    }

    private void SyncSelectedMonthAmounts()
    {
        _balanceInputErrors.Clear();

        foreach (var account in GetAccountsForSelectedMonth())
        {
            var existing = TryGetSelectedMonthBalance(account.Id);
            var amount = existing?.ClosingBalance ?? 0;
            _selectedMonthAmounts[account.Id] = amount;
            _selectedMonthAmountInputs[account.Id] = amount.ToString("0.00", _culture);
        }

        var relevantAccountIds = GetAccountsForSelectedMonth().Select(x => x.Id).ToHashSet();
        foreach (var key in _selectedMonthAmounts.Keys.Where(x => !relevantAccountIds.Contains(x)).ToList())
        {
            _selectedMonthAmounts.Remove(key);
            _selectedMonthAmountInputs.Remove(key);
            _balanceInputErrors.Remove(key);
        }
    }

    private void RebuildPresentationModels()
    {
        _accountRows = _accounts
            .OrderBy(x => x.Order)
            .ThenBy(x => x.Name)
            .Select(ToAccountBalanceRowDto)
            .ToList();

        _balanceRows = GetAccountsForSelectedMonth()
            .Select(ToAccountBalanceRowDto)
            .ToList();

        var checkingBalance = _balanceRows
            .Where(x => x.Type != AccountType.Savings)
            .Sum(x => x.Amount);

        var allAccountsBalance = _balanceRows.Sum(x => x.Amount);
        _savedAmountSelectedMonth = CalculateSavedAmountSelectedMonth();

        _savingsSummary = new SavingsTransferSummaryDto(
            _selectedMonthPlan?.SavingsTransfers.Sum(x => x.Amount) ?? 0,
            _selectedMonthPlan?.SavingsTransfers.Count ?? 0);

        var budgetHealthItems = BuildBudgetHealthItemDtos().ToList();

        _overspentCategories = budgetHealthItems
            .Where(x => x.RemainingAmount < 0)
            .OrderBy(x => x.RemainingAmount)
            .Take(5)
            .ToList();

        _safeCategories = budgetHealthItems
            .Where(x => x.RemainingAmount > 0)
            .OrderByDescending(x => x.RemainingAmount)
            .Take(5)
            .ToList();

        _overview = new AccountsOverviewDto(
            _liveBalance.CurrentBalance,
            _liveBalance.HasCompleteBalanceBase,
            _liveBalance.MissingBalanceAccountNames,
            checkingBalance,
            allAccountsBalance,
            _liveBalance.IncomesTotal,
            _liveBalance.ExpensesTotal,
            0,
            _overspentCategories.Count,
            _accounts.Count,
            _accounts.Count(x => !x.IsArchived),
            IsSelectedMonthClosed());
    }

    private IEnumerable<BudgetHealthItemDto> BuildBudgetHealthItemDtos()
    {
        if (_selectedMonthPlan is null)
        {
            return [];
        }

        var spentByCategory = _selectedMonthPlan.Expenses
            .GroupBy(x => x.CategoryId)
            .ToDictionary(x => x.Key, x => x.Sum(expense => expense.ActualAmount));

        return _categories
            .Where(x => x.EnvelopeLimit is > 0)
            .Select(category =>
            {
                var limit = category.EnvelopeLimit!.Value;
                var spent = spentByCategory.GetValueOrDefault(category.Id, 0);

                return new BudgetHealthItemDto(category.Name, limit, spent, limit - spent);
            });
    }

    private decimal CalculateSavedAmountSelectedMonth()
    {
        if (_balanceRows.Count == 0 || _balanceRows.Any(x => !x.HasRecordedBalance))
        {
            return 0m;
        }

        var currentTotal = _balanceRows.Sum(x => x.Amount);
        var previousMonthDate = new DateTime(_selectedYear, _selectedMonth, 1).AddMonths(-1);
        var previousTotal = GetAccountsForSelectedMonth()
            .Sum(account => account.MonthBalances
                .Where(balance => balance.Year == previousMonthDate.Year && balance.Month == previousMonthDate.Month)
                .Select(balance => balance.ClosingBalance)
                .FirstOrDefault());

        return currentTotal - previousTotal;
    }

    private AccountBalanceRowDto ToAccountBalanceRowDto(AccountDto account)
    {
        return new AccountBalanceRowDto(
            account.Id,
            account.Name,
            account.Type,
            account.Type.GetDisplayName(),
            account.Order,
            GetSelectedMonthAmount(account.Id),
            account.IsArchived,
            TryGetSelectedMonthBalance(account.Id) is not null);
    }

    private async Task OpenCreateAccountDialogAsync()
    {
        var parameters = new DialogParameters
        {
            [nameof(AccountCreateDialog.Model)] = new CreateAccountRequest { Type = AccountType.Bank }
        };

        var options = new DialogOptions
        {
            MaxWidth = MaxWidth.Small,
            FullWidth = true,
            CloseButton = true
        };

        var dialog = await DialogService.ShowAsync<AccountCreateDialog>("Dodaj konto", parameters, options);
        var result = await dialog.Result;
        if (result is null || result.Canceled || result.Data is not CreateAccountRequest request)
        {
            return;
        }

        _isBusy = true;

        try
        {
            await AccountService.CreateAccountAsync(request, CancellationToken.None);
            await LoadAsync();
            Snackbar.Add("Dodano konto.", Severity.Success);
        }
        catch (Exception ex)
        {
            Snackbar.Add(ex.Message, Severity.Error);
        }
        finally
        {
            _isBusy = false;
        }
    }

    private void BeginEdit(AccountBalanceRowDto account)
    {
        _editNameError = null;
        _editModel = new UpdateAccountRequest
        {
            Id = account.Id,
            Name = account.Name,
            Type = account.Type
        };
        MarkDirtyStatePristine();
    }

    private async Task SaveEditAsync()
    {
        _editNameError = null;

        if (_editModel is null)
        {
            return;
        }

        if (string.IsNullOrWhiteSpace(_editModel.Name))
        {
            _editNameError = "Podaj nazwę konta.";
            return;
        }

        _isBusy = true;

        try
        {
            await AccountService.UpdateAccountAsync(_editModel, CancellationToken.None);
            _editModel = null;
            await LoadAsync();
            Snackbar.Add("Zapisano konto.", Severity.Success);
        }
        catch (Exception ex)
        {
            Snackbar.Add(ex.Message, Severity.Error);
        }
        finally
        {
            _isBusy = false;
        }
    }

    private void CancelEdit()
    {
        _editModel = null;
        _editNameError = null;
        MarkDirtyStatePristine();
    }

    private async Task DeleteAccountAsync(AccountBalanceRowDto account)
    {
        var parameters = new DialogParameters
        {
            [nameof(ConfirmDialog.Message)] = $"Usunąć konto '{account.Name}'? Tej operacji nie można cofnąć."
        };

        var dialog = await DialogService.ShowAsync<ConfirmDialog>("Usuń konto", parameters);
        var result = await dialog.Result;
        if (result is null || result.Canceled)
        {
            return;
        }

        _isBusy = true;

        try
        {
            await AccountService.DeleteAccountAsync(new DeleteAccountRequest { Id = account.Id }, CancellationToken.None);
            await LoadAsync();
            Snackbar.Add("Usunięto konto.", Severity.Success);
        }
        catch (Exception ex)
        {
            Snackbar.Add(ex.Message, Severity.Error);
        }
        finally
        {
            _isBusy = false;
        }
    }

    private async Task ToggleArchiveAsync(AccountBalanceRowDto account)
    {
        _isBusy = true;

        try
        {
            await AccountService.SetAccountArchivedAsync(new SetAccountArchivedRequest
            {
                Id = account.Id,
                IsArchived = !account.IsArchived
            }, CancellationToken.None);

            await LoadAsync();
            Snackbar.Add(account.IsArchived ? "Przywrócono konto." : "Zarchiwizowano konto.", Severity.Success);
        }
        catch (Exception ex)
        {
            Snackbar.Add(ex.Message, Severity.Error);
        }
        finally
        {
            _isBusy = false;
        }
    }

    private async Task MoveAccountAsync(int accountId, int direction)
    {
        var ordered = _accountRows.OrderBy(x => x.Order).ToList();
        var index = ordered.FindIndex(x => x.Id == accountId);

        if (index == -1)
        {
            return;
        }

        var newIndex = index + direction;
        if (newIndex < 0 || newIndex >= ordered.Count)
        {
            return;
        }

        (ordered[index], ordered[newIndex]) = (ordered[newIndex], ordered[index]);
        _isBusy = true;

        try
        {
            await AccountService.ReorderAccountsAsync(new ReorderAccountsRequest
            {
                AccountIds = ordered.Select(x => x.Id).ToList()
            }, CancellationToken.None);

            await LoadAsync();
        }
        catch (Exception ex)
        {
            Snackbar.Add(ex.Message, Severity.Error);
        }
        finally
        {
            _isBusy = false;
        }
    }

    private bool IsFirstAccount(int accountId)
    {
        return _accountRows.Count == 0 || _accountRows[0].Id == accountId;
    }

    private bool IsLastAccount(int accountId)
    {
        return _accountRows.Count == 0 || _accountRows[^1].Id == accountId;
    }

    private AccountMonthBalanceDto? TryGetSelectedMonthBalance(int accountId)
    {
        return _accounts.FirstOrDefault(x => x.Id == accountId)?.MonthBalances
            .FirstOrDefault(x => x.Year == _selectedYear && x.Month == _selectedMonth);
    }

    private decimal GetSelectedMonthAmount(int accountId)
    {
        if (_selectedMonthAmounts.TryGetValue(accountId, out var value))
        {
            return value;
        }

        var fallback = TryGetSelectedMonthBalance(accountId)?.ClosingBalance ?? 0;
        _selectedMonthAmounts[accountId] = fallback;
        return fallback;
    }

    private string GetSelectedMonthAmountInput(int accountId)
    {
        if (_selectedMonthAmountInputs.TryGetValue(accountId, out var textValue))
        {
            return textValue;
        }

        var amount = GetSelectedMonthAmount(accountId);
        var formatted = amount.ToString("0.00", _culture);
        _selectedMonthAmountInputs[accountId] = formatted;
        return formatted;
    }

    private void SetSelectedMonthAmountInput(int accountId, string? input)
    {
        var normalizedInput = input ?? string.Empty;
        _selectedMonthAmountInputs[accountId] = normalizedInput;

        if (LocalizedDecimalParser.TryParseOrZero(normalizedInput, out var parsedAmount))
        {
            _selectedMonthAmounts[accountId] = parsedAmount;
            _balanceInputErrors.Remove(accountId);
            RebuildPresentationModels();
            return;
        }

        _balanceInputErrors[accountId] = "Niepoprawna kwota.";
    }

    private Task SetSelectedMonthAmountInputAsync(int accountId, string? input)
    {
        SetSelectedMonthAmountInput(accountId, input);
        return Task.CompletedTask;
    }

    private bool HasBalanceInputError(int accountId)
    {
        return _balanceInputErrors.ContainsKey(accountId);
    }

    private string? GetBalanceInputError(int accountId)
    {
        return _balanceInputErrors.TryGetValue(accountId, out var error) ? error : null;
    }

    private async Task SaveAllSelectedMonthBalancesAsync()
    {
        if (!_canEditSelectedMonth)
        {
            Snackbar.Add(
                _hasSelectedMonthPlan
                    ? "Wybrany miesiąc jest zamknięty."
                    : "Nie można zapisać sald dla miesiąca bez planu.",
                Severity.Info);
            return;
        }

        _balanceInputErrors.Clear();
        var editableAccounts = GetAccountsForSelectedMonth();
        foreach (var account in editableAccounts)
        {
            var input = GetSelectedMonthAmountInput(account.Id);
            if (!LocalizedDecimalParser.TryParseOrZero(input, out _))
            {
                _balanceInputErrors[account.Id] = "Niepoprawna kwota.";
            }
        }

        if (_balanceInputErrors.Count > 0)
        {
            Snackbar.Add("Popraw kwoty przed zapisaniem sald.", Severity.Warning);
            RebuildPresentationModels();
            return;
        }

        _isBusy = true;

        try
        {
            foreach (var account in editableAccounts)
            {
                var input = GetSelectedMonthAmountInput(account.Id);
                LocalizedDecimalParser.TryParseOrZero(input, out var amount);

                var existing = TryGetSelectedMonthBalance(account.Id);
                if (existing is null)
                {
                    await AccountService.UpsertMonthBalanceAsync(new UpsertAccountMonthBalanceRequest
                    {
                        AccountId = account.Id,
                        Year = _selectedYear,
                        Month = _selectedMonth,
                        ClosingBalance = amount
                    }, CancellationToken.None);
                }
                else
                {
                    await AccountService.UpdateMonthBalanceAmountAsync(new UpdateAccountMonthBalanceAmountRequest
                    {
                        Id = existing.Id,
                        ClosingBalance = amount
                    }, CancellationToken.None);
                }
            }

            await LoadAsync();
            Snackbar.Add("Zapisano salda dla wszystkich aktywnych kont.", Severity.Success);
        }
        catch (Exception ex)
        {
            Snackbar.Add(ex.Message, Severity.Error);
        }
        finally
        {
            _isBusy = false;
        }
    }

    private bool IsSelectedMonthClosed()
    {
        var plan = _availablePlanMonths.FirstOrDefault(x => x.Year == _selectedYear && x.Month == _selectedMonth);
        return plan?.IsClosed ?? false;
    }

    private bool HasMonthPlan(int year, int month)
    {
        return _availablePlanMonths.Any(x => x.Year == year && x.Month == month);
    }

    private bool CanSelectAccountPeriod(int year, int month)
    {
        return HasMonthPlan(year, month) || IsPastOrCurrentMonth(year, month);
    }

    private bool IsPastOrCurrentMonth(int year, int month)
    {
        var today = DateTimeProvider.GetLocalDateOnly();
        return ToMonthKey(year, month) <= ToMonthKey(today.Year, today.Month);
    }

    private bool CanMoveSelectedPeriod(int direction)
    {
        return FindAdjacentSelectablePeriod(direction) is not null;
    }

    private YearMonthKeyDto? FindAdjacentSelectablePeriod(int direction)
    {
        if (direction == 0)
        {
            return null;
        }

        var selectedKey = ToMonthKey(_selectedYear, _selectedMonth);
        var ordered = _availableYears
            .SelectMany(year => Enumerable.Range(1, 12).Select(month => new YearMonthKeyDto(year, month)))
            .Where(period => CanSelectAccountPeriod(period.Year, period.Month))
            .Distinct()
            .OrderBy(x => ToMonthKey(x.Year, x.Month))
            .ToList();

        return direction > 0
            ? ordered.FirstOrDefault(x => ToMonthKey(x.Year, x.Month) > selectedKey)
            : ordered.LastOrDefault(x => ToMonthKey(x.Year, x.Month) < selectedKey);
    }

    private static int ToMonthKey(int year, int month)
    {
        return (year * 12) + month;
    }

    private IReadOnlyList<AccountDto> GetAccountsForSelectedMonth()
    {
        if (!CanSelectAccountPeriod(_selectedYear, _selectedMonth))
        {
            return [];
        }

        if (IsSelectedMonthClosed())
        {
            return _accounts
                .Where(x => x.MonthBalances.Any(b => b.Year == _selectedYear && b.Month == _selectedMonth))
                .OrderBy(x => x.Order)
                .ThenBy(x => x.Name)
                .ToList();
        }

        return _accounts
            .Where(IsApplicableForSelectedMonthBalance)
            .OrderBy(x => x.Order)
            .ThenBy(x => x.Name)
            .ToList();
    }

    private bool IsApplicableForSelectedMonthBalance(AccountDto account)
    {
        if (!account.IsArchived)
        {
            return true;
        }

        var nextMonthStartUtc = new DateTime(_selectedYear, _selectedMonth, 1, 0, 0, 0, DateTimeKind.Utc)
            .AddMonths(1);
        if (account.ActiveFromUtc is not null && account.ActiveFromUtc >= nextMonthStartUtc)
        {
            return false;
        }

        return (account.ArchivedAtUtc ?? account.UpdatedAtUtc) >= nextMonthStartUtc;
    }

    private bool HasPermanentBalanceTabForSelectedYear(int month)
    {
        return CanSelectAccountPeriod(_selectedYear, month);
    }

    private string GetShortMonthLabel(int month)
    {
        return _monthOptions.FirstOrDefault(x => x.Value == month).ShortLabel ?? month.ToString();
    }

    private string GetMonthLabel(int month)
    {
        return _monthOptions.FirstOrDefault(x => x.Value == month).Label ?? month.ToString();
    }

    private string FormatMoney(decimal value)
    {
        return $"{value.ToString("0.00", _culture)} PLN";
    }

    private static string FormatMonthlyTransferCount(int count) => count switch
    {
        1 => "1 wpis w miesiącu",
        >= 2 and <= 4 => $"{count} wpisy w miesiącu",
        _ => $"{count} wpisów w miesiącu"
    };

    private string GetAccountIcon(AccountType type)
    {
        return type switch
        {
            AccountType.Cash => Icons.Material.Filled.Payments,
            AccountType.Bank => Icons.Material.Filled.AccountBalance,
            AccountType.Savings => Icons.Material.Filled.Savings,
            _ => Icons.Material.Filled.AccountBalanceWallet
        };
    }

    private Color GetAccountIconColor(AccountType type)
    {
        return type switch
        {
            AccountType.Cash => Color.Secondary,
            AccountType.Bank => Color.Primary,
            AccountType.Savings => Color.Success,
            _ => Color.Default
        };
    }


    private void MarkDirtyStatePristine()
    {
        _dirtyResetVersion++;
        _dirtyStateMonitor?.Reset(GetDirtyState());
    }

    private object GetDirtyState() => new
    {
        Period = new
        {
            _selectedYear,
            _selectedMonth
        },
        Balances = _selectedMonthAmountInputs
            .OrderBy(kvp => kvp.Key)
            .Select(kvp => new
            {
                AccountId = kvp.Key,
                Input = kvp.Value
            })
            .ToList(),
        EditAccount = _editModel is null
            ? null
            : new
            {
                _editModel.Id,
                _editModel.Name,
                _editModel.Type
            }
    };

}
