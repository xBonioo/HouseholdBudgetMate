namespace HouseholdBudgetMate.Web.Services;

public sealed class UnsavedChangesTracker
{
    private readonly List<UnsavedChangesRegistration> _registrations = [];

    public event Action? Changed;

    public bool HasUnsavedChanges => _registrations.Any(registration => registration.IsDirty());

    public IReadOnlyList<string> DirtyLabels => _registrations
        .Where(registration => registration.IsDirty())
        .Select(registration => registration.Label)
        .Where(label => !string.IsNullOrWhiteSpace(label))
        .Distinct(StringComparer.Ordinal)
        .ToList();

    public IDisposable Register(string label, Func<bool> isDirty)
    {
        ArgumentNullException.ThrowIfNull(isDirty);

        var registration = new UnsavedChangesRegistration(this, label, isDirty);
        _registrations.Add(registration);
        NotifyChanged();

        return registration;
    }

    public void NotifyChanged()
    {
        Changed?.Invoke();
    }

    private void Unregister(UnsavedChangesRegistration registration)
    {
        if (_registrations.Remove(registration))
        {
            NotifyChanged();
        }
    }

    private sealed class UnsavedChangesRegistration(
        UnsavedChangesTracker owner,
        string label,
        Func<bool> isDirty) : IDisposable
    {
        private bool _disposed;

        public string Label { get; } = label;

        public bool IsDirty() => !_disposed && isDirty();

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            owner.Unregister(this);
        }
    }
}
