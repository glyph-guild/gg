using Gg.Cli;
using Gg.Local;

namespace Gg.Cli.Tests;

/// <summary>
/// The list of variables the console shows, and what may not be on it.
/// </summary>
/// <remarks>
/// <para>
/// <b>THIS PAGE IS SHOWN ON A SCREEN SOMEBODY MAY BE SHARING</b>, and it is
/// written into the state dump a person attaches to a bug report. A declared
/// list is what keeps that safe — walking the process environment would put
/// every cloud key and token a person exports onto both.
/// </para>
/// <para>
/// <b>The declared list is only safe while nobody adds a secret to it</b>, and
/// the next person adding a row will be adding one they find useful. So the
/// rule is checked rather than remembered: a variable whose NAME says it holds
/// a credential does not belong here, however useful its value would be.
/// </para>
/// </remarks>
public class TheEnvironmentPageNamesNoSecretTests
{
    /// <summary>Words that say "this holds a secret" wherever they appear.</summary>
    /// <remarks>
    /// Matched on the NAME, never the value: a value that looks like a token
    /// might be a path, and a value that looks harmless might be a token. The
    /// name is what the person who exported it chose, and it is the honest
    /// signal.
    /// </remarks>
    private static readonly string[] SaysItIsASecret =
        ["TOKEN", "SECRET", "PASSWORD", "PAT", "APIKEY", "API_KEY", "CREDENTIAL", "PRIVATE"];

    [Test]
    public async Task No_variable_on_the_page_says_it_holds_a_credential()
    {
        var offenders = ConsoleEnvironment.Read()
            .Where(setting => SaysItIsASecret.Any(
                word => setting.Name.Contains(word, StringComparison.OrdinalIgnoreCase)))
            .Select(setting => setting.Name)
            .ToList();

        await Assert.That(offenders).IsEmpty()
            .Because("this page is rendered on a screen somebody may be sharing and written "
                   + "into a state dump they attach to a bug report.");
    }

    [Test]
    public async Task The_walk_would_actually_catch_one()
    {
        // THE LIVENESS ANCHOR. A matcher that matched nothing would make the
        // assertion above pass for a list containing ADO_PAT.
        // NAMED FOR THEIR SHAPE, because ProviderNeutralityTests forbids
        // naming a forge in this repository and it fired on the first version
        // of this line. Which is the guard working: a planted example is still
        // a source file, and the reason the rule exists does not care that this
        // one is a test.
        var planted = new[] { "A_TRACKER_PAT", "A_CLOUD_SECRET_ACCESS_KEY", "A_FORGE_TOKEN" };

        foreach (var name in planted)
        {
            await Assert.That(SaysItIsASecret.Any(
                    word => name.Contains(word, StringComparison.OrdinalIgnoreCase)))
                .IsTrue()
                .Because($"{name} has to be caught, or the guard above is decoration.");
        }
    }

    [Test]
    public async Task Every_row_says_what_it_decides_and_none_is_a_duplicate()
    {
        var read = ConsoleEnvironment.Read();

        await Assert.That(read).IsNotEmpty();
        await Assert.That(read.Select(s => s.Name).Distinct(StringComparer.Ordinal).Count())
            .IsEqualTo(read.Count)
            .Because("a variable listed twice is two answers to one question.");

        foreach (var setting in read)
        {
            await Assert.That(string.IsNullOrWhiteSpace(setting.Why)).IsFalse()
                .Because($"{setting.Name} with no consequence attached is a line a person "
                       + "has to go and look up, which is what they were already doing.");
            await Assert.That(string.IsNullOrWhiteSpace(setting.Name)).IsFalse();
        }
    }

    [Test]
    public async Task The_names_come_from_the_code_that_reads_them_where_it_can()
    {
        // Not a literal copy. Where the declaring constant is reachable, the
        // list uses it, so renaming the variable cannot leave this page
        // describing one that no longer exists.
        var names = ConsoleEnvironment.Read().Select(s => s.Name).ToList();

        await Assert.That(names).Contains(IntentConfiguration.ServedVariable);
        await Assert.That(names).Contains(IntentConfiguration.ReadersVariable);
    }

    [Test]
    public async Task It_reads_the_real_environment_rather_than_reporting_a_shape()
    {
        // The whole point is that it tells the truth about THIS process. A list
        // of names with no values would look identical in every test and be
        // useless in the one case it exists for.
        var before = ConsoleEnvironment.Read().Single(s => s.Name == "EDITOR").Value;

        try
        {
            Environment.SetEnvironmentVariable("EDITOR", "a-probe-value");

            await Assert.That(ConsoleEnvironment.Read().Single(s => s.Name == "EDITOR").Value)
                .IsEqualTo("a-probe-value");
        }
        finally
        {
            Environment.SetEnvironmentVariable("EDITOR", before);
        }
    }
}
