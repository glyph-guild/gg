using Gg.Contracts;

namespace Gg.Console;

/// <summary>
/// What the console saw while somebody answered a gate.
/// </summary>
/// <remarks>
/// <para>
/// <b>Observations, never a conclusion.</b> gg reports what it saw; what that means is the
/// control plane's to decide, somewhere policy is visible. There is no <c>attended</c>
/// field here and there must not be one - a client asserting attendance would be deciding
/// the thing these observations exist to let somebody else decide.
/// </para>
/// <para>
/// <b>Only rendering is observable.</b> This process watched the pane display the case, so
/// <c>EvidenceRendered</c> is a measurement. Whether anybody read it is not, and a field
/// called <c>EvidenceReviewed</c> would be a claim about a person's attention dressed as
/// one - the payload's own measured-versus-stated distinction, applied to a field name.
/// </para>
/// <para>
/// <b>Pure, so it can be asserted.</b> It takes the state and the elapsed time rather than
/// reading a clock or a console handle, which is what lets a test say what each of the
/// three surfaces produces without running any of them.
/// </para>
/// </remarks>
public static class ConsoleObservation
{
    /// <summary>What was true of this decision, as this process saw it.</summary>
    public static DecisionObservations Of(AppState state, TimeSpan open)
    {
        ArgumentNullException.ThrowIfNull(state);

        return new DecisionObservations
        {
            // A modal is only reachable by somebody pressing a key at a terminal. The verb
            // has to ask the operating system whether it is talking to one; the console
            // knows because it could not have been opened otherwise.
            Interactive = true,

            // WHAT WAS ON THE SCREEN, not what was fetched. A payload the pane never held
            // is a case nobody was shown, and saying otherwise would be the one field here
            // capable of flattering somebody.
            EvidenceRendered = state.Payload is { Items.Count: > 0 },

            // How long the modal was open. Not how long they thought - a person can open a
            // gate and go to lunch - which is exactly why this is reported and not
            // interpreted here.
            SecondsToDecide = (int)open.TotalSeconds,
        };
    }
}
