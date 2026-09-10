using Gg.Client;

namespace Gg.Console;

/// <summary>
/// The estate read: every name the tenant has, and what the working copy on
/// this machine says about them.
/// </summary>
/// <remarks>
/// <para>
/// A composition-root function for <see cref="ConsoleChecklist"/>'s reason:
/// <see cref="ConsoleLoop"/> is handed something it can call and never a read
/// surface.
/// </para>
/// <para>
/// <b>Two reads, and the second is allowed to fail on its own.</b> The topology
/// comes off the control plane and the working copy comes off this disk, so a
/// tenant with a whole estate and nothing pulled is the ordinary state before
/// anybody's first pull — not an error, and not the same thing as a tree that
/// would not read. Losing the names because the tree is missing would hide the
/// half that is always available.
/// </para>
/// <para>
/// <b>The results are unwrapped here rather than projected.</b> Every other
/// read maps one <c>VerbResult</c> onto one field, which a projection arm does
/// well; this one joins two of them plus two facts about the machine into a
/// single record, and a pair of arms each filling part of it would leave the
/// record half-built between them.
/// </para>
/// </remarks>
public static class ConsoleEstate
{
    /// <summary>Folds the estate into the model, or says why it could not.</summary>
    /// <param name="root">
    /// Where the working copy is, or null when nobody has said. Resolved by the
    /// composition root through the same setting the verbs use, so the pane
    /// cannot name one tree while <c>gg airspace pull</c> writes to another.
    /// </param>
    public static AppState Read(ConsoleData data, string? root, AppState state)
    {
        ArgumentNullException.ThrowIfNull(data);
        ArgumentNullException.ThrowIfNull(state);

        Gg.Contracts.EnvelopeTopology names;
        try
        {
            names = data.TopologyAsync().GetAwaiter().GetResult()
                is VerbResult.AirspaceTopology topology
                ? topology.Value
                : throw new InvalidOperationException(
                    "The topology read answered something other than a topology.");
        }
        catch (Exception failure) when (failure is NotSignedInException
                                            or ProtocolTooOldException
                                            or HttpRequestException
                                            or InvalidOperationException)
        {
            // NOTHING RATHER THAN A HALF-READ ESTATE. Without the names there
            // are no rows to hang a working-copy state on, and a pane listing
            // local edits to documents it cannot name is worse than one saying
            // it could not ask.
            return state with
            {
                Estate = new EstateOnThisMachine
                {
                    Root = root,
                    Diagnosis = "The airspace could not be read: " + failure.Message,
                },
            };
        }

        var (working, why) = root is { Length: > 0 } tree
            ? Working(data, tree)
            : (null, null);

        return state with
        {
            Estate = new EstateOnThisMachine
            {
                Root = root,
                IsRepository = root is { Length: > 0 } && Git.IsRepository(root),
                Names = names,
                Working = working,
                Diagnosis = why,
            },
        };
    }

    /// <summary>
    /// What the working copy differs by, or null with a reason.
    /// </summary>
    /// <remarks>
    /// <b>The diff verb, not a second reading of the tree.</b> It is the one
    /// place direction is computed, from the comparator the control plane runs,
    /// so the pane reports what the door will decide rather than an opinion
    /// formed here.
    /// </remarks>
    private static (EstateDiff? Working, string? Why) Working(ConsoleData data, string root)
    {
        try
        {
            return data.EstateDiffAsync(root).GetAwaiter().GetResult()
                is VerbResult.AirspaceDiffed diffed
                ? (diffed.Value, null)
                : (null, "The working copy read answered something other than a diff.");
        }
        catch (Exception failure) when (failure is NotSignedInException
                                            or ProtocolTooOldException
                                            or EnvelopeRefusedException
                                            or EnvelopeUnreadableException
                                            or HttpRequestException
                                            or IOException
                                            or UnauthorizedAccessException)
        {
            return (null, "The working copy could not be read: " + failure.Message);
        }
    }
}
