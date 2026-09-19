using Gg.Contracts;
using Gg.Contracts.Authoring;
using Gg.Contracts.Description;
using Gg.Local;

namespace Gg.Client.Tests;

/// <summary>
/// A fleet profile names an agent and credential sources, and each machine
/// under it resolves them; it travels as an airspace document, and its offer is
/// taken without a person only by a machine that enrolled under it.
/// </summary>
/// <remarks>
/// <para>
/// <b>Slice forty-three, rules 13-15 (S43.3-02, and gg's half of S43.3-01 and
/// S43.3-03).</b> A profile says what an enrolled machine is - its roles, its
/// environment, its agent, its forges - so that nothing about a fleet machine
/// is a hand-edited <c>Environment=</c> line any more.
/// </para>
/// <para>
/// <b>It names; the machine resolves.</b> A path where an agent's name belongs,
/// or a value where a reference belongs, is refused at authoring - the one
/// place a document the whole tenant can read would otherwise carry a secret
/// or point every machine at one binary.
/// </para>
/// </remarks>
public class AProfileNamesAndTheMachineResolvesTests
{
    private static FleetProfile ADevWorker() => new()
    {
        Roles = [ProfileRoles.Run],
        Environment = "dev",
        Agent = "claude",
        Forges = ["acme=git.acme.example"],
        Destinations = ["acme=https://git.acme.example/api"],
        Credentials = ["keyvault://acme-fleet.vault.example.net/forge-token", "local:npm-token"],
    };

    // ---- rule 14: it names, and the machine resolves ----

    [Test]
    [Arguments("/usr/local/bin/claude")]
    [Arguments("claude --dangerously")]
    [Arguments("C:\\agents\\claude.exe")]
    public async Task An_agent_is_a_name_never_a_path(string agent)
    {
        var refused = FleetProfile.Validate(ADevWorker() with { Agent = agent });

        await Assert.That(refused).IsNotNull();
        await Assert.That(refused!).Contains("names an agent, never a binary");
    }

    [Test]
    [Arguments("ghp_0123456789abcdefghij")]
    [Arguments("hunter2")]
    [Arguments("keyvault://")]
    [Arguments("local:")]
    public async Task A_credential_is_a_reference_never_a_value_and_is_not_repeated(string credential)
    {
        var refused = FleetProfile.Validate(ADevWorker() with { Credentials = [credential] });

        await Assert.That(refused).IsNotNull();
        await Assert.That(refused!).Contains("never the secret itself");

        // THE VALUE IS NOT REPEATED - checked for the two that could be secrets;
        // the other two are the schemes the sentence itself teaches.
        if (!credential.EndsWith(':') && !credential.EndsWith("//", StringComparison.Ordinal))
        {
            await Assert.That(refused!).DoesNotContain(credential)
                .Because("a refusal that echoed the value would print the secret it just refused.");
        }
    }

    [Test]
    public async Task A_profile_of_names_and_references_is_valid()
    {
        await Assert.That(FleetProfile.Validate(ADevWorker())).IsNull();
        await Assert.That(FleetProfile.Validate(ADevWorker() with
        {
            Roles = [ProfileRoles.Run, ProfileRoles.Maintain], Agent = null, Credentials = [],
        })).IsNull();
    }

    [Test]
    [Arguments(new string[0], "names no role")]
    [Arguments(new[] { "build" }, "not a role")]
    [Arguments(new[] { "run", "run" }, "twice")]
    public async Task Roles_are_run_and_maintain_each_said_once(string[] roles, string said)
    {
        await Assert.That(FleetProfile.Validate(ADevWorker() with { Roles = roles })).Contains(said);
    }

    [Test]
    public async Task A_forge_is_written_as_vcs_hosts_writes_one()
    {
        await Assert.That(FleetProfile.Validate(ADevWorker() with { Forges = ["git.acme.example"] }))
            .Contains("key=host");
    }

    [Test]
    public async Task The_label_a_lease_matches_is_the_profiles_environment()
    {
        await Assert.That(FleetProfile.LabelFor(ADevWorker())).IsEqualTo("environment=dev");
    }

    // ---- rule 13: an airspace document ----

    [Test]
    public async Task A_profile_lives_under_fleet_and_reads_back_as_it_was_written()
    {
        await Assert.That(AirspaceNames.PathFor(Roles.FleetProfile, "dev-worker"))
            .IsEqualTo("fleet/dev-worker.yaml");

        var text = EnvelopeText.Render(ADevWorker());
        var read = EnvelopeYaml.ParseProfile(text);

        await Assert.That(read.Diagnosis).IsNull();
        await Assert.That(EnvelopeText.Render(read.Profile!)).IsEqualTo(text);
        await Assert.That(read.Profile?.Credentials).IsEquivalentTo(ADevWorker().Credentials);
    }

    [Test]
    public async Task A_misspelt_key_is_refused_rather_than_read_as_absent()
    {
        var read = EnvelopeYaml.ParseProfile("""
            roles: [run]
            environment: dev
            credential:
              - local:npm-token
            """);

        await Assert.That(read.Profile).IsNull();
        await Assert.That(read.Diagnosis!).Contains("credential");
    }

