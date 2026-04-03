using HouseholdBudgetMate.Abstractions.Enums;

namespace HouseholdBudgetMate.Abstractions.Models;

public class BaseResponse<T> : IServiceResult
{
    public ServiceResultStatusCode ResponseCode { get; set; }
    public List<BaseResponseError>? BaseResponseError { get; set; }
    public string? Message { get; set; }
    public T? Data { get; set; }
}