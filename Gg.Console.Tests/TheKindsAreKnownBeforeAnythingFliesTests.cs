using Gg.Contracts;

namespace Gg.Console.Tests;

/// <summary>
/// `f` on the intents tab flies only once the console knows the tenant's work
/// kinds, because not knowing them is not the same as there being none.
/// </summary>
/// <remarks>
/// <para>
/// <b>Reported 2026-10-09: "sometimes when I press 'f' for fly this on the
/// intent tab it does not bring up the modal and instead immediately flies it
/// as an implement work kind".</b> Reproduced in a pty: a second and a half
/// after start the tab showed fifty items while <c>Estate.Names</c> was still
/// null, because the boot that reads the names is no longer waited for. With
/// no names, <c>WorkKinds.Declared</c> answered an empty list, the question was
/// skipped as if the tenant declared none, and the flight went with no kind -
/// which the control plane fills with its default.
/// </para>
/// <para>
/// <b>And it could last all session.</b> The boot reads the names once, on its
/// own failure; a read that failed left them null until somebody opened the
/// airspace tab, and every `f` until then flew without asking.
/// </para>
/// </remarks>
public class TheKindsAreKnownBeforeAnythingFliesTests
{
    private static EnvelopeTopology Names(params string[] kinds) => new()
    {
        Names =
        [
            new TopologyName
            {
                Name = "root",
                Role = Roles.Root,
                DeclaredBy = "kdee",
                DeclaredAt = DateTimeOffset.UnixEpoch,
            },
            .. kinds.Select(kind => new TopologyName
            {
                Name = kind,
                Role = Roles.WorkKind,
                Parent = "root",
                DeclaredBy = "kdee",
                DeclaredAt = DateTimeOffset.UnixEpoch,
            }),
        ],
    };

    private static AppState Browsing(EstateOnThisMachine? estate) => new()
    {
        ActiveTab = TabId.Intents,
        BrowseVisible = true,
        Browse = new BrowseListing
        {
            ProviderKey = "jdx",
            Items = [new BrowseRow { Id = "18515", Title = "a thing to do", State = "Active" }],
        },
        Estate = estate,
    };

    [Test]
    public async Task Before_the_names_have_been_read_nothing_flies_and_it_says_why()
    {
        var actions = new ConsoleDoubles.Records(alreadyFlown: null);

        var pressed = Sent.Inline(ConsoleLoop.FlewPicked(Browsing(estate: null), actions), actions);

        await Assert.That(actions.Flown).IsEmpty()
            .Because("flying now would send no kind, and the control plane would pick one - "
                   + "the flight a person did not choose.");
        await Assert.That(pressed.Launches).IsEmpty()
            .Because("and nothing is queued to be sent later either - the press asks for "
                   + "nothing at all.");
        await Assert.That(pressed.Mode).IsNotEqualTo(UiMode.WorkKindChoice)
            .Because("a question offering only `no kind` would be the same wrong answer, "
                   + "asked.");
        await Assert.That(pressed.LastFlightOpened!).Contains("work kinds");
    }

    [Test]
    public async Task A_working_copy_read_without_its_names_is_still_not_knowing()
    {
        // THE AIRSPACE TAB'S LOCAL WALK writes an estate before the topology
        // read answers, and when that read fails it keeps the walk and no
        // names - so "an estate exists" is not "the kinds are known".
        var actions = new ConsoleDoubles.Records(alreadyFlown: null);

        _ = Sent.Inline(ConsoleLoop.FlewPicked(
            Browsing(new EstateOnThisMachine { Uncommitted = [], Names = null }), actions), actions);

        await Assert.That(actions.Flown).IsEmpty();
    }

    [Test]
    public async Task Names_read_with_no_kinds_in_them_still_fly_without_asking()
    {
        var actions = new ConsoleDoubles.Records(alreadyFlown: null);

        var pressed = Sent.Inline(ConsoleLoop.FlewPicked(
            Browsing(new EstateOnThisMachine { Uncommitted = [], Names = Names() }), actions), actions);

        await Assert.That(actions.Flown).Count().IsEqualTo(1)
            .Because("a tenant that declared no kinds has nothing to choose between - known "
                   + "to be none is an answer, which is the difference this class is about.");
        await Assert.That(pressed.Mode).IsNotEqualTo(UiMode.WorkKindChoice);
    }

    [Test]
    public async Task Names_read_with_kinds_ask_as_they_always_did()
    {
        var actions = new ConsoleDoubles.Records(alreadyFlown: null);

        var pressed = Sent.Inline(ConsoleLoop.FlewPicked(
            Browsing(new EstateOnThisMachine { Uncommitted = [], Names = Names("implement", "research") }),
            actions), actions);

        await Assert.That(actions.Flown).IsEmpty();
        await Assert.That(pressed.Mode).IsEqualTo(UiMode.WorkKindChoice);
    }

    [Test]
    public async Task The_intents_tabs_refresh_reads_the_names_while_they_are_unknown()
    {
        // SO IT HEALS. A boot whose names read failed would otherwise leave `f`
        // refusing until somebody opened a tab they had no reason to open.
        var (data, plane) = AConsolePlane.Console();

        var patch = await ConsoleRefresh.ForTabAsync(
            data, TabId.Intents, Browsing(estate: null));

        await Assert.That(plane.Paths).Contains("/v1/airspace/topology");
        await Assert.That(patch(Browsing(estate: null)).Estate?.Names).IsNotNull();
    }

    [Test]
    public async Task And_reads_nothing_for_them_once_they_are_known()
    {
        var (data, plane) = AConsolePlane.Console();

        _ = await ConsoleRefresh.ForTabAsync(
            data, TabId.Intents,
            Browsing(new EstateOnThisMachine { Uncommitted = [], Names = Names("implement") }));

        await Assert.That(plane.Paths).DoesNotContain("/v1/airspace/topology")
            .Because("the names change when somebody applies an airspace, not every thirty "
                   + "seconds; the boot and the airspace tab already read them.");
    }
}
