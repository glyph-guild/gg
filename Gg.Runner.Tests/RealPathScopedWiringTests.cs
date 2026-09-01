using Gg.Runner.Vcs;

namespace Gg.Runner.Tests;

/// <summary>
/// A repository is read, a branch is pushed and a proposal is opened on a real
/// forge that scopes repositories by path — through the wiring production
/// builds, with nothing planted.
/// </summary>
/// <remarks>
/// <para>
/// <b>The sibling of <see cref="RealProposalWiringTests"/>, for the other
/// convention.</b> That one exists because a real-forge suite ran green over an
/// unauthenticated proposal for months: it handed the adapter a client it had
/// already put a token on, so it asked whether the shape compiled rather than
/// whether the service accepted it. This one inherits the fix — the adapters
/// come from <c>FromEnvironment</c> with <b>no factory passed</b>, which is
/// exactly what <c>Gg.Cli</c> does, so a wiring that does not select these
/// adapters fails here rather than passing on a substitute.
/// </para>
/// <para>
/// <b>It reads through the adapter too, not only writes.</b> Its sibling clones
/// by shelling out to git with a url it composes itself, which proves the
/// forge reachable and nothing about the read adapter. Here the clone goes
/// through <c>IVcsAdapter.CloneAsync</c>, so the url this forge actually takes
/// is the adapter's answer rather than the test's.
/// </para>
/// <para>
/// <b>Cleanup is in a <c>finally</c>, and that is a deliberate difference.</b>
/// The sibling abandons its proposal and deletes its branch on the success
/// path, so a failed assertion leaves both behind. This runs against a
/// repository people work in, where debris is somebody's Monday.
/// </para>
/// <para>
/// Excluded from CI by category: it needs a real organisation and a credential
/// that may write to it, and neither belongs in a public repository.
/// </para>
/// </remarks>
[Category("RealRemote")]
public class RealPathScopedWiringTests
{
    private const string Key = "fixture";

    private static string Required(string variable) =>
        Environment.GetEnvironmentVariable(variable) is { Length: > 0 } value
            ? value
            : throw new InvalidOperationException(
                $"{variable} is not set. This needs a real repository on a forge that scopes "
              + "repositories by path, and a credential that may write to it; it is excluded from "
              + "CI by category for that reason.");

    private static string Optional(string variable, string fallback) =>
        Environment.GetEnvironmentVariable(variable) is { Length: > 0 } value ? value : fallback;

