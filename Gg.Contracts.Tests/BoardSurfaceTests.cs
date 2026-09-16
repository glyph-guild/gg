using System.Reflection;
using Gg.Contracts.Description;

namespace Gg.Contracts.Tests;

/// <summary>
/// The board's read surface, and who is allowed to open it.
/// </summary>
/// <remarks>
/// <para>
/// <b>DEVELOPER AUDIENCE, AND THE REASON IS THE ONE THE FLIGHT LIST ALREADY
/// GIVES.</b> A runner that could read the flight list could enumerate a
/// tenant's work from a credential meant only to let it hold one lease at a
/// time. The board is that list one step earlier — every nomination a tenant's
/// agents have made, including the ones nobody has decided about yet — so a
/// runner token opening it would be strictly worse.
/// </para>
/// <para>
/// <b>A nomination is not a flight and the summary must not pretend
/// otherwise.</b> Its identity is the row, and the flight is a member that is
/// null for every row that has not opened one. A summary keyed on a flight
/// would have nothing to say about the state the board exists to show.
/// </para>
/// <para>
/// <b>Every member is enumerated exactly, because the pressure on this type is
/// one direction.</b> A test that only forbade a list of bad names would pass
/// on the next member nobody thought of - <c>FlightNominationSurfaceTests</c>'
/// argument, and this record is read by a console that will want more.
/// </para>
/// </remarks>
public class BoardSurfaceTests
{
    [Test]
    public async Task The_board_is_read_on_a_developer_session()
    {
        var board = ProtocolSurface.Endpoints.Single(
            e => e.Path == "/v1/board" && e.Method == "GET");

        await Assert.That(board.Audience).IsEqualTo(Audience.Developer)
            .Because("the board is the flight list one step earlier, and a runner able to "
                   + "enumerate a tenant's nominations holds strictly more than one able to "
                   + "enumerate its flights - which is already refused.");

        await Assert.That(board.Response).IsEqualTo(typeof(BoardPage));
    }

    [Test]
    public async Task No_runner_credential_opens_the_board()
    {
        var board = ProtocolSurface.Endpoints.Single(
            e => e.Path == "/v1/board" && e.Method == "GET");

        await Assert.That(board.RequiredHeaders).Contains(ProtocolSurface.SessionHeader);
        await Assert.That(board.RequiredHeaders).DoesNotContain(ProtocolSurface.RunnerHeader);
    }

    [Test]
    public async Task A_summary_is_keyed_on_the_nomination_and_the_flight_is_a_member()
    {
        var members = typeof(NominationSummary)
            .GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Select(p => p.Name)
            .ToList();

        await Assert.That(members).IsEquivalentTo(new[]
        {
            nameof(NominationSummary.NominationId),
            nameof(NominationSummary.Nominator),
            nameof(NominationSummary.Subject),
            nameof(NominationSummary.Version),
            nameof(NominationSummary.WorkKind),
            nameof(NominationSummary.Mode),
            nameof(NominationSummary.State),
            nameof(NominationSummary.Ending),
            nameof(NominationSummary.Because),
            nameof(NominationSummary.FlightId),
            nameof(NominationSummary.FlightNumber),
            nameof(NominationSummary.MadeAt),
            nameof(NominationSummary.EndedAt),
            nameof(NominationSummary.IntentKey),
            nameof(NominationSummary.GatedBecause),
        });
    }

    [Test]
    public async Task A_gate_nobody_authored_says_so_as_a_member_rather_than_in_a_sentence()
    {
        // TWO WAYS TO BE GATED, AND A READER MUST NOT HAVE TO PARSE PROSE TO
        // TELL THEM APART. `opens-as: gated` is a person put in front of an
        // opening on purpose, and its reason is the destination somebody
        // wrote. A budget that ran out is the same word arrived at by
        // arithmetic, and its reason exists nowhere else - so it is carried
        // here, and its PRESENCE is the discriminator.
        //
        // WHY NOT A THIRD MODE WORD. `Mode` is the destination's `opens-as`,
        // and `DestinationOpening` reads two words and refuses a third - which
        // `ARepositoryDeclaresWhatItNominatesTests` holds at the registration
        // door. A third would make `opens-as: exhausted` an authorable value
        // in a destination document, and nobody authors exhaustion.
        var gated = typeof(NominationSummary).GetProperty(
            nameof(NominationSummary.GatedBecause))!;

        await Assert.That(gated.PropertyType).IsEqualTo(typeof(string));

        await Assert.That(new NominationSummary
        {
            NominationId = Guid.Parse("01a0792a-5e1f-7030-a5d8-52fd66e510b0"),
            Nominator = "observation:a-forge/acme/payments-service",
            Subject = "pull-request:a-forge/acme/payments-service#7",
            Version = "0f2c1a9b4e7d6c5a8b3f2e1d0c9b8a7f6e5d4c3b",
            WorkKind = "implement",
            Mode = DestinationOpening.Gated,
            State = NominationStates.Standing,
            MadeAt = DateTimeOffset.UnixEpoch,
        }.GatedBecause).IsNull()
            .Because("null is an AUTHORED gate: the destination says why, and the destination "
                   + "is the record. A sentence copied onto every authored row would be this "
                   + "repeating what a document already holds, and would leave the two kinds "
                   + "of gate indistinguishable again.");
    }

