using Gg.Contracts;

namespace Gg.Client;

/// <summary>
/// Somebody else is holding this flight, and here is who.
/// </summary>
/// <remarks>
/// <b>Thrown rather than returned as a result, and the distinction is what the
/// caller does.</b> Every <c>VerbResult</c> is something to print; this is a reason
/// the verb did not happen, and it exits non-zero. Two people looking at the same
/// stopped flight is the ordinary case this exists for rather than a fault, so the
/// message is a sentence a person acts on and not a stack trace.
/// </remarks>
public sealed class TakeoverRefusedException(string message) : Exception(message);

/// <summary>
/// Taking a flight over, and handing it back, from anywhere.
/// </summary>
/// <remarks>
/// <para>
/// <b>A verb, so a headless machine and a second person's terminal are the same
/// path.</b> Until this existed the only thing that could compose a seed was a
/// console, and a console needs a terminal - so a stopped flight was resumable by
/// whoever was sitting at that keyboard and by nobody else, which is the sentence
/// slice seven exists to make untrue.
/// </para>
/// <para>
/// <b>Every method returns a <see cref="VerbResult"/> and none of them writes.</b>
/// The result is the whole output, so <c>--json</c> can reproduce anything a person
/// sees - and the console renders through these same commands, where a stray write
/// would land in the middle of a pane.
/// </para>
/// </remarks>
public sealed class TakeCommands(ControlPlaneClient client, ISessionStore sessions)
{
    private readonly ControlPlaneClient _client = client;
    private readonly ISessionStore _sessions = sessions;

    /// <summary>
    /// Claims the flight and reads what it tried and ruled out.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Claim first, then read, and the order is the property.</b> The claim
    /// decides whether this person may have the flight at all. Reading the seed
    /// first would hand somebody the work of a flight they are then told they
    /// cannot have - and would do it while a colleague was already working it.
    /// </para>
    /// <para>
    /// <b>The hold's expiry is a NOTE.</b> The document is the seed, which is a
    /// fact about the flight; when this invocation's hold lapses is a fact about
    /// this invocation, and <c>envelope apply</c> already carries that kind of thing
    /// beside the document rather than inside it.
    /// </para>
    /// </remarks>
    public async Task<VerbResult> TakeAsync(
        string reference, CancellationToken cancellationToken = default)
    {
        var session = Session();

        var claim = await _client.ClaimTakeoverAsync(session, reference, cancellationToken);

        var hold = Claimed(claim, reference);

        var seed = await _client.GetSeedAsync(session, reference, cancellationToken)
            ?? throw new TakeoverRefusedException(
                $"'{reference}' does not name a flight in this organisation, so nothing was taken "
              + "over. Run gg flights to list flights.");

        return new VerbResult.Taken(seed,
        [
            $"{seed.FlightNumber} is taken over until {hold.HeldUntil:yyyy-MM-dd HH:mm} UTC.",
            $"Renew within {hold.RenewWithinSeconds}s of that time, or the takeover ends and "
          + "another person may take the flight over.",
            $"When finished, run gg take {reference} --return <outcome>, where <outcome> is one "
          + "of " + string.Join(", ", TakeoverOutcomes.All) + ".",
        ]);
    }

