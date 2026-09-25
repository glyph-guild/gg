using Gg.Console;
using Gg.Local;

namespace Gg.Console.Tests;

/// <summary>
/// The console can be asked where its time went.
/// </summary>
/// <remarks>
/// <para>
/// <b>Because guessing ran out.</b> A console taking five seconds to show the
/// flights tab was measured from the outside as far as it could be: every
/// request to the control plane costs about 110ms, the doctor costs 1.25s, and
/// the tab's own reads are four list calls and one log per open flight. That
/// accounts for roughly two of the five seconds, and no amount of reading the
/// source found the rest - so the console has to say it itself.
/// </para>
/// <para>
/// <b>Off unless asked, and asked by an environment variable</b>, like
/// <c>GG_STATE_DUMP</c> beside it: a person with a slow console sets one
/// variable and hands back a file. Nothing is measured, formatted or written
/// when it is unset, because a diagnostic that costs something when nobody
/// wants it is a diagnostic people turn off and then cannot turn on.
/// </para>
/// <para>
/// <b>Written as it goes, not at the end.</b> The symptom being chased is a
/// console that stops responding, and a person chasing it kills the process -
/// so anything held in memory until exit is exactly the evidence that would be
/// lost. One short line appended per phase costs microseconds against the
/// milliseconds it is measuring.
/// </para>
/// <para>
/// <b>And it may never throw.</b> A full disk, a path nobody can write, a file
/// somebody deleted underneath it: a measurement that takes the console down
/// would be worse than the slowness it was added to explain.
/// </para>
/// </remarks>
public class TheConsoleCanSayWhereTimeWentTests
{
    private static (Timings Timings, List<string> Written) Recording()
    {
        var written = new List<string>();
        return (Timings.Writing(written.Add), written);
    }

    [Test]
    public async Task A_phase_says_its_name_and_how_long_it_took()
    {
        var (timings, written) = Recording();

        timings.Took("boot.round-one", TimeSpan.FromMilliseconds(412));

        await Assert.That(written.Count).IsEqualTo(1);
        await Assert.That(written[0]).Contains("boot.round-one");
        await Assert.That(written[0]).Contains("412");
    }

    [Test]
    public async Task And_how_many_reads_it_made_when_that_is_the_point()
    {
        // THE NUMBER THAT EXPLAINS THE DURATION. A phase taking 300ms because
        // it made three calls and one taking 300ms because it made twenty are
        // different findings with different fixes, and a duration alone cannot
        // tell them apart.
        var (timings, written) = Recording();

        timings.Took("refresh.Flights", TimeSpan.FromMilliseconds(441), reads: 23);

        await Assert.That(written[0]).Contains("23");
    }

    [Test]
    public async Task A_phase_with_nothing_to_count_says_no_count()
    {
        // NOT ZERO. A render makes no requests at all, and printing `reads=0'
        // beside it invites somebody to read a zero as a measurement rather
        // than as a column that does not apply.
        var (timings, written) = Recording();

        timings.Took("render.Flights", TimeSpan.FromMilliseconds(3));

        await Assert.That(written[0]).DoesNotContain("reads=");
    }

    [Test]
    public async Task Lines_arrive_in_the_order_they_were_recorded()
    {
        // THE WHOLE POINT IS A SEQUENCE. What somebody reads off this file is
        // which phase ran when; a set of durations in any order would not say
        // whether the doctor overlapped the reads or followed them.
        var (timings, written) = Recording();

        timings.Took("first", TimeSpan.FromMilliseconds(1));
        timings.Took("second", TimeSpan.FromMilliseconds(1));

        await Assert.That(written[0]).Contains("first");
        await Assert.That(written[1]).Contains("second");
    }

    [Test]
    public async Task Measuring_a_block_records_when_it_leaves()
    {
        // THE SHAPE EVERY CALL SITE USES, because the alternative is a
        // stopwatch started and stopped by hand at each one - and the one that
        // returns early is the one that would stop recording.
        var (timings, written) = Recording();

        using (timings.Measure("boot.logs", reads: 19))
        {
            await Assert.That(written).IsEmpty()
                .Because("a phase that has not finished has no duration to report.");
        }

        await Assert.That(written.Count).IsEqualTo(1);
        await Assert.That(written[0]).Contains("boot.logs");
        await Assert.That(written[0]).Contains("19");
    }

    [Test]
    public async Task Nothing_is_measured_or_written_when_nobody_asked()
    {
        // THE DEFAULT, AND IT HAS TO COST NOTHING. This runs on every boot of
        // every console, so `off' may not format a string, take a clock
        // reading or touch a file.
        var off = Timings.Off;

        off.Took("boot.round-one", TimeSpan.FromMilliseconds(412));

        using (off.Measure("boot.logs", reads: 19))
        {
        }

        await Assert.That(off.Asked).IsFalse()
            .Because("a call site asks this before doing work only a measurement needs.");
    }

    [Test]
    public async Task A_writer_that_fails_never_reaches_the_console()
    {
        // A FULL DISK MUST NOT END A SESSION. The console is already the thing
        // being complained about; taking it down to report on it would be the
        // worst outcome this change could have.
        var attempts = 0;
        var timings = Timings.Writing(_ =>
        {
            attempts++;
            throw new IOException("the disk is full");
        });

        timings.Took("boot.round-one", TimeSpan.FromMilliseconds(1));

        using (timings.Measure("boot.logs"))
        {
        }

        await Assert.That(attempts).IsEqualTo(2)
            .Because("both phases reached the writer and neither failure came back out - "
                   + "a count proves the calls happened where reaching the next line "
                   + "would only prove they did not throw.");
    }

    [Test]
    public async Task The_variable_decides_and_an_unset_one_means_off()
    {
        await Assert.That(Timings.For(null).Asked).IsFalse();
        await Assert.That(Timings.For("").Asked).IsFalse();
        await Assert.That(Timings.For("   ").Asked).IsFalse()
            .Because("a variable set to whitespace is somebody clearing it, not a path.");
    }
}
