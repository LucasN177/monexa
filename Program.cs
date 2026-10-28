using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using monexa;
using monexa.Services;
using monexa.Services.Data;
using MudBlazor;
using MudBlazor.Services;

var builder = WebAssemblyHostBuilder.CreateDefault(args);
builder.RootComponents.Add<App>("#app");
builder.RootComponents.Add<HeadOutlet>("head::after");

builder.Services.AddMudServices(config =>
{
    config.SnackbarConfiguration.PositionClass = Defaults.Classes.Position.BottomRight;
    config.SnackbarConfiguration.VisibleStateDuration = 4000;
});

builder.Services.AddScoped(sp => new HttpClient { BaseAddress = new Uri(builder.HostEnvironment.BaseAddress) });

// ── Supabase / Persistenz ─────────────────────────────────────────────────────
var supabaseSettings = new SupabaseSettings
{
    Url = builder.Configuration["Supabase:Url"] ?? "",
    AnonKey = builder.Configuration["Supabase:AnonKey"] ?? ""
};
builder.Services.AddSingleton(supabaseSettings);
builder.Services.AddSingleton<BrowserStorage>();
builder.Services.AddSingleton<PreferencesService>();

if (supabaseSettings.IsConfigured)
{
    builder.Services.AddSingleton(sp => new Supabase.Client(supabaseSettings.Url, supabaseSettings.AnonKey,
        new Supabase.SupabaseOptions
        {
            AutoRefreshToken = true,
            AutoConnectRealtime = false,
            SessionHandler = new LocalStorageSessionHandler(sp.GetRequiredService<BrowserStorage>())
        }));
    builder.Services.AddSingleton<IDataStore, SupabaseDataStore>();
    builder.Services.AddScoped<RealtimeSync>();
}
else
{
    // Demo-/Offline-Modus: alle Daten im localStorage.
    builder.Services.AddSingleton<LocalDataStore>();
    builder.Services.AddSingleton<IDataStore>(sp => sp.GetRequiredService<LocalDataStore>());
}

builder.Services.AddScoped(sp => new AuthService(
    sp.GetRequiredService<PreferencesService>(),
    sp.GetService<Supabase.Client>()));
builder.Services.AddScoped(sp => new FinanceState(
    sp.GetRequiredService<IDataStore>(),
    sp.GetRequiredService<AuthService>(),
    sp.GetRequiredService<PreferencesService>(),
    sp.GetRequiredService<ISnackbar>(),
    sp.GetService<Supabase.Client>()));
builder.Services.AddScoped<ExportService>();

var host = builder.Build();

if (supabaseSettings.IsConfigured)
{
    try
    {
        // Stellt eine gespeicherte Session wieder her und erneuert sie.
        await host.Services.GetRequiredService<Supabase.Client>().InitializeAsync();
    }
    catch (Exception ex)
    {
        Console.WriteLine($"Supabase konnte nicht initialisiert werden: {ex.Message}");
    }
}

await host.RunAsync();
