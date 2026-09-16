using System.Diagnostics;
using Gg.Console;

namespace Gg.Console.Tests;

/// <summary>
/// The help page opens at once, because the catalogue behind it is built once
/// and out of a set of shapes that does not double every time a flag is added.
/// </summary>
/// <remarks>
/// <para>
/// <b>REPORTED FROM USE: "it takes a very long time to open the help modal".</b>
/// Ten seconds, measured - and three times over, because
/// <c>HelpTree.Keys</c>, <c>RunnerDetails</c> and <c>PaneText</c> each ask for
/// the catalogue and each one rebuilt it.
/// </para>
/// <para>
/// <b>It was a cross product over every flag the keymap has.</b> Eight tabs
/// crossed with sixteen booleans is 524,288 contexts per mode, and twenty-four
/// modes is twelve and a half million - each one allocating its own array of
/// bindings, to find one hundred and thirty-seven distinct entries. Every flag
/// added since doubled it, and three were added this month without anybody
/// noticing what it cost.
/// </para>
/// <para>
/// <b>What the product was FOR is still done.</b> A key bound only in one shape
/// has to reach the page or it is a key nobody can discover - the thing this
/// catalogue exists to prevent - so the shapes still cover every flag, and
/// <c>HelpNamesEveryKeyTests</c> still walks the exhaustive product and asserts
/// the catalogue holds everything it can produce. The proof stays exhaustive;
/// the thing a person waits for does not.
/// </para>
/// </remarks>
public class TheHelpPageOpensAtOnceTests
{
    [Test]
    public async Task It_is_built_once_and_then_held()
    {
        // THE CHEAPEST FIX AND THE ONE THAT WAS ALREADY WRITTEN DOWN:
        // ConsoleScreen says "the keys themselves never change at runtime:
        // Keymap.Catalogue() is static". It was static in the sense of being a
        // pure function and rebuilt on every call by three separate readers.
        var first = Keymap.Catalogue();
        var second = Keymap.Catalogue();

        await Assert.That(ReferenceEquals(first, second)).IsTrue()
            .Because("nothing about it can change while the console is running, so the second "
                   + "caller is asking a question that was already answered.");
    }

    [Test]
    public async Task And_building_it_is_not_something_a_person_waits_for()
    {
        // A WALL CLOCK, DELIBERATELY, because what was wrong was a wait. The
        // budget is two orders of magnitude above what this now costs and two
        // below what it cost when it was reported, so it fails on a return to
        // the cross product and on nothing else.
        var watch = Stopwatch.StartNew();
        var fresh = Keymap.Catalogue();
        watch.Stop();

        await Assert.That(fresh).IsNotEmpty();
        await Assert.That(watch.ElapsedMilliseconds).IsLessThan(1_000)
            .Because($"this took ten seconds when it was reported, three times per open. "
                   + $"Took {watch.ElapsedMilliseconds}ms.");
    }

    [Test]
    public async Task The_page_a_person_opens_is_built_from_it_and_is_just_as_quick()
    {
        var watch = Stopwatch.StartNew();
        var groups = HelpTree.Keys();
        watch.Stop();

        await Assert.That(groups).IsNotEmpty();
        await Assert.That(watch.ElapsedMilliseconds).IsLessThan(1_000)
            .Because($"this is the call the keypress makes. Took {watch.ElapsedMilliseconds}ms.");
    }

    [Test]
    public async Task Every_mode_that_binds_anything_still_reaches_the_page()
    {
        // WHAT THE SHAPES ARE FOR, asserted at the coarsest level here and
        // exhaustively in HelpNamesEveryKeyTests: a mode that binds a key and
        // teaches none of them is a mode nobody can look up.
        foreach (var mode in Enum.GetValues<UiMode>())
        {
            var binds = Keymap.Bindings(new KeymapContext(mode))
                .Any(b => !b.Untaught);

            if (!binds)
            {
                continue;
            }

            await Assert.That(Keymap.Catalogue().Any(e => e.Mode == mode)).IsTrue()
                .Because($"{mode} binds something a person is meant to learn.");
        }
    }
}
