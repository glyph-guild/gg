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
    public static VerbResult Validate(string text)
    {
        var parsed = EnvelopeYaml.Parse(text);

        return new VerbResult.EnvelopeValidated(new EnvelopeValidation
        {
            Valid = parsed.Envelope is not null,
            Diagnosis = parsed.Diagnosis,
            Notes = parsed.Notes,
            // The canonical form, so `validate` also answers "what will this
            // look like once it has been through us" - which is the question
            // somebody actually has when comments are about to disappear.
            Canonical = parsed.Envelope is { } envelope ? EnvelopeText.Render(envelope) : null,
        });
    }

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
    public required bool Valid { get; init; }

    /// <summary>What is wrong, when something is.</summary>
    public string? Diagnosis { get; init; }

    /// <summary>What the round trip will not keep. Comments, today.</summary>
    public IReadOnlyList<string> Notes { get; init; } = [];

    /// <summary>What this will look like after gg has rendered it back.</summary>
    public string? Canonical { get; init; }
}
