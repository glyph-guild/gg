using Gg.Client;
using Gg.Contracts;

namespace Gg.Console.Tests;

/// <summary>
/// The repositories pane is not a tab in this console, and everything it was not
/// is kept.
/// </summary>
/// <remarks>
/// <para>
/// <b>S60.5-01 and S60.5-02.</b> Withdrawn because it answered a credential
/// question as one column of seven. The owner's call for slice sixty is that the
/// list is credential-first — the question a person brings to it is about a
/// credential, and the repository is context — so the pane retires into the
/// credentials pane rather than sitting beside it.
/// </para>
/// <para>
/// <b>What is NOT removed, and this is the point of the test.</b>
/// <c>TheChecklistTabIsGoneTests</c> wrote the rule: <i>"A removal that took the
/// wire type with it would drag the control plane into a coordinated release for a
/// UI decision."</i> So <see cref="RegisteredRepositories"/> stays,
/// <c>gg airspace repositories add</c> stays, <c>AirspaceRepositories</c> stays a
/// verb result, and <c>state.Repositories</c> stays in the model — the send
/// chooser reads it, and losing it would make `gg credential send` from the runner
/// modal say there is no repository to send for.
/// </para>
/// <para>
/// <b>The key is not left dead.</b> <c>r</c> carries over to the pane that
/// replaced it, which is the milder version of the dead-key shape this console has
/// paid for four times: muscle memory lands somewhere slightly different rather
/// than nowhere, and the new pane still lists repositories.
/// </para>
/// </remarks>
public class TheRepositoriesTabIsGoneTests
{
    [Test]
    public async Task No_tab_on_the_bar_is_the_repositories_pane()
    {
        var named = Tabs.All.Select(Tabs.Name).ToList();

        await Assert.That(named).DoesNotContain("repositories", StringComparer.OrdinalIgnoreCase)
            .Because("a tab left on the bar is a promise the console no longer keeps.");

        // THE BAR IS STILL A BAR. Without this the assertion above is satisfied
        // by a console with no tabs at all.
        await Assert.That(named).Contains("queue");
        await Assert.That(named.Count).IsGreaterThanOrEqualTo(6)
            .Because($"one pane changed, not the bar. Saw [{string.Join(", ", named)}]");
    }

    [Test]
    public async Task The_registry_is_still_a_verb_and_still_a_wire_type()
    {
        // ONLY THE TAB WENT. Taking the contract with it would drag the control
        // plane into a coordinated release for a decision about a screen.
        await Assert.That(typeof(RegisteredRepositories)).IsNotNull();
        await Assert.That(typeof(VerbResult.AirspaceRepositories)).IsNotNull();
    }

    [Test]
    public async Task The_model_still_holds_the_registry_because_the_send_chooser_reads_it()
    {
        // THE DEPENDENCY THAT MAKES THIS A MOVE RATHER THAN A REMOVAL.
        // CredentialRepositories.Chosen reads state.Repositories to offer a
        // repository when somebody sends a credential from the runner modal. Lose
        // the member - or lose the read that fills it - and that send says there
        // is no repository to send for.
        var state = new AppState
        {
            Repositories = new RegisteredRepositories { Repositories = [ARepository("acme/widgets")] },
        };

        await Assert.That(state.Repositories!.Repositories).IsNotEmpty();
        await Assert.That(CredentialRepositories.Offered(state)).IsNotEmpty()
            .Because("the chooser lists the registry, and the pane that used to fetch it is the "
                   + "one being retired.");
    }

    private static RepositoryRegistered ARepository(string path) => new()
    {
        Name = path.Replace('/', '-'),
        Provider = "forge",
        Id = "R_" + path,
        Path = path,
        Credential = RepositoryCredentialModes.Required,
        RegisteredAt = DateTimeOffset.UnixEpoch,
        RegisteredBy = "a-directory:ada",
    };
}
