using Gg.Contracts;

namespace Gg.Runner.Facts;

/// <summary>
/// Strips control sequences from everything the runner produces, at production.
/// </summary>
/// <remarks>
/// <para>
/// <b>Here, and before the digest.</b> Slice one's rule was that
/// externally-sourced text is stripped at ingress, before storage, so every
/// surface inherits the property. Two things have made that insufficient.
/// </para>
/// <para>
/// The first is correctness. <b>The evidence hash is computed over the fact as
/// produced</b>, so cleaning it on the far side makes the stored bytes disagree
/// with the hash that proves what they were - which is why the control plane
/// now REFUSES dirty text rather than cleaning it. Correct, and it means an
/// escape sequence in a pull-request title fails an honest flight over something
/// the flight never chose.
/// </para>
/// <para>
/// The second is safety. <b>The live channel is never stored</b>, so it inherits
/// nothing from a rule about storage, and it is the path that carries agent
/// output straight to a terminal.
/// </para>
/// <para>
/// So the runner normalises at production. Then the fact as produced is clean,
/// the hash matches what is stored, the live stream is clean, and <i>refuse
/// dirty text at ingress</i> becomes the RE-VALIDATION of a control rather than
/// the only control - which is the pattern this codebase already runs for
/// classification: filter before egress, re-derive at ingress.
/// </para>
/// <para>
/// <b>Every string, not the ones that look risky.</b> A path is a filename
/// somebody chose, a ref is a branch name somebody chose, a tool version is
/// whatever a binary printed. Picking the fields that look external is how the
/// one nobody thought of stays dirty.
/// </para>
/// </remarks>
public static class FactHygiene
{
    /// <summary>
    /// The same facts, with nothing in them that can drive a terminal.
    /// </summary>
    /// <remarks>
    /// Line breaks are kept where a value is prose a person reads - a loop's
    /// reason - and removed everywhere else. A newline in a path is not
    /// formatting, it is a path pretending to be two.
    /// </remarks>
    public static CleanFacts Clean(GatheredFacts gathered)
    {
        ArgumentNullException.ThrowIfNull(gathered);

        return new CleanFacts([.. gathered.Items.Select(Clean)]);
    }

