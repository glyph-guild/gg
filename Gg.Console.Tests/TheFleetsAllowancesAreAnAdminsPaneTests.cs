using Gg.Console;
using Gg.Contracts;

namespace Gg.Console.Tests;

/// <summary>
/// Watching every allowance in the fleet, which is an administrator's business.
/// </summary>
/// <remarks>
/// <para>
/// <b>TWO GATES, AND ONLY ONE OF THEM IS A CHECK.</b> The control plane
/// decides what a person may see: it answers with their own allowances, or
/// with every one in the tenant for an administrator. The local file only
/// decides whether this console DRAWS a pane for the second answer. A local
/// flag that granted visibility would be a client-side authorization check,
/// which is no check at all — anybody could edit their own file.
/// </para>
/// <para>
/// <b>So the pane is absent three ways</b>, and they are different absences: a
/// person who is not an administrator, an administrator who has not turned it
/// on, and a control plane too old to say. None of them is an error.
/// </para>
/// <para>
/// <b>File only, no variable, no <c>gg config set</c></b> — <c>AcceptOffered</c>'s
/// rule, and its reason applies here almost word for word: a variable is a
/// second way to turn it on, and one a container image or a systemd unit could
/// carry without anybody reading it. Turning this on means opening the file.
/// </para>
/// </remarks>
public class TheFleetsAllowancesAreAnAdminsPaneTests
{
    private static AppState Console(bool admin, bool configured) => new()
    {
        IsAdmin = admin,
        FleetAllowancesShown = configured,
        Allowances = new AllowanceList
        {
            Allowances =
            [
                new()
                {
                    Name = "kdee-max",
                    MeasuredAt = DateTimeOffset.UnixEpoch,
                    Runners = ["a-runner"],
                    Owners = ["somebody"],
                    Windows =
                    [
                        new()
                        {
                            Kind = AllowanceWindows.Session,
                            Since = DateTimeOffset.UnixEpoch,
                            InputTokens = 0,
                            OutputTokens = 22_000,
                            CacheReadTokens = 9_999_999,
                            CacheWriteTokens = 0,
                            Limit = 88_000,
                        },
                    ],
                },
            ],
        },
    };

    [Test]
    public async Task It_is_on_the_bar_only_for_an_administrator_who_turned_it_on()
    {
        await Assert.That(Tabs.Offered(Console(admin: true, configured: true)))
            .Contains(TabId.Allowances);

        await Assert.That(Tabs.Offered(Console(admin: false, configured: true)))
            .DoesNotContain(TabId.Allowances)
            .Because("the control plane would answer with this person's own allowances "
                   + "either way, so a pane labelled with the fleet would be showing them "
                   + "their own and calling it everyone's.");

        await Assert.That(Tabs.Offered(Console(admin: true, configured: false)))
            .DoesNotContain(TabId.Allowances)
            .Because("turning it on means opening the file, which is the deliberate "
                   + "amount of friction for showing one person what everybody else is "
                   + "spending.");
    }

    [Test]
    public async Task Its_key_does_nothing_until_both_gates_are_open()
    {
        await Assert.That(Keymap.Resolve(
            KeyStroke.Char('v'), KeymapContext.For(Console(admin: true, configured: true))))
            .IsEqualTo(Command.ToggleAllowances);

        await Assert.That(Keymap.Resolve(
            KeyStroke.Char('v'), KeymapContext.For(Console(admin: false, configured: true))))
            .IsNull()
            .Because("an advertised key that does nothing is how a person concludes the "
                   + "console is broken - and this one would be advertising somebody "
                   + "else's authority.");
    }

    [Test]
    public async Task The_pane_names_the_allowance_its_owners_and_what_is_left()
    {
        var pane = PaneText.ForTab(Console(admin: true, configured: true), TabId.Allowances);

        await Assert.That(pane).Contains("kdee-max", StringComparison.Ordinal);
        await Assert.That(pane).Contains("25%", StringComparison.Ordinal)
            .Because("22,000 of 88,000, with the ten million cache reads excluded - the "
                   + "same arithmetic every other surface uses, because two of them "
                   + "disagreeing about a share is how somebody decides on the wrong one.");
        await Assert.That(pane).Contains("somebody", StringComparison.Ordinal)
            .Because("whose it is, because the question an administrator has while looking "
                   + "at a nearly-spent allowance is who to ask about it.");
    }

    [Test]
    public async Task An_administrator_who_has_read_nothing_is_told_so_rather_than_shown_none()
    {
        var pane = PaneText.ForTab(
            new AppState { IsAdmin = true, FleetAllowancesShown = true }, TabId.Allowances);

        await Assert.That(pane).IsNotEmpty()
            .Because("nothing read and nothing reported are different facts, and a blank "
                   + "pane says the second while meaning the first.");
    }
}
