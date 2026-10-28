namespace monexa.Services;

/// <summary>Konfiguration aus <c>wwwroot/appsettings.json</c> → Abschnitt "Supabase".</summary>
public class SupabaseSettings
{
    public string Url { get; set; } = "";

    /// <summary>Öffentlicher anon- bzw. Publishable-Key. Niemals den service_role/secret Key eintragen.</summary>
    public string AnonKey { get; set; } = "";

    public bool IsConfigured => !string.IsNullOrWhiteSpace(Url) && !string.IsNullOrWhiteSpace(AnonKey);
}
