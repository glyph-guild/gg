using Gg.Contracts;
using Gg.Runner.Exposures;

namespace Gg.Runner.Tests;

/// <summary>
/// A loop whose kind exists to serve a preview cannot report success while
/// nothing answers.
/// </summary>
/// <remarks>
/// <para>
/// <b>Two flights, two causes, one end state.</b> GG-522 served its change,
/// verified it, and had the instance emptied a second later by the departure.
/// GG-524 backgrounded its image build and spent the rest of its budget waiting
/// on it — its last four commands were a <c>kill -0</c> poll, a <c>sleep 60</c>,
/// <c>true</c> and <c>:</c> — and never started the container at all. Both
/// reported <b>completed</b>. Both had gg publish an address, push a branch,
/// open a human gate and hold the machine out of service for twelve hours for a
/// 502.
/// </para>
/// <para>
/// <b>The platform believed the loop.</b> Nothing between "the agent stopped"
/// and "a person is asked to look" checks that there is anything to look at, so
/// a kind whose entire purpose is <i>serve it so somebody can open it</i> could
/// succeed having served nothing. Wording is not the fix: the first of those
/// flights followed its instructions exactly.
/// </para>
/// <para>
/// <b>MEASURED AT THE CONNECTOR'S ORIGIN, not at a declared port.</b> Nothing in
/// gg declares a port for a flight: <c>ExposureInventory.Port</c>,
/// <c>LeasePreview.Port</c> and the <c>PREVIEW_PORT</c> variable are three
/// unconnected answers, and <c>Envelope</c> settles that a flight may serve a
/// port but cannot be FOR one. ADR-0029 asks for "the flight's declared port",
/// which does not exist — and ADR-0033 shows the trap concretely, where
/// <c>ui-preview</c> says 8080, the app binds 4200, and traefik holds 8080, so
/// probing the declared port measures traefik. The origin the connector dials is
/// the one address a person reaches through <c>preview.url</c>, by construction.
/// </para>
/// <para>
/// <b>Every kind in the field is untouched</b>, because the condition is the
/// kind's own declaration: a work kind that wants a preview already says
/// <c>preview.url</c> in <c>produces:</c>, and one that does not says nothing.
/// </para>
/// </remarks>
public class ALoopThatServesNothingDidNotSucceedTests
{
    private static readonly IReadOnlyList<string> Previews = [FactKinds.PreviewUrl];

    /// <summary>A reach that always answers, as a served preview does.</summary>
    private static Task<string?> Answers(string origin, CancellationToken token) =>
        Task.FromResult<string?>(null);

    /// <summary>A reach that never answers, as both real flights' did.</summary>
    private static Task<string?> Refuses(string origin, CancellationToken token) =>
        Task.FromResult<string?>("connection refused");

    [Test]
    public async Task A_preview_kind_that_serves_nothing_is_refused_by_name()
    {
        var refusal = await PreviewAnswers.RefusalAsync(
            Previews, "http://localhost:8080", Refuses, CancellationToken.None);

        await Assert.That(refusal).IsNotNull()
            .Because("GG-522 and GG-524 both reported completed with nothing listening, and gg "
                   + "then asked a person to review a 502 and held the machine twelve hours.");
        await Assert.That(refusal).Contains("http://localhost:8080", StringComparison.Ordinal)
            .Because("the address it measured is the one fact a reader needs, and guessing "
                   + "which port was meant is what ADR-0029 got wrong.");
        await Assert.That(refusal).Contains("connection refused", StringComparison.Ordinal)
            .Because("what the probe saw beats this file's idea of what it means.");
    }

    [Test]
    public async Task A_preview_that_answers_is_not_refused()
    {
        await Assert.That(await PreviewAnswers.RefusalAsync(
                Previews, "http://localhost:8080", Answers, CancellationToken.None)).IsNull();
    }

    [Test]
    public async Task A_kind_that_asked_for_no_preview_is_untouched()
    {
        // EVERY KIND IN THE FIELD. `implement`, `plan`, `review`, a sweep - none
        // declares preview.url, and a check that refused them would fail every
        // flight this tenant flies for a reason none of them has.
        foreach (var produces in (IReadOnlyList<string>?[])
                 [null, [], [FactKinds.ChangeManifest, FactKinds.LoopOutcome]])
        {
            await Assert.That(await PreviewAnswers.RefusalAsync(
                    produces, "http://localhost:8080", Refuses, CancellationToken.None)).IsNull()
                .Because("the kind's own declaration is the condition, and silence is not a "
                       + "claim to anything.");
        }
    }

    [Test]
    public async Task A_preview_that_was_never_granted_an_address_is_not_the_loops_failure()
    {
        // NO ORIGIN IS NOT A FAILED LOOP. A slot that could not be brought up is
        // already narrated by PreviewUnserved, and ExposureServed's own remark
        // settles the principle: a preview that cannot be served is a flight
        // that still did its work. Refusing here would fail a loop for something
        // that happened before it started.
        await Assert.That(await PreviewAnswers.RefusalAsync(
                Previews, null, Refuses, CancellationToken.None)).IsNull();
    }

    [Test]
    public async Task The_origin_comes_from_the_grant_the_connector_was_dialled_with()
    {
        // THE OBSTACLE THIS REMOVES: ExposureServed dropped the port, so the one
        // address a person reaches was knowable at the moment of serving and
        // nowhere afterwards. It is built from the SAME value handed to the
        // connector, so it cannot become a second answer that disagrees.
        var connector = new RecordingConnector();

        var served = await new CloudflareExposureAdapter(connector).ServeAsync(
            new ExposureRequest
            {
                Preview = new LeasePreview
                {
                    Exposure = "jdapp",
                    Slot = 3,
                    Hostname = "jdapp-03.example.dev",
                    Credential = "local:exposure/jdapp-03",
                    Port = 8080,
                },
                Secret = "not-a-real-tunnel-token",
            },
            CancellationToken.None);

        await Assert.That(served.Origin).IsEqualTo($"http://localhost:{connector.Port}")
            .Because("TunnelFiles writes `service: http://localhost:{port}` from this same "
                   + "number, and two spellings of one address is a preview measured where "
                   + "nobody is serving.");
    }

    private sealed class RecordingConnector : IExposureConnector
    {
        internal int? Port { get; private set; }

        public Task<string?> RunAsync(
            string token, string hostname, int? port, CancellationToken cancellationToken)
        {
            Port = port;
            return Task.FromResult<string?>(null);
        }
    }
}
