using Gg.Contracts.Authoring;
using Gg.Contracts;

namespace Gg.Client;

/// <summary>An envelope the control plane refused, with its diagnosis.</summary>
public sealed class EnvelopeRefusedException(string message) : Exception(message);

/// <summary>An envelope this machine could not read.</summary>
public sealed class EnvelopeUnreadableException(string message) : Exception(message);

/// <summary>The tenant has never applied one.</summary>
public sealed class NoEnvelopeException(string message) : Exception(message);

/// <summary>
/// The envelope verbs: show, apply, validate.
/// </summary>
/// <remarks>
/// <para>
/// Each returns a <see cref="VerbResult"/> and none of them writes anything,
/// so <c>--json</c> and the rendered form are two views of one result rather
/// than two implementations that agree today.
/// </para>
/// <para>
/// <b><c>validate</c> contacts nothing.</b> A syntax error should not need a
/// round trip, and somebody working on an envelope offline should still be
/// able to check it. It is the only verb here that works with no session, and
/// that is a property rather than an accident.
/// </para>
/// <para>
/// <b>The text form is a client concern; the wire is JSON.</b> gg holds the
/// only YAML parser in the product and translates at this boundary, so the
/// control plane never has to.
/// </para>
/// </remarks>
public sealed class EnvelopeCommands(ControlPlaneClient client, ISessionStore sessions)
{
    private readonly ControlPlaneClient _client = client;
    private readonly ISessionStore _sessions = sessions;

    /// <summary>The tenant's envelope, as canonical text.</summary>
    public async Task<VerbResult> ShowAsync(CancellationToken cancellationToken = default)
    {
        var state = await _client.GetEnvelopeAsync(Session(), cancellationToken)
            ?? throw new NoEnvelopeException(
                "This tenant has no envelope. Nothing governs its flights yet - write one and run "
              + "gg envelope apply <file>.");

        return new VerbResult.EnvelopeShown(state);
    }

    /// <summary>
    /// Reads envelope text and writes it back.
    /// </summary>
    /// <remarks>
    /// Refused locally before anything is sent, using the schema's own rule -
    /// so a document the control plane would refuse costs no round trip, and
    /// the two cannot disagree about what a valid envelope is. The control
    /// plane still checks: both sides fail closed on their own format.
    /// </remarks>
    public async Task<VerbResult> ApplyAsync(string text, CancellationToken cancellationToken = default)
    {
        var token = Session();
        var parsed = EnvelopeYaml.Parse(text);

        if (parsed.Envelope is not { } envelope)
        {
            throw new EnvelopeUnreadableException(parsed.Diagnosis
                ?? "This is not an envelope, and nothing said why.");
        }

        return new VerbResult.EnvelopeApplied(
            await _client.ApplyEnvelopeAsync(token, envelope, cancellationToken),
            parsed.Notes);
    }

    /// <summary>
    /// Reads envelope text and says whether it is one. Contacts nothing.
    /// </summary>
    /// <remarks>
    /// Returns a result either way rather than throwing on a bad document: an
    /// invalid envelope is the ANSWER to this question, not a failure of it.
    /// <c>apply</c> throws because there the document was in the way of doing
    /// something.
    /// </remarks>
    /// <param name="path">
    /// Where the document lives, when it lives anywhere. The role is inferred
    /// from the containing directory - <c>narrowings/</c> holds narrowings,
    /// <c>work-kinds/</c> holds work kinds - which is what catches the file
    /// somebody gets by copying <c>root.yaml</c> into a narrowings directory:
    /// a legal document of the WRONG TYPE, which parses and validates and would
    /// hand a team `scope:` and `constitution:` through a merge nobody gated.
    /// Null for stdin, where the shape decides instead.
    /// </param>
    public static VerbResult Validate(string text, string? path = null)
    {
        // THE LOCATION FIRST, THE SHAPE SECOND. Reading what a document claims
        // to be would accept a complete envelope anywhere, which is precisely
        // ADR-0018 § 7's fourth refusal - the one that is easy to miss because
        // the document is not malformed, only misplaced.
        //
        // A location we do not recognise says nothing rather than refusing:
        // `gg envelope validate -` is what CI pipes into, and a team keeping
        // policy somewhere else entirely is not doing anything wrong.
        var role = AirspaceNames.RoleOfDirectory(path) ?? ShapeOf(text);

        return new VerbResult.EnvelopeValidated(role switch
        {
            Roles.Narrowing => Answered(role, EnvelopeYaml.ParseNarrowing(text)),
            Roles.Strategy => Answered(role, EnvelopeYaml.ParseStrategy(text)),
            _ => Answered(role, EnvelopeYaml.Parse(text)),
        });
    }

