using System.Diagnostics;
using Gg.Console;
using Gg.Local;

namespace Gg.Console.Tests;

/// <summary>
/// The reader is a child process, and somebody has to own it.
/// </summary>
/// <remarks>
/// <para>
/// <b>S29.3-07 and S29.3-08, and both are about what happens when nothing goes
/// right.</b> A reader that never answers is a console that never redraws, and
/// a stdio child nobody reaps is a credential-holding process that outlives its
/// reason. Neither shows up in a conversation driven over two streams, which is
/// exactly why that half was tested without one.
/// </para>
/// <para>
/// <b>Real processes here, on purpose.</b> A fake that pretends to hang proves
/// the timeout arithmetic and nothing about whether the child dies. These spawn
/// <c>sleep</c>, which exists on both platforms this suite runs on, and then go
/// and look at whether the pid is gone - the same disposition the runner's kill
/// tests already take.
/// </para>
/// <para>
/// <b>Owned by the loop, never by a session.</b> <c>LiveTails</c> is the
/// precedent and states the rule: <i>"this object outlives the session, is
/// owned by whoever composed the console, and the session merely calls it"</i>.
/// A reader is that, plus a process handle, plus a credential's worth of reason
/// to be certain it stops.
/// </para>
/// </remarks>
public class OwningAReaderProcessTests
{
    private static IntentReader Sleeps(int seconds) =>
        new("a-tracker", "/bin/sh", ["-c", $"sleep {seconds}"]);

    private static IntentReader Exits() =>
        new("a-tracker", "/bin/sh", ["-c", "exit 0"]);

    [Test]
    public async Task A_reader_that_never_answers_is_abandoned_at_the_deadline()
    {
        await using var reader = new SpawnedReader(Sleeps(30), TimeSpan.FromMilliseconds(400));

        var clock = Stopwatch.StartNew();
        var outcome = await reader.BrowseAsync(cursor: null, limit: 50);
        clock.Stop();

        var waited = await Assert.That(outcome).IsTypeOf<BrowseOutcome.Silent>();
        await Assert.That(waited!.Why).Contains("a-tracker")
            .Because("a tenant may configure more than one reader.");
        await Assert.That(waited.Why).Contains("400")
            .Because("how long it waited is what says whether the deadline is the problem.");
        await Assert.That(clock.Elapsed).IsLessThan(TimeSpan.FromSeconds(10))
            .Because("the deadline is the point; a browse that waits for the child is no "
                   + "deadline at all.");
    }

    [Test]
    public async Task Giving_up_kills_the_child_rather_than_leaving_it()
    {
        // A STDIO CHILD NOBODY REAPS HOLDS A CREDENTIAL'S WORTH OF REASON TO
        // STOP. Abandoning the read and leaving the process is the shape of
        // leak that survives the console it was started for.
        var reader = new SpawnedReader(Sleeps(30), TimeSpan.FromMilliseconds(400));

        await reader.BrowseAsync(cursor: null, limit: 50);
        var pid = reader.ProcessId;
        await reader.DisposeAsync();

        await Assert.That(pid).IsNotNull();
        await Assert.That(Alive(pid!.Value)).IsFalse()
            .Because("the process the console started is the console's to stop.");
    }

    [Test]
    public async Task Disposing_stops_a_reader_that_was_working_perfectly_well()
    {
        // Ctrl-C on a healthy console is the ordinary case, not the failure
        // case, and it is the one most likely to leave something behind.
        var reader = new SpawnedReader(Sleeps(30), TimeSpan.FromSeconds(5));
        _ = reader.StartAsync();
        var pid = reader.ProcessId;

        await reader.DisposeAsync();

        await Assert.That(pid).IsNotNull();
        await Assert.That(Alive(pid!.Value)).IsFalse();
    }

    [Test]
    public async Task A_child_that_exits_at_once_is_reported_as_silent_not_as_empty()
    {
        // The commonest real failure: a command that is not there, or one that
        // dies on a missing variable. It must not read as a tracker with no
        // work in it.
        await using var reader = new SpawnedReader(Exits(), TimeSpan.FromSeconds(5));

        var outcome = await reader.BrowseAsync(cursor: null, limit: 50);

        await Assert.That(outcome).IsTypeOf<BrowseOutcome.Silent>();
    }

    [Test]
    public async Task A_command_that_does_not_exist_says_so_rather_than_throwing()
    {
        // ARTICLE XI. An operator's typo in GG_INTENT_READERS reaches here as a
        // Win32Exception, and a console that propagated it would die on a
        // configuration mistake.
        await using var reader = new SpawnedReader(
            new IntentReader("a-tracker", "/nonexistent/reader", []),
            TimeSpan.FromSeconds(5));

        var outcome = await reader.BrowseAsync(cursor: null, limit: 50);

        var silent = await Assert.That(outcome).IsTypeOf<BrowseOutcome.Silent>();
        await Assert.That(silent!.Why).Contains("/nonexistent/reader")
            .Because("the thing to fix is the command, so the command is what it names.");
    }

    [Test]
    public async Task The_readers_are_held_one_per_provider_and_all_are_stopped_together()
    {
        // LiveTails' shape: a collaborator the loop owns, so a session retains
        // nothing and one browse per tracker does not mean one process per
        // keystroke.
        var readers = new ReaderSessions(
            [Sleeps(30) with { Key = "one" }, Sleeps(30) with { Key = "two" }],
            TimeSpan.FromSeconds(5));

        var first = readers.For("one");
        var again = readers.For("one");
        var other = readers.For("two");

        await Assert.That(first).IsSameReferenceAs(again)
            .Because("asking twice must not start a second process.");
        await Assert.That(first).IsNotSameReferenceAs(other);
        await Assert.That(readers.For("unconfigured")).IsNull();

        _ = first!.StartAsync();
        _ = other!.StartAsync();
        var pids = new[] { first.ProcessId, other.ProcessId };

        await readers.DisposeAsync();

        foreach (var pid in pids)
        {
            await Assert.That(pid).IsNotNull();
            await Assert.That(Alive(pid!.Value)).IsFalse();
        }
    }

    /// <summary>Whether a pid is still a running process on this machine.</summary>
    private static bool Alive(int pid)
    {
        try
        {
            return !Process.GetProcessById(pid).HasExited;
        }
        catch (ArgumentException)
        {
            // Gone entirely, which is the answer this asks for.
            return false;
        }
    }
}
