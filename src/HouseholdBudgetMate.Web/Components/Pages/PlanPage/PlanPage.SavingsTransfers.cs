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

        try
        {
            if (!TryParseAmountOrWarn(_newSavingsTransferAmountInput, out var savingsAmount))
            {
                return;
            }

            _newSavingsTransfer.Year = Year;
            _newSavingsTransfer.Month = Month;
            _newSavingsTransfer.Amount = savingsAmount;

            await ExpenseService.CreateMonthSavingsTransferItemAsync(_newSavingsTransfer, CancellationToken.None);

            _newSavingsTransfer.Amount = 0;
            _newSavingsTransferAmountInput = FormatDecimalInput(0);
            _newSavingsTransfer.TransferDate = new DateOnly(Year, Month, 1);

            await LoadAsync();
            Snackbar.Add("Dodano pozycję oszczędności.", Severity.Success);
        }
        catch (Exception ex)
        {
            Snackbar.Add(ex.Message, Severity.Error);
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

        try
        {
            if (!TryParseAmountOrWarn(_editSavingsTransferAmountInput, out var savingsAmount))
            {
                return;
            }

            _editSavingsTransfer.Amount = savingsAmount;
            _editSavingsTransfer.TransferDate = _editSavingsTransferDate;
            await ExpenseService.UpdateMonthSavingsTransferItemAsync(_editSavingsTransfer, CancellationToken.None);

            _editSavingsTransfer = null;
            _editSavingsTransferAmountInput = FormatDecimalInput(0);
            await LoadAsync();
            Snackbar.Add("Zapisano pozycję oszczędności.", Severity.Success);
        }
        catch (Exception ex)
        {
            Snackbar.Add(ex.Message, Severity.Error);
        }
    }

    private async Task DeleteSavingsTransferAsync(int id)
    {
        if (!EnsureMonthEditable())
        {
            return;
        }

        try
        {
            await ExpenseService.DeleteMonthSavingsTransferItemAsync(
                new DeleteMonthSavingsTransferItemRequest { Id = id }, CancellationToken.None);
            await LoadAsync();
            Snackbar.Add("Usunięto pozycję oszczędności.", Severity.Success);
        }
        catch (Exception ex)
        {
            Snackbar.Add(ex.Message, Severity.Error);
        }
    }
}

