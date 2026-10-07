using Gg.Contracts;
using Gg.Contracts.Authoring;
using Gg.Local;

namespace Gg.Client;

/// <summary>A plan being drafted: finished or not, it is what the plan file says.</summary>
/// <param name="Planner">The planning kind whose destination bounds the legs.</param>
/// <param name="Intent">What the plan is about, or null until somebody says.</param>
/// <param name="Legs">The legs so far, in order.</param>
public sealed record PlanDraft(string Planner, FlightIntent? Intent, IReadOnlyList<FlightNomination> Legs)
{
    /// <summary>The planning kind a draft names when nobody chose one - the kind that proposes legs.</summary>
    public const string DefaultPlanner = "plan";

    /// <summary>A draft nobody has started.</summary>
    public static PlanDraft Empty { get; } = new(DefaultPlanner, null, []);
}

/// <summary>What reading a draft concluded.</summary>
public abstract record DraftRead
{
    private DraftRead()
    {
    }

    /// <summary>The draft. A file nobody has written yet reads as <see cref="PlanDraft.Empty"/>.</summary>
    public sealed record Held(PlanDraft Draft) : DraftRead;

    /// <summary>The file is there and does not read, in the parser's own sentence.</summary>
    public sealed record Unreadable(string Diagnosis) : DraftRead;
}

/// <summary>What one change to a draft concluded.</summary>
public abstract record DraftChange
{
    private DraftChange()
    {
    }

    /// <summary>The change was written, and this is the draft now.</summary>
    public sealed record Written(PlanDraft Draft) : DraftChange;

    /// <summary>Nothing was written, and this is why.</summary>
    public sealed record Refused(string Diagnosis) : DraftChange;

    /// <summary>A change refusing itself: nothing is written, and the sentence is the answer.</summary>
    public static DraftChange Refuse(string diagnosis) => new Refused(diagnosis);
}

/// <summary>
/// The drafts a planning tool server keeps, one plan file each. Slice sixty-three.
/// </summary>
/// <remarks>
/// <para>
/// <b>Read fresh, written whole</b> (rule 3). Every change reads the file, applies one edit and
/// writes by atomic rename, and nothing is held between calls - so a person editing the file by
/// hand and an agent calling a tool are editing the same draft, and closing either loses nothing.
/// </para>
/// <para>
/// <b>A file that does not read is never written</b> (rule 4). A person's hand edit with a
/// mistake in it is answered with the parser's sentence and left exactly as they typed it.
/// </para>
/// </remarks>
public sealed class ItineraryDrafts(string root)
{
    private readonly string _root = root;

    /// <summary>The drafts kept on this machine, under the state root.</summary>
    public static ItineraryDrafts ForThisMachine(string? stateHome = null) =>
        new(Path.Combine(LocalPaths.StateRoot(stateHome), "itineraries"));

    /// <summary>Why a draft cannot be called <paramref name="name"/>, or null when it can.</summary>
    /// <remarks>
    /// A plain word: letters, digits, <c>-</c> and <c>_</c>, not starting with a dot. Anything else
    /// could put the file outside the drafts directory or hide it inside.
    /// </remarks>
    public static string? Refused(string name) =>
        name is { Length: > 0 and <= 64 }
        && name[0] != '.'
        && name.All(c => char.IsAsciiLetterOrDigit(c) || c is '-' or '_' or '.')
        && !name.Contains("..", StringComparison.Ordinal)
            ? null
            : $"'{name}' is not a draft name. A draft is named by one plain word - letters, digits, "
            + "'-' and '_' - because the name is its file under the drafts directory.";

    /// <summary>Where the draft called <paramref name="name"/> is kept.</summary>
    public string PathOf(string name) => Path.Combine(_root, name + ".yaml");

    /// <summary>
    /// Where the tool server leaves its last result for <paramref name="name"/> - the mux panel's
    /// only source of verdicts. Slice sixty-six.
    /// </summary>
    public string ResultPathOf(string name) => Path.Combine(_root, name + ".result.txt");

