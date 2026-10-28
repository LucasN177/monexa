using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Forms;
using monexa.Models;
using monexa.Models.Enums;
using monexa.Services;
using monexa.Shared;
using MudBlazor;
using static monexa.Services.UiHelpers;

namespace monexa.Pages;

public partial class Transactions : FinancePage
{
    [Inject] private ExportService Export { get; set; } = default!;

    private record MonthGroup(string Label, List<Transaction> Items, decimal Income, decimal Expenses);

    private string searchString = "";
    private Guid? selectedAccount;
    private Guid? selectedCategory;
    private TransactionType? selectedType;
    private DateRange? dateRange;
    private List<Transaction> filtered = new();
    private readonly HashSet<Guid> selected = new();
    private int visibleCount = 100;

    private bool _importOpen;
    private Guid _importAccount;
    private CsvImportResult? _importResult;

    [SupplyParameterFromQuery(Name = "konto")] public Guid? AccountQuery { get; set; }
    [SupplyParameterFromQuery(Name = "kategorie")] public Guid? CategoryQuery { get; set; }

    protected override async Task OnInitializedAsync()
    {
        await base.OnInitializedAsync();
        selectedAccount = AccountQuery;
        selectedCategory = CategoryQuery;
        _importAccount = Finance.ActiveAccounts.FirstOrDefault()?.Id ?? Guid.Empty;
        ApplyFilter();
    }

    protected override void OnDataChanged()
    {
        selected.RemoveWhere(id => Finance.GetTransaction(id) == null);
        ApplyFilter();
    }

    private decimal FilteredIncome => filtered.Where(t => t.Type == TransactionType.Einnahme).Sum(t => t.Amount);
    private decimal FilteredExpenses => filtered.Where(t => t.Type == TransactionType.Ausgabe).Sum(t => t.Amount);

    private string PeriodCaption => dateRange?.Start != null && dateRange.End != null
        ? $"{Date(dateRange.Start.Value)} – {Date(dateRange.End.Value)}"
        : "Gesamter Zeitraum";

    private void ApplyFilter()
    {
        var query = Finance.Transactions.AsEnumerable();

        if (!string.IsNullOrWhiteSpace(searchString))
        {
            query = query.Where(t =>
                t.Description.Contains(searchString, StringComparison.OrdinalIgnoreCase) ||
                (t.Notes != null && t.Notes.Contains(searchString, StringComparison.OrdinalIgnoreCase)) ||
                t.Amount.ToString("0.00", De).Contains(searchString));
        }

        if (selectedAccount.HasValue)
            query = query.Where(t => t.AccountId == selectedAccount || t.ToAccountId == selectedAccount);

        if (selectedCategory.HasValue)
            query = selectedCategory == Guid.Empty
                ? query.Where(t => t.CategoryId == null && t.Type != TransactionType.Umbuchung)
                : query.Where(t => t.CategoryId == selectedCategory);

        if (selectedType.HasValue)
            query = query.Where(t => t.Type == selectedType.Value);

        if (dateRange?.Start != null)
            query = query.Where(t => t.Date.Date >= dateRange.Start.Value.Date);
        if (dateRange?.End != null)
            query = query.Where(t => t.Date.Date <= dateRange.End.Value.Date);

        filtered = query.ToList();
    }

    private IEnumerable<MonthGroup> GetGroupedTransactions() =>
        filtered.Take(visibleCount)
            .GroupBy(t => FinanceState.MonthStart(t.Date))
            .Select(g => new MonthGroup(
                MonthYear(g.Key),
                g.ToList(),
                g.Where(t => t.Type == TransactionType.Einnahme).Sum(t => t.Amount),
                g.Where(t => t.Type == TransactionType.Ausgabe).Sum(t => t.Amount)));

    private void SetQuickRange(string key)
    {
        var today = DateTime.Today;
        var month = FinanceState.MonthStart(today);
        dateRange = key switch
        {
            "lastmonth" => new DateRange(month.AddMonths(-1), month.AddDays(-1)),
            "90" => new DateRange(today.AddDays(-89), today),
            "year" => new DateRange(new DateTime(today.Year, 1, 1), new DateTime(today.Year, 12, 31)),
            _ => new DateRange(month, month.AddMonths(1).AddDays(-1))
        };
        ApplyFilter();
    }

    private void ClearFilters()
    {
        searchString = "";
        selectedAccount = null;
        selectedCategory = null;
        selectedType = null;
        dateRange = null;
        ApplyFilter();
    }

    private static string GetFormattedAmount(Transaction transaction) => transaction.Type switch
    {
        TransactionType.Einnahme => "+ " + Money(transaction.Amount),
        TransactionType.Ausgabe => "- " + Money(transaction.Amount),
        _ => Money(transaction.Amount)
    };

    private void ToggleSelected(Guid id, bool value)
    {
        if (value) selected.Add(id);
        else selected.Remove(id);
    }

    private async Task DeleteTransaction(Transaction transaction)
    {
        if (!await ConfirmAsync("Transaktion löschen",
                $"Soll die Transaktion <strong>{transaction.Description}</strong> ({Money(transaction.Amount)}) wirklich gelöscht werden?"))
            return;
        if (await Finance.DeleteTransactionAsync(transaction.Id))
            Snackbar.Add("Transaktion gelöscht.", Severity.Success);
    }

    private async Task DeleteSelected()
    {
        if (!await ConfirmAsync("Transaktionen löschen", $"Sollen <strong>{selected.Count}</strong> Transaktionen wirklich gelöscht werden?"))
            return;
        var count = selected.Count;
        if (await Finance.DeleteTransactionsAsync(selected.ToList()))
        {
            selected.Clear();
            Snackbar.Add($"{count} Transaktionen gelöscht.", Severity.Success);
        }
    }

    private async Task ExportCsv()
    {
        await Export.ExportTransactionsAsync(filtered);
        Snackbar.Add($"{filtered.Count} Transaktionen exportiert.", Severity.Success);
    }

    private async Task OnImportFile(IBrowserFile? file)
    {
        if (file == null) return;
        try
        {
            using var reader = new StreamReader(file.OpenReadStream(maxAllowedSize: 5 * 1024 * 1024));
            var content = await reader.ReadToEndAsync();
            _importResult = Export.ParseCsv(content, _importAccount);
        }
        catch (Exception ex)
        {
            Snackbar.Add($"Datei konnte nicht gelesen werden: {ex.Message}", Severity.Error);
        }
    }

    private async Task ConfirmImport()
    {
        if (_importResult == null) return;
        var count = _importResult.Transactions.Count;
        if (await Finance.ImportTransactionsAsync(_importResult.Transactions))
        {
            Snackbar.Add($"{count} Transaktionen importiert.", Severity.Success);
            CloseImport();
        }
    }

    private void CloseImport()
    {
        _importOpen = false;
        _importResult = null;
    }
}
