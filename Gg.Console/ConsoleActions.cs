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
}
