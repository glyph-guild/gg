using Gg.Cli;
using Gg.Contracts;
using Gg.Local;

namespace Gg.Cli.Tests;

/// <summary>
/// A running runner says when it is offered configuration it cannot take.
/// </summary>
/// <remarks>
/// <para>
/// <b>Measured on the walk, 2026-09-22.</b> vmlinux002 was on 0.42.0 when
/// `dev-worker@v3` came into force carrying `intent-hosts` and `tracker-apis` -
/// two keys that build does not know. It sat on v2 for four minutes and wrote
/// NOTHING to its journal. The walk read that as a broken offer and went
/// looking at the control plane; the machine was right and simply silent.
/// </para>
/// <para>
/// <b>The sentence already existed and was being dropped.</b>
/// <c>Decide</c> returns a <c>Note</c> for an offer that could not be taken, and
/// its own remark says why: <i>"What earns a sentence is an offer that could not
/// be taken - nobody is here to notice it any other way."</i> The startup path
/// prints it. The running loop read only <c>Write</c> and returned, so the one
/// place where nobody IS here threw the sentence away.
/// </para>
/// <para>
/// <b>Once per offer, not once per beat.</b> A loop beats every few seconds and
/// the same refusal would otherwise fill a log for ever, which is the failure
/// mode that same remark warns about one line further on.
/// </para>
/// </remarks>
public class ARunningLoopSaysWhatItCouldNotTakeTests
{
    private const string Path = "/somewhere/config.json";

    private static OfferedConfiguration Offering(string version, string key, string value) =>
        new()
        {
            Version = version,
            OfferedAt = DateTimeOffset.UnixEpoch,
            Settings = [new OfferedSetting { Key = key, Value = value }],
        };

    private static Configuration Accepting() => new() { AcceptOffered = true };

    [Test]
    public async Task An_offer_this_build_cannot_take_is_said_once()
    {
        // A KEY FROM A NEWER CONTRACT, which is exactly how this arrives: the
        // fleet's document is written by a control plane that knows a key this
        // binary does not.
        var offer = Offering("profile:dev-worker@v3", "a-key-from-the-future", "whatever");

        var first = OfferedAtStartup.OnABeat(offer, Accepting(), Path, alreadySaid: null);

        await Assert.That(first.Say).IsNotNull()
            .Because("nobody is at a runner, so an offer it could not take reaches an operator "
                   + "here or nowhere - and four minutes of silence sent the walk looking at "
                   + "the wrong machine.");

        await Assert.That(first.Stop).IsFalse()
            .Because("there is nothing to restart into: the offer is one this build will not "
                   + "take however many times it starts.");

        var again = OfferedAtStartup.OnABeat(offer, Accepting(), Path, alreadySaid: first.Said);

        await Assert.That(again.Say).IsNull()
            .Because("a loop beats every few seconds, and the same refusal every beat is how a "
                   + "log stops being read.");
    }

    [Test]
    public async Task And_says_it_again_when_the_offer_changes()
    {
        var said = OfferedAtStartup
            .OnABeat(Offering("v3", "a-key-from-the-future", "x"), Accepting(), Path, null).Said;

        var next = OfferedAtStartup.OnABeat(
            Offering("v4", "a-key-from-the-future", "x"), Accepting(), Path, alreadySaid: said);

        await Assert.That(next.Say).IsNotNull()
            .Because("a new version is a new offer, and somebody who fixed the last one needs "
                   + "to know this one is still refused.");
    }

    [Test]
    public async Task An_offer_it_can_take_still_stops_so_the_next_start_takes_it()
    {
        // THE BEHAVIOUR THAT ALREADY WORKED, asserted so this change cannot
        // quietly trade one silence for another.
        var take = OfferedAtStartup.OnABeat(
            Offering("v3", OfferableKeys.StunServers, "stun:relay.invalid:3478"),
            Accepting(), Path, alreadySaid: null);

        await Assert.That(take.Stop).IsTrue();
        await Assert.That(take.Say).IsNotNull()
            .Because("stopping without saying why is a runner that appears to have crashed.");
    }

    [Test]
    public async Task And_an_offer_already_in_force_says_nothing_at_all()
    {
        // THE STEADY STATE IS RECORDED, NOT INFERRED. `AcceptedOffer` is what
        // makes this beat quiet - holding the same VALUE at an unrecorded
        // version is a version this file has not taken, and the runner is
        // right to stop and let the next start record it.
        var steady = OfferedAtStartup.OnABeat(
            Offering("v3", OfferableKeys.StunServers, "stun:relay.invalid:3478"),
            new Configuration
            {
                AcceptOffered = true,
                StunServers = "stun:relay.invalid:3478",
                AcceptedOffer = "v3",
            },
            Path,
            alreadySaid: null);

        await Assert.That(steady.Say).IsNull();
        await Assert.That(steady.Stop).IsFalse()
            .Because("every beat after the first meets an offer already in force, and a line "
                   + "about it each time is a line in every machine's log for ever.");
    }

    /// <summary>
    /// Every part of the beat is read by the root, which is where it was lost.
    /// </summary>
    /// <remarks>
    /// <b>The defect was never in a decision - it was in a lambda ignoring one.</b>
    /// The old callback called <c>Decide</c>, read <c>Write</c>, and returned;
    /// the <c>Note</c> beside it was thrown away unread, and `Program.cs` is
    /// the one file in this repository no test opens. Reflected over the record
    /// rather than written as a list of three names, so a part added later is
    /// one the root must answer for too.
    /// </remarks>
    [Test]
    public async Task And_the_root_reads_every_part_of_it()
    {
        var root = ProgramText();

        var marker = root.IndexOf("offered: carried =>", StringComparison.Ordinal);
        await Assert.That(marker).IsGreaterThan(-1)
            .Because("the loop's offer callback is what this asserts about, and a rename here "
                   + "leaves the assertion measuring nothing.");

        var end = root.IndexOf("\n            },", marker, StringComparison.Ordinal);
        var callback = root[marker..end];

        await Assert.That(callback).Contains("OfferedAtStartup.OnABeat")
            .Because("a decision spelled out in a composition root is a decision no test can "
                   + "reach, which is how this one was wrong in production and green here.");

        foreach (var part in typeof(OfferedAtStartup.Beat).GetProperties())
        {
            await Assert.That(callback).Contains(part.Name)
                .Because($"'{part.Name}' is part of what the beat decided, and a part the root "
                       + "does not read is a decision made and discarded - exactly what "
                       + "happened to the sentence this file exists for.");
        }
    }

    private static string ProgramText()
    {
        var here = new DirectoryInfo(AppContext.BaseDirectory);
        while (here is not null
               && !Directory.Exists(System.IO.Path.Combine(here.FullName, "Gg.Cli")))
        {
            here = here.Parent;
        }

        return File.ReadAllText(System.IO.Path.Combine(here!.FullName, "Gg.Cli", "Program.cs"));
    }
}
