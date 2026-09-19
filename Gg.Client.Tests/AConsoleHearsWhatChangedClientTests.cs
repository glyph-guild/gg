using System.Net;
using System.Text;
using Gg.Client;
using Gg.Contracts;
using Gg.Contracts.Description;

namespace Gg.Client.Tests;

/// <summary>
/// The client opens the change stream and hands back each notice as it
/// arrives.
/// </summary>
/// <remarks>
/// <para>
/// <b>It hands back notices and nothing else.</b> What a console does with one
/// - which pane to re-read, whether to say anything - is the console's; this is
/// the wire, parsed.
/// </para>
/// <para>
/// <b>Server-sent events, parsed here rather than by a package.</b> The format
/// is lines: <c>event:</c>, <c>data:</c>, a comment starting with a colon, and
/// a blank line ending each event. That is a loop, and a dependency for a loop
/// is a dependency that has to stay AOT-clean forever.
/// </para>
/// </remarks>
public class AConsoleHearsWhatChangedClientTests
{
    private sealed class Answers(HttpStatusCode status, string body, string mediaType) : HttpMessageHandler
    {
        public HttpRequestMessage? Seen { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Seen = request;

            return Task.FromResult(new HttpResponseMessage(status)
            {
                Content = new StringContent(body, Encoding.UTF8, mediaType),

                // CARRIED BACK, as a real response does: whether a 401 means
                // "not signed in" turns on whether the REQUEST held a session.
                RequestMessage = request,
            });
        }
    }

    private static (ControlPlaneClient Client, Answers Handler) Streaming(
        string body, HttpStatusCode status = HttpStatusCode.OK)
    {
        var handler = new Answers(status, body, "text/event-stream");
        return (new ControlPlaneClient(
            new HttpClient(handler) { BaseAddress = new Uri("http://cp.test/") }), handler);
    }

    private static async Task<List<ChangeNotice>> AllOf(ControlPlaneClient client)
    {
        var notices = new List<ChangeNotice>();

        await foreach (var notice in client.ChangesAsync("a-session"))
        {
            notices.Add(notice);
        }

        return notices;
    }

    [Test]
    public async Task Ready_comes_first_and_then_each_change()
    {
        var (client, _) = Streaming(
            "event: ready\ndata: {}\n\n"
          + "event: changed\ndata: {\"topic\":\"flights\",\"id\":\"f-1\"}\n\n"
          + "event: changed\ndata: {\"topic\":\"gates\",\"id\":null}\n\n");

        var notices = await AllOf(client);

        await Assert.That(notices.Select(n => n.Topic))
            .IsEquivalentTo((string[])[ChangeTopics.Ready, ChangeTopics.Flights, ChangeTopics.Gates])
            .Because("ready is the connection's first word and asks for one full read; every "
                   + "change after it is a notice.");
        await Assert.That(notices[0].Id).IsNull();
        await Assert.That(notices[1].Id).IsEqualTo("f-1");
        await Assert.That(notices[2].Id).IsNull()
            .Because("a topic with no id means the whole pane, which is a real answer.");
    }

    [Test]
    public async Task A_keepalive_says_nothing()
    {
        var (client, _) = Streaming(
            "event: ready\ndata: {}\n\n"
          + ": keepalive\n\n"
          + ": keepalive\n\n"
          + "event: changed\ndata: {\"topic\":\"board\",\"id\":\"n-1\"}\n\n");

        var notices = await AllOf(client);

        await Assert.That(notices.Select(n => n.Topic))
            .IsEquivalentTo((string[])[ChangeTopics.Ready, ChangeTopics.Board])
            .Because("a comment line is the server proving the stream is alive; handing it "
                   + "back as a notice would have a console re-read on every heartbeat.");
    }