    [Test]
    public async Task The_loop_runs_through_the_wiring_production_builds()
    {
        var host = Required("GG_PATHSCOPED_HOST");
        var slug = Required("GG_PATHSCOPED_SLUG");
        var api = Required("GG_PATHSCOPED_API");
        var secret = Required("GG_PATHSCOPED_SECRET");
        var baseBranch = Optional("GG_PATHSCOPED_BASE", "main");
        var branch = $"gg/pathscoped-{DateTimeOffset.UtcNow.ToUnixTimeSeconds()}";

        // THE PRODUCTION PATH. No adapterFor on either call, because Gg.Cli
        // passes none - so the declaration is what has to choose, and if it
        // does not, the casts below fail rather than a substitute passing.
        var reader = VcsConfiguration.FromEnvironment(
            $"{Key}={host}{HostDeclaration.PathScoped}").Single();
        var lander = DestinationConfiguration.FromEnvironment(
            address => new HttpClient { BaseAddress = new Uri(address) },
            apis: $"{Key}={api}",
            hosts: $"{Key}={host}{HostDeclaration.PathScoped}").Single();

        await Assert.That(reader).IsTypeOf<PathScopedGitVcsAdapter>()
            .Because("a declaration that does not reach this adapter is the defect this slice is "
                   + "about, and it must fail here rather than three assertions later.");
        await Assert.That(lander).IsTypeOf<RefNamedDestinationAdapter>();

        var target = new RepoTarget
        {
            Provider = Key,
            Slug = slug,
            PinnedRef = $"refs/heads/{baseBranch}",
        };

        var resolution = reader.Resolve(target.PinnedRef);
        await Assert.That(resolution).IsTypeOf<RefResolution.Ref>()
            .Because($"'{target.PinnedRef}' is an ordinary branch, and an adapter that refuses one "
                   + "cannot read anything at all. Got: " + resolution);

        var work = Directory.CreateTempSubdirectory("gg-pathscoped-").FullName;
        var tree = Path.Combine(work, "r");
        LandingOutcome.Landed? landed = null;

        try
        {
            // THE READ, through the adapter. The url this forge takes is the
            // adapter's answer, not a string this test composed.
            var clone = await reader.CloneAsync(
                target, ((RefResolution.Ref)resolution).Value, tree, secret, CancellationToken.None);

            await Assert.That(clone.HeadCommit).Length().IsEqualTo(40)
                .Because("this is the assertion step 0 could only make with git directly: the "
                       + "organisation lives in the host, the flight names project/repository, and "
                       + "a commit on disk is what says the composed url was the right one.");
            await Assert.That(clone.FileCount).IsGreaterThan(0)
                .Because("a clone that resolved and put nothing on disk is not something an agent "
                       + "can work in.");

            await File.WriteAllTextAsync(Path.Combine(tree, ".gg-pathscoped"), branch);
            await GitAsync(tree, secret, "add", ".gg-pathscoped");
            await GitAsync(tree, secret, "-c", "user.email=probe@example.invalid",
                "-c", "user.name=probe", "commit", "-m", "wiring probe");

            var request = new LandingRequest
            {
                WorkingDirectory = tree,
                Slug = slug,
                Branch = branch,
                BaseRef = baseBranch,
                Title = "probe: a governed flight proposes",
                Secret = secret,
            };

            // THE PUSH, through the adapter, which composes the same path-scoped
            // url from the same declaration.
            var push = await lander.PushAsync(request, CancellationToken.None);
            await Assert.That(push).IsTypeOf<PushOutcome.Pushed>()
                .Because("the branch has to be on the remote before a proposal can name it. "
                       + "Got: " + push);

            var outcome = await lander.ProposeAsync(request, CancellationToken.None);
            await Assert.That(outcome).IsTypeOf<LandingOutcome.Landed>()
                .Because("an anonymous client and the credential on the request, which is the "
                       + "arrangement that hid the defect last time. Got: " + outcome);

            landed = (LandingOutcome.Landed)outcome;
            await Assert.That(landed.Number).IsGreaterThan(0);

            // THE URL NOBODY RETURNS. This forge describes a proposal without a
            // link a person can open, so the adapter composes one from
            // repository.webUrl - and an api address full of identifiers would
            // look like a link and open nothing.
            await Assert.That(landed.Uri).Contains("/pullrequest/")
                .Because("the composed link is the difference this adapter records as its "
                       + "surprise, and it is only ever true against the real service.");
            await Assert.That(landed.Uri).DoesNotContain("_apis")
                .Because("a url assembled from the api address is not one a person can open.");

            // ASKED AGAIN, THE SAME PROPOSAL. An unauthenticated idempotency
            // query does not error - it fails its success check and degrades to
            // "there is no existing proposal", opening a second one.
            var again = await lander.ProposeAsync(request, CancellationToken.None);
            await Assert.That(again).IsTypeOf<LandingOutcome.Landed>();
            await Assert.That(((LandingOutcome.Landed)again).Number).IsEqualTo(landed.Number);
        }
        finally
        {
            // IN THE FINALLY, because this repository is somebody's Monday.
            await TryAbandonAsync(api, slug, secret, landed?.Number);
            await TryDeleteBranchAsync(reader, target, tree, secret, branch);
            Directory.Delete(work, recursive: true);
        }

        // AND THE CLEANUP IS ASSERTED, outside the finally, because the first
        // run of this test PASSED while leaving a branch behind: the delete
        // pushed to `origin`, which the adapter's clone does not create, and a
        // cleanup that reports rather than throws reported it to nobody. A
        // criterion that says the fixture is left as it was found has to be
        // checked, or it is a comment.
        await Assert.That(await RemoteBranchExistsAsync(reader, target, secret, branch)).IsFalse()
            .Because($"'{branch}' is still on the remote. This runs against a repository people "
                   + "work in, and a walk that accumulates branches is one nobody can run twice.");
    }

