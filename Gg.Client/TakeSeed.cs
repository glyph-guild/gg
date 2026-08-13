using System.Text;
using Gg.Contracts;

namespace Gg.Client;

/// <summary>Why the agent's closing account is not in the seed.</summary>
public enum AccountState
{
    /// <summary>It is here, in the agent's own words.</summary>
    Present,

    /// <summary>There is none, and the seed says so out loud.</summary>
    Missing,

    /// <summary>It was too long and the seed says where it stops.</summary>
    Truncated,
}

/// <summary>
/// What we measured about a loop. Computed by us, always present.
/// </summary>
/// <remarks>
/// Every field here is something a machine counted from the event stream. That
/// is what makes them required: they exist for every flight that ran at all, and
/// nothing in the takeover path may depend on anything that does not.
/// </remarks>
public sealed record TakeMeasurements
{
    public required IReadOnlyList<string> FilesEdited { get; init; }

    public required IReadOnlyList<string> FilesReadNotEdited { get; init; }

    public required IReadOnlyList<string> Searches { get; init; }

    public required IReadOnlyList<string> Errors { get; init; }

    /// <summary>
    /// Tools the agent used that the envelope's moves did not declare.
    /// </summary>
    /// <remarks>
    /// <b>Used, not refused, and the difference is not cosmetic.</b> This was called
    /// <c>RefusedMoves</c> and rendered as "moves refused", which says the system
    /// stopped something. It did not: the allow-list passed to the executor does not
    /// bind - measured, both directions, in <c>EnforcesMovesTests</c> - so these are
    /// tools the agent reached for, was not declared for, and used anyway.
    /// </remarks>
    public required IReadOnlyList<string> UndeclaredMovesUsed { get; init; }

    public required int Attempts { get; init; }

    /// <summary>Where the loop stopped, from <see cref="LoopOutcomes"/>.</summary>
    public required string StopReason { get; init; }

    /// <summary>What the obligation decided, when one was evaluated.</summary>
    public string? Verdict { get; init; }
}

/// <summary>
/// Everything a person needs to pick up a flight, and nothing that types itself
/// into anything.
/// </summary>
/// <remarks>
/// <para>
/// <b>Measurements, plus the agent's account, marked as its words.</b> The
/// account is the thing that stops somebody re-deriving a decision the agent
/// already made and recorded nowhere else - and marking it is what stops that
/// decision being read as a measurement.
/// </para>
/// <para>
/// <b>Nothing here may require the account.</b> It is a <c>string?</c> and the
/// state beside it is an enum, so "the agent said nothing" and "its words were
/// dropped" cannot render the same way. That is structural rather than a null
/// check somebody remembers, because this project's most repeated defect is a
/// plausible value where the absence should have been loud.
/// </para>
/// <para>
/// <b>And the account never enters the digest.</b> Article XIII compares
/// accumulated flights, which needs records computed identically every time;
/// agent prose differs every run. The digest is a fact and this is a document -
/// they are different types in different assemblies, and a test says so.
/// </para>
/// </remarks>
public sealed record TakeSeed
{
    /// <summary>What a person types. GG-42.</summary>
    public required string FlightNumber { get; init; }

    /// <summary>What the return file has to name.</summary>
    public required string FlightId { get; init; }

    /// <summary>Where the work is, on this machine.</summary>
    public required string TreePath { get; init; }

    public required TakeMeasurements Measurements { get; init; }

    /// <summary>The agent's closing words, when there are any.</summary>
    public string? Account { get; init; }

    public required AccountState AccountState { get; init; }

    /// <summary>How much of the account was kept, when it was truncated.</summary>
    public int AccountBytes { get; init; }

    /// <summary>
    /// What the last person to work on this said they did, in their own name.
    /// </summary>
    /// <remarks>
    /// <b>A human assertion, and marked as one.</b> Three kinds of claim now
    /// reach a person taking a flight over - what we measured, what the agent
    /// said about itself, and what a previous taker asserted - and a reader who
    /// cannot tell them apart will weigh a guess like a measurement.
    /// </remarks>
    public HumanAccount? PriorHuman { get; init; }

