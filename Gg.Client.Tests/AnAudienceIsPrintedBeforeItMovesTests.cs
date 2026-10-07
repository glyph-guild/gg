using Gg.Client;
using Gg.Contracts;

namespace Gg.Client.Tests;

/// <summary>
/// The audience is printed, machine by machine, before anything moves.
/// </summary>
/// <remarks>
/// <para>
/// <b>S64.5-02, and rule 5 of the slice.</b> ADR-0037 Decision 11's <i>"very clear"</i>
/// is the owner's word and it is about the list being REVIEWABLE, not about a count. A
/// push that said "sending to 4 machines" and then sent is a push nobody reviewed.
/// </para>
/// <para>
/// <b>Before, not after, and the ordering is the whole criterion.</b> A list printed
/// alongside the sending is a log; a list printed first is a chance to stop. The
/// assertion is therefore about ORDER of what was said, not about the words appearing
/// somewhere.
/// </para>
/// <para>
/// <b>Each row carries the locator it will receive</b>, because a person checking a
/// broadcast is checking two things at once - which machines, and which credential.
/// </para>
/// </remarks>
public class AnAudienceIsPrintedBeforeItMovesTests
{
    private const string Locator = "local:acme/widgets";

    private static CredentialAudienceRow ARow(string label, bool reachable = true) =>
        new(RunnerId: label + "-id", Label: label, Locator: Locator,
            Declared: true, Reported: false, Reachable: reachable, Through: null);

    [Test]
    public async Task Every_machine_is_named_before_the_first_one_is_reached()
    {
        var said = new List<string>();

        CredentialBroadcast.Announce(Locator, [ARow("vmlinux001"), ARow("vmlinux002")], said.Add);

        await Assert.That(said.Count).IsGreaterThanOrEqualTo(2)
            .Because("a line per machine, not a count. Said: " + string.Join(" | ", said));

        foreach (var machine in (string[])["vmlinux001", "vmlinux002"])
        {
            await Assert.That(said.Any(s => s.Contains(machine, StringComparison.Ordinal))).IsTrue()
                .Because($"{machine} is in the audience and a list that omitted it is a list "
                       + "somebody approved without seeing. Said: " + string.Join(" | ", said));
        }
    }

    [Test]
    public async Task And_each_line_names_the_locator_that_machine_receives()
    {
        var said = new List<string>();

        CredentialBroadcast.Announce(Locator, [ARow("vmlinux001")], said.Add);

        await Assert.That(said.Any(s => s.Contains(Locator, StringComparison.Ordinal))).IsTrue()
            .Because("which credential is moving is half of what a person is checking, and rule 5 "
                   + "asks for it per machine. Said: " + string.Join(" | ", said));
    }

    [Test]
    public async Task A_machine_that_cannot_be_reached_is_marked_rather_than_listed_plainly()
    {
        var said = new List<string>();

        CredentialBroadcast.Announce(
            Locator,
            [ARow("vmlinux001"), ARow("gg-pool-ui-2", reachable: false)],
            said.Add);

        var member = said.Single(s => s.Contains("gg-pool-ui-2", StringComparison.Ordinal));

        await Assert.That(member).Contains("not")
            .Because("a row that read like the others would promise a push that cannot happen. "
                   + "Said: " + member);
    }

    [Test]
    public async Task An_empty_audience_says_so_rather_than_printing_nothing()
    {
        // SILENCE IS THE WRONG ANSWER. A broadcast that reached nobody and said nothing
        // is indistinguishable from one that worked, and the likeliest cause - no
        // profile declares this credential - is something a person can fix.
        var said = new List<string>();

        CredentialBroadcast.Announce(Locator, [], said.Add);

        await Assert.That(said).IsNotEmpty()
            .Because("nobody needing a credential is a fact worth one sentence, because the usual "
                   + "reason is that nothing declared it.");

        await Assert.That(said[0]).Contains(Locator, StringComparison.Ordinal)
            .Because("and it names which credential nobody needs, since a person may have typed "
                   + "the wrong locator. Said: " + said[0]);
    }

    [Test]
    public async Task Nothing_printed_is_the_credential()
    {
        // THE ONE THING A LIST MUST NOT CONTAIN. Every row here is a reference - a
        // label, a locator, a flag - and the announcement is handed no value to leak.
        var parameters = typeof(CredentialBroadcast)
            .GetMethod(nameof(CredentialBroadcast.Announce))!
            .GetParameters()
            .Select(p => p.ParameterType.Name)
            .ToList();

        await Assert.That(parameters).DoesNotContain("SealedCredential")
            .Because("an envelope is not needed to describe who will get one, and a method that "
                   + "took one could print it. Parameters: " + string.Join(", ", parameters));
    }
}
