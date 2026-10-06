using Gg.Contracts;

namespace Gg.Contracts.Tests;

/// <summary>
/// <b>S62.1-01</b> - a file intent names a repository and a path, an optional ref, and nothing
/// else; beside text, a uri or a ticket it is refused as two payloads.
/// </summary>
/// <remarks>
/// <para>
/// <b>Why a file is a fourth payload and not a uri.</b> A uri is read by the agent, through a
/// tool, as whatever it points at today. A file is read by the runner at a commit it records,
/// so everything carried from the flight reads the same words. The two need different
/// readers, and a reference spelled as a uri would be read by the wrong one.
/// </para>
/// <para>
/// <b>The path is checked here as well as by the reader.</b> The reader refuses a path that
/// could leave the repository before it fetches anything; refusing it on the wire too means a
/// control plane never records, and a lease never carries, a reference no runner will honour.
/// </para>
/// </remarks>
public class AFileIntentIsOnePayloadTests
{
    [Test]
    public async Task A_repository_and_a_path_is_a_file_intent()
    {
        var intent = FlightIntent.ForFile("JDX/JDNext", "docs/plans/18291.md", "develop");

        await Assert.That(intent.Kind).IsEqualTo(FlightIntentKinds.File);
        await Assert.That(FlightIntent.Validate(intent)).IsNull();
        await Assert.That(FlightIntentKinds.All).Contains(FlightIntentKinds.File);
    }

    [Test]
    public async Task The_ref_is_optional()
    {
        await Assert.That(FlightIntent.Validate(
            FlightIntent.ForFile("JDX/JDNext", "docs/plans/18291.md", null))).IsNull()
            .Because("absent means the repository's default branch, as it does for a checkout.");
    }

    [Test]
    public async Task A_file_intent_names_both_a_repository_and_a_path()
    {
        await Assert.That(FlightIntent.Validate(
            FlightIntent.ForFile("", "docs/plan.md", null))).IsNotNull();
        await Assert.That(FlightIntent.Validate(
            FlightIntent.ForFile("JDX/JDNext", " ", null))).IsNotNull();
    }

    [Test]
    public async Task A_file_beside_text_is_two_payloads()
    {
        var two = FlightIntent.ForFile("JDX/JDNext", "docs/plan.md", null) with { Text = "also this" };

        var because = FlightIntent.Validate(two);

        await Assert.That(because).IsNotNull();
        await Assert.That(because!).Contains("one payload")
            .Because("which payload wins would be decided by whichever reader saw it first.");
    }

    [Test]
    public async Task A_text_intent_carrying_a_repository_is_refused()
    {
        var mixed = FlightIntent.Of("do the thing") with { Repository = "JDX/JDNext" };

        await Assert.That(FlightIntent.Validate(mixed)).IsNotNull()
            .Because("a file member on a text intent is a reference nothing will read.");
    }

    [Test]
    [Arguments("../outside.md")]
    [Arguments("docs/../../outside.md")]
    [Arguments("/etc/passwd")]
    [Arguments("C:\\windows\\win.ini")]
    [Arguments("docs\\plan.md")]
    public async Task A_path_that_could_leave_the_repository_is_refused(string path)
    {
        await Assert.That(FlightIntent.Validate(
            FlightIntent.ForFile("JDX/JDNext", path, null))).IsNotNull()
            .Because($"'{path}' is not a path inside the repository.");
    }

    [Test]
    public async Task The_lease_carries_the_file_and_the_forge_it_is_on()
    {
        var granted = typeof(LeaseGranted).GetProperties().Select(p => p.Name).ToList();

        await Assert.That(granted).Contains("IntentRepository");
        await Assert.That(granted).Contains("IntentPath");
        await Assert.That(granted).Contains("IntentRef");

        // THE PROVIDER TOO, found while building the reader. A slug does not say which forge
        // it is on - `JDX/JDNext` reads the same on any of them - and the runner picks its
        // adapter by provider. IntentProvider is a TRACKER's key and a different namespace
        // (`agentic-backlog` beside `ado` on one runner), so reusing it would conflate two.
        await Assert.That(granted).Contains("IntentRepositoryProvider")
            .Because("the reader cannot choose an adapter from a slug alone.");
    }
}
