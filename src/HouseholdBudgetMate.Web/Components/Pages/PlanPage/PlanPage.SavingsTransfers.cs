using HouseholdBudgetMate.Abstractions.Contracts.Expenses.Dto;
using HouseholdBudgetMate.Abstractions.Contracts.Expenses.Requests;
using MudBlazor;

namespace HouseholdBudgetMate.Web.Components.Pages.PlanPage;

public partial class PlanPage
{
    private async Task CreateSavingsTransferAsync()
    {
        if (!EnsureMonthEditable())
        {
            return;
        }

        if (!TryParseAmountOrWarn(_newSavingsTransferAmountInput, out var savingsAmount))
        {
            return;
        }

        _newSavingsTransfer.Year = Year;
        _newSavingsTransfer.Month = Month;
        _newSavingsTransfer.Amount = savingsAmount;

        if (await ExecutePostSaveAsync(
            () => ExpenseService.CreateMonthSavingsTransferItemAsync(_newSavingsTransfer, CancellationToken.None),
            PostSaveRefreshMode.FullReload,
            () =>
            {
                _newSavingsTransfer.Amount = 0;
                _newSavingsTransferAmountInput = FormatDecimalInput(0);
                _newSavingsTransfer.TransferDate = new DateOnly(Year, Month, 1);
            }))
        {
            Snackbar.Add("Dodano pozycję oszczędności.", Severity.Success);
        }
    }

    private void StartSavingsTransferEdit(MonthSavingsTransferItemDto item)
    {
        _editSavingsTransfer = new UpdateMonthSavingsTransferItemRequest
        {
            Id = item.Id,
            Amount = item.Amount,
            TransferDate = item.TransferDate
        };
        _editSavingsTransferDate = item.TransferDate;
        _editSavingsTransferAmountInput = FormatDecimalInput(item.Amount);
        MarkDirtyStatePristine();
    }

    private void CancelSavingsTransferEdit()
    {
        _editSavingsTransfer = null;
        _editSavingsTransferDate = new DateOnly(Year, Month, 1);
        _editSavingsTransferAmountInput = FormatDecimalInput(0);
        MarkDirtyStatePristine();
    }

    private async Task SaveSavingsTransferEditAsync()
    {
        if (!EnsureMonthEditable() || _editSavingsTransfer is null)
        {
            return;
        }

        if (!TryParseAmountOrWarn(_editSavingsTransferAmountInput, out var savingsAmount))
        {
            return;
        }

        _editSavingsTransfer.Amount = savingsAmount;
        _editSavingsTransfer.TransferDate = _editSavingsTransferDate;

        if (await ExecutePostSaveAsync(
            () => ExpenseService.UpdateMonthSavingsTransferItemAsync(_editSavingsTransfer, CancellationToken.None),
            PostSaveRefreshMode.FullReload,
            () =>
            {
                _editSavingsTransfer = null;
                _editSavingsTransferAmountInput = FormatDecimalInput(0);
            }))
        {
            Snackbar.Add("Zapisano pozycję oszczędności.", Severity.Success);
        }
    }

    private async Task DeleteSavingsTransferAsync(int id)
    {
        if (!EnsureMonthEditable())
        {
            return;
        }

        var confirmation = await ConfirmAsync("Czy na pewno chcesz usunąć?");
        if (!confirmation)
        {
            return;
        }

        if (await ExecutePostSaveAsync(
            () => ExpenseService.DeleteMonthSavingsTransferItemAsync(
                new DeleteMonthSavingsTransferItemRequest { Id = id }, CancellationToken.None),
            PostSaveRefreshMode.FullReload))
        {
            Snackbar.Add("Usunięto pozycję oszczędności.", Severity.Success);
        }
    }
}