    [Test]
    public async Task An_event_it_does_not_know_is_skipped()
    {
        var (client, _) = Streaming(
            "event: ready\ndata: {}\n\n"
          + "event: something-newer\ndata: {\"whatever\":1}\n\n"
          + "event: changed\ndata: {\"topic\":\"runners\",\"id\":\"r-1\"}\n\n");

        var notices = await AllOf(client);

        await Assert.That(notices.Select(n => n.Topic))
            .IsEquivalentTo((string[])[ChangeTopics.Ready, ChangeTopics.Runners])
            .Because("a newer control plane may say more than this gg understands, and the "
                   + "right answer to a word it does not know is to keep listening.");
    }

    [Test]
    public async Task A_notice_it_cannot_read_does_not_end_the_stream()
    {
        var (client, _) = Streaming(
            "event: changed\ndata: {not json\n\n"
          + "event: changed\ndata: {\"topic\":\"flights\",\"id\":\"f-2\"}\n\n");

        var notices = await AllOf(client);

        await Assert.That(notices.Select(n => n.Id)).IsEquivalentTo((string?[])["f-2"])
            .Because("one doorbell nobody could read costs a refresh interval; ending the stream "
                   + "over it would cost every doorbell after it.");
    }

    [Test]
    public async Task Carriage_returns_and_split_data_lines_read_the_same()
    {
        var (client, _) = Streaming(
            "event: ready\r\ndata: {}\r\n\r\n"
          + "event: changed\r\ndata: {\"topic\":\"flights\",\r\ndata: \"id\":\"f-3\"}\r\n\r\n");

        var notices = await AllOf(client);

        await Assert.That(notices.Select(n => n.Topic))
            .IsEquivalentTo((string[])[ChangeTopics.Ready, ChangeTopics.Flights]);
        await Assert.That(notices[1].Id).IsEqualTo("f-3")
            .Because("the format allows CRLF and allows one event's data across several lines, "
                   + "joined by a newline - and a proxy is free to use either.");
    }

    [Test]
    public async Task It_asks_the_declared_door_as_a_signed_in_person()
    {
        var (client, handler) = Streaming("event: ready\ndata: {}\n\n");

        _ = await AllOf(client);

        var seen = handler.Seen!;
        await Assert.That(seen.Method).IsEqualTo(HttpMethod.Get);
        await Assert.That(seen.RequestUri!.AbsolutePath).IsEqualTo("/v1/changes");
        await Assert.That(ProtocolSurface.Endpoints.Any(
                e => e.Method == "GET" && e.Path == seen.RequestUri.AbsolutePath))
            .IsTrue();
        await Assert.That(seen.Headers.Contains(ProtocolSurface.SessionHeader)).IsTrue();

        foreach (var header in ProtocolSurface.VersionHeaders)
        {
            await Assert.That(seen.Headers.Contains(header)).IsTrue()
                .Because($"{header} is sent on every governed request.");
        }

        await Assert.That(seen.Headers.Accept.Select(a => a.MediaType))
            .Contains("text/event-stream");
    }

    [Test]
    public async Task A_session_the_control_plane_refuses_is_not_signed_in()
    {
        var (client, _) = Streaming("", HttpStatusCode.Unauthorized);

        await Assert.That(async () => await AllOf(client)).Throws<NotSignedInException>()
            .Because("the console offers to sign in for this exception and no other.");
    }

    [Test]
    public async Task A_gg_below_the_floor_is_told_so()
    {
        var (client, _) = Streaming("", HttpStatusCode.UpgradeRequired);

        await Assert.That(async () => await AllOf(client)).Throws<ProtocolTooOldException>();
    }

    [Test]
    public async Task A_control_plane_that_does_not_serve_it_says_so_distinctly()
    {
        var (client, _) = Streaming("", HttpStatusCode.NotFound);

        await Assert.That(async () => await AllOf(client))
            .Throws<ChangeStreamUnavailableException>()
            .Because("a control plane older than the stream is not a failure - the console "
                   + "goes on refreshing on its timer - and it can only do that if this is a "
                   + "different fact from the network being down.");
    }
}
