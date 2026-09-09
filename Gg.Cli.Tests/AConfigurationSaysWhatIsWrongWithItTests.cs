using Gg.Local;

namespace Gg.Cli.Tests;

/// <summary>
/// What a bad configuration file says, and to whom.
/// </summary>
/// <remarks>
/// <para>
/// <b>A sentence, never a bool</b>, and it names the offending value every
/// time — the rule <c>Envelope.Validate</c> already states: <i>"'Invalid
/// envelope' sends somebody reading their own file to work out which of nine
/// things went wrong, which is how a schema stops being adopted."</i> This file
/// is edited by hand more often than an envelope is, so it needs the rule more.
/// </para>
/// <para>
/// <b>Blank is refused rather than read as unset.</b> A person who typed
/// <c>"editor": ""</c> wrote a value; the reader would see silence and inherit
/// the default, and nothing on any screen would say the line did nothing. That
/// is the same refusal <c>AirspaceNames</c> makes for a blank name and
/// <c>FlightNomination</c> makes for a blank note.
/// </para>
/// </remarks>
public class AConfigurationSaysWhatIsWrongWithItTests
{
    [Test]
    public async Task A_configuration_with_nothing_wrong_with_it_says_nothing()
    {
        await Assert.That(Configuration.Validate(new Configuration())).IsNull();
        await Assert.That(Configuration.Validate(new Configuration
        {
            ControlPlane = "https://example.invalid",
            RunnerHoldSeconds = 10,
        })).IsNull();
    }

    [Test]
    public async Task A_value_that_is_there_and_blank_is_refused_and_named()
    {
        var refused = Configuration.Validate(new Configuration { Editor = "   " });

        await Assert.That(refused).IsNotNull();
        await Assert.That(refused!).Contains("editor", StringComparison.OrdinalIgnoreCase)
            .Because($"a refusal that does not name the line is a refusal nobody can act "
                   + $"on. Said: {refused}");
    }

    [Test]
    public async Task Every_text_value_is_held_to_that_rule_rather_than_the_one_somebody_remembered()
    {
        // ONE AT A TIME, so a member added later without a guard fails here
        // rather than being the one blank nobody checked.
        var blanks = new (string Key, Configuration Configuration)[]
        {
            ("control-plane", new Configuration { ControlPlane = "" }),
            ("editor", new Configuration { Editor = "" }),
            ("take-command", new Configuration { TakeCommand = "" }),
            ("intent-hosts", new Configuration { IntentHosts = "" }),
            ("intent-readers", new Configuration { IntentReaders = "" }),
            ("vcs-hosts", new Configuration { VcsHosts = "" }),
            ("destination-apis", new Configuration { DestinationApis = "" }),
            ("executor-binary", new Configuration { ExecutorBinary = "" }),
            ("runner-labels", new Configuration { RunnerLabels = "" }),
            ("pool-endpoint", new Configuration { PoolEndpoint = "" }),
            ("stun-servers", new Configuration { StunServers = "" }),
        };

        foreach (var (key, blank) in blanks)
        {
            var refused = Configuration.Validate(blank);

            await Assert.That(refused).IsNotNull()
                .Because($"'{key}' is present and blank, which reads as a value to whoever "
                       + "typed it and as silence to the reader.");
            await Assert.That(refused!).Contains(key, StringComparison.OrdinalIgnoreCase)
                .Because($"the refusal should name '{key}'. Said: {refused}");
        }
    }

    [Test]
    public async Task A_hold_that_returns_immediately_forever_is_refused()
    {
        foreach (var seconds in (int[])[0, -1])
        {
            var refused = Configuration.Validate(
                new Configuration { RunnerHoldSeconds = seconds });

            await Assert.That(refused).IsNotNull()
                .Because($"a hold of {seconds} is a claim that comes back empty as fast as "
                       + "the machine can ask, which is a busy loop against the control "
                       + "plane rather than a configuration.");
            await Assert.That(refused!).Contains(
                seconds.ToString(System.Globalization.CultureInfo.InvariantCulture),
                StringComparison.Ordinal)
                .Because($"the refusal should name the value. Said: {refused}");
        }
    }

    [Test]
    public async Task An_address_that_is_not_one_is_refused_and_named()
    {
        foreach (var (key, bad) in ((string Key, Configuration Value)[])
                 [("control-plane", new Configuration { ControlPlane = "localhost:5199" }),
                  ("pool-endpoint", new Configuration { PoolEndpoint = "not a url" })])
        {
            var refused = Configuration.Validate(bad);

            await Assert.That(refused).IsNotNull()
                .Because($"'{key}' is where a request is sent, and a value that is not an "
                       + "absolute address fails at the first call rather than here.");
            await Assert.That(refused!).Contains(key, StringComparison.OrdinalIgnoreCase);
        }
    }

    [Test]
    public async Task A_file_that_is_not_a_document_is_a_diagnosis_rather_than_a_throw()
    {
        var parsed = ConfigurationFile.Parse("{ this is not json");

        await Assert.That(parsed.Configuration).IsNull();
        await Assert.That(parsed.Diagnosis).IsNotNull()
            .Because("a person hand-editing this file will produce one of these, and a "
                   + "stack trace is not an answer to it.");
    }

    [Test]
    public async Task A_document_that_parses_but_does_not_validate_is_refused_on_the_way_in()
    {
        // The parse and the rule are one door, so a file on disk that Read
        // accepted is a file the rest of the product can use without asking
        // again.
        var parsed = ConfigurationFile.Parse("{ \"runner-hold-seconds\": 0 }");

        await Assert.That(parsed.Configuration).IsNull();
        await Assert.That(parsed.Diagnosis).IsNotNull();
        await Assert.That(parsed.Diagnosis!).Contains("0", StringComparison.Ordinal);
    }

    [Test]
    public async Task A_key_nobody_declared_is_refused_rather_than_ignored()
    {
        // The rule the envelope parser states: a line that looks load-bearing
        // and does nothing is how a field becomes folklore. A typo in a hand
        // edited file is the ordinary way this happens.
        var parsed = ConfigurationFile.Parse("{ \"controlplane\": \"https://example.invalid\" }");

        await Assert.That(parsed.Configuration).IsNull();
        await Assert.That(parsed.Diagnosis).IsNotNull();
        await Assert.That(parsed.Diagnosis!).Contains("controlplane", StringComparison.Ordinal)
            .Because($"the refusal should name the key that is not one. Said: {parsed.Diagnosis}");
    }
}
