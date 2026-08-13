using Gg.Client;
using Gg.Contracts;

namespace Gg.Client.Tests;

/// <summary>
/// A return file that cannot be trusted leaves the flight untouched, and says so.
/// </summary>
/// <remarks>
/// <para>
/// The console handed the terminal to a person for minutes and has no idea what
/// happened in between. <b>Optimism here produces a client that silently applies
/// a garbled decision</b>, so every failure ends the same way: nothing applied, a
/// diagnosis, and a flight a person can resolve by hand.
/// </para>
/// <para>
/// <b>Three cases, and the third is the good one.</b> Garbage and truncation
/// fail to parse, which any implementation catches. A well-formed file naming a
/// DIFFERENT flight parses cleanly and is the one a plausible implementation
/// applies - and a decision applied to the wrong flight is worse than a decision
/// lost.
/// </para>
/// </remarks>
public class ReturnSchemaTests
{
    private const string Taken = "019ff8aa-1111-7000-8000-000000000001";

    private const string Other = "019ff8aa-2222-7000-8000-000000000002";

    private static string Scratch()
    {
        var directory = Path.Combine(
            Path.GetTempPath(), "gg-return-" + Guid.NewGuid().ToString("N")[..8]);

        Directory.CreateDirectory(directory);

        return directory;
    }

    private static (TakeoverReturn? Decision, string? Diagnosis) ReadWritten(string content)
    {
        var directory = Scratch();
        var path = TakeoverReturnReader.PathIn(directory);

        File.WriteAllText(path, content);

        try
        {
            return TakeoverReturnReader.Read(path, Taken);
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    // ---- the case that works, so the absences below mean something ----

    [Test]
    public async Task A_well_formed_decision_for_this_flight_is_read()
    {
        var (decision, diagnosis) = ReadWritten(
            $$"""{"flightId":"{{Taken}}","outcome":"completed","note":"fixed the scope violation"}""");

        await Assert.That(diagnosis).IsNull();
        await Assert.That(decision!.Outcome).IsEqualTo(TakeoverOutcomes.Completed);
        await Assert.That(decision.Note).IsEqualTo("fixed the scope violation");
    }

    // ---- the three that must not ----

    [Test]
    public async Task Garbage_leaves_the_flight_untouched_and_says_so()
    {
        var (decision, diagnosis) = ReadWritten("this is not json, it is a shell history");

        await Assert.That(decision).IsNull();
        await Assert.That(diagnosis).IsNotNull();
        await Assert.That(diagnosis!).Contains("untouched")
            .Because("what a person needs to know is that nothing was applied.");
    }

    [Test]
    public async Task A_file_truncated_mid_write_leaves_the_flight_untouched_and_says_so()
    {
        // A person who was killed, or a disk that filled, halfway through
        // writing. It is JSON right up until it is not.
        var (decision, diagnosis) = ReadWritten(
            $$"""{"flightId":"{{Taken}}","outcome":"comp""");

        await Assert.That(decision).IsNull();
        await Assert.That(diagnosis!).Contains("untouched");
    }

    [Test]
    public async Task A_valid_file_naming_a_different_flight_is_refused_by_name()
    {
        // THE ONE THAT PARSES. A file left over from a previous takeover is
        // well-formed, describes a real decision, and belongs to another flight.
        var (decision, diagnosis) = ReadWritten(
            $$"""{"flightId":"{{Other}}","outcome":"completed","note":"done"}""");

        await Assert.That(decision).IsNull()
            .Because("a decision on the wrong flight is worse than a decision lost.");

        await Assert.That(diagnosis!).Contains(Other);
        await Assert.That(diagnosis!).Contains(Taken)
            .Because("both are named, or a person cannot tell which file they are looking at.");
    }

    // ---- the shapes around the edges ----

    [Test]
    public async Task No_file_at_all_is_not_a_failure()
    {
        // Somebody who took a flight and wrote nothing is the ordinary end of an
        // abandoned takeover. Reporting it as a broken file would send them
        // looking for a file that was never meant to exist.
        var directory = Scratch();

        try
        {
            var (decision, diagnosis) = TakeoverReturnReader.Read(
                TakeoverReturnReader.PathIn(directory), Taken);

            await Assert.That(decision).IsNull();
            await Assert.That(diagnosis).IsNull()
                .Because("nothing went wrong. Nothing happened.");
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [Test]
    public async Task An_outcome_this_version_does_not_know_is_refused_by_name()
    {
        // Article XI on the vocabulary. A newer console writing an outcome this
        // one has never heard of must not have it applied as something else.
        var (decision, diagnosis) = ReadWritten(
            $$"""{"flightId":"{{Taken}}","outcome":"probably-fine"}""");

        await Assert.That(decision).IsNull();
        await Assert.That(diagnosis!).Contains("probably-fine");
        await Assert.That(diagnosis!).Contains("handing-back")
            .Because("the diagnosis lists what this version does understand.");
    }

    [Test]
    public async Task A_file_far_too_large_to_be_a_decision_is_not_read_into_memory()
    {
        // A decision is a few hundred bytes. Anything enormous is a file that is
        // not this one, and reading it to find that out is how a client is made
        // to fall over by a file it did not write.
        var directory = Scratch();
        var path = TakeoverReturnReader.PathIn(directory);

        try
        {
            File.WriteAllText(path, new string('x', TakeoverReturnReader.MaxBytes + 1));

            var (decision, diagnosis) = TakeoverReturnReader.Read(path, Taken);

            await Assert.That(decision).IsNull();
            await Assert.That(diagnosis!).Contains("bytes");
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [Test]
    public async Task Handing_back_is_a_name_this_version_knows_and_does_not_serve()
    {
        // Declared in the vocabulary before anything handles it, so a console
        // that writes it gets a clean read here rather than a refusal - and step
        // 7 is where something acts on it.
        var (decision, diagnosis) = ReadWritten(
            $$"""{"flightId":"{{Taken}}","outcome":"handing-back"}""");

        await Assert.That(diagnosis).IsNull();
        await Assert.That(decision!.Outcome).IsEqualTo(TakeoverOutcomes.HandingBack);
    }
}
