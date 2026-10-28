using Supabase.Gotrue;
using Supabase.Gotrue.Interfaces;

namespace monexa.Services;

/// <summary>Speichert die Supabase-Auth-Session im localStorage, damit Nutzer nach einem Reload angemeldet bleiben.</summary>
public class LocalStorageSessionHandler : IGotrueSessionPersistence<Session>
{
    private const string Key = "monexa.supabase.session";

    private readonly BrowserStorage _storage;

    public LocalStorageSessionHandler(BrowserStorage storage)
    {
        _storage = storage;
    }

    public void SaveSession(Session session) => _storage.Set(Key, session);

    public void DestroySession() => _storage.Remove(Key);

    public Session? LoadSession() => _storage.Get<Session>(Key);
}
