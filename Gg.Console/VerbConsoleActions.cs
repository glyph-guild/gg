using Gg.Client;
using Gg.Contracts;

namespace Gg.Console;

/// <summary>
/// The shell's writes, performed through the same verbs the CLI uses.
/// </summary>
/// <remarks>
/// <para>
/// <b>One writer per transition.</b> This reaches <c>ConsoleData</c>, which reaches
/// <c>FlightCommands</c> - the one place a decision is posted, which
/// <c>GateModalTests</c> holds structurally. A console that posted its own would be
/// a second path to one state transition, and nothing would say which was right
/// when the two disagreed.
/// </para>
/// <para>
/// <b>Sync over async, deliberately and only here.</b> The shell runs between UI
/// lifetimes and is synchronous; the verbs are not. Bridging at this edge is what
/// <c>ConsoleStart.LoadAsync(...).GetAwaiter().GetResult()</c> already does, and
/// keeping <c>IConsoleActions</c> a port is what lets the loop be tested with no
/// HTTP at all.
/// </para>
/// </remarks>
public sealed class VerbConsoleActions(ConsoleData data) : IConsoleActions
{
    private readonly ConsoleData _data = data;

    /// <summary>
    /// Posts the answer and says what was sent.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>What comes back is not interpreted.</b> The sentence returned describes the
    /// POST; what the gate became is the control plane's answer and arrives on the
    /// next load. A console that reported the outcome it hoped for would be deciding.
    /// </para>
    /// <para>
    /// <b>The observations are true here in a way they are not on the command
    /// line.</b> <c>gg decide GG-42 …</c> reports <c>evidenceRendered: false</c>
    /// honestly - nothing was shown, so nothing was read. The gate modal DID render
    /// the evidence before the key was pressed, and this is the one caller entitled
    /// to say so.
    /// </para>
    /// <para>
    /// <b><c>SecondsToDecide</c> stays null, and that is a stated limit rather than a
    /// convenient zero.</b> The number wants the instant the evidence was rendered,
    /// which lives in the reducer - and the reducer touches no clock, on purpose,
    /// because that is what makes every interaction discipline in this console
    /// testable. Putting a clock there to win a field nobody reads yet would be a
    /// bad trade; the honest answer is that this caller cannot measure it.
    /// </para>
    /// </remarks>
    public string Decide(string flight, string obligation, bool approved, string? reason)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(flight);
        ArgumentException.ThrowIfNullOrWhiteSpace(obligation);

        // WHAT THE PERSON ANSWERED, not what the obligation becomes. The first draft
        // of this said ObligationOutcomes.Satisfied and a structural guard refused
        // it - correctly, and for a better reason than the token: satisfied and
        // violated are what the CONTROL PLANE records, and a client that named them
        // would be deciding what "approved" means. `gg decide` passes the word the
        // person typed, and so does this.
        var outcome = approved ? DecisionOutcomes.Approved : DecisionOutcomes.Rejected;

        try
        {
            _ = _data.DecideAsync(
                flight, obligation, outcome,
                new DecisionObservations
                {
                    Interactive = true,
                    EvidenceRendered = true,
                    SecondsToDecide = null,
                },
                reason).GetAwaiter().GetResult();

            return $"{flight}: {obligation} answered {outcome}. What it became is on the flight "
                 + "when this refreshes.";
        }
        catch (Exception refusal) when (refusal is DecisionRefusedException
                                            or NotSignedInException
                                            or FlightNotFoundException
                                            or ProtocolTooOldException
                                            or HttpRequestException)
        {
            // NAMED EXCEPTIONS, and the model stays intact. Swallowing everything
            // here would turn a bug into a console that looks like it answered - the
            // exact shape this whole change exists to remove.
            return $"{flight}: {obligation} was not answered — {refusal.Message}";
        }
    }
}
