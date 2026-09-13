using Gg.Client;
using Gg.Contracts;

namespace Gg.Client.Tests;

/// <summary>
/// Registering a repository, as a verb rather than as an HTTP call by hand.
/// </summary>
/// <remarks>
/// <para>
/// <b>Two success shapes, and the 202 is the ordinary one.</b> A registry entry
/// is reach that did not exist a moment ago (ADR-0016 § 6), so a new repository
/// rides a gate and the answer names the flight and who decides. The 200 is the
/// narrow case - already live and identical - which is the idempotent re-run.
/// Folded into one record the way <c>NameDeclared</c> already folds a gated
/// declaration into the same shape as a landed one.
/// </para>
/// <para>
/// <b>403 IS THE ROLE ANSWER AND IT COMES FROM THE WIRE.</b>
/// <c>WhoAmI.IsAdmin</c> is a hint about what a surface would be allowed to
/// show, never a permission - every route checks the principal itself - so gg
/// asks and renders what it is told. What it must NOT do is what
/// <c>EnsureSuccessStatusCode</c> did on this path: turn "the control plane
/// answered, and the answer is that you may not" into <i>"could not reach the
/// control plane… try gg doctor"</i>, which sends somebody to diagnose a
/// network that is working.
/// </para>
/// <para>
/// <b>400 and 403 are two instructions, not one refusal with two codes.</b> A
/// malformed entry carries the door's own sentence naming which field was
/// blank, and the person who typed it fixes it; a principal without the role
/// has nothing to fix and has to be pointed at somebody who has it. They are
/// separate types here for that reason.
/// </para>
/// </remarks>
public class RegisteringARepositoryIsAVerbTests
{
    private static StoredSession SignedIn { get; } = new()
    {
        SessionToken = "stub-session",
        ExpiresAt = new DateTimeOffset(2027, 1, 1, 0, 0, 0, TimeSpan.Zero),
        TenantId = "stub-tenant",
        PrincipalDisplay = "someone@example.test",
    };

    private static FlightCommands Against(StubControlPlane stub, HttpClient http)
    {
        http.BaseAddress = new Uri(stub.BaseAddress);
        return new FlightCommands(new ControlPlaneClient(http), new HeldSessionStore(SignedIn));
    }

    private static RepositoryRegistered Live() => new()
    {
        Name = "payments",
        Provider = "forge",
        Id = "R_123",
        Path = "acme/payments",
        Credential = RepositoryCredentialModes.Required,
        RegisteredBy = "dana@example.test",
        RegisteredAt = DateTimeOffset.UnixEpoch,
    };

    [Test]
    public async Task A_new_repository_rides_a_flight_and_the_answer_says_who_decides()
    {
        await using var stub = new StubControlPlane
        {
            RepositoryPending = new RegistrationPending
            {
                Flight = "GG-77",
                Awaiting = "platform-oncall",
                Widens = "the registry gaining the name payments",
            },
        };

        using var http = new HttpClient();
        var added = ((VerbResult.RepositoryAdded)await Against(stub, http)
            .RegisterRepositoryAsync("payments", "forge", "R_123", "acme/payments")).Value;

        await Assert.That(added.Flight).IsEqualTo("GG-77");
        await Assert.That(added.Awaiting).IsEqualTo("platform-oncall");
        await Assert.That(added.Widens).IsEqualTo("the registry gaining the name payments");

        await Assert.That(added.RegisteredBy).IsNull()
            .Because("nobody has registered it yet. Filling the attribution with the asker "
                   + "would name somebody who has decided nothing, which is the reason "
                   + "NameDeclared leaves the same member null while a gate is open.");
    }

    [Test]
    public async Task A_repository_already_registered_is_reported_live_rather_than_pending()
    {
        // THE POSITIVE CONTROL. A verb that reported every registration as
        // pending would satisfy the test above and send a person to wait for a
        // gate that will never open, because the entry is already theirs.
        await using var stub = new StubControlPlane { RepositoryLive = Live() };

        using var http = new HttpClient();
        var added = ((VerbResult.RepositoryAdded)await Against(stub, http)
            .RegisterRepositoryAsync("payments", "forge", "R_123", "acme/payments")).Value;

        await Assert.That(added.Flight).IsNull();
        await Assert.That(added.RegisteredBy).IsEqualTo("dana@example.test");
        await Assert.That(added.Credential).IsEqualTo(RepositoryCredentialModes.Required)
            .Because("the resolved mode is said out loud on the way out, so a reader never "
                   + "needs the defaulting rule to know what the entry demands.");
    }

