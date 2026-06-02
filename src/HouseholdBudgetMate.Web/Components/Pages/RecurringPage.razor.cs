using System.Globalization;
using HouseholdBudgetMate.Abstractions.Contracts.Accounts.Dto;
using HouseholdBudgetMate.Abstractions.Contracts.Categories.Dto;
using HouseholdBudgetMate.Abstractions.Contracts.Expenses.Dto;
using HouseholdBudgetMate.Abstractions.Contracts.Expenses.Requests;
using HouseholdBudgetMate.Abstractions.Contracts.Incomes.Dto;
using HouseholdBudgetMate.Abstractions.Contracts.Incomes.Requests;
using HouseholdBudgetMate.Abstractions.Contracts.Loans.Dto;
using HouseholdBudgetMate.Abstractions.Interfaces;
using HouseholdBudgetMate.Abstractions.Parsing;
using HouseholdBudgetMate.Application.Kernel.Timing;
using HouseholdBudgetMate.Web.Components.Dialogs;
using HouseholdBudgetMate.Web.Components.Others;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using MudBlazor;

namespace HouseholdBudgetMate.Web.Components.Pages;

public partial class RecurringPage
{
    [Inject] private IIncomeService IncomeService { get; set; } = default!;
    [Inject] private IExpenseService ExpenseService { get; set; } = default!;
    [Inject] private IAccountService AccountService { get; set; } = default!;
    [Inject] private ICategoryService CategoryService { get; set; } = default!;
    [Inject] private ILoanService LoanService { get; set; } = default!;
    [Inject] private IDateTimeProvider DateTimeProvider { get; set; } = default!;
    [Inject] private IDialogService DialogService { get; set; } = default!;
    [Inject] private ISnackbar Snackbar { get; set; } = default!;

    private readonly CultureInfo _culture = new("pl-PL");

    private bool _isInitialLoading = true;
    private bool _hasLoaded;
    private bool _isBusy;
    private string? _loadError;
    private int _currentYear;
    private int _currentMonth;

    private List<AccountDto> _accounts = [];
    private List<CategoryDto> _categories = [];
    private List<RegularIncomeDefinitionDto> _incomeDefinitions = [];
    private List<RegularExpenseDefinitionDto> _expenseDefinitions = [];
    private List<LoanDto> _loans = [];
    private IReadOnlyList<IncomeDefinitionRow> _incomeRows = [];
    private IReadOnlyList<ExpenseDefinitionRow> _expenseRows = [];
    private IReadOnlyList<LoanRecurringItem> _loanRecurringItems = [];
    private RecurringOverviewModel _overview = new();

    private CreateRegularIncomeDefinitionRequest _newIncomeDefinition = new()
    {
        Name = string.Empty,
        DayOfMonth = 1
    };

    private CreateRegularExpenseDefinitionRequest _newExpenseDefinition = new()
    {
        Name = string.Empty,
        ShowRemainingInUI = true
    };

    private string _createIncomeAmountInput = string.Empty;
    private string _createExpenseAmountInput = string.Empty;

    private string? _createIncomeNameError;
    private string? _createIncomeAmountError;
    private string? _createIncomeDayError;
    private string? _createIncomeAccountError;

    private string? _createExpenseNameError;
    private string? _createExpenseAmountError;
    private string? _createExpenseCategoryError;
    private DirtyStateMonitor? _dirtyStateMonitor;
    private int _dirtyResetVersion;

    private string CurrentMonthPlanHref => $"/plan/{_currentYear}/{_currentMonth}";

    private string NetKpiClass => _overview.NetRecurringAmount < 0
        ? "hb-kpi-card recurring-kpi-card recurring-kpi-attention"
        : "hb-kpi-card recurring-kpi-card";
    private static InputMode DecimalInputMode => InputMode.@decimal;

