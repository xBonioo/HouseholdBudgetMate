using HouseholdBudgetMate.Abstractions.Enums;

namespace HouseholdBudgetMate.Abstractions;

/// <summary>
/// Interface to help with implementing Operation Result Pattern.
/// </summary>
public interface IServiceResult
{
    public ServiceResultStatusCode ResponseCode { get; set; }
}