using FluentAssertions;
using HouseholdBudgetMate.Web.Services;

namespace HouseholdBudgetMate.Tests.Tests.Services;

public sealed class UnsavedChangesTrackerTests
{
    [Fact]
    public void HasUnsavedChanges_ReturnsFalse_WhenNoRegistrationsExist()
    {
        var tracker = new UnsavedChangesTracker();

        tracker.HasUnsavedChanges.Should().BeFalse();
        tracker.DirtyLabels.Should().BeEmpty();
    }

    [Fact]
    public void HasUnsavedChanges_ReturnsTrue_WhenAnyRegistrationIsDirty()
    {
        var tracker = new UnsavedChangesTracker();
        var isDirty = false;

        using var registration = tracker.Register("formularz", () => isDirty);
        isDirty = true;

        tracker.HasUnsavedChanges.Should().BeTrue();
        tracker.DirtyLabels.Should().ContainSingle().Which.Should().Be("formularz");
    }

    [Fact]
    public void DisposedRegistration_IsIgnored()
    {
        var tracker = new UnsavedChangesTracker();
        var registration = tracker.Register("formularz", () => true);

        registration.Dispose();

        tracker.HasUnsavedChanges.Should().BeFalse();
        tracker.DirtyLabels.Should().BeEmpty();
    }
}
