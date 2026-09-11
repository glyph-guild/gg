using Gg.Client;
using Gg.Contracts;

namespace Gg.Client.Tests;

/// <summary>
/// An apply that stops part way reports what it already did, what stopped it,
/// and what it never tried.
/// </summary>
/// <remarks>
/// <para>
/// <b>MEASURED IN THE WORLD, and the report denied an act that had
/// happened.</b> An apply declared a name — which gated, opening a
/// registration flight awaiting an approver — then applied one document, then
/// was refused on the next. The person was shown <i>"Nothing was applied."</i>
/// and the refusal, with no mention of the flight now waiting on somebody.
/// They went looking for a bug in the apply while a governance act sat in a
/// queue.
/// </para>
/// <para>
/// <b>Because the verb THREW.</b> <c>AirspaceApplyAsync</c> collected
/// declarations and applied documents into locals and then let an
/// <c>EnvelopeRefusedException</c> propagate, so everything it had already
/// done went with the stack. The console's catch could only render the
/// refusal, because the refusal was all that survived.
/// </para>
/// <para>
/// <b>STOPPING IS STILL RIGHT; LOSING THE RECORD IS NOT.</b> A changeset is
/// something somebody meant as a whole, so a refusal stops the rest rather
/// than pressing on. What changes is that stopping becomes an ANSWER with the
/// refusal in it, rather than an exception that discards the answer.
/// </para>
/// <para>
/// <b>The refusal before anything is sent still throws</b>, and should: an
/// unreadable file or an undeclared name means nothing happened at all, so
/// there is no partial state to report and a result carrying an empty
/// everything would be a worse way to say it.
/// </para>
/// </remarks>
public class APartialApplyReportsWhatItDidTests
{
    private static StoredSession ASession() => new()
    {
        SessionToken = StubControlPlane.IssuedSessionToken,
        ExpiresAt = DateTimeOffset.UtcNow.AddHours(12),
        TenantId = "019fe062-d000-730c-a37d-7247342cd810",
        PrincipalDisplay = "stub-principal",
    };

    private static FlightCommands Build(StubControlPlane stub) =>
        new(new ControlPlaneClient(new HttpClient { BaseAddress = new Uri(stub.BaseAddress) }),
            new HeldSessionStore(ASession()));

    /// <summary>Two documents, so one can land before the other is refused.</summary>
    private static DirectoryInfo Tree()
    {
        var root = Directory.CreateTempSubdirectory("gg-partial-");
        var airspace = Path.Combine(root.FullName, "airspace");

        Directory.CreateDirectory(Path.Combine(airspace, "work-kinds"));

        File.WriteAllText(
            Path.Combine(airspace, "work-kinds", "score-hal.yaml"),
            """
            context:
              scope: "**"
              constitution: "1.0.0"
            environments: dev
            repositories:
              - "JDX/JDNext"
            accepts:
              - tracker
            produces:
              - loop.outcome
            obligations:
              hal-in-scope:
                check: machine
                rule: no-file-outside-scope
            loops:
              score:
                executor: frontier
                discharges:
                  - hal-in-scope
                moves:
                  - read
                budget:
                  wall-clock: "20m"
                on-exhaustion: handoff-to-human
            """);

        return root;
    }

    [Test]
    public async Task A_gated_declaration_is_reported_even_when_the_apply_is_refused()
    {
        // EXACTLY WHAT HAPPENED. The declaration gated - a registration flight
        // opened and waits for an approver - and then the apply was refused.
        // The flight is the part a person has to act on, and it was the part
        // the report dropped.
        await using var stub = new StubControlPlane();
        var tree = Tree();

        stub.NamePending = new RegistrationPending
        {
            Flight = "GG-88",
            Awaiting = "platform-owner",
            Widens = "the names this tenant can reach",
        };

        stub.ApplyRefusal = "This document names an obligation nothing discharges.";

        try
        {
            var result = await Build(stub).AirspaceApplyAsync(
                tree.FullName, declareNames: true);

            var applied = (VerbResult.AirspaceApplied)result;

            await Assert.That(applied.Value.Declared.Select(d => d.Flight)).Contains("GG-88")
                .Because("a registration flight waiting on an approver is a governance act "
                       + "that HAPPENED. A report saying nothing was applied, with no "
                       + "mention of it, sends somebody looking for a bug while the act "
                       + "sits in a queue.");
        }
        finally
        {
            tree.Delete(recursive: true);
        }
    }

    [Test]
    public async Task The_refusal_is_carried_rather_than_thrown_once_something_has_been_done()
    {
        await using var stub = new StubControlPlane();
        var tree = Tree();

        stub.Topology = new EnvelopeTopology
        {
            Names =
            [
                .. stub.Topology.Names,
                new TopologyName
                {
                    Name = "score-hal",
                    Role = Roles.WorkKind,
                    Parent = "root",
                    DeclaredBy = "somebody, earlier",
                    DeclaredAt = DateTimeOffset.UnixEpoch,
                },
            ],
        };

        stub.ApplyRefusal = "This document names an obligation nothing discharges.";

        try
        {
            var result = await Build(stub).AirspaceApplyAsync(
                tree.FullName, declareNames: false);

            var applied = (VerbResult.AirspaceApplied)result;

            await Assert.That(applied.Value.Refused).IsNotNull()
                .Because("a stop is an answer with the refusal in it, not an exception that "
                       + "discards the answer.");

            await Assert.That(applied.Value.Refused!.Name).IsEqualTo("score-hal")
                .Because("WHICH document stopped it, because the next thing somebody does "
                       + "is open that file.");

            await Assert.That(applied.Value.Refused.Diagnosis)
                .Contains("nothing discharges", StringComparison.Ordinal)
                .Because("in the control plane's own words, carried through unchanged.");
        }
        finally
        {
            tree.Delete(recursive: true);
        }
    }

    [Test]
    public async Task Nothing_sent_at_all_still_throws()
    {
        // THE OTHER HALF, AND IT MUST NOT CHANGE. An unreadable file means
        // nothing happened, so there is no partial state to report - and a
        // result carrying an empty everything would be a worse way to say
        // "this refused before it started".
        await using var stub = new StubControlPlane();
        var root = Directory.CreateTempSubdirectory("gg-unreadable-");

        Directory.CreateDirectory(Path.Combine(root.FullName, "airspace", "narrowings"));
        File.WriteAllText(
            Path.Combine(root.FullName, "airspace", "narrowings", "broken.yaml"),
            "this: is: not: a: narrowing:");

        try
        {
            var refused = await Assert.ThrowsAsync<EnvelopeRefusedException>(
                async () => await Build(stub).AirspaceApplyAsync(
                    root.FullName, declareNames: false));

            await Assert.That(refused!.Message)
                .Contains("broken.yaml", StringComparison.Ordinal);

            await Assert.That(stub.AppliedNames).IsEmpty()
                .Because("and it refused before sending anything, which is what makes "
                       + "throwing the honest shape here.");
        }
        finally
        {
            root.Delete(recursive: true);
        }
    }
}
