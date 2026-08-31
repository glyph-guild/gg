using Gg.Runner.Vcs;

namespace Gg.Runner.Tests;

/// <summary>
/// A proposal opens on a real forge through the wiring production builds.
/// </summary>
/// <remarks>
/// <para>
/// <b>Slice nineteen's step 6, which that slice cut for want of a fixture.</b>
/// Its close says the landing path is <i>proven against a stub</i>, and this is
/// the assertion that replaces it: a real branch, a real proposal, on a real
/// forge, with the client production actually constructs.
/// </para>
/// <para>
/// <b>The client is ANONYMOUS, and that is the entire point.</b>
/// <c>DestinationConfiguration.FromEnvironment</c> builds
/// <c>new HttpClient { BaseAddress = … }</c> and nothing else; the credential
/// rides on the request. <c>AgainstRealRemoteTests</c> hands the adapter a
/// client it has already put a bearer token on — which is why a real-forge suite
/// ran green over an unauthenticated proposal for months. A test that fakes the
/// credential asks whether the shape compiles, not whether the service accepts
/// it.
/// </para>
/// <para>
/// <b>It leaves the fixture as it found it.</b> The proposal is closed and the
/// branch deleted, because a test that accumulates state in a shared repository
/// is one nobody can run twice.
/// </para>
/// </remarks>
[Category("RealRemote")]
public class RealProposalWiringTests
{
    private static string Required(string variable) =>
        Environment.GetEnvironmentVariable(variable) is { Length: > 0 } value
            ? value
            : throw new InvalidOperationException(
                $"{variable} is not set. This needs a real fixture repository and a credential "
              + "that may write to it; it is excluded from CI by category for that reason.");

    [Test]
    public async Task A_proposal_opens_through_the_client_production_builds()
    {
        var slug = Required("GG_FIXTURE_SLUG");
        var secret = Required("GG_FIXTURE_SECRET");
        var host = Required("GG_FIXTURE_HOST");
        var branch = $"gg/wiring-{DateTimeOffset.UtcNow.ToUnixTimeSeconds()}";

        // THE PRODUCTION PATH, built the way a runner builds it: from the two
        // environment declarations, with a client that carries no credential.
        var adapter = DestinationConfiguration.FromEnvironment(
            api => new HttpClient { BaseAddress = new Uri(api) },
            apis: $"{host}={Required("GG_FIXTURE_API")}",
            hosts: $"{host}={host}").Single();

        var work = Directory.CreateTempSubdirectory("gg-wiring-").FullName;
        try
        {
            var tree = Path.Combine(work, "r");
            await GitAsync(work, secret, "clone", "--depth", "1", $"https://{host}/{slug}.git", "r");
            await GitAsync(tree, secret, "checkout", "-b", branch);
            await File.WriteAllTextAsync(Path.Combine(tree, ".gg-wiring"), branch);
            await GitAsync(tree, secret, "add", ".gg-wiring");
            await GitAsync(tree, secret, "-c", "user.email=probe@example.invalid",
                "-c", "user.name=probe", "commit", "-m", "wiring probe");
            await GitAsync(tree, secret, "push", "origin", branch);

            var request = new LandingRequest
            {
                WorkingDirectory = tree,
                Slug = slug,
                Branch = branch,
                BaseRef = "main",
                Title = "probe: a governed flight proposes",
                Secret = secret,
            };

            var outcome = await adapter.ProposeAsync(request, CancellationToken.None);

            await Assert.That(outcome).IsTypeOf<LandingOutcome.Landed>()
                .Because("this is the arrangement that hid the defect: an anonymous client and "
                       + $"the credential on the request. Got: {outcome}");

            var landed = (LandingOutcome.Landed)outcome;
            await Assert.That(landed.Number).IsGreaterThan(0);
            await Assert.That(landed.Uri).Contains(slug);

            // AND THE IDEMPOTENCY QUERY IS AUTHENTICATED, which is the half a
            // stub can only assert about itself. Asked again, the same branch
            // must answer with the SAME proposal rather than opening a second.
            var again = await adapter.ProposeAsync(request, CancellationToken.None);

            await Assert.That(again).IsTypeOf<LandingOutcome.Landed>();
            await Assert.That(((LandingOutcome.Landed)again).Number).IsEqualTo(landed.Number)
                .Because("an unauthenticated query does not error - it fails its success check "
                       + "and degrades silently to 'there is no existing proposal', which opens "
                       + "a second one on a branch that already had one.");

            await CloseAsync(secret, slug, landed.Number);
            await GitAsync(tree, secret, "push", "origin", "--delete", branch);
        }
        finally
        {
            Directory.Delete(work, recursive: true);
        }
    }

    private static async Task GitAsync(string directory, string secret, params string[] arguments)
    {
        var info = new System.Diagnostics.ProcessStartInfo("git")
        {
            WorkingDirectory = directory,
            RedirectStandardError = true,
            RedirectStandardOutput = true,
            Environment = { ["GIT_TERMINAL_PROMPT"] = "0" },
        };
        info.ArgumentList.Add("-c");
        info.ArgumentList.Add(
            $"credential.helper=!f() {{ echo username=x-access-token; echo password={secret}; }}; f");
        foreach (var argument in arguments)
        {
            info.ArgumentList.Add(argument);
        }

        using var git = System.Diagnostics.Process.Start(info)!;
        await git.WaitForExitAsync();

        if (git.ExitCode != 0)
        {
            throw new InvalidOperationException(
                $"git {string.Join(' ', arguments)}: {await git.StandardError.ReadToEndAsync()}");
        }
    }

    private static async Task CloseAsync(string secret, string slug, int proposal)
    {
        using var http = new HttpClient { BaseAddress = new Uri(Required("GG_FIXTURE_API")) };
        using var request = new HttpRequestMessage(
            HttpMethod.Patch, $"repos/{slug}/pulls/{proposal}")
        {
            Content = new StringContent(
                """{"state":"closed"}""", System.Text.Encoding.UTF8, "application/json"),
        };
        request.Headers.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", secret);
        request.Headers.UserAgent.Add(new System.Net.Http.Headers.ProductInfoHeaderValue("gg-tests", "1"));

        using var response = await http.SendAsync(request);
        response.EnsureSuccessStatusCode();
    }
}