    /// <summary>What a document looks like, when its location says nothing.</summary>
    /// <remarks>
    /// Deliberately crude and deliberately last. A narrowing is the ONLY role
    /// whose document cannot carry a complete envelope's keys, so the presence
    /// of any of them settles it; anything else reads as root, which is what
    /// every caller meant before this parameter existed.
    /// </remarks>
    private static string ShapeOf(string text) =>
        EnvelopeYaml.ParseNarrowing(text).Narrowing is not null
            ? Roles.Narrowing
            : EnvelopeYaml.ParseStrategy(text).Strategy is not null
                ? Roles.Strategy
                : Roles.Root;

    private static EnvelopeValidation Answered(string role, EnvelopeParse parsed) => new()
    {
        Role = role,
        Valid = parsed.Envelope is not null,
        Diagnosis = parsed.Diagnosis,
        Notes = parsed.Notes,
        // The canonical form, so `validate` also answers "what will this
        // look like once it has been through us" - which is the question
        // somebody actually has when comments are about to disappear.
        Canonical = parsed.Envelope is { } envelope ? EnvelopeText.Render(envelope) : null,
    };

    private static EnvelopeValidation Answered(string role, EnvelopeNarrowingParse parsed) => new()
    {
        Role = role,
        Valid = parsed.Narrowing is not null,
        Diagnosis = parsed.Diagnosis,
        Notes = parsed.Notes,
        Canonical = parsed.Narrowing is { } narrowing ? EnvelopeText.Render(narrowing) : null,
    };

    private static EnvelopeValidation Answered(string role, StrategyParse parsed) => new()
    {
        Role = role,
        Valid = parsed.Strategy is not null,
        Diagnosis = parsed.Diagnosis,
        Notes = parsed.Notes,
        Canonical = parsed.Strategy is { } strategy ? EnvelopeText.Render(strategy) : null,
    };

    private string Session() =>
        _sessions.Read()?.SessionToken
        ?? throw new NotSignedInException("Not signed in. Run gg login first.");
}

/// <summary>What <c>gg envelope validate</c> concluded.</summary>
/// <remarks>
/// A result type rather than an exception, and a local one rather than a wire
/// one: nothing sends this anywhere, so it does not belong in the contract a
/// customer audits.
/// </remarks>
public sealed record EnvelopeValidation
{
    /// <summary>
    /// Which role this was read as, one of <see cref="Roles"/>.
    /// </summary>
    /// <remarks>
    /// <b>Said out loud, because "valid" against the wrong rules is the failure
    /// this exists to prevent.</b> A person who meant to write a narrowing and
    /// gets `valid: true` against the complete-envelope rules has been told the
    /// opposite of what they need to know.
    /// </remarks>
    public required string Role { get; init; }

    public required bool Valid { get; init; }

    /// <summary>What is wrong, when something is.</summary>
    public string? Diagnosis { get; init; }

    /// <summary>What the round trip will not keep. Comments, today.</summary>
    public IReadOnlyList<string> Notes { get; init; } = [];

    /// <summary>What this will look like after gg has rendered it back.</summary>
    public string? Canonical { get; init; }
}
