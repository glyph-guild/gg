using Gg.Runner.Intent;

namespace Gg.Runner.Tests;

/// <summary>
/// Product code constructs a tracker sink, and constructs none where no
/// tracker write api was declared.
/// </summary>
/// <remarks>
/// <para>
/// <b>S36.4-01 and S36.4-02, and the first exists because the answer was
/// no.</b> Slice thirty-five built <c>WiqlWorkItemSink</c> and nothing in
/// product code ever built one — the machinery existing and nothing
/// constructing it, which S7.4-02 recorded verbatim the last time this
/// repository met it. An adapter nothing builds is an unbuilt feature that
/// reads as a finished one.
/// </para>
/// <para>
/// <b>No declaration, no sink</b>, which is <c>DestinationConfiguration</c>'s
/// disposition one system over: a runner configured to READ a tracker and not
/// to write to one holds no sink at all, so "no destination, no write" is true
/// at the level of which objects exist rather than at the level of a check
/// somebody could delete.
/// </para>
/// <para>
/// <b>Read as source text, on the precedent this repository already uses for
/// wiring.</b> Enumerating call sites is an enumeration that cannot notice the
/// one nobody added; asking whether the root constructs the type is the
/// question that was actually answered wrongly.
/// </para>
/// </remarks>
public class TheRunnerBuildsWhatItWasToldToTests
{
    private static string Root()
    {
        var here = new DirectoryInfo(AppContext.BaseDirectory);
        while (here is not null && !Directory.Exists(Path.Combine(here.FullName, "Gg.Cli")))
        {
            here = here.Parent;
        }

        return File.ReadAllText(Path.Combine(here!.FullName, "Gg.Cli", "Program.cs"));
    }

    [Test]
    public async Task Product_code_constructs_a_tracker_sink()
    {
        await Assert.That(Root()).Contains(nameof(TrackerConfiguration), StringComparison.Ordinal)
            .Because("nothing built a WiqlWorkItemSink for a whole slice, so an admitted "
                   + "proposal reached no tracker and the adapter read as finished work.");
    }

    [Test]
    public async Task No_write_api_declared_means_no_sink_at_all()
    {
        await Assert.That(TrackerConfiguration.FromEnvironment(
            _ => new HttpClient(), apis: "", secretFor: _ => "a-token")).IsEmpty()
            .Because("a runner told about no tracker write api CANNOT write to one - held by "
                   + "there being no object, not by a check somebody could delete.");
    }

    [Test]
    public async Task A_declared_api_with_no_credential_is_refused_rather_than_built_useless()
    {
        // THE CONSTRUCTOR ALREADY REFUSES AN ABSENT CREDENTIAL, and this is
        // where that refusal has to surface: at start-up, where a person is
        // configuring the machine, rather than at the first admitted write.
        var refused = Assert.Throws<InvalidOperationException>(() =>
            TrackerConfiguration.FromEnvironment(
                _ => new HttpClient(),
                apis: "backlog=https://tracker.example/team/project",
                secretFor: _ => null));

        await Assert.That(refused!.Message.Length).IsGreaterThan(60)
            .Because("a machine that would fail at the first write should say so while "
                   + $"somebody is looking at it. Said: {refused.Message}");
    }

    [Test]
    public async Task A_declared_api_builds_one_sink_per_destination_key()
    {
        var sinks = TrackerConfiguration.FromEnvironment(
            _ => new HttpClient(),
            apis: "backlog=https://tracker.example/team/project",
            secretFor: _ => "a-token");

        await Assert.That(sinks.Count).IsEqualTo(1);
        await Assert.That(sinks.ContainsKey("backlog")).IsTrue()
            .Because("keyed by the destination id the envelope names, because that is what "
                   + "an admission comes back carrying.");
    }
}
