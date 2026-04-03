using HouseholdBudgetMate.Abstractions.Interfaces;
using HouseholdBudgetMate.Application.Shared;

namespace HouseholdBudgetMate.Web;

public class WebStoragePathProvider : IStoragePathProvider
{
    private readonly string _baseFolder;

    public WebStoragePathProvider(IWebHostEnvironment env)
    {
        _baseFolder = Path.Combine(env.ContentRootPath, "..", "..", Constants.FolderNameFiles);
        _baseFolder = Path.GetFullPath(_baseFolder);

        if (!Directory.Exists(_baseFolder)) Directory.CreateDirectory(_baseFolder);
    }

    public string GetBaseFolder()
    {
        return _baseFolder;
    }
}