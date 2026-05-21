namespace HouseholdBudgetMate.Web;

internal static class WritableAppDataPathResolver
{
    public static string Resolve(string applicationFolderName)
    {
        var candidateRoots = new[]
        {
            Environment.GetEnvironmentVariable("HOUSEHOLDBUDGETMATE_DATA_DIR"),
            Environment.GetEnvironmentVariable("LOCALAPPDATA"),
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
            Path.GetTempPath()
        };

        foreach (var candidateRoot in candidateRoots)
        {
            if (string.IsNullOrWhiteSpace(candidateRoot))
            {
                continue;
            }

            try
            {
                var fullRoot = Path.GetFullPath(candidateRoot);
                var appDirectory = Path.Combine(fullRoot, applicationFolderName);
                Directory.CreateDirectory(appDirectory);
                return appDirectory;
            }
            catch
            {
                // Try next writable location.
            }
        }

        throw new InvalidOperationException("Nie znaleziono katalogu z prawem zapisu dla danych aplikacji.");
    }
}
