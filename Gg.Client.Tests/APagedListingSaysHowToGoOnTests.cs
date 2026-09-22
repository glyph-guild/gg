using System.Net;
using Gg.Client;
using Gg.Contracts;

namespace Gg.Client.Tests;

/// <summary>
/// <c>gg flights</c> and <c>gg board</c> ask for one page, and the answer says
/// how to ask for the next.
/// </summary>
/// <remarks>
/// <para>
/// <b>A page by default.</b> Neither list had a cap, so both grew with a
/// tenant's history for ever; a person at a terminal is asking about the
/// newest work, not about two hundred rows of it. The size is the contract's
/// own <c>Paging.DefaultLimit</c>, so a page is the same length whoever asked.
/// </para>
/// <para>
/// <b>Said, because a truncated list that does not say so is a lie.</b> Rows
/// stop and nothing on screen explains why - which is the same defect as a
/// pane that shows a subset of what it fetched, one surface over.
/// </para>
/// <para>
/// <b>The cursor is handed back untouched.</b> It is composed by the side that
/// ordered the rows; this side prints it and sends it back.
/// </para>
/// </remarks>
public class APagedListingSaysHowToGoOnTests
{
    private static readonly StoredSession SignedIn = new()
    {
        SessionToken = "a-session",
        ExpiresAt = DateTimeOffset.UtcNow.AddHours(12),
        TenantId = "019fe062-d000-730c-a37d-7247342cd810",
        PrincipalDisplay = "kevin",
    };

    private sealed class Answering(string body) : HttpMessageHandler
    {
        internal HttpRequestMessage? Seen { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Seen = request;
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(body, System.Text.Encoding.UTF8, "application/json"),
            });
        }
    }

    private static FlightCommands Commands(Answering handler) =>
        new(new ControlPlaneClient(
                new HttpClient(handler) { BaseAddress = new Uri("https://cp.invalid/") }),
            new HeldSessionStore(SignedIn));

    private static string AFlightPage(string? next) =>
        System.Text.Json.JsonSerializer.Serialize(
            new FlightList
            {
                Flights =
                [
                    new FlightSummary
                    {
                        FlightId = "019fe815-6136-7518-bb57-b06d6d3f411a",
                        FlightNumber = "GG-208",
                        Name = "a thing somebody asked for",
                        CreatedAt = new DateTimeOffset(2026, 9, 20, 12, 0, 0, TimeSpan.Zero),
                        State = FlightStates.Open,
                        RunnerProtocolVersion = 1,
                        FactVocabularyVersion = "0.32.0",
                        ConstitutionVersion = "1.0.0",
                        EnvelopeVersion = "v12",
                        Intent = new FlightIntent
                        {
                            Kind = FlightIntentKinds.Text,
                            Text = "a thing somebody asked for",
                        },
                        Attempts = 0,
                        Facts = [],
                    },
                ],
                Next = next,
            },
            ProtocolJsonContext.Default.FlightList);

    private static string ABoardPage(string? next) =>
        System.Text.Json.JsonSerializer.Serialize(
            new BoardPage { Nominations = [], IncludedEnded = false, Next = next },
            ProtocolJsonContext.Default.BoardPage);

    [Test]
    public async Task Flights_asks_for_a_page_of_the_declared_size()
    {
        var handler = new Answering(AFlightPage(next: null));

        _ = await Commands(handler).ListAsync(all: true);

        await Assert.That(handler.Seen!.RequestUri!.Query)
            .Contains($"limit={Paging.DefaultLimit}")
            .Because("a person who named no size gets the page both sides agree on, not a "
                   + "tenant's whole history.");
    }

    [Test]
    public async Task A_named_size_and_a_cursor_reach_the_query()
    {
        var handler = new Answering(AFlightPage(next: null));

        _ = await Commands(handler).ListAsync(all: false, limit: 25, after: "R0ctMTA4");

        var query = handler.Seen!.RequestUri!.Query;

        await Assert.That(query).Contains("limit=25");
        await Assert.That(query).Contains("after=R0ctMTA4")
            .Because("the cursor is handed back as it was given; this side does not read it.");
    }

    [Test]
    public async Task A_flights_page_with_more_behind_it_says_how_to_go_on()
    {
        var handler = new Answering(AFlightPage(next: "R0ctMTA4"));

        var text = VerbOutput.ToText(await Commands(handler).ListAsync(all: false));

        await Assert.That(text).Contains("GG-208");
        await Assert.That(text).Contains("--after R0ctMTA4")
            .Because("rows that stop with nothing said about why read as a tenant with no "
                   + "more flights.");
    }

    [Test]
    public async Task A_last_page_says_nothing_about_carrying_on()
    {
        var handler = new Answering(AFlightPage(next: null));

        var text = VerbOutput.ToText(await Commands(handler).ListAsync(all: false));

        await Assert.That(text).DoesNotContain("--after")
            .Because("an instruction to fetch a page that is not there is worse than silence.");
    }

    [Test]
    public async Task The_board_pages_the_same_way_and_says_so()
    {
        var handler = new Answering(ABoardPage(next: "MTIzOmFiYw"));

        var result = await Commands(handler).BoardAsync(ended: false);

        await Assert.That(handler.Seen!.RequestUri!.Query)
            .Contains($"limit={Paging.DefaultLimit}");
        await Assert.That(VerbOutput.ToText(result)).Contains("--after MTIzOmFiYw");
    }

    [Test]
    public async Task A_board_page_that_is_all_of_them_says_nothing()
    {
        var handler = new Answering(ABoardPage(next: null));

        await Assert.That(VerbOutput.ToText(await Commands(handler).BoardAsync(ended: false)))
            .DoesNotContain("--after");
    }
}
