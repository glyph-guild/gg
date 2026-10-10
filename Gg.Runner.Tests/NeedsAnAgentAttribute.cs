using Gg.Local;

namespace Gg.Runner.Tests;

/// <summary>
/// Skips a test, with its reason, wherever no agent binary has been named.
/// </summary>
/// <remarks>
/// <para>
/// <b>Skipped, not thrown and not passed</b> - the rule
/// <c>Gg.Console.Tests</c>' <c>NeedsAControlPlane</c> set for the control plane.
/// Every <c>RealAgent</c> test drives a real agent through
/// <c>GG_EXECUTOR_BINARY</c>, and without it they threw, so a plain
/// <c>dotnet test</c> here ended red with nothing wrong. CI never ran them
/// either: its filter leaves out <c>Category=RealAgent</c>. The summary counts
/// these as skipped and names what to set.
/// </para>
/// <para>
/// <b>Set it and they run</b>, and spend what a real agent spends - which is
/// why they were never in CI.
/// </para>
/// </remarks>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method)]
public sealed class NeedsAnAgentAttribute()
    : SkipAttribute(
        "drives a real agent: set " + ExecutorDeclaration.Variable + " to the agent binary "
      + "to run it")
{
    public override Task<bool> ShouldSkip(TestRegisteredContext context) =>
        Task.FromResult(string.IsNullOrEmpty(
            Environment.GetEnvironmentVariable(ExecutorDeclaration.Variable)));
}
