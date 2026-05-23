namespace HouseholdBudgetMate.Abstractions.Contracts.Audit.Requests;

public class SearchAuditLogsRequest
{
    public string? EntityType { get; set; }
    public string? Operation { get; set; }
    public string? UserId { get; set; }
    public DateTime? FromUtc { get; set; }
    public DateTime? ToUtc { get; set; }
}
