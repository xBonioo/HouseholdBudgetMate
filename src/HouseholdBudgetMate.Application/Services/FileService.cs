using HouseholdBudgetMate.Abstractions.Contracts.Files.Dto;
using HouseholdBudgetMate.Abstractions.Enums;
using HouseholdBudgetMate.Abstractions.Extensions;
using HouseholdBudgetMate.Abstractions.Interfaces;
using HouseholdBudgetMate.Application.Shared;

namespace HouseholdBudgetMate.Application.Services;

public sealed class FileService(IStoragePathProvider storageProvider) : IFileService
{
    private const long MaxFileSize = 10 * 1024 * 1024;

    private readonly string _baseFolder = storageProvider.GetBaseFolder()
                                          ?? throw new ArgumentNullException(nameof(storageProvider));

    public string GetFolderPath(int entityId, FileContextType contextType)
        => Path.Combine(_baseFolder, $"{contextType.GetDisplayName()}-{entityId}");

    public async Task<IReadOnlyList<string>> SaveFileAsync(
        int entityId,
        FileUploadDto file,
        FileContextType contextType,
        CancellationToken cancellationToken)
    {
        var errors = ValidateFile(file);
        if (errors.Count > 0)
            return errors;

        var folderPath = EnsureFolderExists(entityId, contextType);

        var safeFileName = Path.GetFileName(file.Name);
        var filePath = GetUniqueFilePath(folderPath, safeFileName);

        try
        {
            await using var stream = new FileStream(
                filePath,
                FileMode.CreateNew,
                FileAccess.Write,
                FileShare.None,
                bufferSize: 81920,
                useAsync: true);

            await file.Content.CopyToAsync(stream, cancellationToken);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (IOException ex)
        {
            errors.Add($"Błąd zapisu pliku {file.Name}: {ex.Message}");
        }
        catch (Exception)
        {
            errors.Add($"Nieoczekiwany błąd podczas zapisu pliku {file.Name}.");
        }

        return errors;
    }


    private static List<string> ValidateFile(FileUploadDto? file)
    {
        var errors = new List<string>();

        if (file is null)
        {
            errors.Add("Brak pliku.");
            return errors;
        }

        switch (file.Size)
        {
            case <= 0:
                errors.Add($"Plik {file.Name} jest pusty.");
                return errors;
            case > MaxFileSize:
                errors.Add($"Plik {file.Name} jest za duży. Maksymalny rozmiar to 10 MB.");
                return errors;
        }

        var extension = Path.GetExtension(file.Name).ToLowerInvariant();
        if (string.IsNullOrWhiteSpace(extension))
        {
            errors.Add("Plik nie posiada rozszerzenia.");
            return errors;
        }

        if (!Constants.AllowedExtensions.Contains(extension))
        {
            errors.Add($"Niedozwolony typ pliku: {extension}");
        }

        return errors;
    }

    private string EnsureFolderExists(int entityId, FileContextType contextType)
    {
        var path = GetFolderPath(entityId, contextType);

        if (!Directory.Exists(path))
            Directory.CreateDirectory(path);

        return path;
    }

    private static string GetUniqueFilePath(string folderPath, string fileName)
    {
        var extension = Path.GetExtension(fileName);
        var name = Path.GetFileNameWithoutExtension(fileName);

        var counter = 0;
        string path;

        do
        {
            var suffix = counter == 0 ? string.Empty : $"_{counter}";
            path = Path.Combine(folderPath, $"{name}{suffix}{extension}");
            counter++;
        }
        while (File.Exists(path));

        return path;
    }
}