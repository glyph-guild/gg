using System.Diagnostics;

namespace Gg.Runner.Vcs;

/// <summary>
/// One git command, planned before it is run.
/// </summary>
/// <remarks>
/// <para>
/// A plan rather than a call, so <b>"the secret never reaches the argument
/// list"</b> is a property a test can assert about the thing that would be
/// executed, rather than something inferred by reading the code that executes
/// it.
/// </para>
/// <para>
/// The distinction is not cosmetic. <c>argv</c> is readable by every process on
/// the machine - <c>ps</c> shows it - so a token in an argument is a token
/// disclosed before any code of ours could redact it. A child process's
/// environment is not: it is owner-readable at best. So the secret travels in
/// the environment and the credential helper that reads it travels in argv.
/// </para>
/// </remarks>
public sealed record GitInvocation
{
    /// <summary>The environment variable the credential helper reads.</summary>
    private const string SecretVariable = "GG_GIT_SECRET";

    public required IReadOnlyList<string> Arguments { get; init; }

    /// <summary>Extra variables for the child only. Empty when there is no secret.</summary>
    public required IReadOnlyDictionary<string, string> Environment { get; init; }

    /// <summary>
    /// The plan for fetching one ref.
    /// </summary>
    /// <remarks>
    /// <para>
    /// A shallow fetch of exactly the pinned ref: nothing else from the
    /// repository's history is downloaded, which is both the fastest thing and
    /// the least of somebody's code to have on disk.
    /// </para>
    /// <para>
    /// The credential helper is an inline command git hands to a shell. Its
    /// TEXT is in argv - which is fine, it names a variable - and the value it
    /// prints comes from the environment. When there is no secret no helper is
    /// configured at all, and prompting is disabled either way: a runner that
    /// prompts hangs until its lease expires.
    /// </para>
    /// </remarks>
    public static GitInvocation Fetch(string url, string resolvedRef, string? secret)
    {
        var arguments = new List<string>
        {
            // Never prompt. A hung runner is worse than a failed one, because
            // it holds the flight until the lease runs out.
            "-c", "core.askPass=",
            "-c", "credential.interactive=false",
        };

        var environment = new Dictionary<string, string>(StringComparer.Ordinal);

        // The machine's OWN helpers are cleared first, and this is not tidiness.
        // git treats credential.helper as a LIST and tries each in turn, so
        // appending ours to a developer's keychain means git can authenticate
        // with a credential this flight was never granted - and it will,
        // silently, and the operation will succeed. That makes the registered
        // credential advisory, which is the exact property this platform sells.
        // An empty value resets the list; ours is then the only one.
        arguments.Add("-c");
        arguments.Add("credential.helper=");

        if (!string.IsNullOrEmpty(secret))
        {
            // The helper text names the variable; the value is not here.
            arguments.Add("-c");
            arguments.Add(
                $"credential.helper=!f() {{ echo username=x-access-token; echo password=${SecretVariable}; }}; f");
            environment[SecretVariable] = secret;
        }

        arguments.AddRange([
            "fetch", "--depth", "1", "--no-tags", "--no-recurse-submodules", url, resolvedRef,
        ]);

        return new GitInvocation { Arguments = arguments, Environment = environment };
    }

    /// <summary>A plan with no secret in it at all.</summary>
    /// <summary>
    /// The plan for pushing one branch, and only creating it.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>No force, and refspec-refusing rather than refspec-forcing.</b> The
    /// leading <c>+</c> that would make this overwrite is absent and there is no
    /// <c>--force</c>: a push that would not fast-forward FAILS, which is what
    /// makes "an existing branch is refused" a property of the command rather
    /// than of a check somebody remembered to run first. Fifth application of
    /// never overwrite a lifecycle, and the one where the thing overwritten
    /// might be somebody's work.
    /// </para>
    /// <para>
    /// The secret travels in the environment for the same reason it does on a
    /// fetch: <c>argv</c> is readable by every process on the machine.
    /// </para>
    /// </remarks>
    public static GitInvocation Push(string url, string localRef, string remoteBranch, string? secret)
    {
        var arguments = new List<string>
        {
            "-c", "core.askPass=",
            "-c", "credential.interactive=false",
        };

        var environment = new Dictionary<string, string>(StringComparer.Ordinal);

        // Same reset as the fetch, and it matters more here: an ambient helper
        // that can authenticate a PUSH is one that can write to a repository
        // this flight was never granted write on.
        arguments.Add("-c");
        arguments.Add("credential.helper=");

        if (!string.IsNullOrEmpty(secret))
        {
            arguments.Add("-c");
            arguments.Add(
                $"credential.helper=!f() {{ echo username=x-access-token; echo password=${SecretVariable}; }}; f");
            environment[SecretVariable] = secret;
        }

        // No leading '+' on the refspec. That single character is the difference
        // between creating a branch and destroying whatever was there.
        arguments.AddRange(["push", url, $"{localRef}:refs/heads/{remoteBranch}"]);

        return new GitInvocation { Arguments = arguments, Environment = environment };
    }

