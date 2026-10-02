using Gg.Contracts;
using Gg.Contracts.Description;

namespace Gg.Cli.Tests;

/// <summary>
/// A nomination may name the leg it follows, and nothing is ordered by accident.
/// </summary>
/// <remarks>
/// <para>
/// <b>Slice fifty-seven step 1, and the argument is not the warm environment
/// that prompted it.</b> A plan's legs all become claimable at once, so on a
/// one-runner tenant a three-leg plan runs in whatever order the queue happens
/// to offer — every plan whose legs truly depend on each other has been relying
/// on luck, and nothing said so.
/// </para>
/// <para>
/// <b>A SUBJECT, not an index</b> (rule 1). A plan's legs are rarely a total
/// order; two of three are often independent, and an index would make an agent
/// invent a sequence it does not believe in. The subject is the identifier the
/// agent already supplies and that <c>plan</c> already insists on passing as its
/// own argument — <i>"calls that do not differ in the subject ARGUMENT are taken
/// as the same piece of work"</i>.
/// </para>
/// <para>
/// <b>Silence means no constraint</b> (rule 2), which is every nomination in the
/// field. Null and absent are the same answer, and neither is a claim to
/// anything.
/// </para>
/// <para>
/// <b>What this step cannot check.</b> Whether the subject named exists is a
/// question about the OTHER nominations of the same plan, and a single
/// nomination does not know them — the control plane resolves it when the board
/// is approved (S57.3-01), and a cycle is refused there too (rule 6), because an
/// agent nominating one leg at a time cannot see the cycle it is about to close.
/// What is refused here is only what one nomination can be wrong about by
/// itself.
/// </para>
/// </remarks>
public class ANominationMaySayWhatItFollowsTests
{
    private static FlightNomination ALeg(string? after = null, string? subject = "the-schema") => new()
    {
        WorkKind = "implement",
        Reason = "the page cannot be built until the schema it reads exists",
        Subject = subject,
        After = after,
    };

    [Test]
    public async Task A_leg_may_name_the_one_it_follows()
    {
        await Assert.That(FlightNomination.Validate(ALeg(after: "the-schema"))).IsNull();
        await Assert.That(ALeg(after: "the-schema").After).IsEqualTo("the-schema");
    }

    [Test]
    public async Task A_leg_that_names_none_is_unchanged()
    {
        // EVERY NOMINATION IN THE FIELD. Absence is not a claim to anything, and
        // a nomination that said nothing about order before this slice says
        // nothing about order after it.
        await Assert.That(FlightNomination.Validate(ALeg(after: null))).IsNull();
        await Assert.That(ALeg().After).IsNull();
    }

    [Test]
    public async Task A_leg_cannot_follow_itself()
    {
        // THE ONE CYCLE A SINGLE NOMINATION CAN SEE, so it is the one refused
        // here. Every longer cycle needs the other legs, which is the board's to
        // check when it has them all.
        await Assert.That(FlightNomination.Validate(ALeg(after: "the-schema", subject: "the-schema")))
            .IsNotNull()
            .Because("a leg that waits for itself never runs, and nothing downstream would "
                   + "ever say why.");
    }

    [Test]
    public async Task A_leg_with_no_subject_cannot_follow_anything()
    {
        // AN UNNAMEABLE LEG CANNOT BE ORDERED. The board supersedes per
        // (nominator, subject), so legs without subjects collapse into one row -
        // and an edge into a row that is about to be overwritten is an order
        // nobody can honour.
        await Assert.That(FlightNomination.Validate(ALeg(after: "the-schema", subject: null)))
            .IsNotNull()
            .Because("a nomination with no subject is one piece of work, and one piece of "
                   + "work has nothing to follow.");
    }

    [Test]
    public async Task It_is_bounded_like_the_subject_it_names()
    {
        // THE SAME BOUND AS SUBJECT, because it holds one. A longer value is a
        // subject that cannot exist, so it cannot be the subject of any leg.
        await Assert.That(FlightNomination.Validate(
                ALeg(after: new string('x', FlightNomination.MaxSubject + 1)))).IsNotNull();
    }

    [Test]
    public async Task It_travels_on_the_wire()
    {
        await Assert.That(ProtocolSurface.JsonMembers[typeof(FlightNomination)]).Contains("after");
    }

    [Test]
    public async Task The_tool_offers_it_and_says_what_it_names()
    {
        // THE ARGUMENT'S NAME IS THE CONTRACT'S, not the tool's own word for it.
        // Two spellings of one argument is a nomination an agent writes and the
        // platform drops.
        await Assert.That(Gg.Local.NominationTool.After).IsEqualTo("after");
    }
}
