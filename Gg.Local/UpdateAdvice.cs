namespace Gg.Local;

/// <summary>
/// What to tell a person who wants a newer <c>gg</c>, and nothing more than telling.
/// </summary>
/// <remarks>
/// <para>
/// <b>This type moves no bytes and has no way to.</b> It holds strings. There
/// is no download here, no write, no process started to do either — which is
/// the rule stated as a shape rather than as a comment, and
/// <c>UpdateBoundaryTests</c> holds the whole verb to it.
/// </para>
/// <para>
/// <b>An unknown current version is an absence, and renders as one.</b> The
/// failure this project keeps finding is silence reading as agreement, and the
/// field it would matter most on is this one: a person told "up to date"
/// because the oracle could not be reached does not update, and never learns
/// they were asked to.
/// </para>
/// <para>
/// <b>No download URL is compiled in, and that is deliberate rather than
/// awkward.</b> Where the bytes live is the forge's business, and
/// <c>ProviderNeutralityTests</c> keeps a forge's name out of this binary so
/// that a second one ships without changing it. The native shape therefore
/// names the release by version and lets the person go where they got it —
/// or prints a location the control plane supplied, which makes the channel
/// configuration rather than a constant.
/// </para>
/// </remarks>
/// <param name="Shape">Which install this is.</param>
/// <param name="Current">What the oracle says is current, or null when it could not say.</param>
/// <param name="Summary">One line for a person.</param>
/// <param name="Commands">What to run, which may legitimately be nothing.</param>
public sealed record UpdateAdvice(
    InstallShape Shape,
    string? Current,
    string Summary,
    IReadOnlyList<string> Commands)
{
    /// <summary>The id on the feed, which is not the command a person types.</summary>
    public const string PackageId = "GlyphGuild.Gg.Cli";

    /// <summary>Whether the current version is known at all.</summary>
    /// <remarks>
    /// Read by the renderer so that "not known" is a state it must handle
    /// rather than a null it may format into a sentence by accident.
    /// </remarks>
    public bool CurrentIsKnown => Current is { Length: > 0 };

    /// <summary>
    /// The advice for a shape, given what is current — or given that nothing is.
    /// </summary>
    public static UpdateAdvice For(InstallShape shape, string? current)
    {
        ArgumentNullException.ThrowIfNull(shape);

        var known = current is { Length: > 0 };
        var pin = known ? $" --version {current}" : string.Empty;

        // THE CAVEAT IS PART OF THE SENTENCE, not an aside appended to it.
        // Whatever else this says, it must not be readable as currency.
        var caveat = known
            ? $"The current version is {current}."
            : "What version is current could not be established, so this may already be the newest.";

        return shape.Kind switch
        {
            InstallKind.GlobalTool => new UpdateAdvice(
                shape,
                current,
                $"This gg is a .NET tool. {caveat}",
                [$"dotnet tool update -g {PackageId}{pin}"]),

            // THE PATH IS NAMED because -g would resolve somewhere else. On a
            // pool host the shim is root-owned at /usr/local/lib/gg and the
            // runner is another user entirely; -g installs into the home of
            // whoever typed it and reports success.
            InstallKind.ToolPath => new UpdateAdvice(
                shape,
                current,
                $"This gg is a .NET tool installed at {shape.ToolPath}. {caveat}",
                [$"dotnet tool update {PackageId}{pin} --tool-path {shape.ToolPath}"]),

            // NO COMMAND, because there is no command. A self-contained binary
            // is replaced by fetching another one, and offering a `dotnet tool`
            // line here would fail in a way that reads as a broken machine.
            InstallKind.Native => new UpdateAdvice(
                shape,
                current,
                known
                    ? $"This gg is a self-contained binary and updates by being downloaded again. "
                    + $"Fetch the {current} release asset for this platform and install it over "
                    + "this one; nothing here can do it for you."
                    : "This gg is a self-contained binary and updates by being downloaded again. "
                    + caveat,
                []),

            // Rule 3: what reset resets TO must be a fixed point, so a change
            // made inside a member survives until the next reset and no longer.
            InstallKind.Container => new UpdateAdvice(
                shape,
                current,
                known
                    ? $"This gg runs in a container, where the image is the unit of change. "
                    + $"Rebuild the member image on {current} and repin it by digest — a change "
                    + "made in here does not outlive the next reset."
                    : "This gg runs in a container, where the image is the unit of change. "
                    + caveat,
                []),

            _ => new UpdateAdvice(
                shape,
                current,
                "How this gg was installed could not be established, so no command is offered "
                + "rather than a guess. " + caveat,
                []),
        };
    }
}
