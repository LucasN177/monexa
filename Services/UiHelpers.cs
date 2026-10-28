using System.Globalization;
using monexa.Models.Enums;
using MudBlazor;

namespace monexa.Services;

/// <summary>Einheitliche Texte, Icons, Farben und Formatierungen für die gesamte App.</summary>
public static class UiHelpers
{
    public static readonly CultureInfo De = new("de-DE");

    // ── Formatierung ──────────────────────────────────────────────────────────

    public static string Money(decimal value) => value.ToString("C2", De);

    public static string MoneyShort(decimal value) => value.ToString("C0", De);

    public static string SignedMoney(decimal value) =>
        (value > 0 ? "+ " : value < 0 ? "- " : "") + Math.Abs(value).ToString("C2", De);

    public static string Date(DateTime date) => date.ToString("dd.MM.yyyy", De);

    public static string MonthYear(DateTime date) => date.ToString("MMMM yyyy", De);

    public static string Percent(double value, string format = "0") => value.ToString(format, De) + " %";

    // ── Icons ────────────────────────────────────────────────────────────────

    public static readonly IReadOnlyDictionary<string, string> CategoryIcons = new Dictionary<string, string>
    {
        { "Shopping", Icons.Material.Filled.ShoppingCart },
        { "Lebensmittel", Icons.Material.Filled.LocalGroceryStore },
        { "Essen & Trinken", Icons.Material.Filled.Restaurant },
        { "Transport", Icons.Material.Filled.DirectionsCar },
        { "Wohnen", Icons.Material.Filled.Home },
        { "Nebenkosten", Icons.Material.Filled.Receipt },
        { "Unterhaltung", Icons.Material.Filled.Movie },
        { "Freizeit", Icons.Material.Filled.SportsEsports },
        { "Gesundheit", Icons.Material.Filled.LocalHospital },
        { "Bildung", Icons.Material.Filled.School },
        { "Gehalt", Icons.Material.Filled.AttachMoney },
        { "Geschäft", Icons.Material.Filled.Work },
        { "Geschenk", Icons.Material.Filled.CardGiftcard },
        { "Reisen", Icons.Material.Filled.Flight },
        { "Sport", Icons.Material.Filled.FitnessCenter },
        { "Haustiere", Icons.Material.Filled.Pets },
        { "Kleidung", Icons.Material.Filled.Checkroom },
        { "Elektronik", Icons.Material.Filled.Devices },
        { "Versicherung", Icons.Material.Filled.Security },
        { "Telefon", Icons.Material.Filled.Phone },
        { "Internet", Icons.Material.Filled.Wifi },
        { "Streaming", Icons.Material.Filled.Subscriptions },
        { "Investition", Icons.Material.Filled.TrendingUp },
        { "Sparen", Icons.Material.Filled.Savings },
        { "Steuern", Icons.Material.Filled.AccountBalance },
        { "Auto", Icons.Material.Filled.DirectionsCarFilled },
        { "Urlaub", Icons.Material.Filled.BeachAccess },
        { "Notgroschen", Icons.Material.Filled.HealthAndSafety },
        { "Sonstiges", Icons.Material.Filled.MoreHoriz }
    };

    public static readonly IReadOnlyDictionary<string, string> ProjectIcons = new Dictionary<string, string>
    {
        { "Privat", Icons.Material.Filled.Home },
        { "Geschäftlich", Icons.Material.Filled.Business },
        { "Investments", Icons.Material.Filled.Savings },
        { "Familie", Icons.Material.Filled.FamilyRestroom },
        { "Verein", Icons.Material.Filled.Groups },
        { "Immobilie", Icons.Material.Filled.Apartment },
        { "Projekt", Icons.Material.Filled.Folder }
    };

    public static string Icon(string? key) =>
        key != null && CategoryIcons.TryGetValue(key, out var icon) ? icon : Icons.Material.Filled.Category;

    public static string ProjectIcon(string? key) =>
        key != null && ProjectIcons.TryGetValue(key, out var icon) ? icon : Icons.Material.Filled.Folder;

    // ── Farben ───────────────────────────────────────────────────────────────

