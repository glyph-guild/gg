using Gg.Contracts.Description;

namespace Gg.Contracts.Tests;

/// <summary>
/// A list answers with a page and says where it stopped, so a reader can ask
/// for the next one.
/// </summary>
/// <remarks>
/// <para>
/// <b>Asked for 2026-09-20</b>: infinite scroll on flights and on the board.
/// Neither read has a cap today - the flights list performs three unbounded
/// whole-tenant reads on every poll, and the console re-asks every thirty
/// seconds - so both grow with a tenant's history for ever.
/// </para>
/// <para>
/// <b>The envelope was built for this.</b> <see cref="FlightList"/>'s own
/// remark says a bare array "has nowhere to put the paging this will grow",
/// which is what these two members are.
/// </para>
/// <para>
/// <b>The cursor is opaque, and that is the point.</b> What a page stopped at
/// is the control plane's to compose and to read back; a client that parsed it
/// would be a second implementation of an ordering it does not own. Here it is
/// a string that travels out and comes back.
/// </para>
/// <para>
/// <b>Absent means the end.</b> A cursor on the last page would send a reader
/// round once more for nothing, and an empty string is a cursor somebody has
/// to decide about twice.
/// </para>
/// </remarks>
public class AListSaysWhereItStoppedTests
{
    [Test]
    public async Task A_flight_list_can_say_where_it_stopped()
    {
        var page = new FlightList { Flights = [], Next = "GG-108" };

        await Assert.That(page.Next).IsEqualTo("GG-108");
        await Assert.That(new FlightList { Flights = [] }.Next).IsNull()
            .Because("every answer written before paging says nothing about it, and the "
                   + "absence has to read as 'this is all of it'.");
    }

    [Test]
    public async Task A_board_page_can_say_where_it_stopped()
    {
        var page = new BoardPage { Nominations = [], IncludedEnded = false, Next = "c3Rvcg" };

        await Assert.That(page.Next).IsEqualTo("c3Rvcg");
        await Assert.That(new BoardPage { Nominations = [], IncludedEnded = false }.Next).IsNull();
    }

    [Test]
    public async Task Both_answers_declare_the_member_on_the_wire()
    {
        // THE SURFACE IS WHAT THE OTHER SIDE CONFORMS TO. A member the control
        // plane serializes and this list does not name is one its own
        // conformance test cannot see.
        await Assert.That(ProtocolSurface.JsonMembers[typeof(FlightList)]).Contains("next");
        await Assert.That(ProtocolSurface.JsonMembers[typeof(BoardPage)]).Contains("next");
    }

    [Test]
    public async Task A_page_is_a_hundred_unless_somebody_says_otherwise()
    {
        // ONE NUMBER, DECLARED WHERE BOTH SIDES READ IT. The control plane
        // clamps to it and gg asks with it; two spellings of a page size is two
        // pages of different length depending on who asked.
        await Assert.That(Paging.DefaultLimit).IsEqualTo(100);
        await Assert.That(Paging.MaxLimit).IsGreaterThanOrEqualTo(Paging.DefaultLimit);
    }

    [Test]
    public async Task A_limit_nobody_can_serve_is_refused_by_name()
    {
        foreach (var refused in (int[])[0, -1, Paging.MaxLimit + 1])
        {
            await Assert.That(Paging.Validate(refused)).IsNotNull()
                .Because($"{refused} is not a page size, and a door that silently substituted "
                       + "one would answer a question nobody asked.");
        }

        await Assert.That(Paging.Validate(1)).IsNull();
        await Assert.That(Paging.Validate(Paging.DefaultLimit)).IsNull();
        await Assert.That(Paging.Validate(Paging.MaxLimit)).IsNull();
    }

    [Test]
    public async Task The_refusal_says_what_the_bounds_are()
    {
        var refused = Paging.Validate(Paging.MaxLimit + 1);

        await Assert.That(refused!).Contains(Paging.MaxLimit.ToString(
            System.Globalization.CultureInfo.InvariantCulture))
            .Because("a bound a person cannot read from the refusal is one they discover by "
                   + "bisecting.");
    }
}
