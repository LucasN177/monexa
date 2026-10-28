using System.Reflection;
using monexa.Models;
using Supabase.Postgrest.Attributes;
using Supabase.Postgrest.Models;

namespace monexa.Services.Data;

/// <summary>Speichert alle Daten im localStorage des Browsers. Wird genutzt, wenn Supabase nicht konfiguriert ist.</summary>
public class LocalDataStore : IDataStore
{
    public const string KeyPrefix = "monexa.data.";

    private readonly BrowserStorage _storage;

    public LocalDataStore(BrowserStorage storage)
    {
        _storage = storage;
    }

    public string Name => "Lokal";

    public Task<List<T>> GetAllAsync<T>(Guid? projectId = null) where T : BaseModel, IEntity, new()
    {
        var items = Load<T>();
        if (projectId.HasValue)
            items = items.Where(i => i is not IProjectEntity p || p.ProjectId == projectId.Value).ToList();
        return Task.FromResult(items);
    }

    public Task UpsertAsync<T>(T item) where T : BaseModel, IEntity, new() =>
        UpsertManyAsync<T>(new[] { item });

    public Task UpsertManyAsync<T>(IReadOnlyCollection<T> items) where T : BaseModel, IEntity, new()
    {
        var all = Load<T>();
        foreach (var item in items)
        {
            var index = all.FindIndex(i => i.Id == item.Id);
            if (index >= 0) all[index] = item;
            else all.Add(item);
        }
        Save(all);
        return Task.CompletedTask;
    }

    public Task DeleteAsync<T>(Guid id) where T : BaseModel, IEntity, new() =>
        DeleteManyAsync<T>(new[] { id });

    public Task DeleteManyAsync<T>(IReadOnlyCollection<Guid> ids) where T : BaseModel, IEntity, new()
    {
        var all = Load<T>();
        all.RemoveAll(i => ids.Contains(i.Id));
        Save(all);
        return Task.CompletedTask;
    }

    /// <summary>True, solange noch kein einziges Projekt gespeichert wurde.</summary>
    public bool IsEmpty => Load<Project>().Count == 0;

    public void Clear() => _storage.RemoveByPrefix(KeyPrefix);

    private List<T> Load<T>() where T : BaseModel, IEntity, new() =>
        _storage.Get<List<T>>(Key<T>()) ?? new List<T>();

    private void Save<T>(List<T> items) where T : BaseModel, IEntity, new() =>
        _storage.Set(Key<T>(), items);

    private static string Key<T>() =>
        KeyPrefix + (typeof(T).GetCustomAttribute<TableAttribute>()?.Name ?? typeof(T).Name);
}