    [Test]
    public async Task Validating_a_file_under_fleet_refuses_what_apply_would()
    {
        var answered = (VerbResult.EnvelopeValidated)EnvelopeCommands.Validate("""
            roles: [run]
            environment: dev
            agent: /opt/claude
            """, "fleet/dev-worker.yaml");

        await Assert.That(answered.Value.Role).IsEqualTo(Roles.FleetProfile);
        await Assert.That(answered.Value.Valid).IsFalse();
    }

    [Test]
    public async Task The_doors_are_declared()
    {
        await Assert.That(ProtocolSurface.Endpoints.Any(e =>
                e.Method == "PUT" && e.Path == "/v1/airspace/fleet/{name}"
                && e.Request == typeof(FleetProfile) && e.Response == typeof(EnvelopeApplied)
                && e.Statuses.Contains(202)))
            .IsTrue()
            .Because("202 is a widening diverted to the gate, as a strategy's is.");
        await Assert.That(ProtocolSurface.Endpoints.Any(e =>
                e.Method == "GET" && e.Path == "/v1/airspace/fleet" && e.Response == typeof(FleetProfileList)))
            .IsTrue();
    }

    [Test]
    public async Task A_pull_writes_every_profile_in_force_and_an_apply_does_not_read_one_as_retired()
    {
        var root = Directory.CreateTempSubdirectory("gg-fleet-").FullName;
        try
        {
            var estate = new AirspaceEstate
            {
                Documents = [],
                Strategies = [],
                Profiles =
                [
                    new FleetProfileState
                    {
                        Name = "dev-worker", Version = "v1", AppliedAt = DateTimeOffset.UnixEpoch,
                        Profile = ADevWorker(),
                    },
                ],
            };

            var written = AirspaceTree.Write(root, estate);
            var tree = AirspaceTree.Read(root);

            await Assert.That(written.Written.Any(p => p.Replace('\\', '/').EndsWith(
                    "fleet/dev-worker.yaml", StringComparison.Ordinal)))
                .IsTrue();
            await Assert.That(tree.Documents.Single().Profile).IsNotNull();
            await Assert.That(AirspaceTree.Changed(tree, estate)).IsEmpty();
            await Assert.That(AirspaceTree.Retiring(tree, estate)).IsEmpty();
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    // ---- what widens ----

    [Test]
    public async Task More_reach_is_a_widening_and_less_applies_at_once()
    {
        var prior = ADevWorker();

        await Assert.That(FleetProfile.Widening(prior, prior with { Roles = [ProfileRoles.Run, ProfileRoles.Maintain] }))
            .IsNotNull();
        await Assert.That(FleetProfile.Widening(prior, prior with { Environment = "prod" })?.Field)
            .IsEqualTo("environment");
        await Assert.That(FleetProfile.Widening(prior, prior with { Sweeps = true })?.Field).IsEqualTo("sweeps");
        await Assert.That(FleetProfile.Widening(prior, prior with { Credentials = [.. prior.Credentials, "local:more"] }))
            .IsNotNull();

        await Assert.That(FleetProfile.Widening(prior, prior with { Forges = [] })).IsNull();
        await Assert.That(FleetProfile.Widening(prior, prior with { Agent = null })).IsNull();
    }

    // ---- rule 15: its offer is taken by the machine that enrolled under it ----

    private static OfferedConfiguration AnOffer(string? profile) => new()
    {
        Version = "offer@abc",
        OfferedAt = DateTimeOffset.UnixEpoch,
        Profile = profile,
        Settings =
        [
            new OfferedSetting { Key = OfferableKeys.VcsHosts, Value = "acme=git.acme.example" },
            new OfferedSetting { Key = OfferableKeys.RunnerLabels, Value = "environment=dev" },
        ],
    };

    [Test]
    public async Task A_directed_offer_from_the_profile_this_machine_enrolled_under_needs_nobody()
    {
        var taken = OfferedConfigurations.Accept(
            AnOffer("dev-worker"),
            new Configuration { AcceptOffered = true, EnrolledProfile = "dev-worker" },
            attended: false);

        await Assert.That(taken.Waiting).IsFalse();
        await Assert.That(taken.Configuration!.VcsHosts).IsEqualTo("acme=git.acme.example")
            .Because("a person accepted this forge at the profile's gate; asking again on every "
                   + "machine is what enrollment exists to end.");
    }

    [Test]
    public async Task An_offer_naming_any_other_profile_is_held_for_a_person()
    {
        var taken = OfferedConfigurations.Accept(
            AnOffer("prod-worker"),
            new Configuration { AcceptOffered = true, EnrolledProfile = "dev-worker" },
            attended: false);

        await Assert.That(taken.Waiting).IsTrue()
            .Because("the control plane naming a profile is a claim; the machine's own file is "
                   + "the consent, and it consented to dev-worker.");
    }

    [Test]
    public async Task A_machine_that_takes_no_offers_takes_none_from_its_profile_either()
    {
        var taken = OfferedConfigurations.Accept(
            AnOffer("dev-worker"),
            new Configuration { EnrolledProfile = "dev-worker" },
            attended: false);

        await Assert.That(taken.Configuration).IsNull();
    }

    [Test]
    public async Task Enrolled_under_is_the_machines_to_say_and_never_an_offers()
    {
        await Assert.That(OfferableKeys.All).DoesNotContain("enrolled-profile");
        await Assert.That(ProtocolSurface.JsonMembers[typeof(OfferedConfiguration)]).Contains("profile");
    }
}
