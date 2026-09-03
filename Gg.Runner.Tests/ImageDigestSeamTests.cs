using System.Net;
using System.Text;
using Gg.Contracts;
using Gg.Runner.Facts;
using Gg.Runner.Pools;

namespace Gg.Runner.Tests;

/// <summary>
/// A flight that ran in an environment the platform MADE says so, and one that
/// ran on a machine it merely found does not.
/// </summary>
/// <remarks>
/// <para>
/// <b>The fact could not tell them apart, and its own doc comment said it
/// could.</b> <c>EnvironmentIdentity.Provenance</c> is documented as <i>"whether
/// this environment was made for this flight or found"</i>, and the value that
/// reaches it comes from <c>workspace.Reused</c> → <c>AlreadyHeld(flightId)</c>
/// → <c>Directory.Exists</c>. It answers <i>did this flight already have a git
/// checkout on this box</i>. A bare laptop's first flight reports <c>fresh</c>,
/// identical to a container the pool created a second ago.
/// </para>
/// <para>
/// The correct signal exists, on the pool ADAPTER — <c>CreateAndStartAsync</c>
/// alone reports <c>Fresh</c> — but it lands on an attestation, and slice
/// twelve kept attestations out of the fact vocabulary deliberately. Two
/// records, the same word, and they never join.
/// </para>
/// <para>
/// <b>The seam that joins them was already declared and set by nothing.</b>
/// <c>EnvironmentSurvey.ImageDigestVariable</c> is read on every fact ship,
/// <c>ImageDigest != null</c> is the documented <i>"running in this image"</i>
/// signal — and <c>GG_IMAGE_DIGEST</c> appears exactly once in this repository:
/// its own declaration. That is <c>GG_DESTINATION_APIS</c>'s shape one slice
/// later, and the fix is to set it where the platform makes the environment.
/// </para>
/// <para>
/// <b>What this file does NOT prove</b> is the joined sentence end to end — a
/// runner inside a warmed member shipping a fact the control plane reads. That
/// needs a running control plane and belongs to the walk. Here: the spec
/// carries the value, and the survey turns it into a fact that differs.
/// </para>
/// </remarks>
public class ImageDigestSeamTests
{
    private const string Pinned =
        "ghcr.io/acme/env@sha256:" + "3333333333333333333333333333333333333333333333333333333333333333";

    /// <summary>A daemon that keeps the create body, which is the thing under test.</summary>
    private sealed class SpecRecordingDaemon : HttpMessageHandler
    {
        public string? CreateBody { get; private set; }

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var path = request.RequestUri!.AbsolutePath;

            if (path.EndsWith("/create", StringComparison.Ordinal))
            {
                CreateBody = await request.Content!.ReadAsStringAsync(cancellationToken);
                return new HttpResponseMessage(HttpStatusCode.Created);
            }

            if (path.EndsWith("/json", StringComparison.Ordinal))
            {
                return CreateBody is null
                    ? new HttpResponseMessage(HttpStatusCode.NotFound)
                    : new HttpResponseMessage(HttpStatusCode.OK)
                    {
                        Content = new StringContent(
                            "{\"State\":{\"Running\":true,\"Status\":\"running\"},"
                          + "\"Image\":\"sha256:" + new string('c', 64) + "\","
                          + "\"Config\":{\"Image\":\"" + Pinned + "\"}}",
                            Encoding.UTF8, "application/json"),
                    };
            }

