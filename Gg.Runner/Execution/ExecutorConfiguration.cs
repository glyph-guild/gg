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
    /// <summary>Which agent this machine has and where, when it has one.</summary>
    /// <remarks>
    /// Spelled in <see cref="ExecutorDeclaration"/>, which is also what parses
    /// it: a bare path is claude, <c>agent=path</c> names one, and the doctor
    /// reads it through the same parser from a project that cannot see this one.
    /// </remarks>
    public const string BinaryVariable = ExecutorDeclaration.Variable;

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
        // THE CHOICE IS MADE HERE, IN THE DEFAULT, which is where the vcs and
        // destination seams learned it has to be: "the adapterFor parameter
        // was passed only from tests, which is the same bug one layer up".
        // ExecutorDeclaration.Known has one member, so this is not a switch
        // yet - and the day it is, the second arm is here rather than in a
        // caller that assumed the first.
        ExecutorDeclaration.ParseOrNull(
            Environment.GetEnvironmentVariable(BinaryVariable), BinaryVariable)
            is { } declared
            ? new ClaudeCodeExecutor(
                declared.Binary,
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
