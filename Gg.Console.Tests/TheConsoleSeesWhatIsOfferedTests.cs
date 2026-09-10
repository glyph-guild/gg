using Gg.Console;

namespace Gg.Console.Tests;

/// <summary>
/// The Environment page says what this tenant is offered, and how to take it.
/// </summary>
/// <remarks>
/// <para>
/// <b>I declared this unreachable and it is not.</b>
/// <c>ProjectionParityTests</c> exempts <c>ConfigOffered</c> with the reason
/// that "an offer comes from a control plane, and a UI session may read a local
/// file and nothing else". The first half is true and the conclusion does not
/// follow: the rule is about what a SESSION may do, and this console already
/// makes network calls between sessions — the refresh does, on a key. An offer
/// is one more thing fetched where every other read already happens.
/// </para>
/// <para>
/// <b>On the Environment page, because that is where configuration is
/// read.</b> The page already lists every setting with the source that answered
/// it; what is offered is the one thing about this machine's configuration that
/// comes from somewhere else, and a person deciding whether to take it is
/// comparing it against exactly the rows beside it.
/// </para>
/// <para>
/// <b>It says something when nothing is offered, too.</b> A page that mentions
/// offers only when one is waiting cannot be used to check that none is — the
/// argument <c>gg config show</c>'s posture line already carries, on the same
/// question.
/// </para>
/// </remarks>
public class TheConsoleSeesWhatIsOfferedTests
{
    private static AppState Showing(OfferedOnThisMachine? offered) => new()
    {
        Modal = UiMode.Help,
        HelpPage = HelpPage.Environment,
        Settings =
        [
            new EnvironmentSetting
            {
                Name = "GG_CONTROL_PLANE",
                Value = "https://control.invalid",
                Why = "from the environment",
            },
        ],
        Offered = offered,
    };

    private static OfferedOnThisMachine AnOffer() => new()
    {
        Version = "offer@7",
        Settings = 2,
        NeedsAPerson = true,
    };

    [Test]
    public async Task The_page_names_the_offer_that_is_waiting()
    {
        var page = PaneText.Modal(Showing(AnOffer()));

        await Assert.That(page).Contains("offer@7", StringComparison.Ordinal)
            .Because("a person takes it by version, and the version is the thing they have "
                   + "to be able to read.");
    }

    [Test]
    public async Task It_says_so_when_nothing_is_offered()
    {
        var page = PaneText.Modal(Showing(null));

        await Assert.That(page).Contains("offered", StringComparison.OrdinalIgnoreCase)
            .Because("a page that mentions offers only when one is waiting cannot be used "
                   + "to check that none is - which is the question somebody opens this "
                   + "page to answer.");
    }

    [Test]
    public async Task It_says_the_offer_needs_a_person_when_it_does()
    {
        var page = PaneText.Modal(Showing(AnOffer()));

        await Assert.That(page).Contains("repoints", StringComparison.OrdinalIgnoreCase)
            .Because("this is the whole reason the console has any part in this: a directed "
                   + "key is offerable only because somebody sees what is being repointed.");
    }

    [Test]
    public async Task An_offer_already_in_force_does_not_ask_to_be_taken_again()
    {
        var page = PaneText.Modal(Showing(new OfferedOnThisMachine
        {
            Version = "offer@7",
            Settings = 2,
            AlreadyAccepted = true,
        }));

        await Assert.That(page).Contains("already", StringComparison.OrdinalIgnoreCase);
    }

    [Test]
    public async Task What_is_offered_survives_the_terminal_being_handed_away()
    {
        // AppState IS THE ONLY THING THAT CROSSES A SESSION. Views are rebuilt
        // from it after $EDITOR has had the terminal, so a field the
        // source-generated context cannot carry is one that silently empties
        // the moment somebody edits their configuration - on the very page
        // this is drawn.
        var before = Showing(AnOffer());
        var after = AppStateJson.Deserialize(AppStateJson.Serialize(before));

        await Assert.That(after.Offered!.Version).IsEqualTo("offer@7");
        await Assert.That(after.Offered.Settings).IsEqualTo(2);
        await Assert.That(after.Offered.NeedsAPerson).IsTrue();
    }
}
