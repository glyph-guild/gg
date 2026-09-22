using Gg.Runner.Intent;

namespace Gg.Runner.Tests;

/// <summary>
/// The write side's declaration, parsed once and read twice.
/// </summary>
/// <remarks>
/// <para>
/// <b>Because the doctor now reports it</b> (slice forty-seven's walk finding:
/// gg doctor has no line for either tracker), and a second parser for a line an
/// operator wrote is a second answer to what they typed - which is
/// <c>ServedTrackers</c>' own reasoning for the read side, arrived at for the
/// same reason.
/// </para>
/// <para>
/// <b>An entry it cannot use is returned rather than dropped.</b> Skipping is
/// still what the runner does - refusing to start over a spelling a newer
/// contract wrote is the one response that cannot be corrected - but a skip
/// nothing can report is how a machine ends up declared for a tracker it never
/// built a sink for, silently.
/// </para>
/// </remarks>
public class ADeclaredTrackerIsParsedOnceTests
{
    [Test]
    public async Task What_the_sink_is_built_from_is_what_the_doctor_reads()
    {
        var declared = TrackerConfiguration.Declared(
            "backlog=https://forge.example/acme|local:acme/board");

        await Assert.That(declared).HasCount(1);
        await Assert.That(declared[0].Id).IsEqualTo("backlog");
        await Assert.That(declared[0].Host).IsEqualTo("https://forge.example/acme");
        await Assert.That(declared[0].Locator).IsEqualTo("local:acme/board");
        await Assert.That(declared[0].Problem).IsNull();
    }

    [Test]
    public async Task And_a_derived_locator_is_the_one_the_sink_would_have_asked_for()
    {
        // NOT "no credential". An entry with no bar still resolves one - from
        // the host - and a doctor reporting it as needing none would call a
        // machine ready that cannot write.
        var declared = TrackerConfiguration.Declared("backlog=https://Forge.Example/Acme");

        await Assert.That(declared[0].Locator).IsEqualTo("local:forge.example/acme");
    }

    [Test]
    public async Task An_entry_it_cannot_use_comes_back_as_a_sentence()
    {
        var declared = TrackerConfiguration.Declared("backlog=,board=https://forge.example/b");

        await Assert.That(declared).HasCount(2);

        var refused = declared.Single(d => d.Problem is not null);

        await Assert.That(refused.Entry).IsEqualTo("backlog=");
        await Assert.That(refused.Problem!).Contains(TrackerConfiguration.ApisVariable)
            .Because("the sentence has to name the variable to send somebody to the right "
                   + "line: a machine reads its declaration from a file, a unit, or a profile "
                   + "somebody applied on another continent.");
    }

    [Test]
    public async Task And_the_runner_still_skips_it_rather_than_refusing_to_start()
    {
        // THE BEHAVIOUR THIS MUST NOT CHANGE (rule 2). Reporting the skip is
        // the whole change; skipping is what keeps an older build running when
        // a newer contract writes a spelling it does not know.
        var sinks = TrackerConfiguration.FromEnvironment(
            host => new HttpClient { BaseAddress = new Uri(host) },
            "backlog=,board=https://forge.example/b",
            _ => "a-secret");

        await Assert.That(sinks.ContainsKey("board")).IsTrue();
        await Assert.That(sinks.ContainsKey("backlog")).IsFalse();
    }
}