    [Test]
    public async Task The_four_facts_and_the_optional_three_all_reach_the_wire()
    {
        // THE ANSWER DOES NOT PROVE THE REQUEST. A verb that dropped the ref
        // would still be told 202, and the repository it registered would send
        // every ticket flight to an empty tree.
        await using var stub = new StubControlPlane { RepositoryLive = Live() };

        using var http = new HttpClient();
        await Against(stub, http).RegisterRepositoryAsync(
            "payments", "forge", "R_123", "acme/payments",
            credential: RepositoryCredentialModes.None,
            reference: "refs/heads/trunk",
            narrowings: "policy");

        var sent = stub.RegisteredRepository;

        await Assert.That(sent).IsNotNull();
        await Assert.That(sent!.Name).IsEqualTo("payments");
        await Assert.That(sent.Provider).IsEqualTo("forge");
        await Assert.That(sent.Id).IsEqualTo("R_123");
        await Assert.That(sent.Path).IsEqualTo("acme/payments");
        await Assert.That(sent.Credential).IsEqualTo(RepositoryCredentialModes.None);
        await Assert.That(sent.Ref).IsEqualTo("refs/heads/trunk");
        await Assert.That(sent.Narrowings).IsEqualTo("policy");
    }

    [Test]
    public async Task The_optional_three_go_absent_rather_than_blank_when_unsaid()
    {
        await using var stub = new StubControlPlane { RepositoryLive = Live() };

        using var http = new HttpClient();
        await Against(stub, http)
            .RegisterRepositoryAsync("payments", "forge", "R_123", "acme/payments");

        await Assert.That(stub.RegisteredRepository!.Ref).IsNull()
            .Because("a blank ref is not the same fact as no ref: null means the flight has "
                   + "no repository, and an empty string is a ref that resolves to nothing.");
        await Assert.That(stub.RegisteredRepository.Credential).IsNull();
        await Assert.That(stub.RegisteredRepository.Narrowings).IsNull();
    }

    [Test]
    public async Task A_principal_without_the_role_is_told_so_rather_than_told_to_run_doctor()
    {
        await using var stub = new StubControlPlane { RepositoryStatus = 403 };

        using var http = new HttpClient();
        var commands = Against(stub, http);

        var refused = await Assert.That(async () => await commands
                .RegisterRepositoryAsync("payments", "forge", "R_123", "acme/payments"))
            .Throws<PermissionRefusedException>();

        await Assert.That(refused!.Message).Contains("administrator", StringComparison.OrdinalIgnoreCase)
            .Because("there is nothing for this person to fix. The actionable half of the "
                   + "refusal is who CAN do it, and a bare 'forbidden' leaves them retrying "
                   + "the same command.");
    }

    [Test]
    public async Task A_malformed_entry_carries_the_door_s_own_sentence()
    {
        await using var stub = new StubControlPlane
        {
            RepositoryRefusal = "A registry entry names a provider, a forge id and a path.",
        };

        using var http = new HttpClient();
        var commands = Against(stub, http);

        var refused = await Assert.That(async () => await commands
                .RegisterRepositoryAsync("payments", "", "R_123", "acme/payments"))
            .Throws<EnvelopeRefusedException>();

        await Assert.That(refused!.Message)
            .Contains("forge id", StringComparison.Ordinal)
            .Because("the door names which of the four was wrong, and rewording that here "
                   + "would be a second opinion about somebody else's rule.");
    }

    [Test]
    public async Task A_gated_registration_reads_as_not_nameable_yet()
    {
        // THE SENTENCE IS THE DELIVERABLE. A 202 rendered as "registered"
        // would tell somebody to fly against a repository whose name the
        // registry does not hold, and the flight would be refused pointing at
        // the door they just knocked on.
        var text = VerbOutput.ToText(new VerbResult.RepositoryAdded(new RepositoryAdded
        {
            Name = "payments",
            Provider = "forge",
            Id = "R_123",
            Path = "acme/payments",
            Flight = "GG-77",
            Awaiting = "platform-oncall",
            Widens = "the registry gaining the name payments",
        }));

        await Assert.That(text).Contains("GG-77", StringComparison.Ordinal);
        await Assert.That(text).Contains("platform-oncall", StringComparison.Ordinal);
        await Assert.That(text).Contains("not", StringComparison.OrdinalIgnoreCase)
            .Because("what a person needs from this line is that the name is NOT usable "
                   + "yet - the flight number alone reads like a receipt.");
    }

    [Test]
    public async Task A_live_registration_names_who_registered_it()
    {
        var text = VerbOutput.ToText(new VerbResult.RepositoryAdded(new RepositoryAdded
        {
            Name = "payments",
            Provider = "forge",
            Id = "R_123",
            Path = "acme/payments",
            Credential = RepositoryCredentialModes.Required,
            RegisteredBy = "dana@example.test",
        }));

        await Assert.That(text).Contains("dana@example.test", StringComparison.Ordinal);
        await Assert.That(text).DoesNotContain("GG-", StringComparison.Ordinal)
            .Because("nothing rode a flight, so naming one would point at a gate list that "
                   + "does not hold it.");
    }
}