    protected override async Task OnInitializedAsync()
    {
        var today = DateTimeProvider.GetLocalDateOnly();
        _currentYear = today.Year;
        _currentMonth = today.Month;

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
                .Where(x => !x.IsArchived)
                .OrderBy(x => x.Order)
                .ThenBy(x => x.Name)
                .ToList();

            _categories = (await CategoryService.GetAllAsync(CancellationToken.None))
                .OrderBy(x => x.Name)
                .ToList();

            _incomeDefinitions = (await IncomeService.GetRegularDefinitionsAsync(CancellationToken.None)).ToList();
            _expenseDefinitions = (await ExpenseService.GetRegularExpenseDefinitionsAsync(CancellationToken.None)).ToList();
            _loans = (await LoanService.GetAllAsync(CancellationToken.None)).ToList();

            EnsureCreateDefaults();
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

    private void EnsureCreateDefaults()
    {
        if (_newIncomeDefinition.AccountId == 0 && _accounts.Count > 0)
        {
            _newIncomeDefinition.AccountId = _accounts[0].Id;
        }

        if (_newExpenseDefinition.CategoryId == 0 && _categories.Count > 0)
        {
            _newExpenseDefinition.CategoryId = _categories[0].Id;
        }
    }

    private void RebuildPresentationModels()
    {
        _incomeRows = _incomeDefinitions
            .OrderByDescending(x => x.IsActive)
            .ThenBy(x => x.DayOfMonth)
            .ThenBy(x => x.Name)
            .Select(x => new IncomeDefinitionRow(x.Id, x.Name, x.Amount, x.DayOfMonth, x.AccountId, x.AccountName, x.IsActive))
            .ToList();

        _expenseRows = _expenseDefinitions
            .OrderBy(x => x.Order)
            .ThenBy(x => x.Name)
            .Select(x => new ExpenseDefinitionRow(
                x.Id,
                x.Order,
                x.Name,
                x.CategoryId,
                x.CategoryName,
                x.TagId,
                x.TagName,
                x.Amount,
                x.IsActive,
                x.ShowRemainingInUI))
            .ToList();

        _loanRecurringItems = _loans
            .Where(x => x.IsActive)
            .SelectMany(x => x.Installments
                .Where(i => !i.IsPaid)
                .OrderBy(i => i.DueDate)
                .Take(1)
                .Select(i => new LoanRecurringItem(x.Name, $"{i.Month:D2}/{i.Year}", i.Amount, i.IsPaid)))
            .OrderBy(x => x.LoanName)
            .ToList();

        var activeIncomeAmount = _incomeRows.Where(x => x.IsActive).Sum(x => x.Amount);
        var activeExpenseAmount = _expenseRows.Where(x => x.IsActive).Sum(x => x.Amount);

        _overview = new RecurringOverviewModel(
            activeIncomeAmount,
            activeExpenseAmount,
            activeIncomeAmount - activeExpenseAmount,
            _incomeRows.Count(x => x.IsActive),
            _expenseRows.Count(x => x.IsActive));
    }

    private Task OnCreateExpenseCategoryChanged(int categoryId)
    {
        _newExpenseDefinition.CategoryId = categoryId;
        _newExpenseDefinition.TagId = null;
        return Task.CompletedTask;
    }

    private async Task CreateIncomeDefinitionAsync()
    {
        ClearCreateIncomeErrors();
        if (!ValidateCreateIncome())
        {
            return;
        }

        _isBusy = true;

        try
        {
            _newIncomeDefinition.Name = _newIncomeDefinition.Name.Trim();
            await IncomeService.CreateRegularDefinitionAsync(_newIncomeDefinition, CancellationToken.None);
            var accountId = _newIncomeDefinition.AccountId;
            _newIncomeDefinition = new CreateRegularIncomeDefinitionRequest
            {
                Name = string.Empty,
                DayOfMonth = 1,
                AccountId = accountId
            };
            _createIncomeAmountInput = string.Empty;

            await LoadAsync();
            Snackbar.Add("Dodano cykliczny wpływ.", Severity.Success);
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

    private async Task OpenIncomeEditDialogAsync(IncomeDefinitionRow definition)
    {
        var parameters = new DialogParameters
        {
            [nameof(RecurringIncomeDefinitionDialog.Model)] = new UpdateRegularIncomeDefinitionRequest
            {
                Id = definition.Id,
                Name = definition.Name,
                Amount = definition.Amount,
                DayOfMonth = definition.DayOfMonth,
                AccountId = definition.AccountId,
                IsActive = definition.IsActive
            },
            [nameof(RecurringIncomeDefinitionDialog.Accounts)] = _accounts
        };

        var options = new DialogOptions
        {
            MaxWidth = MaxWidth.Small,
            FullWidth = true,
            CloseButton = true
        };

        var dialog = await DialogService.ShowAsync<RecurringIncomeDefinitionDialog>("Edytuj wpływ", parameters, options);
        var result = await dialog.Result;
        if (result is null || result.Canceled || result.Data is not UpdateRegularIncomeDefinitionRequest request)
        {
            return;
        }

        _isBusy = true;
        try
        {
            await IncomeService.UpdateRegularDefinitionAsync(request, CancellationToken.None);
            await LoadAsync();
            Snackbar.Add("Zapisano cykliczny wpływ.", Severity.Success);
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

    private async Task ToggleIncomeDefinitionActiveAsync(IncomeDefinitionRow definition)
    {
        _isBusy = true;

        try
        {
            await IncomeService.UpdateRegularDefinitionAsync(new UpdateRegularIncomeDefinitionRequest
            {
                Id = definition.Id,
                Name = definition.Name,
                Amount = definition.Amount,
                DayOfMonth = definition.DayOfMonth,
                AccountId = definition.AccountId,
                IsActive = !definition.IsActive
            }, CancellationToken.None);

            await LoadAsync();
            Snackbar.Add(definition.IsActive ? "Zdezaktywowano cykliczny wpływ." : "Aktywowano cykliczny wpływ.", Severity.Success);
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

    private async Task DeleteIncomeDefinitionPermanentlyAsync(IncomeDefinitionRow definition)
    {
        if (!await ConfirmAsync("Usuń wpływ", $"Usunąć cykliczny wpływ '{definition.Name}' na stałe? Tej operacji nie można cofnąć."))
        {
            return;
        }

        _isBusy = true;

        try
        {
            await IncomeService.DeleteRegularDefinitionPermanentlyAsync(
                new DeleteRegularIncomeDefinitionRequest { Id = definition.Id },
                CancellationToken.None);

            await LoadAsync();
            Snackbar.Add("Usunięto cykliczny wpływ na stałe.", Severity.Success);
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

    private async Task AddRegularIncomeToCurrentMonthAsync(int definitionId)
    {
        _isBusy = true;

        try
        {
            var wasAdded = await IncomeService.AddRegularDefinitionToMonthAsync(
                definitionId,
                _currentYear,
                _currentMonth,
                CancellationToken.None);

            Snackbar.Add(
                wasAdded
                    ? "Dodano cykliczny wpływ do aktualnego miesiąca."
                    : "Wpływ cykliczny dla aktualnego miesiąca już istnieje.",
                wasAdded ? Severity.Success : Severity.Info);

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

    private async Task CreateExpenseDefinitionAsync()
    {
        ClearCreateExpenseErrors();
        if (!ValidateCreateExpense())
        {
            return;
        }

        _isBusy = true;

        try
        {
            _newExpenseDefinition.Name = _newExpenseDefinition.Name.Trim();
            await ExpenseService.CreateRegularExpenseDefinitionAsync(_newExpenseDefinition, CancellationToken.None);
            var categoryId = _newExpenseDefinition.CategoryId;
            _newExpenseDefinition = new CreateRegularExpenseDefinitionRequest
            {
                Name = string.Empty,
                CategoryId = categoryId,
                ShowRemainingInUI = true
            };
            _createExpenseAmountInput = string.Empty;

            await LoadAsync();
            Snackbar.Add("Dodano cykliczny wydatek.", Severity.Success);
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

    private bool IsFirstExpenseDefinition(int definitionId)
    {
        return _expenseRows.Count == 0 || _expenseRows[0].Id == definitionId;
    }

    private bool IsLastExpenseDefinition(int definitionId)
    {
        return _expenseRows.Count == 0 || _expenseRows[^1].Id == definitionId;
    }

    private async Task MoveExpenseDefinitionAsync(int definitionId, int direction)
    {
        var ordered = _expenseRows.ToList();
        var index = ordered.FindIndex(x => x.Id == definitionId);

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
            await ExpenseService.ReorderRegularExpenseDefinitionsAsync(new ReorderRegularExpenseDefinitionsRequest
            {
                DefinitionIds = ordered.Select(x => x.Id).ToList()
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

    private async Task OpenExpenseEditDialogAsync(ExpenseDefinitionRow definition)
    {
        var parameters = new DialogParameters
        {
            [nameof(RecurringExpenseDefinitionDialog.Model)] = new UpdateRegularExpenseDefinitionRequest
            {
                Id = definition.Id,
                Name = definition.Name,
                CategoryId = definition.CategoryId,
                TagId = definition.TagId,
                Amount = definition.Amount,
                IsActive = definition.IsActive,
                ShowRemainingInUI = definition.ShowRemainingInUI
            },
            [nameof(RecurringExpenseDefinitionDialog.Categories)] = _categories
        };

        var options = new DialogOptions
        {
            MaxWidth = MaxWidth.Small,
            FullWidth = true,
            CloseButton = true
        };

        var dialog = await DialogService.ShowAsync<RecurringExpenseDefinitionDialog>("Edytuj wydatek", parameters, options);
        var result = await dialog.Result;
        if (result is null || result.Canceled || result.Data is not UpdateRegularExpenseDefinitionRequest request)
        {
            return;
        }

        _isBusy = true;
        try
        {
            await ExpenseService.UpdateRegularExpenseDefinitionAsync(request, CancellationToken.None);
            await LoadAsync();
            Snackbar.Add("Zapisano cykliczny wydatek.", Severity.Success);
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

    private async Task ToggleExpenseDefinitionActiveAsync(ExpenseDefinitionRow definition)
    {
        _isBusy = true;

        try
        {
            await ExpenseService.UpdateRegularExpenseDefinitionAsync(new UpdateRegularExpenseDefinitionRequest
            {
                Id = definition.Id,
                Name = definition.Name,
                CategoryId = definition.CategoryId,
                TagId = definition.TagId,
                Amount = definition.Amount,
                IsActive = !definition.IsActive,
                ShowRemainingInUI = definition.ShowRemainingInUI
            }, CancellationToken.None);

            await LoadAsync();
            Snackbar.Add(definition.IsActive ? "Zdezaktywowano cykliczny wydatek." : "Aktywowano cykliczny wydatek.", Severity.Success);
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

    private async Task DeleteExpenseDefinitionPermanentlyAsync(ExpenseDefinitionRow definition)
    {
        if (!await ConfirmAsync("Usuń wydatek", $"Usunąć cykliczny wydatek '{definition.Name}' na stałe? Tej operacji nie można cofnąć."))
        {
            return;
        }

        _isBusy = true;

        try
        {
            await ExpenseService.DeleteRegularExpenseDefinitionPermanentlyAsync(
                new DeleteRegularExpenseDefinitionRequest { Id = definition.Id },
                CancellationToken.None);

            await LoadAsync();
            Snackbar.Add("Usunięto cykliczny wydatek na stałe.", Severity.Success);
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

    private async Task AddRegularExpenseToCurrentMonthAsync(int definitionId)
    {
        _isBusy = true;

        try
        {
            var wasAdded = await ExpenseService.AddRegularExpenseDefinitionToMonthAsync(
                definitionId,
                _currentYear,
                _currentMonth,
                CancellationToken.None);

            Snackbar.Add(
                wasAdded
                    ? "Dodano cykliczny wydatek do aktualnego miesiąca."
                    : "Wydatek cykliczny dla aktualnego miesiąca już istnieje.",
                wasAdded ? Severity.Success : Severity.Info);

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

    private async Task<bool> ConfirmAsync(string title, string message)
    {
        var parameters = new DialogParameters
        {
            [nameof(ConfirmDialog.Message)] = message
        };

        var dialog = await DialogService.ShowAsync<ConfirmDialog>(title, parameters);
        var result = await dialog.Result;
        return result is not null && !result.Canceled;
    }

    private bool ValidateCreateIncome()
    {
        if (string.IsNullOrWhiteSpace(_newIncomeDefinition.Name))
        {
            _createIncomeNameError = "Podaj nazwę wpływu.";
        }

        if (!LocalizedDecimalParser.TryParse(_createIncomeAmountInput, out var amount) || amount <= 0)
        {
            _createIncomeAmountError = "Podaj kwotę większą od zera, np. 100,25.";
        }
        else
        {
            _newIncomeDefinition.Amount = amount;
        }

        if (_newIncomeDefinition.DayOfMonth is < 1 or > 31)
        {
            _createIncomeDayError = "Dzień musi być w zakresie 1-31.";
        }

        if (_newIncomeDefinition.AccountId <= 0)
        {
            _createIncomeAccountError = "Wybierz konto.";
        }

        return HasNoErrors(_createIncomeNameError, _createIncomeAmountError, _createIncomeDayError, _createIncomeAccountError);
    }

    private bool ValidateCreateExpense()
    {
        if (string.IsNullOrWhiteSpace(_newExpenseDefinition.Name))
        {
            _createExpenseNameError = "Podaj nazwę wydatku.";
        }

        if (!LocalizedDecimalParser.TryParse(_createExpenseAmountInput, out var amount) || amount <= 0)
        {
            _createExpenseAmountError = "Podaj kwotę większą od zera, np. 100,25.";
        }
        else
        {
            _newExpenseDefinition.Amount = amount;
        }

        if (_newExpenseDefinition.CategoryId <= 0)
        {
            _createExpenseCategoryError = "Wybierz kategorię.";
        }

        return HasNoErrors(_createExpenseNameError, _createExpenseAmountError, _createExpenseCategoryError);
    }

    private void ClearCreateIncomeErrors()
    {
        _createIncomeNameError = null;
        _createIncomeAmountError = null;
        _createIncomeDayError = null;
        _createIncomeAccountError = null;
    }

    private void ClearCreateExpenseErrors()
    {
        _createExpenseNameError = null;
        _createExpenseAmountError = null;
        _createExpenseCategoryError = null;
    }

    private static bool HasNoErrors(params string?[] errors)
    {
        return errors.All(string.IsNullOrWhiteSpace);
    }

    private string FormatMoney(decimal value)
    {
        return $"{value.ToString("0.00", _culture)} PLN";
    }

    private void MarkDirtyStatePristine()
    {
        _dirtyResetVersion++;
        _dirtyStateMonitor?.Reset(GetDirtyState());
    }

    private object GetDirtyState() => new
    {
        NewIncome = new
        {
            _newIncomeDefinition.Name,
            Amount = _createIncomeAmountInput,
            _newIncomeDefinition.DayOfMonth,
            _newIncomeDefinition.AccountId
        },
        NewExpense = new
        {
            _newExpenseDefinition.Name,
            _newExpenseDefinition.CategoryId,
            _newExpenseDefinition.TagId,
            Amount = _createExpenseAmountInput,
            _newExpenseDefinition.ShowRemainingInUI
        }
    };

    private sealed record RecurringOverviewModel(
        decimal ActiveIncomeAmount = 0,
        decimal ActiveExpenseAmount = 0,
        decimal NetRecurringAmount = 0,
        int ActiveIncomeCount = 0,
        int ActiveExpenseCount = 0);

    private sealed record IncomeDefinitionRow(
        int Id,
        string Name,
        decimal Amount,
        int DayOfMonth,
        int AccountId,
        string AccountName,
        bool IsActive);

    private sealed record ExpenseDefinitionRow(
        int Id,
        int Order,
        string Name,
        int CategoryId,
        string CategoryName,
        int? TagId,
        string? TagName,
        decimal Amount,
        bool IsActive,
        bool ShowRemainingInUI);

    private sealed record LoanRecurringItem(string LoanName, string Label, decimal Amount, bool IsPaid);
}
