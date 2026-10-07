using System.Text.RegularExpressions;
using Gg.Contracts.Description;

namespace Gg.Client.Tests;

/// <summary>
/// A developer-audience read that gg's client can make is one something in gg
/// actually calls.
/// </summary>
/// <remarks>
/// <para>
/// <b>S60.1-03, and it exists because its sibling was satisfied by a method.</b>
/// <c>EveryDeveloperReadHasACallerTests</c> asserts that
/// <c>ControlPlaneClient</c> names every developer-audience <c>GET</c> path, and
/// it is a good ratchet — but naming a path is not opening a door. When
/// <c>GET /v1/auth/keys</c> was declared, that ratchet demanded
/// <c>ListKeysAsync</c> and got it, and the method then sat with **one reference
/// in the whole repository: its own declaration**. The door existed on the wire,
/// a method existed to open it, and no person could reach it.
/// </para>
/// <para>
/// <b>What this adds is the second half of the same sentence.</b> The sibling
/// says gg can make the read; this says something in gg does. Together they mean
/// a declared read reaches a person rather than stopping at an unused method.
/// </para>
/// <para>
/// <b>GET and developer only, deliberately.</b> This is the same scope as the
/// ratchet it completes, so the two cannot disagree about which doors they are
/// about. It therefore does not see a write that nothing drives —
/// <c>RenewTakeoverAsync</c> is one today — and that is a gap worth naming here
/// rather than quietly widening the scan to cover it: a half-built lifecycle is
/// a different argument from an unreachable read, and it deserves its own.
/// </para>
/// <para>
/// <b>A declaration rather than a requirement</b>, which is
/// <c>VerbParityTests</c>' shape: not every door belongs in front of a person,
/// and what this demands is that somebody DECIDED and wrote the decision down.
/// </para>
/// </remarks>
public class AClientMethodIsNotAReachTests
{
    /// <summary>
    /// Reads whose serving method nothing calls, and why that is a decision.
    /// </summary>
    /// <remarks>
    /// <b>Each of these is a single-item read sitting beside a list that a
    /// person already has.</b> That is the shape to be suspicious of when adding
    /// one: a method written because a path was declared, rather than because a
    /// verb needed it.
    /// </remarks>
    private static readonly Dictionary<string, string> Exempt = new(StringComparer.Ordinal)
    {
        ["/v1/airspace/strategies/{name}"] =
            "GetStrategyAsync reads the strategy in force for ONE name, and the estate is how a "
          + "person reads strategies: `gg airspace pull` writes every one into the working copy "
          + "and the tree renders them. A verb for a single strategy would be a second way to ask "
          + "the same question, and this product has committed to the first - which is the rule "
          + "`gg airspace` already follows for documents.",

        ["/v1/airspace/watches/{name}"] =
            "GetWatchAsync reads ONE watch by name, beside `gg watches`, which lists every watch "
          + "with how it is doing. The list is the question a person asks; a single watch is what "
          + "a pane would open, and no pane opens one.",
    };

