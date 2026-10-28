using Microsoft.AspNetCore.Components;
using monexa.Services;
using MudBlazor;

namespace monexa.Shared;

/// <summary>
/// Basisklasse für Seiten, die Finanzdaten anzeigen: stellt die zentralen Services bereit
/// und rendert automatisch neu, sobald sich der <see cref="FinanceState"/> ändert.
/// </summary>
public abstract class FinancePage : ComponentBase, IDisposable
{
    [Inject] protected FinanceState Finance { get; set; } = default!;
    [Inject] protected ISnackbar Snackbar { get; set; } = default!;
    [Inject] protected IDialogService DialogService { get; set; } = default!;
    [Inject] protected NavigationManager Navigation { get; set; } = default!;

    protected override async Task OnInitializedAsync()
    {
        Finance.Changed += OnFinanceChanged;
        await Finance.EnsureLoadedAsync();
    }

    /// <summary>Wird nach jeder Datenänderung aufgerufen, bevor neu gerendert wird.</summary>
    protected virtual void OnDataChanged()
    {
    }

    private void OnFinanceChanged() => InvokeAsync(() =>
    {
        OnDataChanged();
        StateHasChanged();
    });

    protected async Task<bool> ConfirmAsync(string title, string message, string yesText = "Löschen")
    {
        var result = await DialogService.ShowMessageBox(title, (MarkupString)message, yesText: yesText, cancelText: "Abbrechen");
        return result == true;
    }

    public virtual void Dispose()
    {
        Finance.Changed -= OnFinanceChanged;
        GC.SuppressFinalize(this);
    }
}
