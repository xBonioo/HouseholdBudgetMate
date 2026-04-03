using HouseholdBudgetMate.Abstractions.Contracts.Files.Dto;
using HouseholdBudgetMate.Abstractions.Enums;

namespace HouseholdBudgetMate.Abstractions.Interfaces;

public interface IFileService
{
    Task<IReadOnlyList<string>> SaveFileAsync(int entityId, FileUploadDto file, FileContextType contextType,
        CancellationToken cancellationToken);
}