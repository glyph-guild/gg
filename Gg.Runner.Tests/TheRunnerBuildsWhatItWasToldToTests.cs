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
    public async Task The_tracker_api_is_resolved_through_the_configuration_file()
    {
        // FOUND ON A LIVE RUNNER, and the comment above the call already claimed
        // it: "so a tracker-apis line in the configuration file reaches the
        // sinks. Read straight from the environment this would be the
        // stun-servers defect again, one variable over." It passed no
        // configuration, so the file never reached the sinks and the defect it
        // names was the one it had.
        //
        // The machine had `tracker-apis` in its config, `gg config show` read it
        // back as `file GG_TRACKER_APIS ...`, and the flying runner still
        // refused an admitted write saying it had no tracker declared - because
        // Settings.Value's `file` parameter defaults to null and the runner path
        // threads `inForce` through every OTHER setting it reads.
        //
        // ASSERTED OVER THE SOURCE, like the sink construction above it: there
        // is no seam to inject here, and the alternative is noticing again on a
        // live machine.
        var root = Root();
        var calls = root.Split("TrackerConfiguration.ApisVariable");

        await Assert.That(calls.Length).IsGreaterThan(1)
            .Because("with no call to find, this passes without checking anything.");

        // The argument list runs from the variable to the close of the call.
        foreach (var after in calls.Skip(1))
        {
            var upToClose = after.Split(')')[0];

            // EITHER SPELLING OF THE SAME ARGUMENT: the runner path holds the
            // configuration in a local it threads through every setting it
            // reads, and the member path reads the property directly because
            // its local is declared after the offer switch. What must not
            // happen is neither, which leaves `file` defaulting to null.
            var given = upToClose.Contains("inForce", StringComparison.Ordinal)
                     || upToClose.Contains("InForce.Configuration", StringComparison.Ordinal);

            await Assert.That(given).IsTrue()
                .Because("every tracker-apis read has to be given the configuration in "
                       + "force, or a line in the file is written, shown by `gg config "
                       + $"show`, and ignored by the runner. Found: '{upToClose.Trim()}'");
        }
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

        await Assert.That(asked).Contains("local:tracker.example/acme/project")
            .Because("derived from the host: scheme dropped, lowercased, reduced to the "
                   + "locator charset and carrying the only prefix this platform has - so "
                   + "one project is one credential however many destinations aim at it. "
                   + $"Asked for: {string.Join(", ", asked)}");

        await Assert.That(asked).DoesNotContain("the-backlog")
            .Because("the destination id names where work lands, which is not who may "
                   + "change it.");
    }

    [Test]
    public async Task The_derived_locator_is_one_the_credential_store_would_accept()
    {
        // THE TWIN FOR AN ERROR THE TEST ABOVE COULD NOT CATCH. secretFor is a
        // lambda here: it records whatever string it is handed and validates
        // nothing, so the first version of this derivation asked for
        // `tracker.example/acme/project` - no `local:` prefix, a string the
        // contract refuses - and every assertion passed. PathFor would have
        // thrown at the first admitted write, in front of nobody.
        //
        // So this asks the CONTRACT whether what was derived is a locator,
        // rather than asking a double whether it was the string we expected.
        var asked = new List<string>();

        TrackerConfiguration.FromEnvironment(
            _ => new HttpClient(),
            apis: "the-backlog=https://Tracker.Example:8443/Acme/Project",
            secretFor: locator => { asked.Add(locator); return "a-token"; });

        await Assert.That(asked).IsNotEmpty()
            .Because("with nothing recorded this passes without checking anything.");

        foreach (var locator in asked)
        {
            await Assert.That(Gg.Contracts.CredentialLocator.Validate(locator)).IsNull()
                .Because($"'{locator}' has to be something the store can hold. A port colon "
                       + "is exactly the character a host carries and a locator may not, "
                       + "which is why this host has one.");
        }
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
            apis: "the-backlog=https://Tracker.Example/Acme/Project|local:a-held-secret",
            secretFor: locator => { asked.Add(locator); return "a-token"; });

        await Assert.That(asked).Contains("local:a-held-secret")
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
