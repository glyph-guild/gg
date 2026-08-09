using System.Text.Json;
using System.Text.Json.Serialization;

namespace Gg.Console;

[JsonSourceGenerationOptions(WriteIndented = true, UseStringEnumConverter = true)]
[JsonSerializable(typeof(AppState))]
public sealed partial class AppStateJsonContext : JsonSerializerContext;

/// <summary>Source-generated (AOT-safe) serialization for the model.</summary>
public static class AppStateJson
{
    public static string Serialize(AppState state)
    {
        throw new NotImplementedException();
    }

    public static AppState Deserialize(string json)
    {
        throw new NotImplementedException();
    }
}
