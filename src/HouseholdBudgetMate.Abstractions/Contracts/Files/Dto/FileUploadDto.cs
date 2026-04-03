namespace HouseholdBudgetMate.Abstractions.Contracts.Files.Dto;

public sealed class FileUploadDto
{
    public string Name { get; set; } = null!;
    public long Size { get; set; }
    public Stream Content { get; set; } = null!;
}