using HouseholdBudgetMate.Abstractions.Contracts.Audit.Dto;
using HouseholdBudgetMate.Abstractions.Contracts.Audit.Requests;

namespace HouseholdBudgetMate.Abstractions.Interfaces;

public interface IAuditService
{
    Task<IReadOnlyList<AuditLogDto>> SearchAsync(SearchAuditLogsRequest request, CancellationToken cancellationToken);
}