    [Test]
    public async Task A_row_says_what_it_is_about_and_says_nothing_when_it_cannot()
    {
        // THE THING A PERSON IS ACTUALLY DECIDING ABOUT. A line reading
        // "research-27" says which governance regime would apply and nothing
        // about which piece of work - and a board is a list somebody scans to
        // choose. The row already records the key; a summary that dropped it
        // made every renderer print a kind and call it a description.
        var key = typeof(NominationSummary).GetProperty(nameof(NominationSummary.IntentKey))!;

        await Assert.That(key.PropertyType).IsEqualTo(typeof(string));

        await Assert.That(new NominationSummary
        {
            NominationId = Guid.Parse("01a0792a-5e1f-7030-a5d8-52fd66e510b0"),
            Nominator = "flight:019260e0-1f6d-7a1e-9b53-6f2f4c9d0a11",
            Subject = "flight:019260e0-1f6d-7a1e-9b53-6f2f4c9d0a11",
            Version = "an-idempotency-key",
            WorkKind = "research-27",
            Mode = DestinationOpening.Gated,
            State = NominationStates.Standing,
            MadeAt = DateTimeOffset.UnixEpoch,
        }.IntentKey).IsNull()
            .Because("OPTIONAL, AND ABSENT MEANS UNCOUNTABLE. A free-text intent names "
                   + "nothing outside gg, so it has no key - and a hash of its words would "
                   + "look like the others and group nothing. Absent says 'this cannot be "
                   + "counted', which is true.");
    }

    [Test]
    public async Task A_summary_carries_no_flight_by_requirement()
    {
        // THE MEMBER THAT MUST BE NULLABLE, and the reason is what the board is
        // for. A standing row has no flight - that is the whole state - so a
        // required flight id would make the rows the board exists to show
        // unrepresentable.
        var flight = typeof(NominationSummary).GetProperty(nameof(NominationSummary.FlightId))!;
        var number = typeof(NominationSummary).GetProperty(nameof(NominationSummary.FlightNumber))!;

        await Assert.That(Nullable.GetUnderlyingType(flight.PropertyType)).IsNotNull()
            .Because("a standing nomination has opened no flight, which is the state the board "
                   + "exists to render.");
        await Assert.That(number.PropertyType).IsEqualTo(typeof(string));
    }

    [Test]
    public async Task An_ending_and_its_sentence_travel_together()
    {
        // Both nullable, and both for one reason: a standing row has neither.
        // A sentence without an ending would be a reason for something that has
        // not happened, so nothing declares one without the other.
        var ending = typeof(NominationSummary).GetProperty(nameof(NominationSummary.Ending))!;
        var because = typeof(NominationSummary).GetProperty(nameof(NominationSummary.Because))!;

        await Assert.That(ending.PropertyType).IsEqualTo(typeof(string));
        await Assert.That(because.PropertyType).IsEqualTo(typeof(string));
    }

    [Test]
    public async Task A_page_carries_its_rows_and_says_whether_it_showed_the_ended_ones()
    {
        // WHAT A READER CANNOT INFER FROM THE ROWS. A page of standing rows and
        // a page that happens to contain no ended ones look identical, and a
        // person reading "nothing was declined" off the second would be reading
        // a filter rather than a fact.
        var members = typeof(BoardPage)
            .GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Select(p => p.Name)
            .ToList();

        await Assert.That(members).IsEquivalentTo(new[]
        {
            nameof(BoardPage.Nominations),
            nameof(BoardPage.IncludedEnded),
        });
    }
}
