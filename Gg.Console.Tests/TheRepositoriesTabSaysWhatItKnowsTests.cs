using Gg.Console;
using Gg.Client;
using Gg.Contracts;

namespace Gg.Console.Tests;

/// <summary>
/// The tab shows what a registration actually says, including whether this
/// machine could use it.
/// </summary>
/// <remarks>
/// <para>
/// <b>A registration has nine fields and the tab showed two.</b> Path and
/// name, which are the two that cannot stop a flight. The three that can are
/// the credential mode, the default ref and the narrowings directory — and all
/// three were invisible in the one pane whose whole job is to say what this
/// tenant can fly against. <c>gg airspace repositories</c> on the command line
/// already prints more than the tab does.
/// </para>
/// <para>
/// <b>The credential is the one a person cannot work out from anywhere
/// else.</b> It is three facts in three places: the registry says whether one
/// is needed, the control plane holds whether a reference was registered, and
/// this machine holds whether the secret is actually here. Only the last makes
/// the difference between a flight that runs and one that dies at the runner,
/// and until now the only way to learn it was to run <c>gg doctor</c> and read
/// a report about something else.
/// </para>
/// <para>
/// <b>Absences are rendered, not blanked.</b> A null ref and a null narrowings
/// directory are meaningful — "null is different from any ref", and a ticket
/// flight against a repository with no ref is refused — so an empty cell would
/// hide the two states somebody is most likely to be debugging.
/// </para>
/// </remarks>
public class TheRepositoriesTabSaysWhatItKnowsTests
{
    private static RepositoryRegistered Repository(
        string path, string credential, string? reference = null, string? narrowings = null) => new()
    {
        Name = path.Split('/')[^1],
        Provider = "a-forge",
        Id = "42",
        Path = path,
        Credential = credential,
        Ref = reference,
        Narrowings = narrowings,
        RegisteredBy = "somebody",
        RegisteredAt = DateTimeOffset.UnixEpoch,
    };

    /// <summary>Four repositories, one per credential standing.</summary>
    private static AppState Listed() => ConsoleProjection.Apply(
        new AppState(),
        new VerbResult.AirspaceRepositories(
            new RegisteredRepositories
            {
                Repositories =
                [
                    Repository("acme/widgets", RepositoryCredentialModes.Required,
                        reference: "refs/heads/main", narrowings: ".gg/narrowings"),
                    Repository("acme/gadgets", RepositoryCredentialModes.Required),
                    Repository("acme/sprockets", RepositoryCredentialModes.Required),
                    Repository("acme/mirror", RepositoryCredentialModes.None),
                ],
            },
            [
                new RepositoryCredential("acme/widgets", CredentialStanding.Here),
                new RepositoryCredential("acme/gadgets", CredentialStanding.MissingHere),
                new RepositoryCredential("acme/sprockets", CredentialStanding.NoneRegistered),
                new RepositoryCredential("acme/mirror", CredentialStanding.NotNeeded),
            ]));

    private static RepositoryRow Row(AppState state, string path) =>
        Rows.Repositories(state).Single(r => r.Path == path);

    [Test]
    public async Task The_columns_name_what_a_registration_can_refuse_a_flight_for()
    {
        // A column a person cannot see is a fact they will look for somewhere
        // else, and for two of these there is nowhere else in the console.
        await Assert.That(Rows.RepositoryColumns).Contains("credential");
        await Assert.That(Rows.RepositoryColumns).Contains("ref");
        await Assert.That(Rows.RepositoryColumns).Contains("narrowings");
    }

    [Test]
    public async Task Each_credential_standing_reads_differently_from_the_others()
    {
        // FOUR STATES, FOUR WORDS. Collapsing any two would send somebody to
        // the wrong remedy: `gg credential add` here, registering a credential
        // at all, or nothing because the repository needs none.
        var state = Listed();

        await Assert.That(Row(state, "acme/widgets").Credential).IsEqualTo(CredentialStanding.Here);
        await Assert.That(Row(state, "acme/gadgets").Credential).IsEqualTo(CredentialStanding.MissingHere);
        await Assert.That(Row(state, "acme/sprockets").Credential).IsEqualTo(CredentialStanding.NoneRegistered);
        await Assert.That(Row(state, "acme/mirror").Credential).IsEqualTo(CredentialStanding.NotNeeded);
    }

    [Test]
    public async Task A_repository_with_no_default_ref_says_so_rather_than_showing_a_gap()
    {
        // "Null is different from any ref": a flight whose intent names none is
        // refused against this repository, which is a fact worth a word.
        var state = Listed();

        await Assert.That(Row(state, "acme/widgets").Ref).IsEqualTo("refs/heads/main");
        await Assert.That(Row(state, "acme/gadgets").Ref).IsNotEmpty()
            .Because("an empty cell reads as a column that did not load, and this is a repository "
                   + "a ticket flight cannot start work on.");
    }

    [Test]
    public async Task Narrowings_on_and_off_are_both_visible()
    {
        // The asymmetry the contract draws: a repository whose every file is
        // policy and one that is not governed at all must not look alike.
        var state = Listed();

        await Assert.That(Row(state, "acme/widgets").Narrowings).IsEqualTo(".gg/narrowings");
        await Assert.That(Row(state, "acme/gadgets").Narrowings).IsNotEmpty();
    }

    [Test]
    public async Task The_standing_is_matched_to_its_repository_and_not_to_a_position()
    {
        // A LIST JOINED BY INDEX is a list that mislabels every row the moment
        // the two reads disagree about order or length - and they are two
        // reads. Reversing one side must change nothing.
        var state = Listed();
        var reversed = state with
        {
            RepositoryCredentials = [.. state.RepositoryCredentials.Reverse()],
        };

        await Assert.That(Row(reversed, "acme/widgets").Credential).IsEqualTo(CredentialStanding.Here);
        await Assert.That(Row(reversed, "acme/mirror").Credential).IsEqualTo(CredentialStanding.NotNeeded);
    }

    [Test]
    public async Task A_repository_nothing_was_said_about_is_not_reported_as_reachable()
    {
        // THE SILENCE THAT MUST NOT READ AS GOOD NEWS, which is this project's
        // recurring failure one pane over. A standing that never arrived - an
        // older control plane, a read that half-failed - has to render as not
        // known rather than as anything a person would act on.
        var state = Listed() with { RepositoryCredentials = [] };

        await Assert.That(Row(state, "acme/widgets").Credential).IsEqualTo(CredentialStanding.Unknown);
        await Assert.That(Row(state, "acme/widgets").Credential).IsNotEqualTo(CredentialStanding.Here);
    }

    [Test]
    public async Task The_pane_a_person_reads_when_there_are_no_rows_still_says_all_of_it()
    {
        // The table draws when there are rows and this sentence when there are
        // not, so the two have to agree about what a registration says.
        var text = PaneText.Repositories(Listed());

        await Assert.That(text).Contains(CredentialStanding.MissingHere);
        await Assert.That(text).Contains("refs/heads/main");
        await Assert.That(text).Contains(".gg/narrowings");
    }
}
