using System.Net;

namespace Gg.Client.Tests;

/// <summary>
/// The three headers every request must carry, including the request made by a
/// binary that cannot sign in.
/// </summary>
/// <remarks>
/// <para>
/// <b>The same defect, on a second method, found the same way.</b>
/// <see cref="RedeemStatesItsProtocolTests"/> records a pool member that died
/// with <i>"this gg is too old"</i> on a binary built minutes earlier, because
/// redeeming built its request with <c>new HttpRequestMessage(...)</c> and so
/// skipped the protocol header along with the credential it did not have. That
/// file's own remarks say what the fix should have been: <i>"this a test about
/// the helper, not about one call"</i>. It is a test about one call, and
/// <c>CurrentVersionAsync</c> has been building its own request ever since.
/// </para>
/// <para>
/// <b>Which door it is matters.</b> Asking what version is current is the
/// remedy for being below the floor — the one endpoint that declares no
/// <c>426</c>, reachable by a binary too old for everything else. So it is the
/// request most likely to come from a version somebody would want to know
/// about, and the only one that arrived saying nothing about itself. A control
/// plane counting who is behind cannot see the callers furthest behind.
/// </para>
/// <para>
/// <b>It cannot be refused for sending them.</b> The floor exempts this path
/// by reading the contract — an endpoint that declares no
/// <c>ProtocolTooOld</c> is one whose whole purpose a refusal would defeat —
/// so the headers are free to state the truth here. Nothing about this call
/// needed them omitted; the helper was simply not used.
/// </para>
/// <para>
/// <b>The structural half is the one that stops the third time.</b> A
/// behavioural test per method is a list to forget to add to, and this is the
/// second method to be forgotten. So the rule is asked of the shape instead:
/// a client that speaks these headers builds its requests in one place.
/// </para>
/// </remarks>
public class EveryRequestSaysWhichGgItIsTests
{
    /// <summary>Captures the request and answers as the control plane would.</summary>
    private sealed class Capturing : HttpMessageHandler
    {
        internal HttpRequestMessage? Seen { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Seen = request;

            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(
                    """{"version":"0.10.0"}""",
                    System.Text.Encoding.UTF8,
                    "application/json"),
            });
        }
    }

    private static ControlPlaneClient Against(Capturing handler) =>
        new(new HttpClient(handler) { BaseAddress = new Uri("https://control.example.invalid") });

    [Test]
    public async Task Asking_what_is_current_says_which_gg_is_asking()
    {
        // THE DEFECT. The population this endpoint exists for - binaries too
        // old to do anything else - is exactly the population the control plane
        // cannot count, because the one request they can make identifies
        // nothing.
        var handler = new Capturing();

        _ = await Against(handler).CurrentVersionAsync();

        await Assert.That(handler.Seen!.Headers.Contains(GgVersions.RunnerVersionHeader))
            .IsTrue()
            .Because("the control plane reads this header to say how far behind a fleet is, and "
                   + "the call that asks what to install is the one it would most want to hear "
                   + "from.");
        await Assert.That(handler.Seen.Headers.Contains(GgVersions.ProtocolHeader))
            .IsTrue()
            .Because("every other request states it, and the helper's own summary says all three "
                   + "are what every request must carry.");
        await Assert.That(handler.Seen.Headers.Contains(GgVersions.FactVocabularyHeader))
            .IsTrue()
            .Because("a vocabulary that travels on every request except one is a vocabulary "
                   + "whose absence means nothing.");
    }

    [Test]
    public async Task It_still_presents_no_credential()
    {
        // THE CONTROL ON THE FIX. The helper's session token has always been
        // optional, and this call must keep passing none: what the current gg
        // is, is not tenant knowledge, and a machine that cannot sign in is
        // exactly the machine most likely to need the answer.
        var handler = new Capturing();

        _ = await Against(handler).CurrentVersionAsync();

        await Assert.That(handler.Seen!.Headers.Contains(GgVersions.SessionHeader)).IsFalse();
        await Assert.That(handler.Seen.Headers.Authorization).IsNull();
    }

    [Test]
    public async Task A_client_that_speaks_these_headers_builds_its_requests_in_one_place()
    {
        // FOUND BY SHAPE, not by a list of methods or of files. Two calls have
        // now been written by hand, in two different clients' worth of years,
        // and both times every existing test stayed green - because the headers
        // are added where a request is CONSTRUCTED, so a second construction is
        // a second place they can be left out.
        //
        // A file that names these headers is a client that speaks the protocol.
        // A third one gets this rule without anybody remembering to add it, and
        // a legitimate second construction has to be argued rather than
        // quietly appended.
        var clients = Directory
            .EnumerateFiles(Root(), "*.cs", SearchOption.AllDirectories)
            .Where(f => !f.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}",
                            StringComparison.Ordinal)
                     && !f.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}",
                            StringComparison.Ordinal)
                     && !f.Contains(".Tests", StringComparison.Ordinal))
            .Select(f => (Path: f, Text: File.ReadAllText(f)))
            .Where(f => f.Text.Contains("RunnerVersionHeader", StringComparison.Ordinal)
                     && f.Text.Contains("new HttpRequestMessage", StringComparison.Ordinal))
            .ToList();

        await Assert.That(clients).IsNotEmpty()
            .Because("no production file both names the version header and builds a request, so "
                   + "this guard is asking about nothing and would go on passing after one did.");

        var offenders = clients
            .Select(c => (
                Name: Path.GetFileName(c.Path),
                Built: c.Text.Split("new HttpRequestMessage", StringSplitOptions.None).Length - 1))
            .Where(c => c.Built > 1)
            .Select(c => $"{c.Name}: {c.Built}")
            .ToList();

        await Assert.That(offenders).IsEmpty()
            .Because("a request built outside the one helper carries whichever headers whoever "
                   + "wrote it remembered, and the refusal that produces names the wrong cause - "
                   + "'this gg is too old' about a binary built minutes ago. Found: "
                   + string.Join(" | ", offenders));
    }

    private static string Root()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);

        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "Gg.sln")))
        {
            directory = directory.Parent;
        }

        return (directory ?? throw new InvalidOperationException("Gg.sln not found")).FullName;
    }
}
