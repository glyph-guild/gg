using Gg.Client;
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

    private static RunnerList OneRunner() => new()
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
    };

    /// <summary>A chooser open over repositories with the standings given.</summary>
    private static AppState Standing(params (string Path, string Said)[] rows) => new()
    {
        Mode = UiMode.CredentialRepositoryChoice,
        Repositories = new RegisteredRepositories
        {
            Repositories =
            [
                .. rows.Select(r => new RepositoryRegistered
                {
                    Name = r.Path,
                    Provider = "ado",
                    Id = r.Path,
                    Path = r.Path,
                    Credential = RepositoryCredentialModes.Required,
                    RegisteredBy = "kdee",
                    RegisteredAt = DateTimeOffset.UnixEpoch,
                }),
            ],
        },
        RepositoryCredentials =
            [.. rows.Select(r => new Gg.Client.RepositoryCredential(r.Path, r.Said))],
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
    public async Task There_is_no_prompt_row_because_the_registry_is_the_answer()
    {
        // ASKING A PERSON TO TYPE A SLUG WAS THE FALLBACK FOR NOT HAVING READ
        // THE REGISTRY, and the registry is a background read this console can
        // simply do. A row that says "type it yourself" on a screen that could
        // list them is the screen declining to answer its own question.
        var rows = CredentialRepositories.Offered(OnARunner(Two()));

        await Assert.That(rows.Any(row =>
            row.Contains("prompt", StringComparison.OrdinalIgnoreCase))).IsFalse();
    }

    [Test]
    public async Task The_ones_without_a_credential_come_first()
    {
        // WHAT THE SCREEN IS FOR. Every standing names a different remedy, and
        // two of them are "somebody has to send one": missing here, and none
        // registered. Those are the rows this key exists to act on, so they are
        // where the cursor starts - not rows a person scrolls past what is
        // already done to reach.
        var rows = CredentialRepositories.Rows(Standing(
            ("JDX/done", CredentialStanding.Here),
            ("JDX/needed", CredentialStanding.MissingHere)));

        await Assert.That(rows[0].Path).IsEqualTo("JDX/needed");
        await Assert.That(rows[^1].Path).IsEqualTo("JDX/done");
    }

    [Test]
    public async Task A_repository_that_authenticates_to_nothing_sorts_last()
    {
        // NOT NEEDED IS NOT PENDING. A file:// mirror wants no credential at
        // all, so putting it among the rows that need one would be inventing
        // work - the distinction CredentialStanding draws deliberately rather
        // than collapsing into "ok" and "not ok".
        var rows = CredentialRepositories.Rows(Standing(
            ("JDX/mirror", CredentialStanding.NotNeeded),
            ("JDX/needed", CredentialStanding.NoneRegistered)));

        await Assert.That(rows[0].Path).IsEqualTo("JDX/needed");
        await Assert.That(rows[^1].Path).IsEqualTo("JDX/mirror");
    }

    [Test]
    public async Task Opening_it_asks_for_the_registry_rather_than_asking_a_person()
    {
        // THE READ THAT MAKES THE PROMPT UNNECESSARY. ToggleRepositories is
        // already in Reads for the pane; this wants the same registry for the
        // same reason, and a background read folds in without the session
        // ending - so the list fills itself instead of a person filling it.
        await Assert.That(ShellCommands.Reads).Contains(Command.ChooseCredentialRepository);

        await Assert.That(
                Reducer.Reduce(OnARunner(null), Command.ChooseCredentialRepository).ReadInFlight)
            .IsTrue()
            .Because("a console that has not read the registry is the case this is FOR, and "
                   + "it has to say a read is coming or an empty list reads as an answer.");
    }

    [Test]
    public async Task Answering_carries_the_path_out_of_the_session()
    {
        // THE CURSOR CROSSES, NOT A RECORDED ANSWER. Answering ends the
        // session, so a reducer that wrote the choice down would give a shell
        // command a second local effect - which ShellCommands forbids by name,
        // because the local half lands whether or not the send did. The cursor
        // is already in the model and is read where it is spent.
        var answered = Reducer.CredentialRepositoryAsked(OnARunner(Two())) with
        {
            CredentialRepoSelected = 1,
        };

        await Assert.That(CredentialRepositories.Chosen(answered)).IsEqualTo("JDX/JDNext");
    }

    [Test]
    public async Task Nothing_is_chosen_unless_the_chooser_is_open()
    {
        // ROW ZERO IS A REAL REPOSITORY, so a send arriving by any other route
        // would otherwise resolve to whichever happens to be first - a
        // credential placed for something nobody named.
        var elsewhere = OnARunner(Two()) with { CredentialRepoSelected = 0 };

        await Assert.That(CredentialRepositories.Chosen(elsewhere)).IsNull();
    }

    [Test]
    public async Task Nothing_is_sent_when_there_is_nothing_to_choose()
    {
        // AND IT SAYS SO RATHER THAN DROPPING TO A PROMPT. With the registry
        // unread or empty there is no repository to name, and a send carrying
        // none would reach the runner asking it to write a secret for nothing.
        var sent = false;

        var state = Reducer.CredentialRepositoryAsked(OnARunner(null)) with
        {
            Runners = OneRunner(),
            RunnerSelected = 0,
        };

        var after = ConsoleSendCredential.Give(state, (_, _) =>
        {
            sent = true;
            return "sent";
        });

        await Assert.That(sent).IsFalse();
        await Assert.That(after.LastCredential).IsNotNull();
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
            Reducer.CredentialRepositoryAsked(OnARunner(Two())) with
            {
                CredentialRepoSelected = 1,
            });

        await Assert.That(sent.CredentialRepoSelected).IsEqualTo(0);
        await Assert.That(sent.Mode).IsEqualTo(UiMode.Normal)
            .Because("a chooser left open behind a send would be answered again by the next "
                   + "keypress that meant something else.");
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
            Mode = UiMode.CredentialRepositoryChoice,
            CredentialRepoSelected = 1,
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
