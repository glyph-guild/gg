using System.Text.RegularExpressions;
using Gg.Contracts;
using Gg.Local;

namespace Gg.Runner.Tests;

/// <summary>
/// An example gg prints when it refuses a line is a line gg would accept.
/// </summary>
/// <remarks>
/// <para>
/// <b>Measured: it was not.</b> `intent-hosts` reads the field after the bar as
/// the locator WHOLE, and the refusal for too many fields offered
/// <c>|TOKEN=local:acme/board|</c> - which is <c>GG_INTENT_READERS</c>' shape,
/// where the credential half really is <c>VARIABLE=locator</c>. Two variables,
/// two spellings, one example, and it was under the wrong one.
/// </para>
/// <para>
/// <b>What it costs is worse now than when it was written.</b> Nothing
/// validates the read side's locator, so following the example produces a
/// tracker whose credential is the string <c>TOKEN=local:acme/board</c> - and
/// since 0.47.0 `gg doctor` reports that as <i>"declared here with a credential
/// this machine does not hold"</i> and tells the person to run
/// <c>gg credential add --repo TOKEN=local:acme/board</c>. Both sentences are
/// wrong, both came from gg, and the second one cannot be followed.
/// </para>
/// <para>
/// <b>Driven by the message rather than by a list.</b> The refusal is provoked
/// and its own text is fed back to the parser that wrote it, so an example
/// added to one of these later is covered without anybody remembering this
/// file.
/// </para>
/// </remarks>
public class AnExampleInARefusalParsesTests
{
    /// <summary>Lines that make <c>intent-hosts</c> refuse, one per refusal.</summary>
    private static readonly string[] Refused =
    [
        "no-equals-sign-at-all",
        "tracker=https://tracker.example/acme|local:acme/board|svc-triage|one-too-many",
        "tracker=",
    ];

    [Test]
    public async Task Every_example_intent_hosts_prints_is_an_entry_it_accepts()
    {
        var examples = new List<string>();

        foreach (var line in Refused)
        {
            string message;

            try
            {
                IntentConfiguration.ServedTrackers(line);
                continue;
            }
            catch (InvalidOperationException refused)
            {
                message = refused.Message;
            }

            if (Regex.Match(message, @"e\.g\. '([^']+)'") is { Success: true } found)
            {
                examples.Add(found.Groups[1].Value);
            }
        }

        await Assert.That(examples).IsNotEmpty()
            .Because("this measures the examples gg prints, so finding none means the "
                   + "refusals moved and this test is watching nothing.");

        foreach (var example in examples)
        {
            var parsed = IntentConfiguration.ServedTrackers(example);

            await Assert.That(parsed.Count).IsEqualTo(1)
                .Because($"'{example}' is offered to somebody whose line was refused, so it "
                       + "has to be a line that works when they copy it.");

            // AND ITS CREDENTIAL HAS TO BE ONE. Parsing is not enough: the
            // locator is handed to a store, and a string the contract refuses
            // reads downstream as a credential nobody added.
            if (parsed[0].Locator is { Length: > 0 } locator)
            {
                await Assert.That(CredentialLocator.Validate(locator)).IsNull()
                    .Because($"'{example}' names '{locator}' as the credential to resolve, and "
                           + "a locator this contract refuses is the shape of another "
                           + "variable's example, not of this one's.");
            }
        }
    }
}
