namespace Gg.Cli.Tests;

/// <summary>
/// <c>gg fly --ticket azure-boards#4471</c>, and the third kind rendering
/// rather than falling through to an empty cell.
/// </summary>
/// <remarks>
/// <para>
/// <b><c>provider#id</c> is one argument on purpose.</b> Two flags would make
/// the half-supplied case reachable from the command line - <c>--provider</c>
/// with no <c>--id</c> - and the contract already refuses that with a better
/// sentence than an argument parser can produce. One token that splits on the
/// separator every tracker already uses in prose keeps the parser's job to
/// "did they type it" and leaves "is it a ticket" to the one validator.
/// </para>
/// <para>
/// <b>The render half is not decoration.</b> A kind that validates and does not
/// render is a flight whose intent column is blank in the queue every person
/// reads, which is the failure of "registered is not invoked" arriving at a
/// surface instead of at a receptor.
/// </para>
/// </remarks>
public class TicketFlyTests
{
    [Test]
    public async Task Flying_a_work_item_parses_into_a_provider_and_an_id()
    {
        var action = CliArgs.Parse(["fly", "--ticket", "azure-boards#4471"]);

        await Assert.That(action).IsTypeOf<CliAction.Fly>();

        var fly = (CliAction.Fly)action;
        await Assert.That(fly.Provider).IsEqualTo("azure-boards");
        await Assert.That(fly.Id).IsEqualTo("4471");
        await Assert.That(fly.Text).IsNull();
        await Assert.That(fly.Uri).IsNull();
    }

    [Test]
    public async Task The_two_payloads_that_already_worked_still_do()
    {
        // The regression half. Every invocation anybody has typed is one of
        // these two, and a new arm in a pattern match is exactly where the
        // older arms stop matching.
        var text = (CliAction.Fly)CliArgs.Parse(["fly", "fix the login bug"]);
        await Assert.That(text.Text).IsEqualTo("fix the login bug");
        await Assert.That(text.Provider).IsNull();

        var uri = (CliAction.Fly)CliArgs.Parse(["fly", "--uri", "https://example.invalid/issues/7"]);
        await Assert.That(uri.Uri).IsEqualTo("https://example.invalid/issues/7");
        await Assert.That(uri.Provider).IsNull();
    }

    [Test]
    public async Task A_ticket_with_no_separator_is_refused_by_the_parser()
    {
        // Refused HERE rather than sent to the control plane, because the
        // parser is the only thing that knows the token was meant to be two
        // things. The contract sees a provider and no id and says so correctly,
        // but it cannot say "you left out the #".
        var action = CliArgs.Parse(["fly", "--ticket", "azure-boards"]);

        await Assert.That(action).IsTypeOf<CliAction.Unknown>();
        await Assert.That(((CliAction.Unknown)action).Message).Contains("#")
            .Because("a refusal that does not show the shape leaves somebody guessing at a "
                   + "separator.");
    }

    [Test]
    public async Task A_ticket_missing_either_half_is_refused()
    {
        foreach (var token in (string[])["#4471", "azure-boards#", "#"])
        {
            await Assert.That(CliArgs.Parse(["fly", "--ticket", token]))
                .IsTypeOf<CliAction.Unknown>()
                .Because($"'{token}' names at most one of the two things a ticket is.");
        }
    }

    [Test]
    public async Task An_id_containing_the_separator_keeps_all_of_it()
    {
        // Split on the FIRST separator, not on every one. A tracker whose ids
        // contain a # would otherwise lose the tail silently, which is the
        // truncation failure this repository keeps finding one field at a time.
        var fly = (CliAction.Fly)CliArgs.Parse(["fly", "--ticket", "jira#PROJ-1#2"]);

        await Assert.That(fly.Provider).IsEqualTo("jira");
        await Assert.That(fly.Id).IsEqualTo("PROJ-1#2");
    }

    [Test]
    public async Task Flying_a_ticket_and_text_at_once_is_refused()
    {
        var action = CliArgs.Parse(["fly", "some words", "--ticket", "azure-boards#4471"]);

        await Assert.That(action).IsTypeOf<CliAction.Unknown>()
            .Because("an intent that says two things says nothing - the sentence gg fly has "
                   + "used since slice two, and it does not get a second one.");
    }

    [Test]
    public async Task The_help_text_names_the_third_kind()
    {
        // A flag nobody can discover is a flag nobody uses, and the usage list
        // carried on an Unknown is the only place the shape is written down for
        // a person.
        var usage = ((CliAction.Unknown)CliArgs.Parse(["telepathy"])).Message;

        await Assert.That(usage).Contains("--ticket");
        await Assert.That(usage).Contains("#")
            .Because("the separator is the whole of what somebody needs to be told.");
    }
}
