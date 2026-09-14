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
    public async Task The_credential_is_asked_for_by_HOST_and_not_by_destination_id()
    {
        // THE SECRET BELONGS TO THE TRACKER, not to the name of a landing place.
        // This asked for it by destination id, so two destinations aiming at one
        // tracker project each needed their own copy of the same secret, filed
        // under different names - and a live runner already holding that
        // tracker's credential was refused a write because the locator did not
        // happen to match the destination id the envelope used.
        var asked = new List<string>();

        TrackerConfiguration.FromEnvironment(
            _ => new HttpClient(),
            apis: "the-backlog=https://Tracker.Example/Acme/Project",
            secretFor: locator => { asked.Add(locator); return "a-token"; });

        await Assert.That(asked).Contains("tracker.example/acme/project")
            .Because("derived from the host: scheme dropped, lowercased and reduced to the "
                   + "locator charset, so one project is one credential however many "
                   + $"destinations aim at it. Asked for: {string.Join(", ", asked)}");

        await Assert.That(asked).DoesNotContain("the-backlog")
            .Because("the destination id names where work lands, which is not who may "
                   + "change it.");
    }

    [Test]
    public async Task An_entry_may_name_the_credential_itself()
    {
        // THE SIBLING'S SHAPE, because a served intent host already pairs a host
        // with a credential after a bar. A machine that already holds a secret
        // under a name of its own should point at it rather than keep a second
        // copy under a name this derives.
        var asked = new List<string>();

        TrackerConfiguration.FromEnvironment(
            _ => new HttpClient(),
            apis: "the-backlog=https://Tracker.Example/Acme/Project|a-held-secret",
            secretFor: locator => { asked.Add(locator); return "a-token"; });

        await Assert.That(asked).Contains("a-held-secret")
            .Because($"named explicitly, so nothing is derived. Asked for: {string.Join(", ", asked)}");
    }

    [Test]
    public async Task A_host_that_reduces_to_no_locator_is_refused_rather_than_guessed()
    {
        // ARTICLE XI, and the alternative is worse than a refusal: a host this
        // cannot derive a locator from would otherwise ask the store for "" and
        // report the absent-credential error, sending somebody to add a secret
        // under a name nothing could ever hold.
        var refused = Assert.Throws<InvalidOperationException>(() =>
            TrackerConfiguration.FromEnvironment(
                _ => new HttpClient(),
                apis: "backlog=https://",
                secretFor: _ => "a-token"));

        await Assert.That(refused!.Message.Length).IsGreaterThan(60)
            .Because($"it has to say which entry and why. Said: {refused.Message}");
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
