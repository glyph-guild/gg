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
    /// <summary>
    /// What this machine can answer on its own: the working copy, walked.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>FIRST, AND WHETHER OR NOT ANYBODY CAN BE ASKED ANYTHING.</b> This
    /// used to happen after the topology and only if it succeeded, under a
    /// reason that has stopped being true: <i>"without the names there are no
    /// rows to hang a working-copy state on"</i>. There were none when the
    /// rows were names the estate holds. The rows are files on disk now, and
    /// a file does not need permission to exist.
    /// </para>
    /// <para>
    /// <b>Its own function so it can be asserted without a control plane</b>,
    /// which is also the only way to assert the property that matters: that a
    /// refused topology leaves the tree in hand.
    /// </para>
    /// <para>
    /// <b>Nothing when nobody has said where.</b> Walking the directory gg
    /// was launched from on the chance it is an airspace is how <c>p</c> came
    /// to write a tree into somebody's home with no git to refuse it.
    /// </para>
    /// </remarks>
    public static AppState Local(string? root, AppState state)
    {
        ArgumentNullException.ThrowIfNull(state);

        var known = state.Estate ?? Nothing(root);

        if (root is not { Length: > 0 } tree)
        {
            return state with { Estate = known with { Root = root, Tree = null } };
        }

        try
        {
            return state with
            {
                Estate = known with
                {
                    Root = root,
                    IsRepository = Git.IsRepository(tree),
                    Tree = Summarised(tree, Gg.Client.AirspaceTree.Read(tree)),
                    Uncommitted = Gg.Client.AirspaceTree.Dirty(tree),
                },
            };
        }
        catch (Exception unreadable) when (
            unreadable is IOException or UnauthorizedAccessException)
        {
            // SAID RATHER THAN THROWN, and in the slot the working copy's own
            // failures use. A directory gg cannot read is a fact about this
            // machine, which is what this whole function is about.
            return state with
            {
                Estate = known with
                {
                    Root = root,
                    Tree = null,
                    Diagnosis = "The working copy could not be read: " + unreadable.Message,
                },
            };
        }
    }

    /// <summary>
    /// The walk, with the documents themselves left behind.
    /// </summary>
    /// <remarks>
    /// <b>THE LINE THIS TYPE HOLDS, AND IT NEARLY DID NOT.</b>
    /// <c>TreeDocument</c> carries the parsed envelope, narrowing or strategy,
    /// and <c>AppState</c> goes into <c>GG_STATE_DUMP</c> and the diagnostics
    /// bundle — so holding the walk as it comes would ship a tenant's rules in
    /// a file they send us. Name, role, path and version is what a row needs;
    /// the rest stays on disk, which is where the pane's reader can go for it.
    /// </remarks>
    private static WorkingCopy Summarised(string root, Gg.Client.TreeRead read) => new()
    {
        Present = read.Present,
        Documents =
        [
            .. read.Documents.Select(d =>
                new AirspaceFile(d.Role, d.Name, d.Path, d.BasedOn)
                {
                    // VERBATIM, because the point of the on-disk tab is what
                    // is ACTUALLY in the file. Re-rendering the parsed model
                    // would show what gg thinks it means, which is what the
                    // applied tab beside it already answers.
                    Text = Said(root, d.Path),
                }),
        ],
        Unreadable =
        [
            .. read.Unreadable.Select(u => new UnreadableFile(u.Path, u.Diagnosis)),
        ],
    };

    /// <summary>An estate nothing has been read into yet.</summary>
    private static EstateOnThisMachine Nothing(string? root) =>
        new() { Root = root, Uncommitted = [] };

    public static AppState Read(ConsoleData data, string? root, AppState state)
    {
        ArgumentNullException.ThrowIfNull(data);
        ArgumentNullException.ThrowIfNull(state);

        // THE LOCAL WALK FIRST, so everything below is added to it rather than
        // instead of it. A topology that cannot be asked used to cost the
        // whole pane; it costs the names and the diff now, which is what it
        // actually answers.
        state = Local(root, state);

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
            // THE NAMES AND NOTHING ELSE THEY WOULD HAVE BROUGHT. The walk
            // above survives, which is the point: a person with a pulled
            // airspace and no session sees their documents and is told the
            // estate could not be asked, rather than being shown nothing and
            // told the same.
            return state with
            {
                Estate = state.Estate! with
                {
                    Diagnosis = "The airspace could not be read: " + failure.Message,
                },
            };
        }

        var (working, why) = root is { Length: > 0 } tree
            ? Working(data, tree)
            : (null, null);

        return state with
        {
            Estate = state.Estate! with
            {
                Names = names,
                Working = working,

                // AND EVERY DOCUMENT, WHOLE, so the pane beside the tree draws
                // whatever the cursor lands on without asking anybody. One
                // request for all of them: a fetch per row would be I/O on an
                // arrow key, which this console does not do.
                //
                // KEPT ON A FAILURE. The documents are the previous read's and
                // are still true of what is applied; emptying the pane because
                // a later read failed would take away what somebody is looking
                // at to report that something else went wrong.
                Applied = Documents(data) ?? state.Estate.Applied,
                Diagnosis = why,
            },
        };
    }

    /// <summary>
    /// Every applied document, or null when the read refused.
    /// </summary>
    /// <remarks>
    /// <b>Null is "ask again", not "there are none".</b> An estate with no
    /// documents is an empty list and a real state - a tenant before their
    /// first apply - so the two cannot share an answer, and the caller keeps
    /// what it had rather than blanking the pane over a failure the pane is
    /// not about.
    /// </remarks>
    private static IReadOnlyList<Gg.Contracts.NamedEnvelopeState>? Documents(ConsoleData data)
    {
        try
        {
            return data.AirspaceDocumentsAsync().GetAwaiter().GetResult()
                is VerbResult.AirspaceDocuments read
                ? read.Value.Documents
                : null;
        }
        catch (Exception refused) when (refused is NotSignedInException
                                            or ProtocolTooOldException
                                            or HttpRequestException)
        {
            return null;
        }
    }

    /// <summary>
    /// What one file says, or null when it cannot be read.
    /// </summary>
    /// <remarks>
    /// <b>Null is not an empty file.</b> A file the walk parsed and then could
    /// not re-read is a race or a permission change, and a pane drawing an
    /// empty document for it would report a state that does not exist. The
    /// walk has already established the path is one gg wrote, so this is the
    /// local read a session is allowed - and the ONLY read in this function.
    /// </remarks>
    private static string? Said(string root, string path)
    {
        try
        {
            return File.ReadAllText(
                Path.Combine(root, Path.Combine(path.Split('/'))));
        }
        catch (Exception unreadable) when (
            unreadable is IOException or UnauthorizedAccessException)
        {
            return null;
        }
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
