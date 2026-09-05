namespace Gg.Client.Tests;

/// <summary>
/// A pasted line is read as one of the three kinds a flight can have.
/// </summary>
/// <remarks>
/// <para>
/// <b>Every provider here is invented, and that is enforced.</b>
/// <c>NoSourceFileNamesAnIdentityProvider</c> refuses a real tracker's name
/// anywhere in this binary - <i>"gg talks only to the control plane; a provider
/// name here means that boundary has leaked into a public binary"</i> - and it
/// caught this file naming one on the first run. A provider key is a string an
/// operator chose, and a test that hardcodes a real one is a test asserting
/// something about somebody's deployment.
/// </para>
/// <para>
/// <b>The console could reach exactly one of them.</b> `ConsoleData.FlyAsync`
/// called the command with `uri: null` and no provider, so two of the three
/// intent kinds had no console path and a person with a work item in front of
/// them had to leave for a shell.
/// </para>
/// </remarks>
public class PastedIntentTests
{
    [Test]
    public async Task A_url_is_a_uri_intent()
    {
        var read = PastedIntent.Of("https://tracker.invalid/x/y/_workitems/edit/18398");

        await Assert.That(read.Uri).IsEqualTo("https://tracker.invalid/x/y/_workitems/edit/18398");
        await Assert.That(read.Text).IsNull();
        await Assert.That(read.Provider).IsNull();
        await Assert.That(read.Refusal).IsNull();
    }

    [Test]
    public async Task A_url_with_a_fragment_is_still_a_uri()
    {
        // THE ORDER MATTERS AND THIS IS WHY. A url can carry a '#', and reading
        // one as a ticket would split it at the anchor and open a flight against
        // a provider named "https://example.invalid/issues/4". The flight would
        // open, reach a runner, and do the wrong work - silently.
        var read = PastedIntent.Of("https://example.invalid/issues/4#comment-9");

        await Assert.That(read.Uri).IsEqualTo("https://example.invalid/issues/4#comment-9");
        await Assert.That(read.Provider).IsNull()
            .Because("a fragment is part of a url, not a ticket separator.");
    }

    [Test]
    public async Task A_provider_and_an_id_is_a_ticket()
    {
        var read = PastedIntent.Of("atracker#18398");

        await Assert.That(read.Provider).IsEqualTo("atracker");
        await Assert.That(read.Id).IsEqualTo("18398");
        await Assert.That(read.Uri).IsNull();
        await Assert.That(read.Text).IsNull();
    }

    [Test]
    public async Task A_ticket_id_containing_a_separator_keeps_its_tail()
    {
        // Split on the FIRST separator, never on every one: a tracker whose ids
        // contain a '#' would otherwise lose the tail silently, which is the
        // truncation failure this repository keeps finding one field at a time.
        var read = PastedIntent.Of("another#PROJ-1#2");

        await Assert.That(read.Provider).IsEqualTo("another");
        await Assert.That(read.Id).IsEqualTo("PROJ-1#2");
    }

    [Test]
    public async Task Anything_else_is_the_free_text_that_already_worked()
    {
        var read = PastedIntent.Of("  add a health check to the payments service  ");

        await Assert.That(read.Text).IsEqualTo("add a health check to the payments service")
            .Because("trimmed, because a paste carries whatever whitespace came with it.");
        await Assert.That(read.Uri).IsNull();
        await Assert.That(read.Provider).IsNull();
    }

    [Test]
    public async Task A_half_written_ticket_is_refused_with_the_half_that_is_missing()
    {
        // The parser is the only thing that knows the token was MEANT to be two
        // things. The contract would see a provider and no id and say so
        // correctly, but it cannot say "you left out the #".
        foreach (var half in new[] { "atracker#", "#18398" })
        {
            var read = PastedIntent.Of(half);

            await Assert.That(read.Refusal).IsNotNull()
                .Because($"'{half}' is a ticket somebody did not finish, and opening it as free "
                       + "text would send an agent the literal string.");
            await Assert.That(read.Refusal!).Contains("both halves are needed",
                StringComparison.OrdinalIgnoreCase);
        }
    }

    [Test]
    public async Task Nothing_pasted_is_refused_rather_than_flown()
    {
        foreach (var nothing in new[] { "", "   ", null })
        {
            var read = PastedIntent.Of(nothing);

            await Assert.That(read.Refusal).IsNotNull()
                .Because("an empty prompt is a person who changed their mind, and opening a "
                       + "flight with no intent at all spends a runner on nothing.");
        }
    }

    [Test]
    public async Task A_scheme_that_is_not_the_web_is_not_a_uri_intent()
    {
        // file:// and ssh:// are not work items, and a uri intent is resolved by
        // a runner against a tracker. Treating one as text is the safe answer:
        // an agent is handed a sentence rather than told to fetch a local path.
        var read = PastedIntent.Of("file:///etc/passwd");

        await Assert.That(read.Uri).IsNull();
        await Assert.That(read.Text).IsEqualTo("file:///etc/passwd");
    }
}
