namespace Gg.Cli;

/// <summary>The <c>gg-manage</c> tool server.</summary>
public static class ManageToolServer
{
    public static Task<int> RunAsync(
        TextReader input, TextWriter output, RunVerb run, CancellationToken cancellationToken = default) =>
        Task.FromResult(0);
}
