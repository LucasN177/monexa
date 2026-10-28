using System.Globalization;
using System.Text;
using System.Text.Json;
using Microsoft.JSInterop;
using monexa.Models;
using monexa.Models.Enums;

namespace monexa.Services;

/// <summary>CSV-Export/-Import von Buchungen und JSON-Backups eines Projekts.</summary>
public class ExportService
{
    private const char Separator = ';';
    private static readonly string[] CsvHeader =
        { "Datum", "Beschreibung", "Typ", "Betrag", "Konto", "Zielkonto", "Kategorie", "Notizen" };

    private static readonly JsonSerializerOptions BackupJson = new() { WriteIndented = true };

    private readonly IJSRuntime _js;
    private readonly FinanceState _finance;

    public ExportService(IJSRuntime js, FinanceState finance)
    {
        _js = js;
        _finance = finance;
    }

    // ── Download ──────────────────────────────────────────────────────────────

    public async Task DownloadAsync(string fileName, string content, string contentType)
    {
        // BOM, damit Excel Umlaute korrekt erkennt.
        var bytes = Encoding.UTF8.GetPreamble().Concat(Encoding.UTF8.GetBytes(content)).ToArray();
        await _js.InvokeVoidAsync("monexa.downloadFile", fileName, contentType, Convert.ToBase64String(bytes));
    }

    // ── CSV ───────────────────────────────────────────────────────────────────

    public string ToCsv(IEnumerable<Transaction> transactions)
    {
        var sb = new StringBuilder();
        sb.AppendLine(string.Join(Separator, CsvHeader));
        foreach (var t in transactions.OrderBy(t => t.Date))
        {
            sb.AppendLine(string.Join(Separator, new[]
            {
                t.Date.ToString("dd.MM.yyyy", UiHelpers.De),
                Escape(t.Description),
                t.Type.ToString(),
                t.Amount.ToString("0.00", UiHelpers.De),
                Escape(_finance.AccountName(t.AccountId)),
                Escape(t.ToAccountId.HasValue ? _finance.AccountName(t.ToAccountId) : ""),
                Escape(t.CategoryId.HasValue ? _finance.CategoryName(t.CategoryId) : ""),
                Escape(t.Notes ?? "")
            }));
        }
        return sb.ToString();
    }

    public Task ExportTransactionsAsync(IEnumerable<Transaction> transactions) =>
        DownloadAsync($"monexa-transaktionen-{DateTime.Now:yyyy-MM-dd}.csv", ToCsv(transactions), "text/csv;charset=utf-8");

    /// <summary>
    /// Liest Buchungen aus einer CSV-Datei (Format wie beim Export). Unbekannte Konten werden
    /// <paramref name="fallbackAccountId"/> zugeordnet, unbekannte Kategorien bleiben leer.
    /// </summary>
    public CsvImportResult ParseCsv(string content, Guid fallbackAccountId)
    {
        var result = new CsvImportResult();
        var lines = content.TrimStart('﻿').Split('\n').Select(l => l.TrimEnd('\r')).Where(l => l.Length > 0).ToList();
        if (lines.Count == 0) return result;

        var separator = lines[0].Contains(';') ? ';' : ',';
        var header = SplitLine(lines[0], separator).Select(h => h.Trim().ToLowerInvariant()).ToList();
        int Col(params string[] names) => header.FindIndex(h => names.Contains(h));

        var iDate = Col("datum", "date", "buchungstag");
        var iDesc = Col("beschreibung", "description", "verwendungszweck");
        var iType = Col("typ", "type");
        var iAmount = Col("betrag", "amount");
        var iAccount = Col("konto", "account");
        var iTo = Col("zielkonto", "to_account");
        var iCategory = Col("kategorie", "category");
        var iNotes = Col("notizen", "notes");

        if (iDate < 0 || iAmount < 0)
        {
            result.Errors.Add("Pflichtspalten „Datum“ und „Betrag“ wurden nicht gefunden.");
            return result;
        }

        for (var row = 1; row < lines.Count; row++)
        {
            var cells = SplitLine(lines[row], separator);
            string Cell(int i) => i >= 0 && i < cells.Count ? cells[i].Trim() : "";

            if (!TryParseDate(Cell(iDate), out var date))
            {
                result.Errors.Add($"Zeile {row + 1}: Ungültiges Datum „{Cell(iDate)}“.");
                continue;
            }
            if (!TryParseAmount(Cell(iAmount), out var amount))
            {
                result.Errors.Add($"Zeile {row + 1}: Ungültiger Betrag „{Cell(iAmount)}“.");
                continue;
            }

            var type = Enum.TryParse<TransactionType>(Cell(iType), true, out var parsedType)
                ? parsedType
                : amount < 0 ? TransactionType.Ausgabe : TransactionType.Einnahme;

            var account = FindAccount(Cell(iAccount));
            var toAccount = FindAccount(Cell(iTo));
            if (type == TransactionType.Umbuchung && toAccount == null) type = TransactionType.Ausgabe;

            var categoryName = Cell(iCategory);
            var category = _finance.Categories.FirstOrDefault(c =>
                string.Equals(c.Name, categoryName, StringComparison.OrdinalIgnoreCase));

            var description = Cell(iDesc);
            var tx = new Transaction
            {
                Date = date,
                Description = string.IsNullOrEmpty(description) ? "Import" : description,
                Amount = Math.Abs(amount),
                Type = type,
                AccountId = account?.Id ?? fallbackAccountId,
                ToAccountId = type == TransactionType.Umbuchung ? toAccount?.Id : null,
                Notes = string.IsNullOrEmpty(Cell(iNotes)) ? null : Cell(iNotes)
            };
            tx.CategoryId = category?.Id ?? _finance.SuggestCategory(tx.Description, tx.Type);
            result.Transactions.Add(tx);
        }

        return result;
    }

