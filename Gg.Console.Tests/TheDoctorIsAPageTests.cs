using Gg.Client;
using Gg.Console;

namespace Gg.Console.Tests;

/// <summary>
/// What the console can say about its own health, without being asked twice.
/// </summary>
/// <remarks>
/// <para>
/// <b>This reverses a written decision, so it says why.</b> The parity list
/// held <c>gg doctor</c> "deliberately outside: it is the verb a person
/// reaches for when the console looks broken, which makes inside the console
/// the worst place to run it from." The real constraint is sharper than that
/// sentence and it is structural: the doctor PINGS the control plane, and a UI
/// session may not make a network call.
/// </para>
/// <para>
/// <b>Which is also the way round it.</b> The console's BOOT already makes six
/// network reads, outside any session, each on its own failure. A doctor page
/// filled there is a report rather than a remedy — it says what was true when
/// the console opened, and `r' asks again. Running it is still the command
/// line's.
/// </para>
/// <para>
/// <b>And the versions belong on it.</b> Three numbers decide whether anything
/// else on the page can be believed: the binary, the protocol it speaks, and
/// the fact vocabulary it evaluates against. A runner judging facts against a
/// vocabulary the control plane has moved past gives a silently wrong answer,
/// which is exactly the failure a health page exists to surface.
/// </para>
/// </remarks>
public class TheDoctorIsAPageTests
{
    private static DoctorReport Report(params DoctorCheck[] checks) =>
        new() { Checks = checks };

    private static DoctorCheck Check(
        string name, bool passed, bool blocking = false, string? fix = null) => new()
        {
            Name = name,
            Passed = passed,
            Detail = $"{name} detail",
            Blocking = blocking,
            Fixable = fix is not null,
            Fix = fix,
        };

    [Test]
    public async Task The_doctor_is_one_of_the_pages_the_bar_offers()
    {
        await Assert.That(HelpPages.All).Contains(HelpPage.Doctor);
        await Assert.That(HelpPages.Title(HelpPage.Doctor)).IsNotEmpty();
    }

    [Test]
    public async Task It_says_what_this_binary_is_before_anything_it_found()
    {
        var text = PaneText.HelpDoctorText(new AppState
        {
            Doctor = Report(Check("control plane", passed: true)),
        });

        await Assert.That(text).Contains(GgVersions.Binary, StringComparison.Ordinal);
        await Assert.That(text).Contains(
            GgVersions.FactVocabulary, StringComparison.Ordinal)
            .Because("a runner judging facts against a vocabulary the control plane has "
                   + "moved past gives a silently wrong answer, and this is the page where "
                   + "that is visible.");

        await Assert.That(text.IndexOf(GgVersions.Binary, StringComparison.Ordinal))
            .IsLessThan(text.IndexOf("control plane", StringComparison.Ordinal))
            .Because("what this gg IS comes before what it found, because every finding "
                   + "below is a finding BY that binary.");
    }

    [Test]
    public async Task A_failing_check_says_what_to_do_where_there_is_something()
    {
        var text = PaneText.HelpDoctorText(new AppState
        {
            Doctor = Report(Check("session", passed: false, fix: "run gg login")),
        });

        await Assert.That(text).Contains("run gg login", StringComparison.Ordinal)
            .Because("a health page that reports a problem and withholds the remedy it was "
                   + "handed is a page that makes somebody go and run the verb anyway.");
    }

    [Test]
    public async Task A_console_that_has_not_read_one_says_so_rather_than_looking_healthy()
    {
        var text = PaneText.HelpDoctorText(new AppState());

        await Assert.That(text).IsNotEmpty();
        await Assert.That(text).DoesNotContain("passed", StringComparison.OrdinalIgnoreCase)
            .Because("no report and a clean bill of health are different facts, and the "
                   + "empty one must never read as the good one - the rule the Settings "
                   + "page already follows for a console nobody told.");
    }

    [Test]
    public async Task A_blocking_failure_is_marked_apart_from_an_advisory_one()
    {
        var text = PaneText.HelpDoctorText(new AppState
        {
            Doctor = Report(
                Check("credentials", passed: false, blocking: false),
                Check("control plane", passed: false, blocking: true)),
        });

        await Assert.That(text).Contains("blocking", StringComparison.OrdinalIgnoreCase)
            .Because("one of these stops gg working and the other does not, and a page "
                   + "that listed them alike would make a person chase the wrong one.");
    }
}
