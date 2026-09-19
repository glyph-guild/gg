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
/// <b>A write that the console has to look for says WHAT to look for</b>, in a
/// <see cref="Receipt"/>. That is not an outcome either - the door answers 202
/// before a flight exists anywhere a read can see it, and before a gate it
/// answered has closed - but it is the question the console now has to ask: is
/// it there yet. Without it the only way to ask was a reload that ran before
/// the change could be seen and saw nothing.
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

    /// <summary>
    /// Answers a standing nomination, and says what was sent.
    /// </summary>
    /// <remarks>
    /// <b>Beside <see cref="Decide"/> rather than sharing it.</b> They read
    /// alike - a subject, an answer and a reason - and they are two different
    /// transitions through two different doors: a gate says whether work
    /// already flying may continue, and this says whether work nobody asked for
    /// should start at all.
    /// </remarks>
    /// <param name="nomination">
    /// The row's own id, never its subject. Two nominations can name one work
    /// item - a second sweep after the first was declined - and only one of
    /// them is under the cursor.
    /// </param>
    /// <param name="open">
    /// What the PERSON answered. What the row becomes is an admission pass that
    /// may still refuse, and it arrives on the next load.
    /// </param>
    /// <param name="reason">
    /// Required for BOTH answers, which is where this differs from a gate. The
    /// door refuses a decision that says nothing, and the loop refuses one here
    /// too - so somebody who changed their mind by saving an empty buffer has
    /// not opened a flight by accident.
    /// </param>
    Receipt AnswerNomination(string nomination, bool open, string reason);

    /// <summary>
    /// Keeps a share of an allowance back, or clears the floor, and says what
    /// happened.
    /// </summary>
    /// <remarks>
    /// <b>A fraction of BOTH windows, because the console offers a decision
    /// rather than a form.</b> Keeping a third of the week and nothing of the
    /// session is coherent and is what <c>gg allowances floor</c> is for; what
    /// somebody wants while looking at a fleet is <i>hold some of this back</i>.
    /// Null clears it.
    /// </remarks>
    string KeepBack(string allowance, double? share);

    /// <summary>Opens a flight from intent text, and says what happened.</summary>
    /// <param name="repository">
    /// The registered repository this flight is about, or null to let the
    /// envelope resolve it — which is what every flight does by default.
    /// </param>
    /// <param name="repositories">
    /// Which repositories the flight names, or empty to let the envelope
    /// resolve it. A LIST since contract 0.161.0 — the client fills the
    /// singular when there is exactly one, so a flight naming one travels
    /// exactly as it always did.
    /// </param>
    Receipt Fly(string intent, IReadOnlyList<string> repositories, string? workKind);

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
    /// <param name="workKind">
    /// What this flight is FOR, or null to inherit the floor.
    /// </param>
    /// <remarks>
    /// <b>Null is what every flight before kinds existed was</b>, and it stays
    /// null rather than becoming a local default: the control plane reads a
    /// missing kind as <c>implement</c>, and a console that supplied that name
    /// would be declaring something nobody chose.
    /// </remarks>
    Receipt FlyTicket(
        string provider, string id, IReadOnlyList<string> repositories, string? workKind);

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
    /// Forgets a credential, and says what was forgotten.
    /// </summary>
    /// <remarks>
    /// <b>By REPOSITORY, which is what a person knows.</b> The control plane
    /// removes by credential id and nobody has one of those in their head; the
    /// verb resolves the name to the id by re-reading the list, rather than
    /// trusting a model that may be a minute old. Removing the wrong credential
    /// because the list moved is not a mistake this can be allowed to make.
    /// </remarks>
    string ForgetCredential();

    /// <summary>
    /// Issues an invitation and places the link, returning WHERE it went.
    /// </summary>
    /// <remarks>
    /// Never the link. Whoever holds it becomes a principal in this tenant.
    /// </remarks>
    string Invite();
}

/// <summary>
/// What a write said, and what the console now looks for because of it.
/// </summary>
/// <remarks>
/// <b>Nothing to look for whenever nothing can be named</b>: a refusal, a
/// control plane that answered without an id, a nomination declined rather than
/// opened. Null is not "nothing happened" - a POST that reached the control
/// plane and failed on the way back opens a flight and reports a refusal - so
/// the loop re-reads when there is nothing named, exactly as it did before
/// anything could be.
/// </remarks>
/// <param name="Said">The sentence a person reads.</param>
/// <param name="Expected">What the console now looks for, when the write named it.</param>
public sealed record Receipt(string Said, Expectation? Expected = null)
{
    /// <summary>A flight opened, looked for by the id the door named - or not, when it named none.</summary>
    public static Receipt Opened(string said, string? flightId) => new(
        said,
        flightId is { Length: > 0 } id
            ? new Expectation { Kind = ExpectationKind.FlightAppears, Id = id }
            : null);
}
