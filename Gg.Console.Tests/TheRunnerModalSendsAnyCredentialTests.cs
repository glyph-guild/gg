using Gg.Console;
using Gg.Contracts;

namespace Gg.Console.Tests;

/// <summary>
/// The runner modal's send offers every credential this tenant has registered, not only
/// repositories.
/// </summary>
/// <remarks>
/// <para>
/// <b>Owner, 2026-10-11:</b> the runner modal's send listed repositories and nothing
/// else, so a tracker's or an analysis service's credential - a SonarCloud token - could
/// not be put on a machine from the screen that shows the machine.
/// </para>
/// <para>
/// <b>The row carries its locator</b>, derived for a repository by the contract's own
/// <c>ForRepo</c> and read off the reference for everything else, so the sender moves a
/// locator and derives nothing - the rule <c>gg credential send</c> follows.
/// </para>
/// </remarks>
public class TheRunnerModalSendsAnyCredentialTests
{
    private const string RunnerId = "01a06572-a784-72ae-b951-f147553cd48e";
    private const string Sonar = "local:analysis/sonarcloud";

    private static CredentialSummary Registered(string locator, string kind = "local") => new()
    {
        CredentialId = locator,
        For = locator,
        AddedAt = DateTimeOffset.UnixEpoch,
        Reference = new CredentialReference
        {
            Kind = kind,
            Locator = locator,
            Identity = "kevin-deenanauth47049",
            Scopes = [CredentialScopes.Read],
        },
    };

    private static AppState Choosing() => new()
    {
        Mode = UiMode.CredentialRepositoryChoice,
        Repositories = new RegisteredRepositories
        {
            Repositories =
            [
                new RepositoryRegistered
                {
                    Name = "JDNext",
                    Provider = "ado",
                    Id = "ado-jdx-jdnext",
                    Path = "JDX/JDNext",
                    Credential = RepositoryCredentialModes.Required,
                    RegisteredBy = "kdee",
                    RegisteredAt = DateTimeOffset.UnixEpoch,
                },
            ],
        },
        Credentials = new CredentialList
        {
            Credentials =
            [
                Registered(CredentialLocator.ForRepo("JDX/JDNext")),
                Registered(Sonar),
            ],
        },
        Runners = new RunnerList
        {
            Runners =
            [
                new RunnerSummary
                {
                    RunnerId = RunnerId,
                    Label = "vmlinux002",
                    State = RunnerStates.Idle,
                    LastHeartbeatAt = DateTimeOffset.UnixEpoch,
                },
            ],
        },
        RunnerSelected = 0,
    };

    [Test]
    public async Task An_analysis_service_s_credential_is_offered()
    {
        var locators = CredentialRepositories.Rows(Choosing()).Select(r => r.Locator).ToList();

        await Assert.That(locators).Contains(Sonar);
    }

    [Test]
    public async Task A_repository_s_row_carries_the_locator_its_path_derives()
    {
        var row = CredentialRepositories.Rows(Choosing()).Single(r => r.Path == "JDX/JDNext");

        await Assert.That(row.Locator).IsEqualTo(CredentialLocator.ForRepo("JDX/JDNext"));
    }

    [Test]
    public async Task A_repository_s_credential_is_offered_once()
    {
        var locators = CredentialRepositories.Rows(Choosing()).Select(r => r.Locator).ToList();

        await Assert.That(locators.Count(l => l == CredentialLocator.ForRepo("JDX/JDNext")))
            .IsEqualTo(1)
            .Because("the registry row and the registered reference are the same credential.");
    }

    [Test]
    public async Task The_sender_is_handed_the_chosen_locator()
    {
        var state = Choosing();
        state = state with
        {
            CredentialRepoSelected = CredentialRepositories.Rows(state)
                .Select((row, at) => (row.Locator, at))
                .First(r => r.Locator == Sonar).at,
        };

        string? handed = null;
        _ = ConsoleSendCredential.Give(state, (_, locator) =>
        {
            handed = locator;
            return "sent";
        });

        await Assert.That(handed).IsEqualTo(Sonar);
    }

    [Test]
    public async Task The_question_asks_which_credential()
    {
        var title = PaneText.ModalTitle(Choosing());

        await Assert.That(title).Contains("which credential", StringComparison.OrdinalIgnoreCase);
    }
}
