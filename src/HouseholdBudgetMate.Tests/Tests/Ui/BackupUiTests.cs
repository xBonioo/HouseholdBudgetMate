using FluentAssertions;

namespace HouseholdBudgetMate.Tests.Tests.Ui;

public sealed class BackupUiTests
{
    [Fact]
    public void AdminBackup_Should_Surface_Backup_Restore_And_Schedule_Guardrails()
    {
        var page = ReadRepoFile("src/HouseholdBudgetMate.Web/Components/Pages/AdminBackup.razor");

        page.Should().Contain("@page \"/admin/backup\"");
        page.Should().Contain("IsAdminSession");
        page.Should().Contain("NavigationManager.NavigateTo(\"/\")");
        page.Should().Contain("Wrażliwy plik backupu");
        page.Should().Contain("plain JSON backup can contain household financial data");
        page.Should().Contain("Eksport CSV");
        page.Should().Contain("Backup JSON");
        page.Should().Contain("Przywracanie backupu");
        page.Should().Contain("automatic pre-restore backup copy");
        page.Should().Contain("backup-drop-zone");
        page.Should().Contain("Przeciągnij plik backupu JSON tutaj");
        page.Should().Contain("RESTORE BACKUP");
        page.Should().Contain("Restore preview counts");
        page.Should().Contain("Co przywrócić z backupu");
        page.Should().Contain("Profil / budżet");
        page.Should().Contain("Dane budżetowe");
        page.Should().Contain("BuildRestoreUserSections");
        page.Should().Contain("UserSections = BuildRestoreUserSections()");
        page.Should().Contain("BuildRestoreSections");
        page.Should().Contain("Sections = BuildRestoreSections()");
        page.Should().Contain("Harmonogram backupu");
        page.Should().Contain("Harmonogram działa tylko wtedy, gdy proces aplikacji jest uruchomiony");
        page.Should().Contain("Ścieżka backupu");
        page.Should().Contain("Częstotliwość");
        page.Should().Contain("Lokalny czas HH:mm");
        page.Should().Contain("Run backup now");
        page.Should().Contain("Profiles and PIN hashes");
        page.Should().Contain("BackupService.RestoreBackupAsync");
        page.Should().Contain("BackupService.PreviewRestoreAsync");
        page.Should().Contain("BackupService.RunScheduledBackupNowAsync");
        page.Should().Contain("OpenBackupFolderPickerAsync");
        page.Should().Contain("BackupFolderPickerDialog");
        page.Should().Contain("Icons.Material.Filled.FolderOpen");
        page.Should().Contain("Immediate=\"true\"");
        page.Should().Contain("Disabled=\"@(!CanRestoreBackup)\"");
        page.Should().Contain("HasValidRestoreRequest");
        page.Should().Contain("IsRestoreConfirmationValid");
        page.Should().Contain("StringComparison.OrdinalIgnoreCase");
        page.Should().Contain("ConfirmationPhrase = _restoreConfirmation.Trim()");
        page.Should().Contain("Snackbar.Add(message, Severity.Success)");
        page.Should().Contain("Snackbar.Add(message, Severity.Error)");
        page.Should().Contain("registerBackupDropZone");
        page.Should().Contain("DisposeAsync");

        var css = ReadRepoFile("src/HouseholdBudgetMate.Web/Components/Pages/AdminBackup.razor.css");
        css.Should().Contain(".backup-drop-zone-input");
        css.Should().Contain("z-index: 2");
        css.Should().Contain("pointer-events: none");
        css.Should().Contain("backup-drop-zone-active");

        var js = ReadRepoFile("src/HouseholdBudgetMate.Web/wwwroot/js/backup-drop-zone.js");
        js.Should().Contain("registerBackupDropZone");
        js.Should().Contain("dragover");
        js.Should().Contain("new DataTransfer()");
        js.Should().Contain("input.dispatchEvent(new Event('change'");

        var folderPicker = ReadRepoFile("src/HouseholdBudgetMate.Web/Components/Dialogs/BackupFolderPickerDialog.razor");
        folderPicker.Should().Contain("Directory.GetLogicalDrives()");
        folderPicker.Should().Contain("EnumerateDirectories");
        folderPicker.Should().Contain("DialogResult.Ok(_currentPath)");
        folderPicker.Should().Contain("Folder jeszcze nie istnieje");
    }

    [Fact]
    public void MainLayout_Should_Expose_Backup_Navigation_Only_For_Admin_Sessions()
    {
        var layout = ReadRepoFile("src/HouseholdBudgetMate.Web/Components/Layout/MainLayout.razor");

        layout.Should().Contain("Href=\"/admin/backup\"");
        layout.Should().Contain("Label=\"Admin\"");
        layout.Should().Contain("GetNavClass(\"/admin\")");
        layout.Should().Contain("Href=\"/admin/config\"");

        var desktopIndex = layout.IndexOf("Href=\"/admin/backup\"", StringComparison.Ordinal);
        var mobileIndex = layout.IndexOf("Href=\"/admin/backup\"", desktopIndex + 1, StringComparison.Ordinal);
        var adminMenuIndex = layout.LastIndexOf("Label=\"Admin\"", desktopIndex, StringComparison.Ordinal);

        desktopIndex.Should().BeGreaterThan(0);
        mobileIndex.Should().BeGreaterThan(desktopIndex);
        adminMenuIndex.Should().BeGreaterThan(0);
        layout.LastIndexOf("@if (IsAdminSession)", desktopIndex, StringComparison.Ordinal).Should().BeGreaterThan(0);
        layout.LastIndexOf("@if (IsAdminSession)", mobileIndex, StringComparison.Ordinal).Should().BeGreaterThan(0);
    }

    private static string ReadRepoFile(string relativePath)
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            var candidate = Path.Combine(directory.FullName, relativePath);
            if (File.Exists(candidate))
            {
                return File.ReadAllText(candidate);
            }

            directory = directory.Parent;
        }

        throw new FileNotFoundException($"Could not find repository file '{relativePath}'.", relativePath);
    }
}