    private static FactPayload Clean(FactPayload payload) => payload switch
    {
        FactPayload.Environment environment => new FactPayload.Environment(environment.Value with
        {
            HostFingerprint = Text(environment.Value.HostFingerprint),
            ImageDigest = Optional(environment.Value.ImageDigest),
            Provenance = Text(environment.Value.Provenance),
            Locks = [.. environment.Value.Locks.Select(l => l with
            {
                Path = Text(l.Path),
                Sha256 = Text(l.Sha256),
            })],
            Tools = [.. environment.Value.Tools.Select(t => t with
            {
                Name = Text(t.Name),
                Version = Text(t.Version),
            })],
        }),

        FactPayload.Source source => new FactPayload.Source(source.Value with
        {
            Provider = Text(source.Value.Provider),
            Slug = Text(source.Value.Slug),
            RequestedRef = Text(source.Value.RequestedRef),
            ResolvedRef = Text(source.Value.ResolvedRef),
            HeadCommit = Text(source.Value.HeadCommit),
            // The pull-request title's neighbour, and the field a forge fills
            // in from somebody else's account name.
            ForkSlug = Optional(source.Value.ForkSlug),
        }),

        // A push carries a slug, a branch and a sha - none of them prose, and all of
        // them stripped anyway. A control sequence in a branch name reaches a
        // person's terminal through the gate list.
        FactPayload.Push push => new FactPayload.Push(push.Value with
        {
            Slug = Text(push.Value.Slug),
            Branch = Text(push.Value.Branch),
            Commit = Text(push.Value.Commit),
        }),

        FactPayload.Change change => new FactPayload.Change(change.Value with
        {
            BaseCommit = Text(change.Value.BaseCommit),
            HeadCommit = Text(change.Value.HeadCommit),
            Resolution = Text(change.Value.Resolution),
            DiffBasis = Text(change.Value.DiffBasis),
            Paths = [.. change.Value.Paths.Select(p => p with
            {
                // A filename can contain a control byte. Rare, entirely legal,
                // and it reaches a terminal through the console's manifest view.
                Path = Text(p.Path),
                Change = Text(p.Change),
                Classification = Text(p.Classification),
            })],
            Directories = [.. change.Value.Directories.Select(d => d with
            {
                Directory = Text(d.Directory),
            })],
            Languages = [.. change.Value.Languages.Select(l => l with
            {
                Language = Text(l.Language),
            })],
        }),

        FactPayload.Loop loop => new FactPayload.Loop(loop.Value with
        {
            LoopId = Text(loop.Value.LoopId),
            Outcome = Text(loop.Value.Outcome),
            // THE ONE THE CONTROL PLANE REFUSES TODAY. It is the only free text
            // in the vocabulary that comes out of a process rather than out of a
            // measurement, so it is the field most likely to carry one - and
            // line breaks are kept, because it is prose somebody reads.
            Reason = Prose(loop.Value.Reason),
            Executor = Text(loop.Value.Executor),
            MovesUsed = [.. loop.Value.MovesUsed.Select(Text)],
        }),

        FactPayload.Transcript transcript => new FactPayload.Transcript(transcript.Value with
        {
            Locator = Text(transcript.Value.Locator),
            Sha256 = Text(transcript.Value.Sha256),
            MediaType = Text(transcript.Value.MediaType),
            Scope = Text(transcript.Value.Scope),
        }),

        // THE SAME FOUR STRINGS, because it is the same shape one level in. A
        // nested payload whose members nobody cleaned is how the one field
        // nobody thought of stays dirty - which is this file's own argument.
        FactPayload.Session session => new FactPayload.Session(session.Value with
        {
            Artifact = session.Value.Artifact with
            {
                Locator = Text(session.Value.Artifact.Locator),
                Sha256 = Text(session.Value.Artifact.Sha256),
                MediaType = Text(session.Value.Artifact.MediaType),
                Scope = Text(session.Value.Artifact.Scope),
            },
        }),

        FactPayload.Landing landing => new FactPayload.Landing(landing.Value with
        {
            DestinationId = Text(landing.Value.DestinationId),
            Branch = Text(landing.Value.Branch),
            // Both come back from a forge, which is somebody else's server
            // answering with somebody else's text.
            PullRequestUri = Text(landing.Value.PullRequestUri),
        }),

        FactPayload.Digest summary => new FactPayload.Digest(summary.Value with
        {
            LoopId = Text(summary.Value.LoopId),
            FilesReadNotEdited = [.. summary.Value.FilesReadNotEdited.Select(Text)],
            FilesEdited = [.. summary.Value.FilesEdited.Select(Text)],
            Searches = [.. summary.Value.Searches.Select(Text)],
            Errors = [.. summary.Value.Errors.Select(e => e with
            {
                Source = Text(e.Source),
                Detail = Text(e.Detail),
            })],
            RefusedMoves = [.. summary.Value.RefusedMoves.Select(Text)],
            StopReason = Text(summary.Value.StopReason),
        }),

        FactPayload.Nomination nomination => new FactPayload.Nomination(nomination.Value with
        {
            // A NAME AND A SENTENCE, cleaned as what each is. The work kind is
            // a name in a topology, so a line break in one is a name nobody
            // declared; the reason is prose the agent wrote for a reader, and
            // its line breaks are its own - the same split loop.outcome's
            // reason already gets.
            WorkKind = Text(nomination.Value.WorkKind),
            Reason = Prose(nomination.Value.Reason),
        }),

        FactPayload.Proposal proposal => new FactPayload.Proposal(proposal.Value with
        {
            // THE NOMINATION'S SPLIT, one noun over: names are names and the
            // reason is prose. The operation is a closed vocabulary value, the
            // target is an id "as the tracker spells its own", and the score is
            // bounded so it cannot become the analysis - so a line break in any
            // of the three is a value that arrived with somebody's formatting
            // still attached rather than formatting anybody chose.
            Operation = Text(proposal.Value.Operation),
            Reason = Prose(proposal.Value.Reason),
            Target = proposal.Value.Target is { } target ? Text(target) : null,
            Score = proposal.Value.Score is { } score ? Text(score) : null,

            // A PATH IS AN IDENTIFIER AND A VALUE IS NOT. The path is matched
            // against a menu, so a line break in one is a field nobody
            // permitted; what the field HOLDS belongs to the tracker and the
            // rubric, and a description set over three lines is one somebody
            // wrote that way. Flattening it would put our formatting into
            // somebody else's backlog.
            Fields = proposal.Value.Fields is { } fields
                ? [.. fields.Select(f => f with { Path = Text(f.Path), Value = Prose(f.Value) })]
                : null,

            // DETAIL IS NOT TOUCHED, and that is deliberate rather than missed.
            // It is a JsonElement whose shape is the agent's, nothing here
            // reads it, and the server already bounds its size - so there is no
            // string here to clean without deciding what its fields mean.
        }),

        // A TITLE IS TEXT AND A DESCRIPTION IS PROSE, which is the same split
        // the proposal above makes: one is a line somebody reads in a list, and
        // the contract already refuses a title carrying a line break, so
        // stripping one here would be stripping what cannot arrive. The
        // description is a paragraph a reviewer reads and keeps its breaks.
        FactPayload.ProposedLanding landing => new FactPayload.ProposedLanding(
            landing.Value with
            {
                Title = Text(landing.Value.Title),
                Description = landing.Value.Description is { } said ? Prose(said) : null,
            }),

        FactPayload.Question question => new FactPayload.Question(question.Value with
        {
            // PROSE, so its line breaks are its own. A question laid out over
            // three lines is one somebody wrote to be read, and the field a
            // person reads while deciding something is the wrong one to
            // flatten.
            Question = Prose(question.Value.Question),
        }),

        FactPayload.Attended attended => new FactPayload.Attended(attended.Value with
        {
            // TIGHT THROUGHOUT, and it is the only payload here where that is
            // true of every member. Nothing on this fact was written for a
            // reader - a loop id, a rung, a binary name, a version string a tool
            // printed - so a line break in one of them is a value captured with
            // its own newline still attached rather than somebody's formatting.
            LoopId = Text(attended.Value.LoopId),
            Rung = Text(attended.Value.Rung),
            Binary = Text(attended.Value.Binary),
            BinaryVersion = Text(attended.Value.BinaryVersion),
            Unmeasured = [.. attended.Value.Unmeasured.Select(Text)],
            SettingsCleared = [.. attended.Value.SettingsCleared.Select(Text)],
        }),

        FactPayload.Preview preview => new FactPayload.Preview(preview.Value with
        {
            // TIGHT THROUGHOUT, like the attended fact above it. An address, an
            // exposure's name and a two-digit slot are all values rather than
            // anything written to be read, so a line break in one of them is
            // something that came off a machine with its newline still
            // attached - never somebody's formatting.
            //
            // THE ADDRESS IS BUILT FROM THE GRANT and never read back out of a
            // connector's output, so nothing a provider printed reaches here.
            // It is cleaned anyway: this class's job is that no payload crosses
            // unexamined, and "the source is trusted" is the argument every
            // unexamined field was let through on.
            Url = Text(preview.Value.Url),
            Exposure = Text(preview.Value.Exposure),
            Slot = Text(preview.Value.Slot),
        }),

        // Unreachable while every payload is handled above, and a compile error
        // is not available for a switch over a hierarchy. Throwing beats
        // returning the payload unchanged: a new fact type that quietly skipped
        // this is the exact failure this class exists to prevent.
        _ => throw new InvalidOperationException(
            $"{payload.GetType().Name} is a fact the runner produces and nothing strips it. Add it "
          + "to FactHygiene rather than letting it cross unexamined."),
    };

    /// <summary>A value that is not prose: no control sequences, no line breaks.</summary>
    private static string Text(string value) => ControlText.Strip(value) ?? "";

    /// <summary>Prose a person reads, so line breaks survive and nothing else does.</summary>
    private static string Prose(string value) =>
        ControlText.Strip(value, allowLineBreaks: true) ?? "";

    private static string? Optional(string? value) =>
        value is null ? null : ControlText.Strip(value);
}