    /// <summary>The repository root, found by the solution file.</summary>
    private static string RepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "Gg.sln")))
        {
            dir = dir.Parent;
        }

        return dir!.FullName;
    }

    /// <summary>
    /// Every product source file, which is what "something in gg calls it" means.
    /// </summary>
    /// <remarks>
    /// <b>Tests are excluded on purpose.</b> A method called only by its own test
    /// is the exact condition this ratchet exists to find: the test proves it
    /// works and nothing proves anybody can use it.
    /// </remarks>
    private static IReadOnlyList<string> ProductSources()
    {
        var root = RepoRoot();

        return
        [
            .. Directory.EnumerateDirectories(root, "Gg.*")
                .Where(d => !Path.GetFileName(d).EndsWith(".Tests", StringComparison.Ordinal))
                .SelectMany(d => Directory.EnumerateFiles(d, "*.cs", SearchOption.AllDirectories)),
        ];
    }

    private static string Client() =>
        File.ReadAllText(Path.Combine(RepoRoot(), "Gg.Client", "ControlPlaneClient.cs"));

    /// <summary>
    /// Which client method serves a path: the one declared most recently before
    /// the path's literal appears.
    /// </summary>
    private static IReadOnlyList<string> Serving(string source, string path)
    {
        var declarations = Regex.Matches(
            source, @"public (?:async )?Task(?:<[^>]*>)? ([A-Za-z]+Async)");

        var spelled = Regex.Replace(Regex.Escape(path), @"\\\{[^}]*\\\}", "[^\"]*?");

        var serving = new List<string>();

        foreach (var use in Regex.Matches(source, "\"" + spelled + "\"").Cast<Match>())
        {
            string? owner = null;

            foreach (var declaration in declarations.Cast<Match>())
            {
                if (declaration.Index > use.Index)
                {
                    break;
                }

                owner = declaration.Groups[1].Value;
            }

            if (owner is not null && !serving.Contains(owner))
            {
                serving.Add(owner);
            }
        }

        return serving;
    }

    /// <summary>
    /// How many times a name appears in product code, ignoring doc comments.
    /// </summary>
    /// <remarks>
    /// <b>Doc comments are stripped</b>, because a <c>&lt;see cref&gt;</c> naming
    /// a method is prose about it rather than a call to it — and a ratchet that
    /// counted one would be answered by writing about the dead method instead of
    /// calling it.
    /// </remarks>
    private static int Mentions(IReadOnlyList<string> sources, string name)
    {
        var pattern = new Regex(@"\b" + Regex.Escape(name) + @"\b");
        var count = 0;

        foreach (var file in sources)
        {
            var lines = File.ReadAllLines(file)
                .Where(l => !l.TrimStart().StartsWith("///", StringComparison.Ordinal));

            count += lines.Sum(l => pattern.Matches(l).Count);
        }

        return count;
    }

    [Test]
    public async Task Every_read_gg_can_make_is_one_something_in_gg_calls()
    {
        var reads = ProtocolSurface.Endpoints
            .Where(e => e.Method == "GET" && e.Audience == Audience.Developer)
            .Select(e => e.Path)
            .Distinct(StringComparer.Ordinal)
            .Order(StringComparer.Ordinal)
            .ToList();

        await Assert.That(reads).IsNotEmpty()
            .Because("no developer reads were found, so this ratchet asserted nothing. That is a "
                   + "broken scan, never a protocol with no reads in it.");

        var client = Client();
        var sources = ProductSources();

        var unreached = new List<string>();

        foreach (var path in reads.Where(p => !Exempt.ContainsKey(p)))
        {
            foreach (var method in Serving(client, path))
            {
                // ONE MENTION IS THE DECLARATION. Anything above it is a call,
                // from this file or another - a composing method on the client
                // counts, because whatever calls IT is then on the hook.
                if (Mentions(sources, method) <= 1)
                {
                    unreached.Add($"{path} ({method})");
                }
            }
        }

        await Assert.That(unreached).IsEmpty()
            .Because("a client method nothing calls is not a reach: the door is declared, a "
                   + "method exists to open it, and no person can. Give it a verb, or name the "
                   + "path above with the reason nobody should. Found: "
                   + string.Join(", ", unreached));
    }

    [Test]
    public async Task The_exemption_list_names_nothing_that_is_now_reached()
    {
        // THE OTHER DIRECTION, which its sibling also carries. An exemption kept
        // after somebody wired the method up is a recorded decision that has
        // stopped being true, and the next reader believes it.
        var client = Client();
        var sources = ProductSources();

        var reached = Exempt.Keys
            .Where(path => Serving(client, path) is { Count: > 0 } serving
                        && serving.All(m => Mentions(sources, m) > 1))
            .Order(StringComparer.Ordinal)
            .ToList();

        await Assert.That(reached).IsEmpty()
            .Because("these are recorded as unreachable and something now calls them, so the "
                   + "reason beside each has stopped being true. Remove the entry. Found: "
                   + string.Join(", ", reached));
    }

    [Test]
    public async Task The_exemption_list_names_nothing_that_has_gone()
    {
        var declared = ProtocolSurface.Endpoints
            .Where(e => e.Method == "GET" && e.Audience == Audience.Developer)
            .Select(e => e.Path)
            .ToHashSet(StringComparer.Ordinal);

        var ghosts = Exempt.Keys.Where(p => !declared.Contains(p)).Order(StringComparer.Ordinal);

        await Assert.That(ghosts).IsEmpty()
            .Because("an exemption for a path the protocol no longer declares is a sentence about "
                   + "nothing, and it hides the next one. Found: " + string.Join(", ", ghosts));
    }

    [Test]
    public async Task The_scan_can_see_a_method_nothing_calls()
    {
        // THE PLANTED TWIN. Without it, every assertion above is satisfied by a
        // counter that never returns 1 - which is the shape LiveStreamingTests
        // pairs its regex for, and the shape a scan over a moved file has.
        var sources = ProductSources();

        await Assert.That(sources).IsNotEmpty()
            .Because("no product sources were found, so nothing above counted anything.");

        await Assert.That(Mentions(sources, "AMethodNobodyHasEverWritten")).IsEqualTo(0)
            .Because("a name nothing mentions must count zero, or the counter is not counting.");

        await Assert.That(Mentions(sources, "ListCredentialsAsync")).IsGreaterThan(1)
            .Because("a method the CLI plainly calls must count more than its declaration, or "
                   + "this ratchet would indict everything and be turned off.");
    }
}