    /// <summary>Why there is no account, when there is none.</summary>
    /// <remarks>
    /// "no account: the runner was killed" and "no account: the agent produced
    /// none" send a person to different places. An absent section sends them
    /// nowhere.
    /// </remarks>
    public string? AccountAbsence { get; init; }
}

/// <summary>
/// Turns a flight's evidence into the thing a person reads before taking over.
/// </summary>
/// <remarks>
/// <para>
/// <b>Composed here, rendered here, and stripped here.</b> Control sequences are
/// stripped at production now, so this inherits the property - and it is
/// re-asserted anyway, because this is the one place in the product where text is
/// deliberately put into a terminal, and inheriting a property is not the same as
/// having it.
/// </para>
/// <para>
/// <b>The account is bounded.</b> A seed nobody can read is a seed nobody reads,
/// and a clipboard is not a document store.
/// </para>
/// </remarks>
public static class TakeSeedComposer
{
    /// <summary>How much of the agent's account is worth carrying.</summary>
    /// <remarks>
    /// Enough for a closing paragraph and not enough for a transcript. When it
    /// bites, the seed says so - a summary that stops mid-sentence with no mark
    /// reads as an agent that stopped mid-sentence.
    /// </remarks>
    public const int MaxAccount = 4000;

    /// <summary>The seed for one flight.</summary>
    /// <param name="flightNumber">What a person types.</param>
    /// <param name="flightId">What the return file must name.</param>
    /// <param name="treePath">Where the work is.</param>
    /// <param name="digest">What we measured, when the flight got far enough to be measured.</param>
    /// <param name="account">The agent's closing words, or null.</param>
    /// <param name="verdict">The obligation's decision, when one was reached.</param>
    public static TakeSeed Compose(
        string flightNumber,
        string flightId,
        string treePath,
        LoopDigest? digest,
        string? account,
        string? verdict = null,
        HumanAccount? priorHuman = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(flightNumber);
        ArgumentException.ThrowIfNullOrWhiteSpace(flightId);

        var measurements = new TakeMeasurements
        {
            // A flight killed before it produced a digest still gets a seed.
            // Empty measurements are measurements; a takeover that refused to
            // start because a list was empty would fail exactly the flight most
            // likely to need taking over.
            FilesEdited = Clean(digest?.FilesEdited),
            FilesReadNotEdited = Clean(digest?.FilesReadNotEdited),
            Searches = Clean(digest?.Searches),
            Errors = Clean(digest?.Errors.Select(e => $"{e.Source}: {e.Detail}").ToList()),
            UndeclaredMovesUsed = Clean(digest?.RefusedMoves),
            Attempts = digest?.Attempts ?? 0,
            StopReason = ControlText.Strip(digest?.StopReason) is { Length: > 0 } stop
                ? stop
                : "unknown",
            Verdict = verdict is null ? null : ControlText.Strip(verdict),
        };

        if (account is not { Length: > 0 })
        {
            return new TakeSeed
            {
                FlightNumber = flightNumber,
                FlightId = flightId,
                TreePath = treePath,
                Measurements = measurements,
                Account = null,
                AccountState = AccountState.Missing,
                AccountAbsence = "the flight produced none",
                PriorHuman = priorHuman,
            };
        }

        // Line breaks survive: this is prose somebody wrote for a reader.
        var cleaned = ControlText.Strip(account, allowLineBreaks: true);
        var truncated = cleaned.Length > MaxAccount;

        return new TakeSeed
        {
            FlightNumber = flightNumber,
            FlightId = flightId,
            TreePath = treePath,
            Measurements = measurements,
            Account = truncated ? cleaned[..MaxAccount] : cleaned,
            AccountState = truncated ? AccountState.Truncated : AccountState.Present,
            AccountBytes = truncated ? MaxAccount : cleaned.Length,
            PriorHuman = priorHuman,
        };
    }

