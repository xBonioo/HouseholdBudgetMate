using HouseholdBudgetMate.Abstractions.Interfaces;
using HouseholdBudgetMate.Application.Shared;

namespace HouseholdBudgetMate.Web;

public class WebStoragePathProvider : IStoragePathProvider
{
    private readonly string _baseFolder;

    public WebStoragePathProvider()
    {
        var appDataDirectory = WritableAppDataPathResolver.Resolve("HouseholdBudgetMate");
        _baseFolder = Path.Combine(appDataDirectory, Constants.FolderNameFiles);

        if (!Directory.Exists(_baseFolder)) Directory.CreateDirectory(_baseFolder);
    }

    public string GetBaseFolder()
    {
        return _baseFolder;
    }
}