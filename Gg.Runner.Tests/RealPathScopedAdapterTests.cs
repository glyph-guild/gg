using Gg.Runner.Vcs;

namespace Gg.Runner.Tests;

/// <summary>
/// The path-scoped adapters, against a real service.
/// </summary>
/// <remarks>
/// <b>Category RealRemote, and excluded from CI by name</b> — it needs a real
/// project and a real credential, and it opens and abandons a real proposal.
/// Every other assertion about these adapters is over a stub, which is exactly
/// the arrangement that hid an unauthenticated proposal for months: a test that
/// fakes the credential is asking whether the shape compiles, not whether the
/// service accepts it.
/// </remarks>
[Category("RealRemote")]
public class RealPathScopedAdapterTests
{
    private static string Required(string variable) =>
        Environment.GetEnvironmentVariable(variable) is { Length: > 0 } value
            ? value
            : throw new InvalidOperationException(
                $"{variable} is not set. This suite needs a real project and a real credential; "
              + "it is excluded from CI by category for that reason.");

    /// <summary>The host, from configuration, because naming one here is the
    /// violation this repository's neutrality guard exists to catch.</summary>
    private static string Host => $"{Required("GG_ADO_HOST")}/{Required("GG_ADO_ORG")}";

    private static string Slug => $"{Required("GG_ADO_PROJECT")}/{Required("GG_ADO_REPO")}";

    [Test]
    public async Task The_clone_url_this_adapter_builds_is_one_the_service_serves()
    {
        // The whole reason this is a second adapter: the suffix the other one
        // appends is REFUSED here, and that was measured rather than assumed.
        var url = new PathScopedGitVcsAdapter("forge", Host).CloneUrlFor(new RepoTarget
        {
            Provider = "forge",
            Slug = Slug,
            PinnedRef = "refs/heads/main",
        });

        using var git = System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo("git")
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            Environment = { ["GIT_TERMINAL_PROMPT"] = "0" },
            ArgumentList =
            {
                "-c",
                "credential.helper=!f() { echo username=x-access-token; "
              + $"echo password={Required("GG_ADO_SECRET")}; }}; f",
                "ls-remote", url, "HEAD",
            },
        })!;

        var output = await git.StandardOutput.ReadToEndAsync();
        await git.WaitForExitAsync();

        await Assert.That(git.ExitCode).IsEqualTo(0)
            .Because($"'{url}' is what this adapter builds, and the service has to serve it. "
                   + await git.StandardError.ReadToEndAsync());
        await Assert.That(output).Contains("HEAD");
    }

    [Test]
    public async Task A_proposal_opens_through_the_production_wiring_and_is_abandoned()
    {
        // THE HALF THAT HAS NEVER RUN, for any provider. The destination client
        // production builds carries no credential of its own - `new HttpClient
        // { BaseAddress = ... }` and nothing else - so this is the arrangement
        // that hid an unauthenticated proposal, exercised deliberately.
        //
        // It creates a real branch and a real proposal, and abandons both. A
        // walk that left either behind would be a test that changes the project
        // it is run against.
        var branch = $"gg/probe-{DateTimeOffset.UtcNow.ToUnixTimeSeconds()}";
        var secret = Required("GG_ADO_SECRET");
        var url = new PathScopedGitVcsAdapter("forge", Host).CloneUrlFor(new RepoTarget
        {
            Provider = "forge", Slug = Slug, PinnedRef = "refs/heads/main",
        });

        var work = Directory.CreateTempSubdirectory("gg-real-ado-").FullName;
        try
        {
            await GitAsync(work, secret, "clone", "--depth", "1", url, "r");
            var tree = Path.Combine(work, "r");
            await GitAsync(tree, secret, "checkout", "-b", branch);
            await File.WriteAllTextAsync(Path.Combine(tree, ".gg-probe"), branch);
            await GitAsync(tree, secret, "add", ".gg-probe");
            await GitAsync(tree, secret, "-c", "user.email=probe@example.invalid",
                "-c", "user.name=probe", "commit", "-m", "probe");
            await GitAsync(tree, secret, "push", "origin", branch);

            // ANONYMOUS, exactly as DestinationConfiguration builds one.
            using var api = new HttpClient
            {
                BaseAddress = new Uri(
                    $"https://{Host}/{Required("GG_ADO_PROJECT")}/_apis/git/"),
            };

            var outcome = await new RefNamedDestinationAdapter("forge", Host, api).ProposeAsync(
                new LandingRequest
                {
                    WorkingDirectory = tree,
                    Slug = Slug,
                    Branch = branch,
                    BaseRef = "main",
                    Title = "probe: a governed flight proposes",
                    Secret = secret,
                },
                CancellationToken.None);

            await Assert.That(outcome).IsTypeOf<LandingOutcome.Landed>()
                .Because("the credential is on the REQUEST and the client is anonymous, which "
                       + $"is what production builds. Got: {outcome}");

            var landed = (LandingOutcome.Landed)outcome;
            await Assert.That(landed.Uri).Contains("/pullrequest/")
                .Because("the provider returns no page link, so the adapter composes one - and "
                       + "a person has to be able to open it.");
            await Assert.That(landed.Number).IsGreaterThan(0);

            await AbandonAsync(secret, landed.Number);
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

    private static async Task AbandonAsync(string secret, int proposal)
    {
        using var http = new HttpClient();
        using var request = new HttpRequestMessage(
            HttpMethod.Patch,
            $"https://{Host}/{Required("GG_ADO_PROJECT")}"
          + $"/_apis/git/repositories/{Required("GG_ADO_REPO")}/pullrequests/{proposal}"
          + "?api-version=7.1")
        {
            Content = new StringContent(
                """{"status":"abandoned"}""", System.Text.Encoding.UTF8, "application/json"),
        };
        request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue(
            "Basic", Convert.ToBase64String(System.Text.Encoding.ASCII.GetBytes($":{secret}")));

        using var response = await http.SendAsync(request);
        response.EnsureSuccessStatusCode();
    }
}
