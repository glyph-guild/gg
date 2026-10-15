using Gg.Client;
using Gg.Contracts;

namespace Gg.Console.Tests;

/// <summary>
/// Enter on a credential opens where it is: a row per machine that holds it, is meant to
/// and lacks it, or holds it with nothing asking it to (slice seventy-three, S73.4-01).
/// </summary>
/// <remarks>
/// <para>
/// <b>Owner, 2026-10-11:</b> <i>"We should also be able to go to any credential and see
/// which machines it has already been pushed to via a modal (pressing enter on a
/// credential)."</i>
/// </para>
/// <para>
/// <b>What machines SAY, never a log of pushes.</b> A machine that has never said what it
/// holds - an older gg - says so, rather than reading as missing.
/// </para>
/// <para>
/// <b>One derivation with the broadcast review.</b> The rows are the audience's, each
/// carrying whether its machine holds the credential, so the two screens cannot disagree
/// about which machines matter.
/// </para>
/// </remarks>
public class WhereACredentialIsTests
{
    private const string Sonar = "local:analysis/sonarcloud";

    private static RunnerSummary ARunner(
        string id, string label, IReadOnlyList<string>? holds,
        string? profile = null, string state = RunnerStates.Idle,
        IReadOnlyList<ReadinessItem>? lacks = null) =>
        new()
        {
            RunnerId = id, Label = label, State = state, Profile = profile,
            Holds = holds, Lacks = lacks ?? [],
        };

    private static FleetProfileState Declaring(string name) => new()
    {
        Name = name,
        Version = "v1",
        AppliedAt = DateTimeOffset.UnixEpoch,
        Profile = new FleetProfile { Environment = name, Roles = [], Credentials = [Sonar] },
    };

    private static IReadOnlyList<CredentialAudienceRow> Fleet() => CredentialAudience.For(
        Sonar,
        [
            ARunner("r1", "vmlinux001", holds: [], profile: "dev"),
            ARunner("r2", "vmlinux002", holds: [Sonar], profile: "dev"),
            ARunner("r3", "vmlinux003", holds: null, profile: "dev"),
            ARunner("r4", "kevin-mac", holds: [Sonar], state: RunnerStates.Offline),
            ARunner("r5", "vmlinux005", holds: ["local:jdx/jdnext"]),
        ],
        [Declaring("dev")],
        everyone: true);

    [Test]
    public async Task Each_row_says_whether_its_machine_holds_it()
    {
        var rows = Fleet().ToDictionary(r => r.Label);

        await Assert.That(rows["vmlinux002"].HoldsIt is true).IsTrue();
        await Assert.That(rows["vmlinux001"].HoldsIt is false).IsTrue();
        await Assert.That(rows["vmlinux003"].HoldsIt).IsNull()
            .Because("a machine that never said is not a machine that lacks it.");
    }

    [Test]
    public async Task A_machine_that_holds_it_is_listed_even_offline_and_undeclared()
    {
        await Assert.That(Fleet().Select(r => r.Label)).Contains("kevin-mac")
            .Because("where a credential IS includes machines nothing asked to hold it.");
    }

    [Test]
    public async Task Enter_on_a_credential_opens_where_it_is()
    {
        await Assert.That(Keymap.Resolve(
                KeyStroke.EnterKey, new KeymapContext(UiMode.Normal, TabId.Credentials)))
            .IsEqualTo(Command.ShowWhereCredentialIs);
        await Assert.That(ShellCommands.Reads).Contains(Command.ShowWhereCredentialIs)
            .Because("the fleet and the profiles are read beside the console, so it does not flash.");
    }

    [Test]
    public async Task The_modal_says_each_machine_s_standing()
    {
        var text = PaneText.Whereabouts(new AppState
        {
            Mode = UiMode.CredentialWhereabouts,
            Audience = Fleet(),
            AudienceFor = Sonar,
        });

        await Assert.That(text).Contains("vmlinux002").And.Contains("has it");
        await Assert.That(text).Contains("declared, missing");
        await Assert.That(text).Contains("has not said");
        await Assert.That(text).Contains("has it, nothing declares it");
        await Assert.That(text).DoesNotContain("vmlinux005")
            .Because("a machine that neither holds it nor is meant to is not part of where it is.");
    }

    [Test]
    public async Task A_credential_nowhere_says_so_and_nothing_more()
    {
        // OWNER, 2026-10-10: the modal opened on enter "isn't necessary to say `b` on the
        // credentials tab sends to a machine", nor "and none is meant to" when it is on no
        // other machine. Where a credential is answers one question.
        var text = PaneText.Whereabouts(new AppState
        {
            Mode = UiMode.CredentialWhereabouts,
            Audience = [],
            AudienceFor = Sonar,
        });

        await Assert.That(text.Trim()).IsEqualTo($"No machine says it has {Sonar}.");
    }

    [Test]
    public async Task A_machine_too_old_to_say_is_listed_as_not_having_said()
    {
        // OWNER, 2026-10-11: "i sent credentials to vmlinux001 and vmlinux002 ... when i hit
        // enter on the credential it says the credential is not on any machine." Both ran a
        // gg from before slice 73 (0.88.12 and 0.88.23), whose heartbeat carries no `holds`,
        // and nothing declared the credential for them - so the modal dropped them and then
        // said nowhere, which is a claim about machines that never answered.
        var audience = CredentialAudience.For(
            Sonar,
            [
                ARunner("r1", "vmlinux001", holds: null),
                ARunner("r2", "vmlinux002", holds: ["local:jdx/jdnext"]),
            ],
            [],
            everyone: true);

        var text = PaneText.Whereabouts(new AppState
        {
            Mode = UiMode.CredentialWhereabouts,
            Audience = audience,
            AudienceFor = Sonar,
        });

        await Assert.That(text).Contains("vmlinux001").And.Contains("has not said")
            .And.DoesNotContain("No machine says")
            .Because("a machine that never said is not a machine that lacks it.");
        await Assert.That(text).DoesNotContain("vmlinux002")
            .Because("one that said, and does not hold it, is still not part of where it is.");
    }

    [Test]
    public async Task Escape_closes_it()
    {
        await Assert.That(Keymap.Resolve(
                KeyStroke.Esc, new KeymapContext(UiMode.CredentialWhereabouts, TabId.Credentials)))
            .IsEqualTo(Command.CloseModal);
    }
}
