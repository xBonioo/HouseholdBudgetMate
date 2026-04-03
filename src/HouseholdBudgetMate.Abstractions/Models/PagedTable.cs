namespace HouseholdBudgetMate.Abstractions.Models;

/// <summary>
///     Generic class for inheritance in paged tables
/// </summary>
/// <typeparam name="T">Type of the rows contained in paged table</typeparam>
public class PagedTable<T>
{
    public PagedTable(List<T> rows, int totalCount, int pageSize, int pageNumber)
    {
        Rows = rows;
        TotalCount = totalCount;
        ItemFrom = pageSize * (pageNumber - 1) + 1;
        ItemsTo = ItemFrom + pageSize - 1;
        TotalPages = (int)Math.Ceiling(totalCount / (double)pageSize);
    }

    /// <summary>
    ///     List of rows matching criteria
    /// </summary>
    public List<T> Rows { get; set; }
    
    public int TotalPages { get; set; }
    
    public int ItemFrom { get; set; }
    public int ItemsTo { get; set; }
    
    /// <summary>
    ///     Total elements count
    /// </summary>
    public int TotalCount { get; set; }
    
    /// <summary>
    ///     Column used to sorting
    /// </summary>
    public string? SortBy { get; set; }
}