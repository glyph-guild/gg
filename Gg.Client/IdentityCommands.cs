namespace Gg.Client;

/// <summary>
/// Who this session is, as a value.
/// </summary>
/// <remarks>
/// <para>
/// <b>Separated from <see cref="AuthCommands"/> because printing is not
/// reading.</b> <c>AuthCommands</c> takes an <see cref="IConsoleWriter"/>, an
/// <see cref="IClock"/> and a delay - everything <c>login</c> needs to poll a
/// device authorization at the cadence the server asked for. A console that
/// wanted the tenant's notices had to construct all of that, and would then be
/// holding a writer that prints over the terminal it is drawing.
/// </para>
/// <para>
/// Two arguments, the same pair <see cref="EnvelopeCommands"/> takes, and the
/// command line's <c>whoami</c> renders what this returns - so there is one
/// answer with two surfaces rather than two reads.
/// </para>
/// </remarks>
public sealed class IdentityCommands(ControlPlaneClient client, ISessionStore sessions)
{
    private readonly ControlPlaneClient _client = client;
    private readonly ISessionStore _sessions = sessions;

    /// <summary>
    /// The principal, its tenant, when the session expires, and any notices.
    /// </summary>
    /// <remarks>
    /// <b>Refuses rather than answering an empty identity.</b> A caller handed
    /// a <c>WhoAmI</c> with blank fields cannot tell "nobody is signed in" from
    /// "this tenant has nothing to report", and one of those is a console that
    /// should say so and stop.
    /// </remarks>
    public async Task<VerbResult> ShowAsync(CancellationToken cancellationToken = default)
    {
        var stored = _sessions.Read()
            ?? throw new NotSignedInException("Not signed in. Run gg login.");

        var who = await _client.WhoAmIAsync(stored.SessionToken, cancellationToken)
            ?? throw new NotSignedInException("This session is no longer valid. Run gg login.");

        return new VerbResult.Identity(who);
    }
}