    private Konto? FindAccount(string name) => string.IsNullOrWhiteSpace(name)
        ? null
        : _finance.Accounts.FirstOrDefault(a => string.Equals(a.Name, name, StringComparison.OrdinalIgnoreCase));

    private static bool TryParseDate(string value, out DateTime date) =>
        DateTime.TryParseExact(value, new[] { "dd.MM.yyyy", "d.M.yyyy", "dd.MM.yy", "yyyy-MM-dd" },
            UiHelpers.De, DateTimeStyles.None, out date);

    private static bool TryParseAmount(string value, out decimal amount)
    {
        value = value.Replace("€", "").Replace(" ", "").Trim();
        // "1.234,56" (de) bevorzugen, sonst "1234.56" (invariant)
        return decimal.TryParse(value, NumberStyles.Number, UiHelpers.De, out amount) && value.Contains(',')
               || decimal.TryParse(value, NumberStyles.Number, CultureInfo.InvariantCulture, out amount);
    }

    private static string Escape(string value) =>
        value.IndexOfAny(new[] { Separator, '"', '\n', '\r' }) >= 0
            ? "\"" + value.Replace("\"", "\"\"") + "\""
            : value;

    private static List<string> SplitLine(string line, char separator)
    {
        var cells = new List<string>();
        var current = new StringBuilder();
        var quoted = false;
        for (var i = 0; i < line.Length; i++)
        {
            var c = line[i];
            if (quoted)
            {
                if (c == '"' && i + 1 < line.Length && line[i + 1] == '"') { current.Append('"'); i++; }
                else if (c == '"') quoted = false;
                else current.Append(c);
            }
            else if (c == '"') quoted = true;
            else if (c == separator) { cells.Add(current.ToString()); current.Clear(); }
            else current.Append(c);
        }
        cells.Add(current.ToString());
        return cells;
    }

    // ── JSON-Backup ──────────────────────────────────────────────────────────

    public Task ExportBackupAsync()
    {
        var backup = new ProjectBackup
        {
            ProjectName = _finance.CurrentProject?.Name ?? "",
            ExportedAt = DateTime.Now,
            Accounts = _finance.Accounts,
            Categories = _finance.Categories,
            Transactions = _finance.Transactions,
            Budgets = _finance.Budgets,
            Recurring = _finance.Recurring,
            Goals = _finance.Goals
        };
        var name = string.Concat((backup.ProjectName.Length > 0 ? backup.ProjectName : "projekt")
            .Select(c => char.IsLetterOrDigit(c) ? char.ToLowerInvariant(c) : '-'));
        return DownloadAsync($"monexa-backup-{name}-{DateTime.Now:yyyy-MM-dd}.json",
            JsonSerializer.Serialize(backup, BackupJson), "application/json");
    }

    public ProjectBackup? ParseBackup(string json)
    {
        try
        {
            return JsonSerializer.Deserialize<ProjectBackup>(json.TrimStart('﻿'));
        }
        catch (JsonException)
        {
            return null;
        }
    }
}

public class CsvImportResult
{
    public List<Transaction> Transactions { get; } = new();
    public List<string> Errors { get; } = new();
}

public class ProjectBackup
{
    public string ProjectName { get; set; } = "";
    public DateTime ExportedAt { get; set; }
    public List<Konto> Accounts { get; set; } = new();
    public List<Category> Categories { get; set; } = new();
    public List<Transaction> Transactions { get; set; } = new();
    public List<Budget> Budgets { get; set; } = new();
    public List<RecurringTransaction> Recurring { get; set; } = new();
    public List<SavingsGoal> Goals { get; set; } = new();
}
