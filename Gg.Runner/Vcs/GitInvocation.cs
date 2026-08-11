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
    public static GitInvocation Plain(params string[] arguments) =>
        new()
        {
            Arguments = arguments,
            Environment = new Dictionary<string, string>(StringComparer.Ordinal),
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
