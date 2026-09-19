namespace Gg.Contracts.Tests;

/// <summary>
/// A branch template written as a full ref names the whole branch; every other
/// template still names the part after <c>gg/</c>.
/// </summary>
/// <remarks>
/// <para>
/// <b>Asked for 2026-09-19:</b> <i>"can you make the branch actually like
/// feature/GG-189-18349"</i>, the ticket being 18493. The team's branches read
/// <c>feature/…</c>, and a template could only ever write the part after
/// <c>gg/</c>.
/// </para>
/// <para>
/// <b>The prefix's stated reason was only half true.</b>
/// <c>ABranchCanCarryItsTicketTests</c> says <c>IsOurs</c> is what branch
/// cleanup reads. No product code calls it; only tests do. What does read a
/// prefix is <c>IsHandoff</c>, on the runner, to tell work kept for a takeover
/// from work offered for merge. So a landing branch can be named anything, and
/// a handoff stays where <c>IsHandoff</c> looks.
/// </para>
/// <para>
/// <b>Opted into, not reinterpreted.</b> Every template in force names a tail,
/// and a stored document must keep meaning what it meant, so the existing form
/// is untouched. A template that starts with <c>refs/heads/</c> says, in git's
/// own words, that it names the whole branch. Nobody writes that form wanting
/// <c>gg/refs/heads/…</c>, which is what it rendered before.
/// </para>
/// </remarks>
public class ATemplateMayNameTheWholeBranchTests
{
    private const string Flight = "GG-189";

    private const string Feature = "refs/heads/feature/{flight}-{ticket}";

    [Test]
    public async Task A_full_ref_names_the_branch_itself()
    {
        await Assert.That(DestinationBranch.For(Flight, Feature, "18493"))
            .IsEqualTo("feature/GG-189-18493");
    }

    [Test]
    public async Task Without_a_ticket_its_part_drops_out_as_before()
    {
        await Assert.That(DestinationBranch.For(Flight, Feature, ticket: null))
            .IsEqualTo("feature/GG-189");
    }

    [Test]
    public async Task A_tail_template_still_lives_under_gg()
    {
        // THE DEV TENANT'S ROOT v11, which must go on meaning what it says.
        await Assert.That(DestinationBranch.For(Flight, "{ticket}-{flight}", "18493"))
            .IsEqualTo("gg/18493-GG-189");
        await Assert.That(DestinationBranch.For(Flight, template: null, "18493"))
            .IsEqualTo("gg/GG-189");
    }

    [Test]
    public async Task Work_kept_for_a_takeover_stays_where_IsHandoff_looks()
    {
        var kept = DestinationBranch.ForHandoff(Flight, Feature, "18493");

        await Assert.That(kept).IsEqualTo("gg/handoff/feature/GG-189-18493");
        await Assert.That(DestinationBranch.IsHandoff(kept)).IsTrue()
            .Because("the runner reads this to tell a preservation from a landing, and a "
                   + "handoff it cannot see is reported as work offered for merge.");
    }

    [Test]
    public async Task A_landing_branch_is_never_read_as_a_handoff()
    {
        await Assert.That(DestinationBranch.IsHandoff(DestinationBranch.For(Flight, Feature, "18493")))
            .IsFalse();
    }

    [Test]
    public async Task The_full_ref_form_is_accepted_at_authoring()
    {
        await Assert.That(DestinationBranch.Validate(Feature)).IsNull();
    }

    [Test]
    public async Task A_full_ref_outside_the_branches_is_refused()
    {
        // A TAG OR A PULL REF IS NOT A BRANCH. Pushing a flight's work to
        // refs/tags/… would make it look released; refs/pull/… is the forge's.
        foreach (var template in (string[])["refs/tags/{flight}", "refs/pull/{flight}/head"])
        {
            await Assert.That(DestinationBranch.Validate(template)).IsNotNull()
                .Because($"'{template}' names a ref that is not a branch.");
        }
    }

    [Test]
    public async Task A_full_ref_still_needs_the_flight_number()
    {
        await Assert.That(DestinationBranch.Validate("refs/heads/feature/{ticket}")).IsNotNull()
            .Because("two flights on one ticket is the ordinary case, and the second push "
                   + "would want the first one's branch.");
    }
}
