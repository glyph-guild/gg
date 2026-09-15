namespace Gg.Cli.Tests;

/// <summary>
/// <c>gg environment chart</c> — the last act of standing an environment up
/// that could only be done with <c>curl</c>.
/// </summary>
/// <remarks>
/// <para>
/// <b>Found by standing one up.</b> The browser-environment spike needed a
/// second environment on a pool host and recorded the four acts it took, two of
/// them with no product surface. One of the two was wrong — <c>gg airspace name
/// strategy ui</c> has declared names since the topology door shipped — and this
/// is the one that was right: <c>POST /v1/environments</c> had a pinned request
/// type, a serializer registration, and no caller in this binary.
/// </para>
/// <para>
/// <b>Singular verb, plural list, which is the house pattern.</b>
/// <c>gg envelope apply</c> beside <c>gg envelopes</c>, <c>gg strategy
/// apply</c> beside <c>gg strategies</c>, <c>gg runner retire</c> beside <c>gg
/// runners</c>. So <c>gg environment chart</c> beside <c>gg environments</c>,
/// and the asymmetry is the convention rather than a slip.
/// </para>
/// <para>
/// <b>The meaning is a flag because it is optional and the name is not.</b>
/// Absent means the name is a claim — <c>stated</c> — and registering a meaning
/// is what earns <c>measured</c>. A positional second argument would make
/// "chart a name I cannot yet describe" look like a mistake.
/// </para>
/// </remarks>
public class AnEnvironmentIsChartedByAVerbTests
{
    [Test]
    public async Task A_name_is_taken()
    {
        var parsed = CliArgs.Parse(["environment", "chart", "ui"]);

        var charting = await Assert.That(parsed).IsTypeOf<CliAction.EnvironmentChart>();

        await Assert.That(charting!.Name).IsEqualTo("ui");
        await Assert.That(charting.Meaning).IsNull()
            .Because("a name with no meaning is a claim, which is a real thing to chart - "
                   + "and null is what the door reads as one.");
    }

    [Test]
    public async Task A_meaning_can_be_given()
    {
        var parsed = (CliAction.EnvironmentChart)CliArgs.Parse(
            ["environment", "chart", "ui", "--means", "a browser is present"]);

        await Assert.That(parsed.Name).IsEqualTo("ui");
        await Assert.That(parsed.Meaning).IsEqualTo("a browser is present")
            .Because("a meaning is what earns `measured`, and a verb that dropped it would "
                   + "chart names that can only ever be claims.");
    }

    [Test]
    public async Task A_chart_with_no_name_says_what_the_verb_takes()
    {
        var parsed = CliArgs.Parse(["environment", "chart"]);

        var unknown = await Assert.That(parsed).IsTypeOf<CliAction.Unknown>();

        await Assert.That(unknown!.Message).Contains("--means", StringComparison.Ordinal)
            .Because("the refusal is where somebody learns the optional half exists; "
                   + "leaving it out makes `measured` unreachable by anybody who did not "
                   + "read the contract.");
    }

    [Test]
    public async Task The_family_refusal_names_what_it_takes()
    {
        var parsed = CliArgs.Parse(["environment", "banana"]);

        var unknown = await Assert.That(parsed).IsTypeOf<CliAction.Unknown>();

        await Assert.That(unknown!.Message).Contains("chart", StringComparison.Ordinal);
    }

    [Test]
    public async Task And_the_plural_list_still_parses()
    {
        // THE ANCHOR THAT MATTERS MOST HERE. `environment` and `environments`
        // differ by one character, and a pattern that matched the prefix would
        // take the list verb away from everybody who already uses it.
        var parsed = CliArgs.Parse(["environments"]);

        await Assert.That(parsed).IsTypeOf<CliAction.Environments>();
    }
}
