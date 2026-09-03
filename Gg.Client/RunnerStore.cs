using System.Text.Json;
using System.Text.Json.Serialization;

namespace Gg.Client;

/// <summary>The stored runner: its own credential, and when it stops working.</summary>
public sealed record StoredRunner
{
    public required string RunnerId { get; init; }

    public required string RunnerToken { get; init; }

    public required DateTimeOffset ExpiresAt { get; init; }

    /// <summary>What this runner may advertise, when its credential said so.</summary>
    /// <remarks>
    /// <para>
    /// <b>Empty for a host runner, which reads its labels from its
    /// environment.</b> A pool member's arrive WITH the credential, decided
    /// control-plane-side from the strategy - so they are kept here, where they
    /// survive a restart that does not redeem.
    /// </para>
    /// <para>
    /// <b>Not read from the container's environment, deliberately.</b> A member
    /// that took them from a variable would advertise what somebody put in a
    /// container rather than what the strategy decided, which is the thing the
    /// member-identity work exists to stop.
    /// </para>
    /// </remarks>
    public IReadOnlyList<string> Labels { get; init; } = [];
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
    /// <remarks>
    /// <b>The unnamed path, and it is the maintain service's.</b> This is the
    /// only file any version of gg has ever written, and on a live pool host it
    /// holds that service's thirty-day token. It keeps its name so an upgrade
    /// does not take a host down: a maintain start that cannot find its
    /// credential refuses unless somebody is signed in, and nobody is.
    /// </remarks>
    public static string DefaultPath() =>
        Path.Combine(Root(), "runner.json");

    /// <summary>Where the runner registered under <paramref name="name"/> is kept.</summary>
    /// <remarks>
    /// <b>One slot per name, because a pool host has two runners.</b> It runs
    /// <c>gg runner up</c> as itself and <c>gg runner maintain</c> as
    /// <c>&lt;machine&gt;:maintain</c>, and a single file meant whichever
    /// registered last owned the only credential.
    /// </remarks>
    public static string PathFor(string name) =>
        Path.Combine(Root(), FileNameFor(name));

    /// <summary>
    /// A runner name as one file name, on every platform.
    /// </summary>
    /// <remarks>
    /// <b>Every invalid character becomes '-', and the name is kept
    /// otherwise.</b> The maintain name carries a ':', which is not a filename
    /// character everywhere - and a scheme that flattened two names to one
    /// string would recreate the shared slot with extra steps.
    /// </remarks>
    public static string FileNameFor(string name)
    {
        ArgumentNullException.ThrowIfNull(name);

        var safe = new string([.. name.Select(c =>
            Path.GetInvalidFileNameChars().Contains(c) ? '-' : c)]);

        return $"runner-{safe}.json";
    }

    private static string Root()
    {
        var root = Environment.GetEnvironmentVariable("XDG_CONFIG_HOME") is { Length: > 0 } configured
            ? configured
            : Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".config");

        return Path.Combine(root, "good-grief");
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

/// <summary>
/// The runner this machine already is, or the one it becomes.
/// </summary>
/// <remarks>
/// <para>
/// <b>One decision, called by both verbs, because they had drifted.</b>
/// <c>gg runner maintain</c> read-or-registered; <c>gg runner up</c> registered
/// unconditionally and stored nothing, so one host appeared as eleven runners
/// with ten permanently offline - one per restart. The duplication was the
/// defect's whole cause, so the fix removes it rather than copying the good half
/// across.
/// </para>
/// <para>
/// <b>Registration is passed in.</b> This decides WHETHER to register, and the
/// caller owns how - which session authorizes it, and what name it registers
/// under. That keeps the rule testable without a control plane, which is why it
/// was untestable while it lived inside a CLI entry point.
/// </para>
/// </remarks>
public static class RunnerIdentity
{
    /// <summary>
    /// Returns the stored runner when it still works, and otherwise registers
    /// one and keeps it.
    /// </summary>
    public static async Task<StoredRunner> EnsureAsync(
        FileRunnerStore store, Func<Task<StoredRunner>> register, DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(store);
        ArgumentNullException.ThrowIfNull(register);

        // USABLE rather than merely present. A lapsed credential handed to the
        // protocol fails as a 401 on the first call, which is the wrong place
        // to learn that a person is needed.
        if (store.Usable(now) is { } held)
        {
            return held;
        }

        var registered = await register();

        // Kept, or the next start does this again and the row count grows.
        store.Write(registered);
        return registered;
    }
}