    /// <summary>
    /// The plan for asking a remote where one of its branches points.
    /// </summary>
    /// <remarks>
    /// <b>Reads nothing and writes nothing.</b> It is how a refused push is told apart
    /// from a branch that moved, and it is asked of the remote rather than inferred from
    /// git's wording, which changes between versions. Carries the same credential as the
    /// push because a private repository will not advertise its refs without one.
    /// </remarks>
    public static GitInvocation LsRemote(string url, string branch, string? secret)
    {
        var (arguments, environment) = Anonymous(secret);

        arguments.AddRange(["ls-remote", url, $"refs/heads/{branch}"]);

        return new GitInvocation { Arguments = arguments, Environment = environment };
    }

    /// <summary>
    /// The argument and environment preamble that keeps an ambient credential out.
    /// </summary>
    /// <remarks>
    /// Shared by every plan that talks to a remote. A helper the machine already has
    /// configured is one that can authenticate as somebody this flight was never granted
    /// anything as, and forgetting the reset on one plan out of several is exactly the
    /// kind of gap that never shows up in a test.
    /// </remarks>
    private static (List<string> Arguments, Dictionary<string, string> Environment) Anonymous(
        string? secret)
    {
        var arguments = new List<string>
        {
            "-c", "core.askPass=",
            "-c", "credential.interactive=false",
            "-c", "credential.helper=",
        };

        var environment = new Dictionary<string, string>(StringComparer.Ordinal);

        if (!string.IsNullOrEmpty(secret))
        {
            arguments.Add("-c");
            arguments.Add(
                $"credential.helper=!f() {{ echo username=x-access-token; echo password=${SecretVariable}; }}; f");
            environment[SecretVariable] = secret;
        }

        return (arguments, environment);
    }

    public static GitInvocation Plain(params string[] arguments) =>
        new()
        {
            Arguments = arguments,
            Environment = new Dictionary<string, string>(StringComparer.Ordinal),
        };

    /// <summary>
    /// A plan that stages into an index of our own, leaving the repository's alone.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>The tree may be handed to a person, so measuring it changes nothing in
    /// it.</b> Making an untracked file visible to <c>git diff</c> needs it in an
    /// index; staging into the repository's own would leave a customer's working
    /// copy with somebody else's staged changes in it, and a flight that does not
    /// land is exactly the one somebody takes over.
    /// </para>
    /// <para>
    /// The path is outside the tree for the reason a transcript is: a scratch file
    /// inside it would itself become an untracked path in the next measurement.
    /// </para>
    /// </remarks>
    public static GitInvocation InScratchIndex(string indexPath, params string[] arguments) =>
        new()
        {
            Arguments = arguments,
            Environment = new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["GIT_INDEX_FILE"] = indexPath,
            },
        };

    /// <summary>Runs the plan and returns stdout, throwing with git's own words on failure.</summary>
    public async Task<string> RunAsync(string workingDirectory, CancellationToken cancellationToken = default)
    {
        var start = new ProcessStartInfo("git")
        {
            WorkingDirectory = workingDirectory,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
        };

        foreach (var argument in Arguments)
        {
            start.ArgumentList.Add(argument);
        }
        foreach (var (key, value) in Environment)
        {
            start.Environment[key] = value;
        }

        // A developer's own configuration must not change what a flight sees,
        // and a hook somebody installed globally must not run against a
        // customer's code on our disk.
        start.Environment["GIT_CONFIG_GLOBAL"] = "/dev/null";
        start.Environment["GIT_CONFIG_SYSTEM"] = "/dev/null";

        // NOT redundant with the line above, which is the whole reason this is
        // here. GIT_CONFIG_SYSTEM replaces ONE path; a git built by somebody
        // else reads another - on macOS the command-line tools ship a gitconfig
        // at share/git-core/gitconfig which declares an osxkeychain credential
        // helper and survives GIT_CONFIG_SYSTEM entirely. Measured, not assumed:
        // with only the two lines above, a push carrying a deliberately invalid
        // secret authenticated from the developer's keychain and succeeded.
        start.Environment["GIT_CONFIG_NOSYSTEM"] = "1";

        start.Environment["GIT_TERMINAL_PROMPT"] = "0";

        using var process = Process.Start(start)
            ?? throw new InvalidOperationException("git could not be started. Is it installed?");

        var stdout = await process.StandardOutput.ReadToEndAsync(cancellationToken);
        var stderr = await process.StandardError.ReadToEndAsync(cancellationToken);
        await process.WaitForExitAsync(cancellationToken);

        if (process.ExitCode != 0)
        {
            // git's own message, because ours would be a worse version of it.
            // Nothing here can contain the secret: it was never an argument,
            // and git does not echo the value a helper printed.
            throw new InvalidOperationException(
                $"git exited {process.ExitCode}: {Summarize(stderr)}{Summarize(stdout)}");
        }

        return stdout;
    }

    private static string Summarize(string output) =>
        output.Length <= 2000 ? output : output[..2000] + "…";
}
