using System.Text.Json;
using System.Text.Json.Serialization;

namespace Gg.Client;

/// <summary>The stored session: a token and when it stops working.</summary>
public sealed record StoredSession
{
    public required string SessionToken { get; init; }

    public required DateTimeOffset ExpiresAt { get; init; }

    public required string TenantId { get; init; }

    public required string PrincipalDisplay { get; init; }
}

[JsonSourceGenerationOptions(WriteIndented = true)]
[JsonSerializable(typeof(StoredSession))]
internal sealed partial class SessionJsonContext : JsonSerializerContext;

/// <summary>Reads and writes the developer's session.</summary>
public interface ISessionStore
{
    StoredSession? Read();
    void Write(StoredSession session);
    void Clear();
}

/// <summary>
/// Stores the session as a mode-0600 file under the platform config directory.
/// </summary>
/// <remarks>
/// <para>
/// A file with restrictive permissions is what the widely-used CLIs do, and it
/// is honest about what it is: anything with the developer's uid can read it.
/// </para>
/// <para>
/// The OS keychain is genuinely better and is deliberately NOT built here.
/// That question belongs to <b>step 5</b>, which introduces the Secrets port;
/// building a half-port now would prejudge its shape. When that port arrives
/// this class becomes one of its adapters.
/// </para>
/// </remarks>
public sealed class FileSessionStore : ISessionStore
{
    private readonly string _path;

    public FileSessionStore(string? path = null) =>
        _path = path ?? DefaultPath();

    /// <summary>Where the session lives when nobody overrides it.</summary>
    public static string DefaultPath()
    {
        var configHome = Environment.GetEnvironmentVariable("XDG_CONFIG_HOME");
        var root = !string.IsNullOrWhiteSpace(configHome)
            ? configHome
            : Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
                OperatingSystem.IsWindows() ? "AppData/Roaming" : ".config");

        return Path.Combine(root, "good-grief", "session.json");
    }

    /// <summary>The file path in use.</summary>
    public string FilePath => _path;

    public StoredSession? Read()
    {
        if (!File.Exists(_path))
        {
            return null;
        }

        try
        {
            return JsonSerializer.Deserialize(File.ReadAllText(_path), SessionJsonContext.Default.StoredSession);
        }
        catch (JsonException)
        {
            // A corrupt session file is the same as no session: the caller logs
            // in again rather than being handed a confusing parse error.
            return null;
        }
    }

    public void Write(StoredSession session)
    {
        var directory = Path.GetDirectoryName(_path)!;
        Directory.CreateDirectory(directory);

        // Create the file empty and lock it down BEFORE the token goes in, so
        // there is no instant where a readable file holds a live credential.
        if (!File.Exists(_path))
        {
            using (File.Create(_path)) { }
        }
        Restrict(_path);

        File.WriteAllText(_path, JsonSerializer.Serialize(session, SessionJsonContext.Default.StoredSession));
        Restrict(_path);
    }

    public void Clear()
    {
        if (File.Exists(_path))
        {
            File.Delete(_path);
        }
    }

    private static void Restrict(string path)
    {
        if (!OperatingSystem.IsWindows())
        {
            File.SetUnixFileMode(path, UnixFileMode.UserRead | UnixFileMode.UserWrite);
        }
    }
}
