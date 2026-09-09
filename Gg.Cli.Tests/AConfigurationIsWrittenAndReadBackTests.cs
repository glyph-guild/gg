using Gg.Local;

namespace Gg.Cli.Tests;

/// <summary>
/// The configuration file: rendered, parsed, and the same document either way.
/// </summary>
/// <remarks>
/// <para>
/// <b>The values are kept as the STRINGS the variables carry</b>, not as parsed
/// structures. Every reader in the product already takes its declaration as
/// text — <c>VcsConfiguration.FromEnvironment(string? declared)</c> and its five
/// siblings — and that parameter is the seam this file plugs into. A file
/// holding a parsed shape would have to render it back to feed that seam, which
/// is a second representation of one value and a second thing to get wrong.
/// </para>
/// <para>
/// <b>Absent and blank are different answers, and the round trip has to keep
/// them apart.</b> A key that was never written must not come back as an empty
/// string: the reader would then see a declaration where the author left
/// silence, and silence is what inherits the default. This is the hazard
/// <c>AbsentCollectionsSurviveTheWireTests</c> found one project over, arriving
/// through a file instead of a wire.
/// </para>
/// </remarks>
public class AConfigurationIsWrittenAndReadBackTests
{
    private static Configuration Filled() => new()
    {
        ControlPlane = "https://example.invalid",
        Editor = "hx",
        TakeCommand = "claude",
        IntentHosts = "tracker=host.invalid|locator",
        IntentReaders = "other=cmd --flag | OTHER_TOKEN=local:other",
        VcsHosts = "forge=host.invalid",
        DestinationApis = "forge=api.host.invalid",
        ExecutorBinary = "/usr/local/bin/claude",
        RunnerLabels = "environment=dev",
        RunnerHoldSeconds = 10,
        PoolEndpoint = "https://pool.invalid",
        StunServers = "stun:stun.example.invalid:19302",
    };

    [Test]
    public async Task Everything_set_survives_the_round_trip()
    {
        var parsed = ConfigurationFile.Parse(ConfigurationFile.Render(Filled()));

        await Assert.That(parsed.Diagnosis).IsNull();
        await Assert.That(parsed.Configuration).IsEqualTo(Filled())
            .Because("a document that changes on the way through is a document a person "
                   + "cannot edit and re-apply.");
    }

    [Test]
    public async Task Nothing_set_survives_the_round_trip()
    {
        var parsed = ConfigurationFile.Parse(ConfigurationFile.Render(new Configuration()));

        await Assert.That(parsed.Diagnosis).IsNull();
        await Assert.That(parsed.Configuration).IsEqualTo(new Configuration());
    }

    [Test]
    public async Task A_value_nobody_set_is_not_written_at_all()
    {
        // ABSENT STAYS ABSENT. Writing `"editor": null` would put a key in front
        // of the next reader that the author never wrote - and on the way back
        // in, a member that arrived as null is indistinguishable from one that
        // arrived as "". The reader inherits the default only for silence.
        var text = ConfigurationFile.Render(new Configuration { Editor = "hx" });

        await Assert.That(text).Contains("editor", StringComparison.Ordinal);
        await Assert.That(text).DoesNotContain("control-plane", StringComparison.Ordinal)
            .Because($"nothing set it, so nothing should write it. Rendered:\n{text}");
        await Assert.That(text).DoesNotContain("null", StringComparison.Ordinal);
    }

    [Test]
    public async Task The_keys_are_the_words_the_product_already_uses()
    {
        // kebab-case, like every closed vocabulary in the envelope -
        // `pull-request`, `on-exhaustion`, `wall-clock`. A file a person opens
        // should look like the other documents this product asks them to read.
        var text = ConfigurationFile.Render(Filled());

        foreach (var key in (string[])
                 ["control-plane", "take-command", "intent-hosts", "vcs-hosts",
                  "destination-apis", "executor-binary", "runner-labels",
                  "runner-hold-seconds", "pool-endpoint", "stun-servers"])
        {
            await Assert.That(text).Contains(key, StringComparison.Ordinal)
                .Because($"'{key}' is how this value is spelled everywhere else.");
        }
    }

    [Test]
    public async Task A_file_that_is_not_there_is_silence_rather_than_an_error()
    {
        // The ordinary case on a machine nobody has configured. Not an error,
        // not an empty configuration - nothing to say, so the environment and
        // the defaults answer.
        var read = ConfigurationFile.Read(
            Path.Combine(Path.GetTempPath(), $"gg-config-absent-{Guid.NewGuid():N}.json"));

        await Assert.That(read.Diagnosis).IsNull();
        await Assert.That(read.Configuration).IsNull()
            .Because("a missing file and an empty one are different facts: the second is "
                   + "somebody having written a document that sets nothing.");
    }

    [Test]
    public async Task A_written_file_reads_back_as_what_was_written()
    {
        var path = Path.Combine(Path.GetTempPath(), $"gg-config-{Guid.NewGuid():N}.json");

        try
        {
            ConfigurationFile.Write(Filled(), path);
            var read = ConfigurationFile.Read(path);

            await Assert.That(read.Diagnosis).IsNull();
            await Assert.That(read.Configuration).IsEqualTo(Filled());
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Test]
    public async Task The_rendered_form_is_a_file_rather_than_a_fragment()
    {
        // The rule EnvelopeRoundTripTests states for the other document this
        // product asks a person to edit: text that does not end in a newline
        // makes every diff tool say so, forever.
        var text = ConfigurationFile.Render(Filled());

        await Assert.That(text.EndsWith('\n')).IsTrue();
        await Assert.That(text.EndsWith("\n\n", StringComparison.Ordinal)).IsFalse();
        await Assert.That(text).DoesNotContain("\r", StringComparison.Ordinal)
            .Because("the bytes must not depend on the machine that rendered them.");
    }
}
