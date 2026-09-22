using Gg.Contracts;

namespace Gg.Client.Tests;

/// <summary>
/// <c>gg doctor</c> answering "can this machine read the ticket a flight is
/// about", which it had no line for at all.
/// </summary>
/// <remarks>
/// <para>
/// <b>Walk finding, slice forty-seven.</b> The doctor reports the forge it
/// clones from and the destination it proposes to, and says nothing about
/// either tracker - so a machine configured to read work items has no check
/// confirming it, and the only way to find out was to fly something.
/// </para>
/// <para>
/// <b>Both halves fail silently today, in different directions.</b> An entry
/// `tracker-apis` cannot parse is SKIPPED - deliberately, so a newer contract's
/// spelling cannot stop an older runner - and an entry `intent-hosts` cannot
/// parse THROWS where the runner is composed, which on a fleet host is in front
/// of a systemd unit. Neither reaches a person. A doctor is where a person
/// looks.
/// </para>
/// <para>
/// <b>And a declared tracker with no credential here is the likely mistake.</b>
/// Declaring the tracker and adding the secret are two commands on two
/// machines; the first can be offered by a profile and the second never can.
/// </para>
/// </remarks>
public class TheDoctorSaysWhichTrackersTests
{
    private const string Backlog = "https://forge.example/acme";

    private static MachineRole Declaring(params DeclaredTracker[] trackers) =>
        MachineRole.None with { Trackers = trackers };

    /// <summary>Holds exactly the locators named, and nothing else.</summary>
    private static DoctorCheck Check(MachineRole role, params string[] held) =>
        Doctor.TrackerCheck(
            role, locator => !held.Contains(locator, StringComparer.Ordinal));

    [Test]
    public async Task A_machine_that_reads_a_tracker_says_which_one()
    {
        var check = Check(
            Declaring(new DeclaredTracker
            {
                Key = "backlog", Host = Backlog, Locator = "local:forge.example/acme",
            }),
            "local:forge.example/acme");

        await Assert.That(check.Passed).IsTrue();
        await Assert.That(check.Detail).Contains("backlog");
        await Assert.That(check.Detail).Contains(Backlog)
            .Because("confirming a machine is configured means naming what it is configured "
                   + "with - 'a tracker is declared' is a sentence somebody has to go and "
                   + "check.");
    }

    [Test]
    public async Task And_a_tracker_whose_credential_is_not_on_this_machine_is_named()
    {
        // THE LIKELY MISTAKE, and the one nothing reports: a profile can offer
        // the declaration, and no profile can ever carry the secret.
        var check = Check(
            Declaring(new DeclaredTracker
            {
                Key = "backlog", Host = Backlog, Locator = "local:forge.example/acme",
            }));

        await Assert.That(check.Passed).IsFalse();
        await Assert.That(check.Detail).Contains("local:forge.example/acme")
            .Because("naming the locator ends the search, the way the credentials check "
                   + "already names its own.");
        await Assert.That(check.Fixable).IsTrue();
        await Assert.That(check.Fix).IsNotNull();
        await Assert.That(check.Blocking).IsFalse()
            .Because("plenty of machines take flights that never name a work item, and a "
                   + "doctor that exits non-zero on a laptop is one nobody runs.");
    }

    [Test]
    public async Task A_declaration_nothing_could_parse_reaches_a_person_here()
    {
        var check = Check(MachineRole.None with
        {
            TrackerProblems =
                ["'backlog' in GG_TRACKER_APIS names no host, so nothing was declared for it."],
        });

        await Assert.That(check.Passed).IsFalse();
        await Assert.That(check.Detail).Contains("GG_TRACKER_APIS")
            .Because("the parser's own sentence is the one that says which line is wrong, and "
                   + "rewriting it here would be a second answer to what an operator typed.");
        await Assert.That(check.Fixable).IsTrue();
    }

    [Test]
    public async Task The_side_that_writes_is_told_apart_from_the_side_that_reads()
    {
        // TWO PERMISSIONS ON TWO CREDENTIALS, which is Configuration's own
        // sentence: a machine that reads a backlog does not thereby write to
        // one. A line that merged them would report a triage host as ready
        // when it can only read.
        var check = Check(
            Declaring(
                new DeclaredTracker { Key = "backlog", Host = Backlog, Locator = "local:a" },
                new DeclaredTracker
                {
                    Key = "board", Host = "https://forge.example/board", Locator = "local:b",
                    Writes = true,
                }),
            "local:a", "local:b");

        await Assert.That(check.Passed).IsTrue();
        await Assert.That(check.Detail).Contains("reads");
        await Assert.That(check.Detail).Contains("writes");
    }

    [Test]
    public async Task And_a_machine_that_declares_none_still_gets_the_line()
    {
        // ORDINARY, AND STILL SAID. Absence is the normal state - a link flight
        // names no tracker - so this passes; what it must not do is vanish,
        // because "no line" is what sent the walk looking.
        var check = Check(MachineRole.None);

        await Assert.That(check.Passed).IsTrue();
        await Assert.That(check.Blocking).IsFalse();
        await Assert.That(check.Detail).IsNotEmpty();
    }

    [Test]
    public async Task And_which_credential_each_one_will_use()
    {
        // FOUND BY WALKING IT. On the machine this was written on, the line
        // named the tracker's host and passed - and that entry names no
        // credential at all, so the reader gg spawns for it reaches the
        // tracker as nobody. A locator is optional because a
        // tracker may need none; which of the two this is, is a fact somebody
        // should not have to reconstruct from a 401 inside an agent.
        var check = Check(
            Declaring(
                new DeclaredTracker { Key = "open", Host = "https://forge.example/open" },
                new DeclaredTracker
                {
                    Key = "backlog", Host = Backlog, Locator = "local:forge.example/acme",
                }),
            "local:forge.example/acme");

        await Assert.That(check.Passed).IsTrue()
            .Because("naming no credential is not a fault - it is what a tracker needing "
                   + "none looks like.");

        await Assert.That(check.Detail).Contains("local:forge.example/acme");
        await Assert.That(check.Detail).Contains("no credential named");
    }

    [Test]
    public async Task And_a_locator_that_is_not_one_says_so_instead_of_looking_unheld()
    {
        // THE REMEDY IS THE TELL. A string the contract refuses is not a
        // credential somebody forgot to add, and reporting it as one prints
        // `gg credential add --repo TOKEN=local:acme/board` - advice that
        // cannot be followed, about a mistake that is one character of
        // punctuation away from being named exactly.
        var check = Check(
            Declaring(new DeclaredTracker
            {
                Key = "my-tracker", Host = Backlog, Locator = "TOKEN=local:acme/board",
            }));

        await Assert.That(check.Passed).IsFalse();
        await Assert.That(check.Detail).Contains("TOKEN=local:acme/board");
        await Assert.That(check.Detail).DoesNotContain("does not hold")
            .Because("a machine cannot hold something that could never be a credential name, "
                   + "and saying it does not sends somebody to the credential store.");
        await Assert.That(check.Fix!).DoesNotContain("credential add")
            .Because("the line to correct is the tracker declaration, not the store.");
    }
}
