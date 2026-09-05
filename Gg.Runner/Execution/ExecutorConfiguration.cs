using Gg.Local;
namespace Gg.Runner.Execution;

/// <summary>
/// Which executor this machine has, read from its environment.
/// </summary>
/// <remarks>
/// <para>
/// <b>Configured, like the vcs and destination adapters beside it.</b> Which agent
/// binary a machine has is deployment knowledge: <c>gg</c> is public and
/// distributed, and an operator who has not installed one has not installed one.
/// </para>
/// <para>
/// <b>Null is the ordinary state and not a degraded one.</b> A runner with no
/// executor does what every runner did before an executor existed - materialize,
/// extract, ship - and a flight that declares no loop was never going to invoke
/// anything anyway.
/// </para>
/// <para>
/// <b>This existing is the fix.</b> Until it did, <c>ClaudeCodeExecutor</c> was
/// constructed nowhere outside the test assemblies: <c>RunnerHost</c> took no
/// executor and the CLI passed none, so every runner the product started built a
/// loop whose executor was null and no flight ever invoked an agent. The seventh
/// instance of <i>registered is not invoked</i>, and the one that made every
/// question about what a loop may do unanswerable, because no loop ran.
/// </para>
/// </remarks>
public static class ExecutorConfiguration
{
    /// <summary>Where the agent binary is, when this machine has one.</summary>
    public const string BinaryVariable = "GG_EXECUTOR_BINARY";

    /// <summary>The executor this machine is configured for, or null for none.</summary>
    /// <remarks>
    /// <b>Built WITH the trackers this runner can read</b>, because an executor
    /// that had them and was never given them is the shape this whole slice
    /// exists to remove. One place reads the environment; nothing downstream
    /// reads it again and reaches a different answer.
    /// </remarks>
    public static IExecutorPort? FromEnvironment(
        IReadOnlyList<IntentReader>? readers = null,
        Func<string, string?>? secretFor = null) =>
        Environment.GetEnvironmentVariable(BinaryVariable) is { Length: > 0 } binary
            ? new ClaudeCodeExecutor(
                binary,
                readers ?? IntentConfiguration.FromEnvironment(),
                secretFor,
                // THE ONE PLACE, again. How this process re-execs itself is a
                // process fact rather than configuration, so it cannot drift
                // between reads - but it is resolved here anyway, beside the
                // trackers, because an executor that had it and was never given
                // it is the shape this type exists to remove.
                SelfInvocation.Current)
            : null;
}
