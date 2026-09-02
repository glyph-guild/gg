using System.Text.Json;
using System.Text.Json.Serialization;

namespace Gg.Client;

/// <summary>The stored runner: its own credential, and when it stops working.</summary>
public sealed record StoredRunner
{
    public required string RunnerId { get; init; }

    public required string RunnerToken { get; init; }

    public required DateTimeOffset ExpiresAt { get; init; }
}

[JsonSerializable(typeof(StoredRunner))]
internal sealed partial class RunnerJsonContext : JsonSerializerContext;

/// <summary>
/// A runner's own credential, kept so a host restarts without a person.
/// </summary>
/// <remarks>
/// <para>
/// <b>The runner's token rather than the person's session, and the reason is
/// arithmetic.</b> A session lasts twelve hours and a runner token thirty days.
/// <c>gg runner maintain</c> registers on every start, so a host holding a
/// session cannot restart after half a day — a machine rebooted the next
/// morning fails with <i>not signed in</i>, on a box with nobody at it.
/// </para>
/// <para>
/// <b>The separation was designed for this.</b> <c>RunnerRegistry</c>:
/// <i>"the runner's lifetime is its own. Nothing here records which session
/// registered it, so revoking that session, or simply letting it expire
/// overnight, cannot take a running runner down mid-flight."</i> Keeping the
/// runner's token preserves that; keeping the session would discard it and hold
/// the wider authority as well.
/// </para>
/// <para>
/// <b>Thirty days is a cadence, not a bug.</b> Nothing renews a runner token —
/// the protocol's <c>RenewAsync</c> renews a LEASE — so when it lapses a person
/// signs in again. <see cref="Usable"/> is what reports that at the one place
/// able to say so, rather than letting it arrive as a 401 on the first
/// protocol call.
/// </para>
/// </remarks>
public sealed class FileRunnerStore
{
    private readonly string _path;

    public FileRunnerStore(string? path = null) => _path = path ?? DefaultPath();

    /// <summary>Beside the session, because it is the same kind of thing at rest.</summary>
    public static string DefaultPath()
    {
        var root = Environment.GetEnvironmentVariable("XDG_CONFIG_HOME") is { Length: > 0 } configured
            ? configured
            : Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".config");

        return Path.Combine(root, "good-grief", "runner.json");
    }

    public string FilePath => _path;

    public StoredRunner? Read()
    {
        if (!File.Exists(_path))
        {
            return null;
        }

        try
        {
            return JsonSerializer.Deserialize(
                File.ReadAllText(_path), RunnerJsonContext.Default.StoredRunner);
        }
        catch (JsonException)
        {
            // A file we cannot read is a file we do not have. Registering again
            // is always available; guessing at half a credential is not.
            return null;
        }
    }

    /// <summary>The stored runner if it still works, or null.</summary>
    /// <remarks>
    /// Separate from <see cref="Read"/> so that "there is one" and "it still
    /// works" are two questions. A caller that conflated them would start a
    /// host on a lapsed credential and discover it on the first protocol call,
    /// which is the wrong place to learn that a person is needed.
    /// </remarks>
    public StoredRunner? Usable(DateTimeOffset now) =>
        Read() is { } runner && runner.ExpiresAt > now ? runner : null;

    public void Write(StoredRunner runner)
    {
        ArgumentNullException.ThrowIfNull(runner);

        var directory = Path.GetDirectoryName(_path)!;
        Directory.CreateDirectory(directory);

        // Created empty and locked down BEFORE the token goes in, exactly as
        // the session is: there must be no instant where a readable file holds
        // a live credential. On a pool host this file is the runner's whole
        // authority for thirty days.
        if (!File.Exists(_path))
        {
            using (File.Create(_path)) { }
        }
        Restrict(_path);

        File.WriteAllText(_path, JsonSerializer.Serialize(runner, RunnerJsonContext.Default.StoredRunner));
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
