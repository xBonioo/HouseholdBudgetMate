namespace HouseholdBudgetMate.Application.Shared;

public static class Constants
{
    public const string Title = "Household Budget Mate";
    public const string Favicon = "/images/favicon.ico";

    public const string RequestPathFiles = "/files";
    public const string FolderNameFiles = "files";
    
    private const string Pdf = ".pdf";
    private const string Jpg = ".jpg";
    private const string Jpeg = ".jpeg";
    private const string Png = ".png";
    private const string Doc = ".doc";
    private const string Docx = ".docx";
    private const string Xls = ".xls";
    private const string Xlsx = ".xlsx";

    public static readonly IReadOnlyCollection<string> AllowedExtensions =
        [Pdf, Jpg, Jpeg, Png, Doc, Docx, Xls, Xlsx];
    
    public const int ClaimsCacheDurationInMinutes = 30;
}