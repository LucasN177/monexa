using monexa.Models;
using Supabase.Postgrest.Models;
using static Supabase.Postgrest.Constants;

namespace monexa.Services.Data;

/// <summary>Persistenz über Supabase/PostgREST. Zugriffsrechte werden per Row Level Security in der Datenbank geprüft.</summary>
public class SupabaseDataStore : IDataStore
{
    private readonly Supabase.Client _client;

    public SupabaseDataStore(Supabase.Client client)
    {
        _client = client;
    }

    public string Name => "Supabase";

    /// <summary>PostgREST liefert standardmäßig höchstens 1000 Zeilen pro Anfrage – daher seitenweise laden.</summary>
    private const int PageSize = 1000;

    public async Task<List<T>> GetAllAsync<T>(Guid? projectId = null) where T : BaseModel, IEntity, new()
    {
        var filterByProject = projectId.HasValue && typeof(IProjectEntity).IsAssignableFrom(typeof(T));
        var result = new List<T>();
        for (var offset = 0; ; offset += PageSize)
        {
            var table = _client.From<T>();
            var query = filterByProject
                ? table.Filter("project_id", Operator.Equals, projectId!.Value.ToString())
                : table;
            var response = await query.Order("id", Ordering.Ascending).Range(offset, offset + PageSize - 1).Get();
            result.AddRange(response.Models);
            if (response.Models.Count < PageSize) return result;
        }
    }

    public async Task UpsertAsync<T>(T item) where T : BaseModel, IEntity, new() =>
        await _client.From<T>().Upsert(item);

    public async Task UpsertManyAsync<T>(IReadOnlyCollection<T> items) where T : BaseModel, IEntity, new()
    {
        if (items.Count == 0) return;
        await _client.From<T>().Upsert(items.ToList());
    }

    public async Task DeleteAsync<T>(Guid id) where T : BaseModel, IEntity, new() =>
        await _client.From<T>().Filter("id", Operator.Equals, id.ToString()).Delete();

    public async Task DeleteManyAsync<T>(IReadOnlyCollection<Guid> ids) where T : BaseModel, IEntity, new()
    {
        if (ids.Count == 0) return;
        await _client.From<T>()
            .Filter("id", Operator.In, ids.Select(i => i.ToString()).ToList())
            .Delete();
    }
}