    /// <summary>The last result, or null when the server has said nothing yet.</summary>
    public string? LastResult(string name) =>
        Refused(name) is null && File.Exists(ResultPathOf(name)) ? File.ReadAllText(ResultPathOf(name)) : null;

    /// <summary>
    /// Leaves <paramref name="text"/> as the last result, written whole: a panel reading it sees
    /// the old result or the new, never half of either.
    /// </summary>
    public void KeepResult(string name, string text)
    {
        if (Refused(name) is not null)
        {
            return;
        }

        Directory.CreateDirectory(_root);
        var staged = ResultPathOf(name) + ".writing";
        File.WriteAllText(staged, text);
        File.Move(staged, ResultPathOf(name), overwrite: true);
    }

    /// <summary>Where the record of what this draft was last proposed as is kept.</summary>
    public string ProposedPathOf(string name) => Path.Combine(_root, name + ".proposed");

    /// <summary>What this draft was last proposed as, or null when it never was.</summary>
    /// <remarks>
    /// <b>A record that cannot be read is no record</b>, and says nothing: the bar then shows the
    /// draft as unproposed, and the door's own claims are what stop a second plan regardless.
    /// </remarks>
    public ProposedPlan? Proposed(string name)
    {
        if (Refused(name) is not null || !File.Exists(ProposedPathOf(name)))
        {
            return null;
        }

        try
        {
            using var read = System.Text.Json.JsonDocument.Parse(File.ReadAllText(ProposedPathOf(name)));
            var root = read.RootElement;
            return new ProposedPlan(
                root.GetProperty("itinerary").GetString()!,
                root.GetProperty("pass").GetString()!,
                [.. root.GetProperty("gates").EnumerateArray().Select(g => g.GetString()!)],
                root.GetProperty("digest").GetString()!,
                root.GetProperty("at").GetDateTimeOffset());
        }
        catch (Exception unreadable) when (unreadable is System.Text.Json.JsonException
                                              or KeyNotFoundException or InvalidOperationException
                                              or FormatException or IOException)
        {
            return null;
        }
    }

    /// <summary>
    /// Records what this draft was proposed as, written whole by rename (slice sixty-eight): no
    /// tool call but a later proposal of the same draft replaces it.
    /// </summary>
    public void KeepProposed(string name, ProposedPlan plan)
    {
        ArgumentNullException.ThrowIfNull(plan);
        if (Refused(name) is not null)
        {
            return;
        }

        Directory.CreateDirectory(_root);
        var staged = ProposedPathOf(name) + ".writing";
        using (var stream = File.Create(staged))
        using (var writer = new System.Text.Json.Utf8JsonWriter(stream, new() { Indented = true }))
        {
            writer.WriteStartObject();
            writer.WriteString("itinerary", plan.Itinerary);
            writer.WriteString("pass", plan.Pass);
            writer.WriteStartArray("gates");
            foreach (var gate in plan.Gates)
            {
                writer.WriteStringValue(gate);
            }

            writer.WriteEndArray();
            writer.WriteString("digest", plan.Digest);
            writer.WriteString("at", plan.At);
            writer.WriteEndObject();
        }

        File.Move(staged, ProposedPathOf(name), overwrite: true);
    }

    /// <summary>
    /// The draft file's digest: what "changed since it was proposed" compares. Of the bytes, so a
    /// hand edit counts as a change as much as a tool's does.
    /// </summary>
    /// <summary>
    /// Every draft that has been proposed, with what it was proposed as: the mux's history screen
    /// lists them (slice sixty-nine). A record that does not read is left out, as
    /// <see cref="Proposed"/> leaves it.
    /// </summary>
    public IReadOnlyList<(string Draft, ProposedPlan Plan)> ProposedDrafts()
    {
        if (!Directory.Exists(_root))
        {
            return [];
        }

        return [.. Directory.EnumerateFiles(_root, "*.proposed")
            .Select(path => Path.GetFileNameWithoutExtension(path))
            .Select(name => (Draft: name, Plan: Proposed(name)))
            .Where(found => found.Plan is not null)
            .Select(found => (found.Draft, found.Plan!))];
    }

