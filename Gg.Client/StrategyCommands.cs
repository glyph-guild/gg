namespace Gg.Client;

/// <summary>
/// The strategy verbs: apply a management document to its topology name.
/// </summary>
/// <remarks>
/// The envelope verbs' shape one document over: parsed and refused locally
/// first, using the schema's own rule, so a document the control plane would
/// refuse costs no round trip — and the control plane still checks, because
/// both sides fail closed on their own format.
/// </remarks>
public sealed class StrategyCommands(ControlPlaneClient client, ISessionStore sessions)
{
    private readonly ControlPlaneClient _client = client;
    private readonly ISessionStore _sessions = sessions;

    /// <summary>Reads strategy text and applies it to the named stream.</summary>
    public async Task<VerbResult> ApplyAsync(
        string name, string text, CancellationToken cancellationToken = default)
    {
        var token = Session();
        var parsed = EnvelopeYaml.ParseStrategy(text);

        if (parsed.Strategy is not { } strategy)
        {
            throw new StrategyUnreadableException(parsed.Diagnosis
                ?? "This is not a strategy, and nothing said why.");
        }

        return new VerbResult.EnvelopeApplied(
            await _client.ApplyStrategyAsync(token, name, strategy, cancellationToken),
            parsed.Notes);
    }

    private string Session() =>
        _sessions.Read()?.SessionToken
        ?? throw new NotSignedInException("Not signed in. Run gg login first.");
}

/// <summary>The text was not a strategy; the diagnosis says why.</summary>
public sealed class StrategyUnreadableException(string message) : Exception(message);

/// <summary>The control plane refused the strategy; the diagnosis is its own.</summary>
public sealed class StrategyRefusedException(string message) : Exception(message);
