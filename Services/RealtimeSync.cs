using Supabase.Realtime;
using Supabase.Realtime.PostgresChanges;

namespace monexa.Services;

/// <summary>
/// Lauscht im Cloud-Modus auf Datenbankänderungen (Supabase Realtime) und lädt die Projektdaten
/// neu, wenn ein anderes Projektmitglied etwas geändert hat.
/// </summary>
public class RealtimeSync : IAsyncDisposable
{
    private static readonly string[] Tables =
        { "accounts", "transactions", "categories", "budgets", "recurring_transactions", "savings_goals", "project_members" };

    private readonly Supabase.Client _client;
    private readonly FinanceState _finance;
    private RealtimeChannel? _channel;
    private Guid? _projectId;
    private CancellationTokenSource? _debounce;

    public RealtimeSync(Supabase.Client client, FinanceState finance)
    {
        _client = client;
        _finance = finance;
    }

    public bool IsConnected => _channel != null;

    /// <summary>Abonniert Änderungen des aktuellen Projekts (idempotent).</summary>
    public async Task SubscribeAsync()
    {
        var projectId = _finance.CurrentProject?.Id;
        if (projectId == null || projectId == _projectId) return;
        await UnsubscribeAsync();

        try
        {
            await _client.Realtime.ConnectAsync();
            var channel = _client.Realtime.Channel($"project-{projectId}");
            foreach (var table in Tables)
            {
                channel.OnPostgresChange((_, _) => ScheduleReload(), PostgresChangesOptions.ListenType.All,
                    new PostgresChangesFilter { Schema = "public", Table = table, Filter = $"project_id=eq.{projectId}" });
            }
            await channel.Subscribe();
            _channel = channel;
            _projectId = projectId;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Realtime nicht verfügbar: {ex.Message}");
        }
    }

    public Task UnsubscribeAsync()
    {
        _channel?.Unsubscribe();
        _channel = null;
        _projectId = null;
        return Task.CompletedTask;
    }

    private void ScheduleReload()
    {
        _debounce?.Cancel();
        _debounce = new CancellationTokenSource();
        var token = _debounce.Token;
        _ = Task.Run(async () =>
        {
            await Task.Delay(1500, token);
            if (!token.IsCancellationRequested && !_finance.IsLoading)
                await _finance.LoadAsync();
        }, token);
    }

    public async ValueTask DisposeAsync() => await UnsubscribeAsync();
}
