using Gg.Contracts;

namespace Gg.Console.Tests;

/// <summary>
/// A session the console starts or resumes is preceded by the credential the person delegated
/// to it, so gg's tools in it act as them; an attach carries none (ADR-0039 Amendment 2,
/// Decision 13).
/// </summary>
/// <remarks>
/// <b>Before the start, on the same channel</b>, because the machine hands it to the next
/// start there and to nothing else. <b>Never before an attach</b>: the agent is already
/// running, and its environment was set when it began.
/// </remarks>
public class AStartedSessionCarriesThePersonsCredentialTests
{
    private static readonly RemoteMachine Vm2 = new("runner-2", "vmlinux002");
    private static readonly DateTimeOffset Later = new(2026, 10, 10, 16, 0, 0, TimeSpan.Zero);

    private static (MuxFixture Fixture, FakeLink Link, List<string> Delegated) Machine(bool delegates = true)
    {
        var fixture = new MuxFixture(columns: 120, rows: 20);
        var link = FakeLink.Machine(
            new AgentSessionStanding { SessionId = "live", StartedAt = Later, Alive = true });
        var delegated = new List<string>();
        fixture.Mux.Reaching(() => [Vm2], _ => new RemoteReach(link, null, delegates
            ? sessionId =>
            {
                delegated.Add(sessionId);
                return new DelegateAgentSession { Token = "t0k3n", ExpiresAt = Later };
            }
            : null));
        return (fixture, link, delegated);
    }

    [Test]
    public async Task A_new_session_is_preceded_by_the_credential_minted_for_its_id()
    {
        var (fixture, link, delegated) = Machine();
        using var _ = fixture;

        var opened = fixture.Mux.OpenRemote(Vm2, sessionId: null, alive: false);

        await Assert.That(opened.Refused).IsNull();
        var sent = link.Sent.ToList();
        var start = sent.OfType<StartAgentSession>().Single();
        await Assert.That(sent.IndexOf(sent.OfType<DelegateAgentSession>().Single()))
            .IsLessThan(sent.IndexOf(start))
            .Because("the machine hands a credential to the next start on the channel.");
        await Assert.That(delegated).IsEquivalentTo([start.SessionId!])
            .Because("the credential is minted for this session and no other.");
    }

    [Test]
    public async Task A_resumed_session_carries_one_too()
    {
        var (fixture, link, _) = Machine();
        using var __ = fixture;

        _ = fixture.Mux.OpenRemote(Vm2, "ended-1", alive: false);

        await Assert.That(link.Sent.OfType<DelegateAgentSession>()).IsNotEmpty();
    }

    [Test]
    public async Task An_attach_carries_none()
    {
        var (fixture, link, delegated) = Machine();
        using var _ = fixture;

        _ = fixture.Mux.OpenRemote(Vm2, "live", alive: true);

        await Assert.That(link.Sent.OfType<DelegateAgentSession>()).IsEmpty();
        await Assert.That(delegated).IsEmpty().Because("nothing is minted that nothing would use.");
    }

    [Test]
    public async Task Without_a_way_to_mint_one_the_session_starts_without_it()
    {
        var (fixture, link, _) = Machine(delegates: false);
        using var _2 = fixture;

        var opened = fixture.Mux.OpenRemote(Vm2, sessionId: null, alive: false);

        await Assert.That(opened.Refused).IsNull();
        await Assert.That(link.Sent.OfType<DelegateAgentSession>()).IsEmpty();
        await Assert.That(link.Sent.OfType<StartAgentSession>()).IsNotEmpty();
    }
}
