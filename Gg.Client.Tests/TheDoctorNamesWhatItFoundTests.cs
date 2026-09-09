using Gg.Client;

namespace Gg.Client.Tests;

/// <summary>
/// Advice that names what is already on the machine.
/// </summary>
/// <remarks>
/// <para>
/// <b>An unset executor is the quietest failure this product has.</b> The runner
/// takes work, materializes it, ships facts and invokes no agent — so it looks
/// busy and does nothing, and the only surface that says so is this one. Telling
/// somebody to set a path is right; telling them the path, when it is sitting on
/// their own <c>PATH</c>, turns two steps into one.
/// </para>
/// <para>
/// <b>Shown, never applied.</b> The doctor writes nothing. What it found is a
/// sentence with a command in it, and a person runs the command — which is the
/// rule the whole configuration slice holds: gg proposes, a person accepts.
/// </para>
/// </remarks>
public class TheDoctorNamesWhatItFoundTests
{
    private static MachineRole WithNothingConfigured(string? found) => new()
    {
        ExecutorBinary = null,
        ExecutorPresent = false,
        ExecutorOnPath = found,
    };

    [Test]
    public async Task An_executor_found_on_the_path_is_named_in_the_advice()
    {
        var check = Doctor.ExecutorCheck(WithNothingConfigured("/opt/agent/claude"));

        await Assert.That(check.Fix).IsNotNull();
        await Assert.That(check.Fix!).Contains("/opt/agent/claude", StringComparison.Ordinal)
            .Because("it is on this machine already, and a person told only to set something "
                   + "has to go and find what this already knows.");
        await Assert.That(check.Fix!).Contains("gg config set", StringComparison.Ordinal)
            .Because("naming it without the command that uses it is half an answer.");
    }

    [Test]
    public async Task Finding_nothing_says_nothing_rather_than_an_empty_offer()
    {
        // An offer of nothing reads as an offer. The advice falls back to what
        // it said before, which is still correct.
        var check = Doctor.ExecutorCheck(WithNothingConfigured(found: null));

        await Assert.That(check.Fix).IsNotNull();
        await Assert.That(check.Fix!).DoesNotContain("found", StringComparison.OrdinalIgnoreCase)
            .Because($"nothing was found, so nothing should be offered. Said: {check.Fix}");
    }

    [Test]
    public async Task A_configured_executor_that_is_there_is_not_offered_an_alternative()
    {
        // Somebody who configured one does not want to be told about another.
        var check = Doctor.ExecutorCheck(new MachineRole
        {
            ExecutorBinary = "/usr/local/bin/claude",
            ExecutorPresent = true,
            ExecutorOnPath = "/opt/agent/claude",
        });

        await Assert.That(check.Fix).IsNull()
            .Because("it is configured and it is there, so there is nothing to fix.");
    }
}
