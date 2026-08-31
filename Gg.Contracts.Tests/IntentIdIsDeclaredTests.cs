using System.Text.RegularExpressions;

namespace Gg.Contracts.Tests;

/// <summary>
/// A work item id is a declared field, and nothing derives one from a string
/// somebody typed.
/// </summary>
/// <remarks>
/// <para>
/// <b>This is slice nine's refusal, one noun over.</b> <c>FlightRepos.From</c>
/// used to lift a repository's provider out of an intent URI's host, and slice
/// nine took it out because it put a host in the request path: whoever shaped a
/// URI shaped which key a runner resolves. Pulling an id out of a URI is the
/// smaller version of the same move and it breaks the same way — on a vanity
/// host, and on a work item moved between projects.
/// </para>
/// <para>
/// <b>And the larger version is still live, which is why this is a guard and
/// not a comment.</b> <c>FlightRepos.From</c> reads only the URI's
/// <c>AbsolutePath</c> and discards the host, and the registry matches on that
/// path alone — so a URI at any host resolves to whichever registered entry
/// shares its path, and inherits that entry's credential. That is deliberate
/// and it is out of this slice's scope, but it is the strongest possible
/// argument for not building a second thing shaped like it.
/// </para>
/// <para>
/// <b>Asserted over the surface, not over a sample.</b> A test that tried one
/// vanity host would prove one vanity host. What has to be true is that no code
/// path exists at all, which is a claim about the source text.
/// </para>
/// </remarks>
public class IntentIdIsDeclaredTests
{
    /// <summary>An id assigned from something a URI knows.</summary>
    /// <remarks>
    /// Deliberately generous on the right-hand side and narrow on the left: it
    /// is the ASSIGNMENT TO AN ID that is forbidden, and every way a URI gets
    /// taken apart mentions one of these words.
    /// </remarks>
    private static readonly Regex IdFromAUri = new(
        @"\bId\s*=\s*[^;,}]*\b(?:Uri|uri|AbsolutePath|AbsoluteUri|Segments|LocalPath|Host|Authority)\b",
        RegexOptions.Compiled);

    private static string RepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);

        while (dir is not null
            && !File.Exists(Path.Combine(dir.FullName, "Gg.Contracts", "fact-vocabulary.json")))
        {
            dir = dir.Parent;
        }

        return (dir ?? throw new InvalidOperationException("repository root not found")).FullName;
    }

    /// <summary>The two projects that could construct an intent.</summary>
    /// <remarks>
    /// Scoped rather than whole-tree on <c>ChecklistSingleEvaluatorTests</c>'
    /// rule: widening a structural guard to everything makes it fail for work
    /// nobody has planned, and that is how a guard gets weakened to make a
    /// commit green.
    /// </remarks>
    private static IEnumerable<string> IntentBuildingSource() =>
        new[] { "Gg.Contracts", "Gg.Client" }
            .Select(p => Path.Combine(RepoRoot(), p))
            .Where(Directory.Exists)
            .SelectMany(d => Directory.EnumerateFiles(d, "*.cs", SearchOption.AllDirectories))
            .Where(f => !f.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}", StringComparison.Ordinal)
                     && !f.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}", StringComparison.Ordinal));

    // ---- the claim ----

    [Test]
    public async Task Nothing_derives_an_id_from_a_uri()
    {
        var offenders = IntentBuildingSource()
            .Where(f => IdFromAUri.IsMatch(File.ReadAllText(f)))
            .Select(Path.GetFileName)
            .Order(StringComparer.Ordinal)
            .ToList();

        await Assert.That(offenders).IsEmpty()
            .Because("an id lifted out of a URI is structure derived from a string somebody "
                   + "typed, which is what slice nine retired for repositories. It breaks on a "
                   + "vanity host and on a work item moved between projects, and neither "
                   + "failure is visible at the moment somebody types it. Found: "
                   + string.Join(", ", offenders));
    }

    [Test]
    public async Task The_scan_would_see_one_if_it_were_there()
    {
        // THE POISON TWIN. The assertion above is an absence over a file walk,
        // and it passes just as well on a regex that matches nothing and on a
        // walk that found no files.
        await Assert.That(IdFromAUri.IsMatch("Id = new Uri(text).Segments[^1],")).IsTrue();
        await Assert.That(IdFromAUri.IsMatch("Id = parsed.AbsolutePath.Split('/')[^1],")).IsTrue();
        await Assert.That(IdFromAUri.IsMatch("Id = uri[(uri.LastIndexOf('/') + 1)..],")).IsTrue();

        // And the shapes it must NOT claim, each of which is really in the tree.
        await Assert.That(IdFromAUri.IsMatch("Id = request.Id,")).IsFalse();
        await Assert.That(IdFromAUri.IsMatch("FlightId = flight.Id,")).IsFalse()
            .Because("an id copied from an id is the ordinary case and there are many of them.");
    }

    [Test]
    public async Task The_walk_can_see_the_files_it_is_asserting_about()
    {
        // The other half: an empty walk satisfies the absence above.
        var files = IntentBuildingSource().ToList();

        await Assert.That(files).IsNotEmpty();
        await Assert.That(files.Select(Path.GetFileName)).Contains("Flights.cs")
            .Because("the file the intent is declared in has to be among what was read, or the "
                   + "guard is about a tree nobody looked at.");
    }

    // ---- and the refusal at the edge, so it cannot arrive from outside ----

    [Test]
    public async Task An_id_that_is_a_uri_is_refused_naming_the_field()
    {
        // The guard above holds OUR code. This holds everybody else's: a caller
        // that is not gg can put whatever it likes in the field, and pasting the
        // work item's URL into it is the single most likely thing a person does.
        var diagnosis = FlightIntent.Validate(new FlightIntent
        {
            Kind = FlightIntentKinds.Ticket,
            Provider = "azure-boards",
            Id = "https://dev.azure.com/acme/_workitems/edit/4471",
        });

        await Assert.That(diagnosis).IsNotNull();
        await Assert.That(diagnosis!).Contains("id")
            .Because("naming the field is the difference between a diagnosis and a complaint.");
    }

    [Test]
    public async Task An_id_carrying_a_path_is_refused_too()
    {
        // The half-way house, and the one an absolute-URI check alone misses:
        // no scheme, so it is not a URI, and it is still somebody handing over
        // a path and hoping the last segment gets used.
        await Assert.That(FlightIntent.Validate(new FlightIntent
        {
            Kind = FlightIntentKinds.Ticket,
            Provider = "azure-boards",
            Id = "acme/_workitems/edit/4471",
        })).IsNotNull();
    }

    [Test]
    public async Task The_ids_real_trackers_actually_use_are_accepted()
    {
        // The over-refusal half. A rule tight enough to catch a URL and loose
        // enough to be useless is the failure mode here, so the shapes that
        // must keep working are named rather than assumed.
        foreach (var id in (string[])["4471", "PROJ-123", "ENG-4471", "42", "a1b2c3d4"])
        {
            await Assert.That(FlightIntent.Validate(new FlightIntent
            {
                Kind = FlightIntentKinds.Ticket,
                Provider = "azure-boards",
                Id = id,
            })).IsNull().Because($"'{id}' is an id a real tracker issues.");
        }
    }
}
