using HouseholdBudgetMate.Abstractions.Contracts.Categories.Dto;
using HouseholdBudgetMate.Abstractions.Contracts.Categories.Requests;

namespace HouseholdBudgetMate.Abstractions.Interfaces;

public interface ICategoryService
{
    Task<IReadOnlyList<CategoryDto>> GetAllAsync(CancellationToken cancellationToken);

    Task<CategoryDto> CreateCategoryAsync(CreateCategoryRequest request, CancellationToken cancellationToken);
    Task<TagDto> CreateTagAsync(CreateTagRequest request, CancellationToken cancellationToken);

    Task<CategoryDeletionImpactDto> GetCategoryDeletionImpactAsync(int categoryId, CancellationToken cancellationToken);
    Task<TagDeletionImpactDto> GetTagDeletionImpactAsync(int tagId, CancellationToken cancellationToken);

    Task<CategoryDto> UpdateCategoryAsync(UpdateCategoryRequest request, CancellationToken cancellationToken);
    Task<TagDto> UpdateTagAsync(UpdateTagRequest request, CancellationToken cancellationToken);

    Task DeleteCategoryAsync(DeleteCategoryRequest request, CancellationToken cancellationToken);
    Task DeleteTagAsync(DeleteTagRequest request, CancellationToken cancellationToken);
}
