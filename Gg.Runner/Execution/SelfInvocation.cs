namespace Gg.Runner.Execution;

/// <summary>
/// How this process starts another copy of itself.
/// </summary>
/// <remarks>
/// <para>
/// <b>Needed because the platform's own tool server is a re-exec.</b> The launch
/// hands the agent a command to start, and the command is this binary with a
/// verb - so the runner has to be able to name its own executable, and be right
/// about it in both shapes this program ships in.
/// </para>
/// <para>
/// <b>The two shapes, and why guessing is not available.</b> Published as an
/// apphost, <c>Environment.ProcessPath</c> IS <c>gg</c> and the verb is the
/// whole argument list. Run as <c>dotnet gg.dll</c>, the process path is the
/// <c>dotnet</c> host and the entry assembly is the dll - so the command needs
/// the dll before the verb. Reading only the process path gives
/// <c>dotnet runner nominate</c>, which starts nothing, and the agent would
/// still be told the tool exists.
/// </para>
/// <para>
/// <b>Resolved once, as a process fact.</b> <see cref="Current"/> cannot differ
/// between reads, so the launch and the pre-flight refusal ask the same
/// question and get the same answer - which is what
/// <c>ExecutorConfiguration</c>'s rule about one place reading the environment
/// is protecting.
/// </para>
/// </remarks>
/// <param name="Command">The executable to start.</param>
/// <param name="Arguments">Everything after it, verb included.</param>
public sealed record SelfInvocation(string Command, IReadOnlyList<string> Arguments)
{
    /// <summary>The verb the tool server is served under.</summary>
    private static readonly string[] Verb = ["runner", "nominate"];

    /// <summary>
    /// How to start this process again, or null where it cannot be named.
    /// </summary>
    public static SelfInvocation? Current { get; } =
        For(Environment.ProcessPath, Environment.GetCommandLineArgs().FirstOrDefault());

    /// <summary>
    /// The same decision over values, so both deployment shapes are testable
    /// without being in one.
    /// </summary>
    /// <param name="processPath">
    /// <c>Environment.ProcessPath</c>: the apphost, or the <c>dotnet</c> host.
    /// </param>
    /// <param name="entryPath">
    /// <c>Environment.GetCommandLineArgs()[0]</c>: the dll when hosted.
    /// </param>
    public static SelfInvocation? For(string? processPath, string? entryPath)
    {
        if (processPath is not { Length: > 0 })
        {
            // SAYS SO RATHER THAN GUESSING. A server configured with a path
            // that is not this binary is a child that fails at startup, and the
            // agent would have been told the tool exists.
            return null;
        }

        // THE DLL IS THE TELL. A hosted run's entry assembly is a managed
        // library; an apphost's is the apphost. Nothing else distinguishes them
        // from inside the process.
        return entryPath is { Length: > 0 } entry
            && entry.EndsWith(".dll", StringComparison.OrdinalIgnoreCase)
            ? new SelfInvocation(processPath, [entry, .. Verb])
            : new SelfInvocation(processPath, Verb);
    }
}
