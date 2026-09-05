namespace Gg.Console;

/// <summary>
/// The writes the shell performs between UI sessions.
/// </summary>
/// <remarks>
/// <para>
/// <b>A port, and sync, because the shell is.</b> <c>ConsoleLoop</c> runs between
/// UI lifetimes with the terminal free; the verbs underneath are async, and the
/// edge that bridges them is the same one <c>ConsoleStart.LoadAsync(...)
/// .GetAwaiter().GetResult()</c> already is. Keeping it a port is what lets the
/// loop be tested without HTTP - which is how the dead keys were found in the
/// first place, by reading rather than by running.
/// </para>
/// <para>
/// <b>Every method returns the sentence a person reads.</b> Not a result the
/// console interprets: what a write BECAME is the control plane's answer and
/// arrives on the next load. This returns what happened when the key was pressed,
/// which is a different fact and the only one the console is entitled to.
/// </para>
/// <para>
/// <b>Nothing that must not be stored crosses this boundary.</b> A secret and an
/// invitation link are both capabilities, and <c>AppState</c> is source-generated
/// JSON that is written to disk under <c>GG_STATE_DUMP</c> and fed to the
/// diagnostics bundle. So the implementation prompts for a secret itself and
/// places a link itself; neither value is a parameter here and neither is a return
/// value.
/// </para>
/// </remarks>
public interface IConsoleActions
{
    /// <summary>
    /// Answers a gate, and says what was sent.
    /// </summary>
    /// <param name="approved">What the PERSON answered, not what the obligation becomes.</param>
    /// <param name="reason">
    /// Required when rejecting. The verb refuses a rejection without one: the loop
    /// runs again with it, and a rejection that says nothing sends the work back to
    /// be done the same way.
    /// </param>
    string Decide(string flight, string obligation, bool approved, string? reason);

    /// <summary>Opens a flight from intent text, and says what happened.</summary>
    string Fly(string intent);

    /// <summary>
    /// Open a flight for a work item somebody picked, by provider and id.
    /// </summary>
    /// <remarks>
    /// <b>Two values, not a formatted string.</b> <see cref="Fly"/> takes what a
    /// person typed and parses it, which is right for a paste. This takes what
    /// a reader already told us, and formatting it into <c>provider#id</c> only
    /// to parse it again would lose the first id that contained the separator -
    /// the rule <c>FlightIntent.Id</c> already states.
    /// </remarks>
    string FlyTicket(string provider, string id);

    /// <summary>
    /// Why opening a flight for this work item deserves a second thought, or
    /// null when it does not.
    /// </summary>
    /// <remarks>
    /// <b>Non-null is "ask the person", not "refuse".</b> Two flights on one
    /// work item is legal and occasionally wanted. It is also what pressing a
    /// key twice produces, which is why it is worth a question.
    /// </remarks>
    string? AlreadyFlown(string provider, string id);

    /// <summary>
    /// Registers a credential, prompting for the repository and the value.
    /// </summary>
    /// <remarks>
    /// <b>No parameters, deliberately.</b> A secret crossing this boundary would be a
    /// secret in a frame the console owns, and the console is the thing that
    /// serializes itself to disk. What comes back names the reference and never its
    /// value.
    /// </remarks>
    string AddCredential();

    /// <summary>
    /// Issues an invitation and places the link, returning WHERE it went.
    /// </summary>
    /// <remarks>
    /// Never the link. Whoever holds it becomes a principal in this tenant.
    /// </remarks>
    string Invite();
}
