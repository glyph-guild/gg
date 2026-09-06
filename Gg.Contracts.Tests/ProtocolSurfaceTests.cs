using System.Text.RegularExpressions;
using Gg.Contracts.Description;

namespace Gg.Contracts.Tests;

/// <summary>
/// The story arrives beside the log rather than instead of it.
/// </summary>
/// <remarks>
/// <para>
/// <b>Additive, and the walks are why.</b> Three walk scripts grep
/// <c>/v1/flights/{ref}/log</c>'s JSON for kinds by name, and the console fetches
/// a log per flight at boot to count expiries. A slice that moved the surface
/// and the assertions in one step would leave neither as independent evidence of
/// the other.
/// </para>
/// <para>
/// <b>No <c>Deprecated</c> flag.</b> Adding a member to the protocol's own
/// description type so a comment can be machine-readable, with nothing reading
/// it, is a declaration nobody consumes — the shape this slice exists to remove.
/// The supersession is a remark, and this asserts it is written down; what is
/// enforced is that <c>/log</c> did not move.
/// </para>
/// </remarks>
public class ProtocolSurfaceTests
{
    private static Endpoint Endpoint(string method, string path) =>
        ProtocolSurface.Endpoints.Single(
            e => string.Equals(e.Method, method, StringComparison.Ordinal)
              && string.Equals(e.Path, path, StringComparison.Ordinal));

    // ---- S32.1-06 ----

    [Test]
    public async Task The_log_keeps_its_type_and_its_statuses()
    {
        var log = Endpoint("GET", "/v1/flights/{ref}/log");

        await Assert.That(log.Response).IsEqualTo(typeof(FlightLog))
            .Because("three walks and the console's boot read this route. It is superseded "
                   + "for a person, not withdrawn from a script.");
        await Assert.That(log.Statuses).IsEquivalentTo(
            new[] { 200, 401, 403, 404, ProtocolSurface.ProtocolTooOld });
    }

    [Test]
    public async Task The_story_is_declared_beside_it()
    {
        var story = Endpoint("GET", "/v1/flights/{ref}/story");

        await Assert.That(story.Response).IsEqualTo(typeof(FlightStory));
        await Assert.That(story.Audience).IsEqualTo(Audience.Developer)
            .Because("a flight's whole history is a developer's read, and the log beside it "
                   + "asks for the same session.");
    }

    [Test]
    public async Task The_log_says_in_writing_what_supersedes_it()
    {
        // A READER OF THE DECLARATION FINDS THE OTHER ONE. Two routes answering
        // overlapping questions, with nothing in either saying so, is how a
        // caller picks the older one for years.
        var source = File.ReadAllText(Path.Combine(RepoRoot(), "Gg.Contracts", "Description",
            "ProtocolSurface.cs"));

        var index = source.IndexOf("\"/v1/flights/{ref}/log\"", StringComparison.Ordinal);

        await Assert.That(index).IsGreaterThan(0);

        // THIS ROW'S OWN BLOCK, and the window matters. Cutting at the nearest
        // `new()` starts AFTER the comment, which is written above it - so the
        // first version of this scan looked at the declaration and never at the
        // remark it was asserting about, and failed against a file that said the
        // right thing. The block is what follows the previous row's closing
        // brace.
        var preceding = source[Math.Max(0, index - 2000)..index];
        var previousRow = preceding.LastIndexOf("},", StringComparison.Ordinal);

        await Assert.That(preceding[(previousRow < 0 ? 0 : previousRow)..]).Contains("/story")
            .Because("the remark on this row is where a person reading the declaration "
                   + "learns there is a composed surface, and it is the only place that "
                   + "says so - there is deliberately no machine-readable flag.");
    }

    private static string RepoRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "Gg.sln")))
        {
            directory = directory.Parent;
        }

        return directory?.FullName
            ?? throw new InvalidOperationException("no Gg.sln above " + AppContext.BaseDirectory);
    }
}
