using Gg.Contracts;

namespace Gg.Client;

/// <summary>
/// The two control-plane reads a plan is drafted against: what a leg may name, and what admission
/// would do with each leg. Slice sixty-three.
/// </summary>
/// <remarks>
/// <b>Reads, and nothing else</b> (rule 10). A drafting tool server can neither propose, open
/// nor nominate, because nothing it is handed can.
/// </remarks>
public interface IPlanningReads
{
    /// <summary>What a leg of a plan under <paramref name="planner"/> may name.</summary>
    Task<ItineraryMenu> MenuAsync(string planner, CancellationToken cancellationToken = default);

    /// <summary>What admission would do with each leg of <paramref name="draft"/>, writing nothing.</summary>
    Task<ItineraryCheck> CheckAsync(ItineraryDraft draft, CancellationToken cancellationToken = default);
}

/// <summary>
/// The one write a planning tool server may make: proposing the draft. Slice sixty-five.
/// </summary>
/// <remarks>
/// <b>Apart from <see cref="IPlanningReads"/>, deliberately</b>: the reads stay two and the write
/// is one, so a second write would have to be handed to the server where a test sees it.
/// </remarks>
public interface IPlanningProposals
{
    /// <summary>Proposes the plan as the signed-in person; the control plane holds its gate.</summary>
    Task<ItineraryProposed> ProposeAsync(ItineraryProposal proposal, CancellationToken cancellationToken = default);
}

/// <summary>The planning reads made as the person signed in on this machine.</summary>
public sealed class SessionPlanningReads(ControlPlaneClient client, ISessionStore sessions)
    : IPlanningReads, IPlanningProposals
{
    private readonly ControlPlaneClient _client = client;
    private readonly ISessionStore _sessions = sessions;

    public Task<ItineraryMenu> MenuAsync(string planner, CancellationToken cancellationToken = default) =>
        _client.ItineraryMenuAsync(Session(), planner, cancellationToken);

    public Task<ItineraryCheck> CheckAsync(ItineraryDraft draft, CancellationToken cancellationToken = default) =>
        _client.CheckItineraryAsync(Session(), draft, cancellationToken);

    public Task<ItineraryProposed> ProposeAsync(
        ItineraryProposal proposal, CancellationToken cancellationToken = default) =>
        _client.ProposeItineraryAsync(Session(), proposal, cancellationToken);

    // READ ON EVERY CALL, never kept: a person who signs in again while the server runs is the
    // person the next read is made as.
    private string Session() =>
        _sessions.Read()?.SessionToken
        ?? throw new NotSignedInException("Not signed in. Run gg login, then call the tool again.");
}