            return new HttpResponseMessage(HttpStatusCode.NoContent);
        }
    }

    [Test]
    public async Task A_member_the_platform_makes_is_told_which_image_it_is()
    {
        var daemon = new SpecRecordingDaemon();
        var adapter = new DockerPoolAdapter(
            new HttpClient(daemon) { BaseAddress = new Uri("http://pull-point") });

        _ = await adapter.RefreshAsync("gg-pool", "gg-pool-1", Spec(Pinned));

        await Assert.That(daemon.CreateBody!).Contains(EnvironmentSurvey.ImageDigestVariable)
            .Because("the variable the survey reads on every fact ship was declared, read, "
                   + "and set by NOTHING - so ImageDigest was always null and 'running in "
                   + "this image' was a signal nothing could ever send.");
        await Assert.That(daemon.CreateBody!).Contains(Pinned)
            .Because("and the value is the pinned reference the strategy names, so the fact "
                   + "and the attestation name the same image rather than two spellings "
                   + "of it.");
    }

    [Test]
    public async Task The_spec_carries_the_image_the_label_and_nothing_else()
    {
        // THE COMMENT THAT HAS TO STAY TRUE. The create spec said "the image and
        // nothing else"; it is now the image, one label, and one non-secret
        // variable naming the image itself. That is a boundary claim, so it is
        // asserted rather than left to a reader - a second variable arriving
        // here later is exactly the drift ADR-0024 is about.
        var daemon = new SpecRecordingDaemon();
        var adapter = new DockerPoolAdapter(
            new HttpClient(daemon) { BaseAddress = new Uri("http://pull-point") });

        _ = await adapter.RefreshAsync("gg-pool", "gg-pool-1", Spec(Pinned));

        using var spec = System.Text.Json.JsonDocument.Parse(daemon.CreateBody!);
        var members = spec.RootElement.EnumerateObject().Select(p => p.Name).ToArray();

        await Assert.That(members).IsEquivalentTo((string[])["Image", "Labels", "Env"])
            .Because("no binds, no HostConfig, nothing privileged - a pool member is the "
                   + "image, the label that says which pool, and what it needs to become a "
                   + "runner. HostConfig would pass the proxy unexamined.");

        // REVISED, DELIBERATELY. This asserted ONE variable and said "it carries
        // no credential - what a member can reach is decided by the image it was
        // built from, not by what we inject". That was true and right while a
        // member was inert scenery, and it is exactly what kept any member from
        // ever running a flight: nothing could tell one where to call or who to
        // be, so the only working example baked a developer's session into an
        // image.
        //
        // Three now: which image it is, where to answer, and a single-use nonce
        // to become somebody with. NOT what it may advertise - that comes back
        // with the credential it redeems, so putting it here as well would be a
        // second source of truth for the one value this slice exists to stop
        // taking on a runner's word.
        //
        // The old sentence's real content survives in the test below: what is
        // injected is still not a credential.
        await Assert.That(spec.RootElement.GetProperty("Env").GetArrayLength()).IsEqualTo(3)
            .Because("a member is a runner now, and a runner needs an address and an identity "
                   + "it can obtain.");
    }

    [Test]
    public async Task What_is_injected_is_still_never_a_credential()
    {
        // THE HALF OF THE OLD PRINCIPLE THAT SURVIVES, kept as its own
        // assertion so revising the count above could not quietly take it too.
        // An inspect of a member is permitted through the scope proxy, so a
        // token here would be readable for the life of the container.
        var daemon = new SpecRecordingDaemon();
        var adapter = new DockerPoolAdapter(
            new HttpClient(daemon) { BaseAddress = new Uri("http://pull-point") });

        _ = await adapter.RefreshAsync("gg-pool", "gg-pool-1", Spec(Pinned));

        await Assert.That(daemon.CreateBody!.Contains("TOKEN", StringComparison.OrdinalIgnoreCase))
            .IsFalse();
        await Assert.That(daemon.CreateBody!.Contains("SESSION", StringComparison.OrdinalIgnoreCase))
            .IsFalse();
    }

    [Test]
    public async Task A_made_environment_and_a_found_one_produce_different_facts()
    {
        // THE SENTENCE, at the level this file can hold it. The survey reads
        // the variable the spec now sets, so a runner inside a made member
        // reports the image and one on a bare host reports null - which is the
        // documented meaning of ImageDigest, finally sendable.
        var made = EnvironmentSurvey.Observe(
            treePath: null, EnvironmentProvenance.Reused, imageDigest: Pinned);
        var found = EnvironmentSurvey.Observe(
            treePath: null, EnvironmentProvenance.Reused, imageDigest: null);

        await Assert.That(made.ImageDigest).IsEqualTo(Pinned);
        await Assert.That(found.ImageDigest).IsNull()
            .Because("absent is the honest answer on a laptop, and it is a DIFFERENT answer "
                   + "from an image - which is the whole distinction the fact was "
                   + "documented to carry and could not.");
        await Assert.That(made).IsNotEqualTo(found)
            .Because("two flights, one in an environment the platform made and one on a "
                   + "machine it found, no longer produce the same environment.identity.");
        await Assert.That(made.Provenance).IsEqualTo(found.Provenance)
            .Because("and Provenance is NOT what distinguishes them - it says the same word "
                   + "for both, because it answers a different question than its own doc "
                   + "comment claims. Recorded rather than renamed: renaming a fact member "
                   + "is a vocabulary change this slice does not need to make the sentence "
                   + "true.");
    }
    /// <summary>
    /// A member spec around an image, for tests whose subject is not the spec.
    /// </summary>
    /// <remarks>
    /// The nonce is present because a member without one is refused before
    /// anything is created - see MemberIsARunnerTests, which is where that
    /// refusal is the subject.
    /// </remarks>
    private static MemberSpec Spec(string image) => new()
    {
        Image = image,
        ControlPlane = "https://control.example.invalid",
        Nonce = "a-nonce",
    };

}
