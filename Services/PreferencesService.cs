namespace monexa.Services;

/// <summary>Geräte-lokale Benutzereinstellungen (Theme, zuletzt gewähltes Projekt, …).</summary>
public class PreferencesService
{
    private const string Key = "monexa.preferences";

    private readonly BrowserStorage _storage;
    private Preferences _current;

    public event Action? Changed;

    public PreferencesService(BrowserStorage storage)
    {
        _storage = storage;
        _current = _storage.Get<Preferences>(Key) ?? new Preferences();
    }

    public bool DarkMode
    {
        get => _current.DarkMode;
        set { _current.DarkMode = value; Save(); }
    }

    public Guid? LastProjectId
    {
        get => _current.LastProjectId;
        set { _current.LastProjectId = value; Save(); }
    }

    public string DisplayName
    {
        get => _current.DisplayName;
        set { _current.DisplayName = value; Save(); }
    }

    /// <summary>Warnschwelle für Budgets in Prozent.</summary>
    public int BudgetWarningPercent
    {
        get => _current.BudgetWarningPercent;
        set { _current.BudgetWarningPercent = Math.Clamp(value, 1, 100); Save(); }
    }

    public bool AutoBookRecurring
    {
        get => _current.AutoBookRecurring;
        set { _current.AutoBookRecurring = value; Save(); }
    }

    private void Save()
    {
        _storage.Set(Key, _current);
        Changed?.Invoke();
    }

    private class Preferences
    {
        public bool DarkMode { get; set; } = true;
        public Guid? LastProjectId { get; set; }
        public string DisplayName { get; set; } = "";
        public int BudgetWarningPercent { get; set; } = 80;
        public bool AutoBookRecurring { get; set; } = true;
    }
}