    public string DigestOf(string name) =>
        File.Exists(PathOf(name))
            ? Convert.ToHexStringLower(System.Security.Cryptography.SHA256.HashData(File.ReadAllBytes(PathOf(name))))
            : "";

    /// <summary>
    /// The line the bar, the panel and every tool result open with once the draft has been
    /// proposed; null before.
    /// </summary>
    /// <remarks>
    /// <b>Found on ITN-61 and ITN-62</b>: nothing a person or the agent read said a draft had been
    /// proposed, so it was edited and proposed again as a second plan.
    /// </remarks>
    public string? ProposalLine(string name) =>
        Proposed(name) is not { } plan
            ? null
            : string.Equals(plan.Digest, DigestOf(name), StringComparison.Ordinal)
                ? $"proposed as {plan.Itinerary} · waiting on "
                  + (plan.Gates.Count > 0 ? string.Join(", ", plan.Gates) : "its gate")
                : $"changed since {plan.Itinerary} was proposed · proposing again replaces it";

    /// <summary>The draft as the file holds it now.</summary>
    public DraftRead Read(string name)
    {
        if (Refused(name) is { } refused)
        {
            return new DraftRead.Unreadable(refused);
        }

        var path = PathOf(name);
        if (!File.Exists(path))
        {
            return new DraftRead.Held(PlanDraft.Empty);
        }

        var read = EnvelopeYaml.ReadItinerary(File.ReadAllText(path), PlanDraft.DefaultPlanner);

        return read.Diagnosis is { } diagnosis
            ? new DraftRead.Unreadable(
                $"The draft at {path} does not read, and was left as it is: {diagnosis}")
            : new DraftRead.Held(new PlanDraft(read.Planner, read.Intent, read.Legs));
    }

    /// <summary>One edit to the draft, written whole, or nothing written and why.</summary>
    public DraftChange Change(string name, Func<PlanDraft, PlanDraft> edit)
    {
        ArgumentNullException.ThrowIfNull(edit);

        return Change(name, draft => new DraftChange.Written(edit(draft)));
    }

    /// <summary>One edit that may refuse itself, written whole, or nothing written and why.</summary>
    public DraftChange Change(string name, Func<PlanDraft, DraftChange> edit)
    {
        ArgumentNullException.ThrowIfNull(edit);

        // ONE READ AND ONE EDIT. Reading twice could see two files, and an edit may be a closure
        // that counts or logs - it runs once.
        var read = Read(name);
        if (read is DraftRead.Unreadable { Diagnosis: var unreadable })
        {
            return new DraftChange.Refused(unreadable);
        }

        var outcome = edit(((DraftRead.Held)read).Draft);
        if (outcome is not DraftChange.Written { Draft: var next })
        {
            return outcome;
        }

        var path = PathOf(name);
        Directory.CreateDirectory(_root);

        // ATOMIC: a reader - the person's editor, the panel, the next call - sees the old file or
        // the new one, never half of either.
        var staged = path + ".writing";
        File.WriteAllText(staged, EnvelopeText.RenderItinerary(next.Planner, next.Intent, next.Legs));
        File.Move(staged, path, overwrite: true);

        return new DraftChange.Written(next);
    }
}

/// <summary>
/// What a draft was last proposed as, kept beside it so that no later tool call can lose it (slice
/// sixty-eight): the plan, its pass, the gates it waits on, and the draft's digest when it went.
/// </summary>
public sealed record ProposedPlan(
    string Itinerary, string Pass, IReadOnlyList<string> Gates, string Digest, DateTimeOffset At);
