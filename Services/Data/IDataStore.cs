using monexa.Models;
using Supabase.Postgrest.Models;

namespace monexa.Services.Data;

/// <summary>Persistenzschicht. Implementiert durch Supabase (Cloud) oder localStorage (Demo/Offline).</summary>
public interface IDataStore
{
    /// <summary>"Supabase" oder "Lokal" – für Statusanzeigen.</summary>
    string Name { get; }

    Task<List<T>> GetAllAsync<T>(Guid? projectId = null) where T : BaseModel, IEntity, new();

    Task UpsertAsync<T>(T item) where T : BaseModel, IEntity, new();

    Task UpsertManyAsync<T>(IReadOnlyCollection<T> items) where T : BaseModel, IEntity, new();

    Task DeleteAsync<T>(Guid id) where T : BaseModel, IEntity, new();

    Task DeleteManyAsync<T>(IReadOnlyCollection<Guid> ids) where T : BaseModel, IEntity, new();
}
