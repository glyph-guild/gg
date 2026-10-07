using Gg.Client;
using Gg.Contracts;

namespace Gg.Client.Tests;

/// <summary>
/// A push can name a credential rather than a machine, and the list of machines comes
/// from what the tenant has already declared.
/// </summary>
/// <remarks>
/// <para>
/// <b>S64.5-01, and ADR-0037 Decisions 9 to 11.</b> A push names a credential; the
/// fleet says who needs it. The owner's framing is that <i>"the fleet says who is
/// missing what, and that report IS the feature"</i>.
/// </para>
/// <para>
/// <b>Both halves already exist in this contract, which is why this step needs no
/// endpoint.</b> <c>FleetProfile.Credentials</c> is the list a profile declares —
/// <i>"the credentials it needs, as references its own sources resolve - never a
/// value"</i> — and <c>RunnerSummary.Profile</c> says which profile a machine is
/// under. Two reads a person can already make, joined here.
/// </para>
/// <para>
/// <b>It is a PURE derivation over data, taking no store and no delegate</b>, which is
/// <c>CredentialRows.For</c>'s rule and for its reason: a builder that could be handed
/// a store is one a later change can make resolve a credential on a render path. The
/// audience is printed; printing must not open anything.
/// </para>
/// <para>
/// <b>Declared need is not the same as missing.</b> A machine whose profile names a
/// locator is in the audience whether or not it already holds a copy — re-pushing is
/// harmless, because <c>Rewrap</c> refuses a holder it already has and the sender
/// treats that as "the same credential arriving at a machine that has it". What a
/// machine REPORTS it cannot resolve is a different column, and
/// <c>AnAudienceNamesWhoCannotResolveItTests</c> is that one.
/// </para>
/// </remarks>
public class ACredentialsAudienceIsDeclaredTests
{
    private const string Locator = "local:acme/widgets";
    private const string Other = "local:acme/other";

    private static RunnerSummary ARunner(
        string id,
        string label,
        string? profile = null,
        string state = RunnerStates.Idle,
        string? host = null,
        IReadOnlyList<ReadinessItem>? lacks = null) =>
        new()
        {
            RunnerId = id,
            Label = label,
            State = state,
            Profile = profile,
            HostRunnerId = host,
            Lacks = lacks ?? [],
        };

    private static FleetProfileState AProfile(string name, params string[] credentials) =>
        new()
        {
            Name = name,
            Version = "v1",
            AppliedAt = DateTimeOffset.UnixEpoch,
            Profile = new FleetProfile
            {
                Environment = name,
                Roles = [],
                Credentials = credentials,
            },
        };

    [Test]
    public async Task A_machine_whose_profile_declares_the_credential_is_in_the_audience()
    {
        var audience = CredentialAudience.For(
            Locator,
            [ARunner("r1", "vmlinux001", profile: "dev")],
            [AProfile("dev", Locator)]);

        await Assert.That(audience.Count).IsEqualTo(1);

        await Assert.That(audience[0].Label).IsEqualTo("vmlinux001")
            .Because("a person reads a label, not a runner id - the id is for the push and the "
                   + "label is for the decision.");

        await Assert.That(audience[0].Declared).IsTrue()
            .Because("its profile names the locator, which is the whole of Decision 9: the list is "
                   + "the rendering of something somebody wrote.");
    }

    [Test]
    public async Task A_machine_under_a_profile_that_does_not_name_it_is_not()
    {
        var audience = CredentialAudience.For(
            Locator,
            [ARunner("r1", "vmlinux001", profile: "dev")],
            [AProfile("dev", Other)]);

        await Assert.That(audience).IsEmpty()
            .Because("pushing a credential to a machine nothing says needs it is a copy of a "
                   + "secret somebody has to be told about later.");
    }

    [Test]
    public async Task A_machine_under_no_profile_is_not_in_a_declared_audience()
    {
        // A LAPTOP, A RESIDENT, A HAND-BROUGHT-UP MACHINE. Nothing declares what they
        // need, so nothing can derive that they need this - and `--to` stays the way to
        // reach one, which is what the slice says.
        var audience = CredentialAudience.For(
            Locator,
            [ARunner("r1", "a-laptop")],
            [AProfile("dev", Locator)]);

        await Assert.That(audience).IsEmpty()
            .Because("an audience derived from declarations cannot include a machine that declared "
                   + "nothing, and pretending otherwise would push a secret on a guess.");
    }

    [Test]
    public async Task A_profile_nothing_runs_under_contributes_nobody()
    {
        var audience = CredentialAudience.For(
            Locator,
            [ARunner("r1", "vmlinux001", profile: "prod")],
            [AProfile("dev", Locator), AProfile("prod", Other)]);

        await Assert.That(audience).IsEmpty()
            .Because("the dev profile declares it and nothing is running under dev. A count of "
                   + "profiles is not a count of machines.");
    }

    [Test]
    public async Task Every_machine_under_a_declaring_profile_is_named_separately()
    {
        // ONE ROW PER MACHINE, not per profile. A pool under one profile is "however
        // many members the pool happens to be running" - the amendment's own words -
        // and a person approving a push needs to see each one.
        var audience = CredentialAudience.For(
            Locator,
            [
                ARunner("r1", "vmlinux001", profile: "dev"),
                ARunner("r2", "vmlinux002", profile: "dev"),
                ARunner("r3", "a-laptop"),
            ],
            [AProfile("dev", Locator)]);

        await Assert.That(audience.Select(a => a.Label).Order())
            .IsEquivalentTo(new[] { "vmlinux001", "vmlinux002" })
            .Because("two machines under the declaring profile, named one per row.");
    }

    [Test]
    public async Task The_audience_carries_the_locator_each_machine_receives()
    {
        var audience = CredentialAudience.For(
            Locator,
            [ARunner("r1", "vmlinux001", profile: "dev")],
            [AProfile("dev", Locator)]);

        await Assert.That(audience[0].Locator).IsEqualTo(Locator)
            .Because("rule 5 asks for the locator each one will receive, because an audience that "
                   + "said only how many is a count rather than a list somebody can review.");
    }

    [Test]
    public async Task Nothing_in_the_derivation_can_open_a_credential()
    {
        // THE RULE THAT MAKES IT SAFE TO PRINT. CredentialRows.For takes no store and
        // no delegate for exactly this reason, and an audience is rendered on the same
        // paths. Asserted over the signature: a parameter it could resolve through is
        // the hole, whether or not today's code uses it.
        var parameters = typeof(CredentialAudience)
            .GetMethod(nameof(CredentialAudience.For))!
            .GetParameters()
            .Select(p => p.ParameterType.Name)
            .ToList();

        foreach (var reachable in (string[])["ICredentialStore", "FileCredentialStore",
                                             "ControlPlaneClient", "HttpClient", "Func`1"])
        {
            await Assert.That(parameters).DoesNotContain(reachable)
                .Because($"{reachable} would let a render path resolve a credential, which is the "
                       + "thing `gg doctor` shows how easily happens. Parameters: "
                       + string.Join(", ", parameters));
        }
    }

    [Test]
    public async Task An_empty_fleet_is_an_empty_audience_rather_than_a_refusal()
    {
        // A TENANT WITH NOTHING YET. The honest answer is a list of nobody, which the
        // caller turns into a sentence; throwing would make "you have not set this up"
        // read as a fault.
        var audience = CredentialAudience.For(Locator, [], []);

        await Assert.That(audience).IsEmpty();
    }
}