    /// <summary>Whether the branch is still on the remote.</summary>
    private static async Task<bool> RemoteBranchExistsAsync(
        IVcsAdapter reader, RepoTarget target, string secret, string branch)
    {
        var url = ((PathScopedGitVcsAdapter)reader).CloneUrlFor(target);
        var listed = await GitOutputAsync(
            Path.GetTempPath(), secret, "ls-remote", "--heads", url, $"refs/heads/{branch}");

        return listed.Contains(branch, StringComparison.Ordinal);
    }

    /// <summary>Abandons the proposal, reporting rather than throwing.</summary>
    /// <remarks>
    /// A cleanup that throws replaces the real failure with its own, and the
    /// real failure is the one worth reading. What it could not clean it names.
    /// </remarks>
    private static async Task TryAbandonAsync(string api, string slug, string secret, int? proposal)
    {
        if (proposal is not { } number)
        {
            return;
        }

        var repository = slug[(slug.IndexOf('/', StringComparison.Ordinal) + 1)..];

        try
        {
            using var http = new HttpClient { BaseAddress = new Uri(api) };
            using var request = new HttpRequestMessage(
                HttpMethod.Patch,
                $"repositories/{repository}/pullrequests/{number}?api-version=7.1")
            {
                Content = new StringContent(
                    """{"status":"abandoned"}""", System.Text.Encoding.UTF8, "application/json"),
            };
            request.Headers.UserAgent.Add(new System.Net.Http.Headers.ProductInfoHeaderValue("gg-tests", "1"));
            request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue(
                "Basic", Convert.ToBase64String(System.Text.Encoding.ASCII.GetBytes($":{secret}")));

            using var response = await http.SendAsync(request);

            if (!response.IsSuccessStatusCode)
            {
                Console.WriteLine(
                    $"[cleanup] proposal {number} was not abandoned ({(int)response.StatusCode}). "
                  + "Close it by hand.");
            }
        }
        catch (HttpRequestException error)
        {
            Console.WriteLine($"[cleanup] proposal {number} was not abandoned: {error.Message}");
        }
    }

    /// <summary>
    /// Deletes the branch by URL, because there is no <c>origin</c> to push to.
    /// </summary>
    /// <remarks>
    /// The first draft pushed to <c>origin</c>, copied from the sibling test
    /// that clones by shelling out to git. This one clones through
    /// <c>IVcsAdapter.CloneAsync</c>, which fetches an explicit url and creates
    /// no remote — so the delete failed every time, and the walk passed anyway
    /// because a cleanup that reports rather than throws reported it to nobody.
    /// The url comes from the adapter, which is also the only thing that knows
    /// how this forge spells one.
    /// </remarks>
    private static async Task TryDeleteBranchAsync(
        IVcsAdapter reader, RepoTarget target, string tree, string secret, string branch)
    {
        if (!Directory.Exists(tree))
        {
            return;
        }

        try
        {
            var url = ((PathScopedGitVcsAdapter)reader).CloneUrlFor(target);
            await GitAsync(tree, secret, "push", url, "--delete", $"refs/heads/{branch}");
        }
        catch (InvalidOperationException error)
        {
            Console.WriteLine($"[cleanup] branch {branch} was not deleted: {error.Message}");
        }
    }

    /// <summary>Runs git and returns its output, for the checks rather than the acts.</summary>
    private static async Task<string> GitOutputAsync(
        string directory, string secret, params string[] arguments)
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
        var output = await git.StandardOutput.ReadToEndAsync();
        await git.WaitForExitAsync();

        return output;
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
}
