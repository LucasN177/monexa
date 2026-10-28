using monexa.Models;
using monexa.Models.Enums;
using monexa.Shared;
using MudBlazor;

namespace monexa.Pages;

public partial class Konten : FinancePage
{
    private string searchString = "";
    private bool showInactive = true;

    private decimal LiquidBalance => Finance.ActiveAccounts
        .Where(a => a.Type is AccountType.Girokonto or AccountType.Tagesgeld or AccountType.Sparkonto)
        .Sum(a => a.Balance);

    private decimal Debts => Finance.ActiveAccounts.Where(a => a.Balance < 0).Sum(a => a.Balance);

    private IEnumerable<Konto> GetFilteredAccounts()
    {
        var accounts = Finance.Accounts.Where(a => showInactive || a.IsActive);
        if (string.IsNullOrWhiteSpace(searchString))
            return accounts;

        return accounts.Where(a =>
            a.Name.Contains(searchString, StringComparison.OrdinalIgnoreCase) ||
            a.BankName.Contains(searchString, StringComparison.OrdinalIgnoreCase) ||
            a.IBAN.Contains(searchString, StringComparison.OrdinalIgnoreCase));
    }

    private async Task DeleteAccount(Konto account)
    {
        var count = Finance.Transactions.Count(t => t.AccountId == account.Id || t.ToAccountId == account.Id);
        var message = $"Möchtest du das Konto <strong>{account.Name}</strong> wirklich löschen?";
        if (count > 0)
            message += $"<br/><br/>Dabei werden auch <strong>{count} Transaktionen</strong> und zugehörige Daueraufträge gelöscht. " +
                       "Alternativ kannst du das Konto deaktivieren.";

        if (!await ConfirmAsync("Konto löschen", message)) return;
        if (await Finance.DeleteAccountAsync(account.Id))
            Snackbar.Add($"Konto „{account.Name}“ wurde gelöscht.", Severity.Success);
    }

    private void ViewDetails(Konto account) => Navigation.NavigateTo($"konten/{account.Id}");
}