    public static readonly IReadOnlyList<(string Key, string Label, Color Color)> ColorOptions = new List<(string, string, Color)>
    {
        ("Primary", "Blau", Color.Primary),
        ("Secondary", "Lila", Color.Secondary),
        ("Tertiary", "Türkis", Color.Tertiary),
        ("Success", "Grün", Color.Success),
        ("Info", "Hellblau", Color.Info),
        ("Warning", "Orange", Color.Warning),
        ("Error", "Rot", Color.Error),
        ("Dark", "Dunkel", Color.Dark)
    };

    public static Color ParseColor(string? key) =>
        Enum.TryParse<Color>(key, true, out var color) ? color : Color.Primary;

    /// <summary>Ampelfarbe für eine Auslastung in Prozent.</summary>
    public static Color UsageColor(double percent) =>
        percent >= 100 ? Color.Error : percent >= 80 ? Color.Warning : Color.Success;

    // ── Kontotypen ───────────────────────────────────────────────────────────

    public static string AccountTypeText(AccountType type) => type switch
    {
        AccountType.Girokonto => "Girokonto",
        AccountType.Sparkonto => "Sparkonto",
        AccountType.Tagesgeld => "Tagesgeldkonto",
        AccountType.Kreditkarte => "Kreditkarte",
        AccountType.Depot => "Depot",
        _ => type.ToString()
    };

    public static string AccountTypeIcon(AccountType type) => type switch
    {
        AccountType.Girokonto => Icons.Material.Filled.AccountBalance,
        AccountType.Sparkonto => Icons.Material.Filled.Savings,
        AccountType.Tagesgeld => Icons.Material.Filled.TrendingUp,
        AccountType.Kreditkarte => Icons.Material.Filled.CreditCard,
        AccountType.Depot => Icons.Material.Filled.ShowChart,
        _ => Icons.Material.Filled.AccountBalanceWallet
    };

    public static Color AccountTypeColor(AccountType type) => type switch
    {
        AccountType.Girokonto => Color.Primary,
        AccountType.Sparkonto => Color.Success,
        AccountType.Tagesgeld => Color.Info,
        AccountType.Kreditkarte => Color.Warning,
        AccountType.Depot => Color.Secondary,
        _ => Color.Default
    };

    // ── Transaktionstypen ────────────────────────────────────────────────────

    public static Color TransactionTypeColor(TransactionType type) => type switch
    {
        TransactionType.Einnahme => Color.Success,
        TransactionType.Ausgabe => Color.Error,
        TransactionType.Umbuchung => Color.Info,
        _ => Color.Default
    };

    public static string TransactionTypeIcon(TransactionType type) => type switch
    {
        TransactionType.Einnahme => Icons.Material.Filled.TrendingUp,
        TransactionType.Ausgabe => Icons.Material.Filled.TrendingDown,
        TransactionType.Umbuchung => Icons.Material.Filled.SwapHoriz,
        _ => Icons.Material.Filled.Receipt
    };

    public static string CategoryTypeText(CategoryType type) => type == CategoryType.Einnahme ? "Einnahme" : "Ausgabe";

    public static Color CategoryTypeColor(CategoryType type) => type == CategoryType.Einnahme ? Color.Success : Color.Error;

    // ── Perioden ─────────────────────────────────────────────────────────────

    public static string PeriodText(BudgetPeriod period) => period switch
    {
        BudgetPeriod.Woechentlich => "Wöchentlich",
        BudgetPeriod.Monatlich => "Monatlich",
        BudgetPeriod.Jaehrlich => "Jährlich",
        _ => period.ToString()
    };

    public static string IntervalText(RecurrenceInterval interval) => interval switch
    {
        RecurrenceInterval.Woechentlich => "Wöchentlich",
        RecurrenceInterval.Monatlich => "Monatlich",
        RecurrenceInterval.Quartalsweise => "Quartalsweise",
        RecurrenceInterval.Halbjaehrlich => "Halbjährlich",
        RecurrenceInterval.Jaehrlich => "Jährlich",
        _ => interval.ToString()
    };

    public static string RoleText(ProjectRole role) => role switch
    {
        ProjectRole.Leser => "Leser",
        ProjectRole.Bearbeiter => "Bearbeiter",
        ProjectRole.Admin => "Admin",
        _ => role.ToString()
    };
}
