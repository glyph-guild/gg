using Gg.Client;
using Gg.Contracts;

namespace Gg.Client.Tests;

/// <summary>
/// `gg doctor` names the verb that fits the thing it is talking about, instead of
/// telling somebody to call a tracker a repository.
/// </summary>
/// <remarks>
/// <para>
/// <b>S64.4-03, and the product instructing the lie in its own words.</b> The
/// intent-hosts check's fix line reads:
/// </para>
/// <para>
/// <i>"Run `gg config set intent-hosts &lt;key=host|reference&gt;` for the tracker this
/// machine should read ... and `gg credential add --repo &lt;slug&gt;` for the reference
/// it names, where the slug is the locator without its 'local:' prefix."</i>
/// </para>
/// <para>
/// <b>That sentence is about a TRACKER and it says <c>--repo</c>.</b> Worse, it
/// explains how: take the locator and strip the prefix. Somebody following it
/// faithfully produces exactly what this fleet has — <c>local:jdx/jdnext</c>, a
/// repository-shaped locator for a hosted tracker — and the product told them
/// to. A diagnosis that hands over a wrong command is worse than one that says
/// nothing, because it is trusted.
/// </para>
/// <para>
/// <b>The example in the same sentence is the other half.</b>
/// <c>my-tracker=https://tracker.example/acme|local:acme/board</c> shows a
/// repository-shaped locator too, so the two halves of the advice agree with each
/// other and both disagree with what the thing is.
/// </para>
/// <para>
/// <b>Asserted over the sentence rather than the behaviour</b>, because the sentence
/// IS the behaviour here: `gg doctor` prints a fix and a person types it. There is
/// nothing else to check.
/// </para>
/// </remarks>
public class TheDoctorNamesTheRightVerbTests
{
    /// <summary>Every fix line the doctor can print, as source rather than as output.</summary>
    /// <remarks>
    /// <b>Read off the file, so a second place that says <c>--repo</c> about a tracker
    /// cannot hide behind a check this test does not trigger.</b> Running the doctor
    /// would need a tenant, a store and an intent-hosts setting per case; the claim is
    /// about what it is capable of saying.
    /// </remarks>
    private static async Task<string> TheDoctorsSource()
    {
        var at = new DirectoryInfo(AppContext.BaseDirectory);

        while (at is not null && !File.Exists(Path.Combine(at.FullName, "Gg.sln")))
        {
            at = at.Parent;
        }

        return await File.ReadAllTextAsync(Path.Combine(
            (at ?? throw new InvalidOperationException("Gg.sln not found")).FullName,
            "Gg.Client", "Doctor.cs"));
    }

    [Test]
    public async Task No_sentence_about_a_tracker_sends_somebody_to_the_repository_verb()
    {
        var source = await TheDoctorsSource();

        var offenders = source.Split('\n')
            .Select((line, at) => (Line: line, At: at + 1))
            .Where(l => l.Line.Contains("credential add --repo", StringComparison.Ordinal))
            .Where(l => l.Line.Contains("tracker", StringComparison.OrdinalIgnoreCase))
            .Select(l => $"Doctor.cs:{l.At}")
            .ToList();

        await Assert.That(offenders).IsEmpty()
            .Because("a fix line is a command a person types. One that names the repository verb "
                   + "for a tracker produces a repository-shaped locator for a tracker, which is "
                   + "what this fleet has and what the product told it to do.");
    }

    [Test]
    public async Task The_tracker_check_names_the_tracker_verb()
    {
        var source = await TheDoctorsSource();

        await Assert.That(source).Contains("credential add --tracker")
            .Because("the intent-hosts check is about a tracker, so the one command it hands over "
                   + "has to be the one that registers a tracker's credential.");
    }

    [Test]
    public async Task And_its_example_locator_is_shaped_like_a_tracker()
    {
        // THE SECOND HALF OF THE SAME ADVICE. The example
        // `my-tracker=...|local:acme/board` shows a repository-shaped locator, so
        // somebody copying the example lands in the same place as somebody following
        // the command - and the two agreeing with each other is why neither looked
        // wrong.
        var source = await TheDoctorsSource();

        var offenders = source.Split('\n')
            .Select((line, at) => (Line: line, At: at + 1))
            .Where(l => l.Line.Contains("my-tracker=", StringComparison.Ordinal)
                     || (l.Line.Contains("tracker.example", StringComparison.Ordinal)
                      && l.Line.Contains("local:", StringComparison.Ordinal)))
            // EITHER SPELLING COUNTS, and the interpolated one is the better code: a
            // literal "local:tracker/" in a sentence is a second place the segment is
            // named, and the one that goes stale. This test originally accepted only
            // the literal and failed on the fix that used the constant - a scan over
            // source cannot see through an interpolation, which is a limit worth
            // writing down rather than working around by hard-coding the string.
            .Where(l => !l.Line.Contains($"local:{CredentialLocator.TrackerSegment}/", StringComparison.Ordinal)
                     && !l.Line.Contains("local:{CredentialLocator.TrackerSegment}/", StringComparison.Ordinal))
            .Select(l => $"Doctor.cs:{l.At}: {l.Line.Trim()}")
            .ToList();

        await Assert.That(offenders).IsEmpty()
            .Because("an example is copied more often than a sentence is read, so an example "
                   + "showing the wrong shape is the more effective instruction of the two.");
    }

    [Test]
    public async Task A_locator_a_person_is_told_to_make_is_one_this_contract_produces()
    {
        // THE ADVICE HAS TO BE FOLLOWABLE, which is the thing a prose assertion cannot
        // see: a doctor could name `--tracker` and still describe a locator shape the
        // contract refuses. This checks the verb's own output.
        var locator = CredentialLocator.For(CredentialSubjects.Tracker, "board");

        await Assert.That(CredentialLocator.Validate(locator)).IsNull()
            .Because("somebody following the fix line has to end up with something gg accepts.");

        await Assert.That(CredentialLocator.SubjectOf(locator))
            .IsEqualTo(CredentialSubjects.Tracker)
            .Because("and with something that reads back as a tracker, or the next doctor run "
                   + "says the same thing again about the credential it just told them to make.");
    }

    [Test]
    public async Task The_repository_advice_is_unchanged_where_it_is_right()
    {
        // NOT A SWEEP. Most of the doctor's credential advice IS about repositories and
        // `--repo` is the correct verb there; a change that renamed every occurrence
        // would trade one wrong sentence for several.
        var source = await TheDoctorsSource();

        await Assert.That(source).Contains("credential add --repo")
            .Because("a repository's credential is still registered with --repo, and the sentences "
                   + "about repositories were never the problem.");
    }
}
