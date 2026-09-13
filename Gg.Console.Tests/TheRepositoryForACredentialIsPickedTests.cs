using Gg.Console;
using Gg.Contracts;

namespace Gg.Console.Tests;

/// <summary>
/// The console asks which repository a credential is for, on the screen that
/// already knows the answer.
/// </summary>
/// <remarks>
/// <para>
/// <b>The question was asked after the terminal was gone.</b> Pressing `c` on a
/// runner ended the session and typed <i>"Which repository is this credential
/// for?"</i> at a bare prompt — so the one screen holding the registry, the
/// credential standings and the runner under the cursor handed the question to
/// the one place holding none of them. A person who did not have the slug
/// memorised had to leave, look it up, and come back.
/// </para>
/// <para>
/// <b>The secret still goes at the prompt, and that is not the same question.</b>
/// <c>LiveStreamingTests</c> forbids a session reaching a credential at all, and
/// <c>ConsoleSendCredential</c>'s own rule is that nothing holds the secret
/// beyond the call — <c>AppState</c> is serialized into a crash dump and a
/// diagnostics bundle, so a secret that crossed a session boundary would cross
/// it in writing. A repository PATH is already in that state, drawn in a pane;
/// carrying it costs nothing that is not already paid.
/// </para>
/// <para>
/// <b>It degrades by saying so.</b> The registry is read when the Repositories
/// pane is first shown, so a console that has never shown it holds none — and a
/// chooser that silently offered nothing would read as "there are no
/// repositories". The last row is always <i>ask me at the prompt</i>, which is
/// exactly what this key did before, so the old path is a row rather than a
/// fallback nobody can see.
/// </para>
/// </remarks>
public class TheRepositoryForACredentialIsPickedTests
{
    private static RegisteredRepositories Two() => new()
    {
        Repositories =
        [
            new RepositoryRegistered
            {
                Name = "agile-cortex",
                Provider = "ado",
                Id = "ado-jdx-agile-cortex",
                Path = "JDX/agile-cortex",
                Credential = RepositoryCredentialModes.Required,
                RegisteredBy = "kdee",
                RegisteredAt = DateTimeOffset.UnixEpoch,
            },
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
    };

    private static AppState OnARunner(RegisteredRepositories? registry) => new()
    {
        Mode = UiMode.Runner,
        Repositories = registry,
    };

    [Test]
    public async Task Pressing_it_on_a_runner_asks_here_rather_than_at_a_bare_prompt()
    {
        var asked = Reducer.CredentialRepositoryAsked(OnARunner(Two()));

        await Assert.That(asked.Mode).IsEqualTo(UiMode.CredentialRepositoryChoice)
            .Because("the screen holding the registry is the one that should ask, and the "
                   + "prompt this replaced held none of it.");

        await Assert.That(asked.CredentialRepoSelected).IsEqualTo(0)
            .Because("a cursor left where it was last would offer a different repository "
                   + "than the one somebody is looking at.");
    }

    [Test]
    public async Task Every_registered_repository_is_offered_by_path()
    {
        // BY PATH, NEVER BY KEY. `gg credential list` keys a reference by the
        // path - JDX/agile-cortex - and CredentialLocator.ForRepo derives the
        // locator from whatever it is handed. Offering the registry KEY here
        // would mint local:jdnext where the reference says local:jdx/jdnext,
        // and the runner would resolve neither.
        var rows = CredentialRepositories.Offered(OnARunner(Two()));

        await Assert.That(rows).Contains("JDX/agile-cortex");
        await Assert.That(rows).Contains("JDX/JDNext");
    }

    [Test]
    public async Task The_last_row_is_always_the_prompt_this_replaced()
    {
        var rows = CredentialRepositories.Offered(OnARunner(Two()));

        await Assert.That(rows[^1]).Contains("prompt", StringComparison.OrdinalIgnoreCase)
            .Because("a repository registered somewhere this console has not read is still a "
                   + "repository somebody may be sending a credential for, and the old path "
                   + "has to stay reachable rather than become unreachable.");
    }

    [Test]
    public async Task A_console_that_never_read_the_registry_offers_only_that_row()
    {
        // NULL IS "NEVER ASKED", which is the distinction AppState.Repositories
        // already draws. The registry is read when the Repositories pane is
        // first shown, so a person who went straight to the fleet holds none -
        // and a chooser that rendered an empty list would say "there are no
        // repositories" to somebody who has several.
        var rows = CredentialRepositories.Offered(OnARunner(null));

        await Assert.That(rows.Count).IsEqualTo(1);
        await Assert.That(rows[0]).Contains("prompt", StringComparison.OrdinalIgnoreCase);
    }

    [Test]
    public async Task Answering_carries_the_path_out_of_the_session()
    {
        var answered = Reducer.CredentialRepositoryPicked(
            Reducer.CredentialRepositoryAsked(OnARunner(Two())) with
            {
                CredentialRepoSelected = 1,
            });

        await Assert.That(answered.CredentialFor).IsEqualTo("JDX/JDNext")
            .Because("the loop sends after the session ends, so what was chosen has to cross "
                   + "in the model - which is exactly what the secret may not do.");
    }

    [Test]
    public async Task Choosing_the_prompt_row_carries_nothing_and_asks_as_it_always_did()
    {
        var rows = CredentialRepositories.Offered(OnARunner(Two()));

        var answered = Reducer.CredentialRepositoryPicked(
            Reducer.CredentialRepositoryAsked(OnARunner(Two())) with
            {
                CredentialRepoSelected = rows.Count - 1,
            });

        await Assert.That(answered.CredentialFor).IsNull()
            .Because("null is what the sender reads as 'ask me', so the row and the old "
                   + "behaviour are the same thing rather than two.");
    }

    [Test]
    public async Task The_answer_is_spent_so_the_next_send_asks_again()
    {
        // A CHOICE LEFT BEHIND IS ONE THE NEXT SEND INHERITS WITHOUT BEING
        // ASKED - the argument the work-kind cursor already makes for being a
        // cursor rather than a setting. Here it is worse than a wrong flight:
        // it would put a credential on a machine for a repository nobody named
        // this time.
        var sent = ConsoleSendCredential.Spent(
            Reducer.CredentialRepositoryPicked(
                Reducer.CredentialRepositoryAsked(OnARunner(Two())) with
                {
                    CredentialRepoSelected = 0,
                }));

        await Assert.That(sent.CredentialFor).IsNull();
    }

    [Test]
    public async Task The_sender_is_handed_the_chosen_repository()
    {
        string? handedRunner = null;
        string? handedRepo = null;

        var state = OnARunner(Two()) with
        {
            Runners = new RunnerList
            {
                Runners =
                [
                    new RunnerSummary
                    {
                        RunnerId = "01a06572-a784-72ae-b951-f147553cd48e",
                        Label = "vmlinux001",
                        State = RunnerStates.Idle,
                        LastHeartbeatAt = DateTimeOffset.UnixEpoch,
                    },
                ],
            },
            RunnerSelected = 0,
            CredentialFor = "JDX/JDNext",
        };

        _ = ConsoleSendCredential.Give(state, (runnerId, repo) =>
        {
            handedRunner = runnerId;
            handedRepo = repo;
            return "sent";
        });

        await Assert.That(handedRunner).IsEqualTo("01a06572-a784-72ae-b951-f147553cd48e");
        await Assert.That(handedRepo).IsEqualTo("JDX/JDNext")
            .Because("the whole point of asking on the screen is that the answer reaches the "
                   + "sender - a chooser whose answer was dropped would ask twice.");
    }
}