    /// <summary>
    /// The seed as a person reads it.
    /// </summary>
    /// <remarks>
    /// <b>The marking is the point.</b> The measurements are what we counted; the
    /// account is what the agent said. A reader who cannot tell them apart will
    /// treat a claim as a measurement, which is the whole failure this design is
    /// avoiding - so the account gets a header saying whose words these are, and
    /// its absence gets a line rather than a gap.
    /// </remarks>
    public static string Render(TakeSeed seed)
    {
        ArgumentNullException.ThrowIfNull(seed);

        var text = new StringBuilder();

        text.AppendLine($"{seed.FlightNumber} — taking over");
        text.AppendLine();
        text.AppendLine($"Working tree: {seed.TreePath}");
        text.AppendLine($"Flight id:    {seed.FlightId}");
        text.AppendLine();

        text.AppendLine("MEASURED (by gg, from the run's own event stream)");
        text.AppendLine($"  stopped:  {seed.Measurements.StopReason} after "
                      + $"{seed.Measurements.Attempts} turn(s)");

        if (seed.Measurements.Verdict is { Length: > 0 } verdict)
        {
            text.AppendLine($"  verdict:  {verdict}");
        }

        Section(text, "changed", seed.Measurements.FilesEdited);
        Section(text, "read, not changed", seed.Measurements.FilesReadNotEdited);
        Section(text, "searched for", seed.Measurements.Searches);
        Section(text, "errors", seed.Measurements.Errors);
        // NOT "moves refused". Nothing refused them - the allow-list does not bind, so
        // this is what the agent did outside what the envelope declared. A person
        // taking the flight over reads this and acts on it.
        Section(text, "moves used but not declared", seed.Measurements.UndeclaredMovesUsed);

        text.AppendLine();

        switch (seed.AccountState)
        {
            case AccountState.Present:
                text.AppendLine("THE AGENT'S OWN ACCOUNT (its words, not a measurement)");
                text.AppendLine(Indent(seed.Account!));
                break;

            case AccountState.Truncated:
                text.AppendLine("THE AGENT'S OWN ACCOUNT (its words, not a measurement)");
                text.AppendLine(Indent(seed.Account!));
                text.AppendLine($"  [truncated at {seed.AccountBytes} characters]");
                break;

            case AccountState.Missing:
                // LOUD. An absent section and an agent that said nothing must not
                // read the same way, and the difference is a line that says which.
                text.AppendLine($"NO ACCOUNT: {seed.AccountAbsence}");
                text.AppendLine("  The measurements above are everything there is.");
                break;
        }

        // LAST, and marked hardest of the three. The person reading this is
        // picking up work somebody else did, and the one thing they must not do
        // is mistake a colleague's assertion for something a machine measured.
        if (seed.PriorHuman is { } human)
        {
            text.AppendLine();
            text.AppendLine(
                $"A PERSON WORKED ON THIS BEFORE YOU — {human.By} says (their words, a human "
              + "assertion):");
            text.AppendLine(Indent(human.Statement));
            text.AppendLine($"  [{Confirmed(human)}, {human.ConfirmedAt:yyyy-MM-dd HH:mm}]");
        }

        return text.ToString().TrimEnd();
    }

    /// <summary>
    /// How much of that account was theirs.
    /// </summary>
    /// <remarks>
    /// An accepted account is one they read and agreed with; an edited one is a
    /// sentence they changed; a written one is entirely theirs. All three are
    /// their assertion, and the difference is still worth a reader knowing.
    /// </remarks>
    private static string Confirmed(HumanAccount human) => human.Confirmation switch
    {
        AccountConfirmations.Accepted => "accepted an account proposed to them",
        AccountConfirmations.Edited => "edited the account proposed to them",
        AccountConfirmations.Replaced => "wrote this themselves",
        _ => human.Confirmation,
    };

    private static void Section(StringBuilder text, string title, IReadOnlyList<string> items)
    {
        if (items.Count == 0)
        {
            return;
        }

        text.AppendLine($"  {title}:");
        foreach (var item in items)
        {
            text.AppendLine($"    - {item}");
        }
    }

    private static string Indent(string account) =>
        string.Join('\n', account.Split('\n').Select(l => "  " + l));

    private static IReadOnlyList<string> Clean(IReadOnlyList<string>? values) =>
        values is null ? [] : [.. values.Select(v => ControlText.Strip(v))];
}
