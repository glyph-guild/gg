using System.Net;
using System.Net.Http.Json;
using Gg.Runner.Vcs;

namespace Gg.Runner.Tests;

/// <summary>
/// The dispatch seam takes a second provider that spells everything differently.
/// </summary>
/// <remarks>
/// <para>
/// <b>Planted rather than real, deliberately.</b> <c>HttpsDestinationAdapter</c>
/// names its own limit — <i>"The path shapes below are one convention; a
/// provider that spells them differently is a second adapter, not a special case
/// in this one"</i> — and the way to find out whether that is true is to write
/// the second adapter. A planted one costs no credential, no project and no
/// integration, and it proves the same thing about the seam.
/// </para>
/// <para>
/// <b>It proves DISPATCH and not correctness.</b> It shows the seam takes a
/// second provider; it says nothing about whether any particular forge's path
/// shapes are right. The slice that adds a real one must not read this green as
/// evidence about that forge.
/// </para>
/// <para>
/// <b>And <c>RunnerLoop</c> does not change.</b> Selection is already
/// <c>_destinations.FirstOrDefault(d => d.Provider == …)</c>, keyed on a string
/// the control plane owns. If a second provider needed a line there, the seam
/// would be a claim rather than a seam.
/// </para>
/// </remarks>
public class PlantedDestinationTests
{
    /// <summary>A provider that spells a proposal nothing like the first one.</summary>
    /// <remarks>
    /// Paths, body members and response members all differ — which is the point,
    /// because those are exactly what <c>HttpsDestinationAdapter</c> hard-codes.
    /// </remarks>
    private sealed class PlantedAdapter(string provider, HttpClient http) : IDestinationAdapter
    {
        public string Provider { get; } = provider;

        internal List<string> Asked { get; } = [];

        public Task<PushOutcome> PushAsync(
            LandingRequest request, CancellationToken cancellationToken = default) =>
            Task.FromResult<PushOutcome>(new PushOutcome.Pushed(request.Branch, "planted-commit"));

        public async Task<LandingOutcome> ProposeAsync(
            LandingRequest request, CancellationToken cancellationToken)
        {
            // A different shape at every point: a different path, different body
            // members, a different reference member coming back.
            var path = $"{request.Slug}/_apis/git/pullrequests?api-version=7.1";
            Asked.Add(path);

            using var created = await http.PostAsJsonAsync(
                path,
                new { sourceRefName = $"refs/heads/{request.Branch}", targetRefName = $"refs/heads/{request.BaseRef}" },
                cancellationToken);

            return new LandingOutcome.Landed(request.Branch, "https://planted.example/pr/11", 11);
        }
    }

    private static LandingRequest Landing(string slug) => new()
    {
        WorkingDirectory = Path.GetTempPath(),
        Slug = slug,
        Branch = "gg/GG-42",
        BaseRef = "main",
        Title = "GG-42: a change",
        Secret = "a-registered-credential",
    };

    private sealed class Accepts : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken) =>
            Task.FromResult(new HttpResponseMessage(HttpStatusCode.Created)
            {
                Content = new StringContent("{}", System.Text.Encoding.UTF8, "application/json"),
            });
    }

    // ---- the seam ----

    [Test]
    public async Task A_second_provider_registers_through_the_same_variable()
    {
        // THE HALF THAT NEEDED A SEAM. FromEnvironment constructed
        // HttpsDestinationAdapter unconditionally, so a second adapter could be
        // dispatched to and never REGISTERED - which would have made this a
        // proof about a list somebody hand-built rather than about the wiring
        // a runner really uses.
        var adapters = DestinationConfiguration.FromEnvironment(
            api => new HttpClient(new Accepts()) { BaseAddress = new Uri(api) },
            apis: "planted=https://api.planted.example/",
            hosts: "planted=planted.example",
            adapterFor: (provider, _, client) => new PlantedAdapter(provider, client));

        await Assert.That(adapters.Single().Provider).IsEqualTo("planted");
        await Assert.That(adapters.Single()).IsTypeOf<PlantedAdapter>();
    }

    [Test]
    public async Task Production_still_gets_the_adapter_it_always_did()
    {
        // The regression half, and the one a defaulted seam threatens: every
        // runner anybody has configured takes this path.
        var adapters = DestinationConfiguration.FromEnvironment(
            api => new HttpClient { BaseAddress = new Uri(api) },
            apis: "forge=https://api.forge.example/",
            hosts: "forge=forge.example.com");

        await Assert.That(adapters.Single()).IsTypeOf<HttpsDestinationAdapter>();
    }

    [Test]
    public async Task The_planted_provider_lands_through_its_own_shape()
    {
        var planted = new PlantedAdapter(
            "planted", new HttpClient(new Accepts()) { BaseAddress = new Uri("https://api.planted.example/") });

        var outcome = await planted.ProposeAsync(Landing("acme/widgets"), CancellationToken.None);

        await Assert.That(outcome).IsTypeOf<LandingOutcome.Landed>();
        await Assert.That(planted.Asked.Single()).Contains("_apis/git/pullrequests")
            .Because("a provider that spells the path differently is the case the seam exists "
                   + "for, and the shipped adapter's `repos/{slug}/pulls` is one convention "
                   + "rather than the convention.");
    }

    [Test]
    public async Task The_key_still_needs_a_host_as_well_as_an_api()
    {
        // The refusal the seam must not weaken: landing pushes a branch to the
        // git host AND asks an api, so a destination needs both declarations.
        // A planted adapter does not get to skip that.
        var thrown = Assert.Throws<InvalidOperationException>(() =>
            DestinationConfiguration.FromEnvironment(
                _ => new HttpClient(),
                apis: "planted=https://api.planted.example/",
                hosts: "forge=forge.example.com",
                adapterFor: (provider, _, client) => new PlantedAdapter(provider, client)));

        await Assert.That(thrown!.Message).Contains("planted");
    }
}
