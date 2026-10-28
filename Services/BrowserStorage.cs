using System.Text.Json;
using Microsoft.JSInterop;

namespace monexa.Services;

/// <summary>Synchroner Zugriff auf <c>localStorage</c> (in Blazor WASM ist IJSRuntime in-process).</summary>
public class BrowserStorage
{
    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };

    private readonly IJSInProcessRuntime _js;

    public BrowserStorage(IJSRuntime js)
    {
        _js = (IJSInProcessRuntime)js;
    }

    public string? GetString(string key) => _js.Invoke<string?>("localStorage.getItem", key);

    public void SetString(string key, string value) => _js.InvokeVoid("localStorage.setItem", key, value);

    public void Remove(string key) => _js.InvokeVoid("localStorage.removeItem", key);

    public T? Get<T>(string key)
    {
        var json = GetString(key);
        if (string.IsNullOrEmpty(json)) return default;
        try
        {
            return JsonSerializer.Deserialize<T>(json, JsonOptions);
        }
        catch (JsonException)
        {
            return default;
        }
    }

    public void Set<T>(string key, T value) => SetString(key, JsonSerializer.Serialize(value, JsonOptions));

    /// <summary>Entfernt alle Schlüssel mit dem angegebenen Präfix.</summary>
    public void RemoveByPrefix(string prefix)
    {
        var length = _js.Invoke<int>("eval", "localStorage.length");
        var keys = new List<string>();
        for (var i = 0; i < length; i++)
        {
            var key = _js.Invoke<string?>("localStorage.key", i);
            if (key != null && key.StartsWith(prefix, StringComparison.Ordinal)) keys.Add(key);
        }
        keys.ForEach(Remove);
    }
}
