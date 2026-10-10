namespace Gg.Console.Tests;

/// <summary>
/// Skips a test, with its reason, wherever no control plane has been named.
/// </summary>
/// <remarks>
/// <para>
/// <b>Skipped, not thrown and not passed.</b> These used to throw when
/// <c>GG_CONSOLE_API</c> was unset, so a plain <c>dotnet test</c> on any machine
/// without the stack up ended 13 red with nothing wrong - and those 13 hid
/// whatever else had gone red beside them. CI never ran them either; its filter
/// leaves out <c>Category=RealStack</c>. A skip says the same thing honestly: the
/// summary counts them as skipped, never as passed, and names what to set.
/// </para>
/// <para>
/// <b>Set the variable and they run</b>, against whatever <c>Gg.AppHost</c> it
/// points at. Nothing about what they measure changed.
/// </para>
/// </remarks>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method)]
public sealed class NeedsAControlPlaneAttribute()
    : SkipAttribute(
        "needs a live control plane: bring up Gg.AppHost and set GG_CONSOLE_API to the api's "
      + "address")
{
    /// <summary>The variable that names one.</summary>
    public const string Variable = "GG_CONSOLE_API";

    public override Task<bool> ShouldSkip(TestRegisteredContext context) =>
        Task.FromResult(string.IsNullOrEmpty(Environment.GetEnvironmentVariable(Variable)));
}
