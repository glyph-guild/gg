using System.Text.Json;
using System.Text.Json.Serialization;

namespace Gg.Console;

[JsonSourceGenerationOptions(WriteIndented = true, UseStringEnumConverter = true)]
[JsonSerializable(typeof(AppState))]
[JsonSerializable(typeof(Gg.Contracts.FlightSummary))]
[JsonSerializable(typeof(Gg.Contracts.FlightLog))]
[JsonSerializable(typeof(Gg.Contracts.RunnerList))]
public sealed partial class AppStateJsonContext : JsonSerializerContext;

/// <summary>Source-generated (AOT-safe) serialization for the model.</summary>
public static class AppStateJson
{
    public static string Serialize(AppState state) =>
        JsonSerializer.Serialize(state, AppStateJsonContext.Default.AppState);

    public static AppState Deserialize(string json) =>
        JsonSerializer.Deserialize(json, AppStateJsonContext.Default.AppState)
            ?? throw new InvalidOperationException("AppState JSON deserialized to null");
}