    /// <summary>
    /// Hands the flight back with a decision, and shows the record it wrote.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>It re-claims rather than remembering a generation.</b> A second
    /// invocation has none, and there is nowhere honest to keep one: a file beside
    /// the tree would be exactly the machine-local state this slice removes. The
    /// re-claim is granted to the same holder and refused to anybody else - and
    /// being refused is the right answer, because a decision recorded against
    /// somebody else's hold would attribute their work to this person.
    /// </para>
    /// <para>
    /// <b>It answers with the flight's LOG</b>, which is where the record lands.
    /// The write is a command, so there is nothing to answer with inline; `gg
    /// decide` submits and observes a read surface for the same reason. The entry
    /// may not be there yet, and the log is still the surface to look at.
    /// </para>
    /// </remarks>
    public async Task<VerbResult> ReturnAsync(
        string reference,
        string outcome,
        string? note = null,
        CancellationToken cancellationToken = default)
    {
        // REFUSED HERE TOO. Both sides fail closed on their own format, the way
        // the envelope does, so a typo costs a diagnosis rather than a round trip -
        // and the control plane would refuse it anyway.
        if (!TakeoverOutcomes.All.Contains(outcome, StringComparer.Ordinal))
        {
            throw new ArgumentException(
                $"'{outcome}' is not an outcome gg recognises. Nothing was recorded. Use one of: "
              + string.Join(", ", TakeoverOutcomes.All) + ".", nameof(outcome));
        }

        var session = Session();

        var claim = await _client.ClaimTakeoverAsync(session, reference, cancellationToken);
        var hold = Claimed(claim, reference);

        var accepted = await _client.ReturnTakeoverAsync(
            session, reference,
            new TakeoverReturnRequest
            {
                Generation = hold.Generation,
                Outcome = outcome,
                Note = note,
            },
            cancellationToken);

        if (!accepted)
        {
            throw new TakeoverRefusedException(
                $"The decision about {reference} was not recorded, because the takeover has "
              + "ended or passed to another person. Nothing was changed. Run "
              + $"gg show {reference} to see its current state.");
        }

        var log = await _client.GetFlightLogAsync(session, reference, cancellationToken)
            ?? throw new TakeoverRefusedException(
                $"The decision about {reference} was recorded, but its log could not be read back. "
              + $"Run gg log {reference} to read it.");

        return new VerbResult.Log(log);
    }

    /// <summary>
    /// What a flight tried and ruled out, WITHOUT claiming it.
    /// </summary>
    /// <remarks>
    /// <b>For a pane, and separate from <see cref="TakeAsync"/> on purpose.</b> A
    /// console rendering a queue would otherwise claim every flight a person
    /// scrolled past, holding work nobody is doing and refusing colleagues who
    /// actually want it. Reading is not taking.
    /// </remarks>
    public Task<TakeSeed?> SeedAsync(
        string reference, CancellationToken cancellationToken = default) =>
        _client.GetSeedAsync(Session(), reference, cancellationToken);

    /// <summary>Whose session this is.</summary>
    /// <remarks>
    /// From the stored session, never from anything typed in. A takeover is the one
    /// record that exists to say a person did this and a machine did not.
    /// </remarks>
    public string Principal() =>
        _sessions.Read()?.PrincipalDisplay
        ?? throw new NotSignedInException("This machine is not signed in. Run gg login, then try again.");

    /// <summary>
    /// Claims the flight, for a caller that is about to hand over a terminal.
    /// </summary>
    /// <remarks>
    /// <b>The console needs the claim without the seed fetch.</b> It already has a
    /// seed in its model - the pane rendered one - and what it needs at the moment
    /// somebody presses the key is exclusivity. Returning the outcome rather than
    /// throwing, because the console folds a refusal into its state and renders it;
    /// it has no terminal to print to at that instant.
    /// </remarks>
    public Task<TakeoverClaim> ClaimAsync(
        string reference, CancellationToken cancellationToken = default) =>
        _client.ClaimTakeoverAsync(Session(), reference, cancellationToken);

    /// <summary>The hold, or the reason there is not one.</summary>
    /// <remarks>
    /// Three answers collapse to two here, and the sentences differ: no such
    /// flight, and somebody else has it. Telling a person the flight is taken when
    /// it does not exist would send them to ask a colleague about nothing.
    /// </remarks>
    private static TakeoverClaimed Claimed(TakeoverClaim claim, string reference) => claim switch
    {
        TakeoverClaim.Granted granted => granted.Hold,

        TakeoverClaim.Refused { Holder: { } holder } => throw new TakeoverRefusedException(
            $"{holder.By} took over {reference} at {holder.Since:yyyy-MM-dd HH:mm} UTC, until "
          + $"{holder.HeldUntil:HH:mm} UTC. Nothing was changed: only one person can take over a "
          + $"flight at a time. Ask {holder.By}, or run gg take {reference} again after that."),

        TakeoverClaim.Refused => throw new TakeoverRefusedException(
            $"{reference} has already been taken over, and the control plane cannot say by "
          + $"whom. Nothing was changed. Run gg show {reference} to see its current state."),

        _ => throw new TakeoverRefusedException(
            $"'{reference}' does not name a flight in this organisation. Nothing was changed. "
          + "Run gg flights to list flights."),
    };

    private string Session() =>
        _sessions.Read()?.SessionToken
        ?? throw new NotSignedInException("This machine is not signed in. Run gg login, then try again.");
}
