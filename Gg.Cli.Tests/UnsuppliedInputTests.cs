namespace Gg.Cli.Tests;

/// <summary>
/// An input no production caller ever supplies is a feature nobody has.
/// </summary>
/// <remarks>
/// <para>
/// <b>The instance that prompted this had a configured machine behind it.</b>
/// <c>ConsoleLoop</c> declared <c>IWorkBrowser? browser = null</c> and nothing
/// in <c>Gg.Cli</c> ever passed one, so <c>ConfiguredWorkBrowser</c> was
/// constructed nowhere outside its own declaration and <c>ReaderSessions</c>
/// only in a test. On a host with <c>GG_INTENT_HOSTS</c> declared correctly, a
/// credential in the store and <c>gg runner tools</c> answering a JSON-RPC
/// handshake, the browse pane still said <i>"No tracker is configured to
/// browse"</i> — and it was right, because it had no browser.
/// </para>
/// <para>
/// <b>No caller count would have caught it.</b> Every optional parameter has
/// production callers; that is what makes it optional. <i>Does anything call
/// this</i> answers yes and means nothing. What has to be asked is whether
/// anything ever supplies the INPUT, and the answer reaches production as a
/// default for ever when it does not.
/// </para>
/// <para>
/// <b>It starts non-empty and that is honest; it becomes decoration the moment
/// an entry has no trigger.</b> Every exemption says why it is not a defect and
/// what would take it off the list.
/// </para>
/// </remarks>
public class UnsuppliedInputTests
{

    [Test]
    public async Task The_console_is_handed_a_browser_it_can_actually_use()
    {
        // THE DEFECT THIS SCAN WAS BUILT FOR, asserted on its own rather than as
        // one row of a repository-wide list. The wider assertion is not here
        // yet: the scan reports RunnerLoop's collaborators as unsupplied when
        // RunnerHost supplies them positionally, and a guard with a false
        // positive in it teaches people to add exemptions rather than to look.
        // That is a second pass, and it is not this fix.
        var findings = UnsuppliedInputs.Analyse(UnsuppliedInputs.Production())
            .Findings.Select(UnsuppliedInputs.Key)
            .ToList();

        await Assert.That(findings).DoesNotContain("ConsoleLoop.ConsoleLoop(browser)")
            .Because("the browse pane answers \"No tracker is configured to browse\" whenever "
                   + "this is null, and it is null whenever the composition root does not pass "
                   + "one - which is what it did on a host whose tracker was configured "
                   + "correctly, whose credential resolved, and whose `gg runner tools` "
                   + "answered a handshake.");
    }

    [Test]
    public async Task The_scan_can_tell_a_supplied_input_from_an_unsupplied_one()
    {
        // THE POISON TWIN. Without it the assertion above also passes on a scan
        // that considers every input supplied, which is the shape a textual
        // guard fails into most quietly.
        var supplied = UnsuppliedInputs.Analyse(new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["a.cs"] = "public class A { public void M(int x, string y = \"\") { } }",
            ["b.cs"] = "public class B { void Go() { new A().M(1, y: \"given\"); } }",
        });

        var never = UnsuppliedInputs.Analyse(new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["a.cs"] = "public class A { public void M(int x, string y = \"\") { } }",
            ["b.cs"] = "public class B { void Go() { new A().M(1); } }",
        });

        await Assert.That(supplied.Findings.Select(UnsuppliedInputs.Key)).DoesNotContain("A.M(y)");
        await Assert.That(never.Findings.Select(UnsuppliedInputs.Key)).Contains("A.M(y)");
    }

    [Test]
    public async Task Prose_that_names_an_argument_does_not_count_as_supplying_it()
    {
        // THE DETAIL THE WHOLE SCAN TURNS ON, and the one that has already
        // fooled a guard in this family: a comment describing a call, or a
        // literal containing one, is prose. Counting it reports an input as
        // supplied because somebody wrote about it.
        var analysis = UnsuppliedInputs.Analyse(new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["a.cs"] = "public class A { public void M(int x, string y = \"\") { } }",
            ["b.cs"] = "public class B { void Go() { /* M(1, y: \"x\") */ var s = \"M(1, y: 2)\"; } }",
        });

        await Assert.That(analysis.Findings.Select(UnsuppliedInputs.Key)).Contains("A.M(y)")
            .Because("neither the comment nor the string is a call, and a scan that counted "
                   + "either would report this input as live because it was mentioned.");
    }

    [Test]
    public async Task What_the_scan_cannot_decide_is_reported_rather_than_dropped()
    {
        // Two types declaring one method name: calls are matched by name, so
        // one type's caller is evidence about the other's parameter. Saying so
        // beats losing the finding.
        var analysis = UnsuppliedInputs.Analyse(new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["a.cs"] = "public class A { public void M(int x, string y = \"\") { } }",
            ["b.cs"] = "public class B { public void M(int x, string y = \"\") { } }",
            ["c.cs"] = "public class C { void Go() { new A().M(1); } }",
        });

        await Assert.That(analysis.Undecidable).IsNotEmpty();
        await Assert.That(analysis.Findings.Select(UnsuppliedInputs.Key)).DoesNotContain("A.M(y)");
    }
}
