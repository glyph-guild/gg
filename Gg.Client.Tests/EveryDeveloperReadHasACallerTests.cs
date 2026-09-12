using System.Text.RegularExpressions;
using Gg.Contracts.Description;

namespace Gg.Client.Tests;

/// <summary>
/// Every read a person is entitled to make, and whether gg can make it.
/// </summary>
/// <remarks>
/// <para>
/// <b>The direction <see cref="ProtocolConformanceTests"/> never asserted.</b>
/// That one checks every path the client CALLS is declared, which catches a
/// client reaching for a door the control plane does not serve. It cannot
/// catch the opposite - a door the control plane serves and gg never opens -
/// because a path nobody calls appears in no observed traffic.
/// </para>
/// <para>
/// <b>And the opposite is the one that happened.</b> <c>GET /v1/environments</c>
/// and <c>GET /v1/pools</c> have been declared, pinned and governed for
/// slices: the chart is what an envelope's environment is refused against, and
/// the ledger's own remark calls it "what gg pools renders". Neither had a
/// caller. The chart's three wire types are even registered in
/// <c>ControlPlaneClient</c>'s JSON context and then never used, and
/// <c>ReasonKinds.Uncharted</c> tells a person to "Chart it first
/// (POST /v1/environments)" - advice gg itself could not follow.
/// </para>
/// <para>
/// <b>Reads only, and only a developer's.</b> A runner's surface is reached by
/// <c>RunnerProtocolClient</c> rather than from here, and a write is a verb
/// somebody has to decide to offer. A READ a person may make and gg cannot is
/// a product surface that does not exist, whatever the protocol says.
/// </para>
/// </remarks>
public class EveryDeveloperReadHasACallerTests
{
    /// <summary>
    /// Declared reads gg deliberately does not make, each with its reason.
    /// </summary>
    /// <remarks>
    /// <b>A declaration rather than a requirement</b>, which is
    /// <c>VerbParityTests</c>' shape and its argument: not every door belongs
    /// in this binary, and what this demands is that somebody DECIDED and wrote
    /// the decision down. <b>It is empty today</b>, and an entry is the place
    /// to say why a person can reach something through a browser that their own
    /// tool cannot.
    /// </remarks>
    private static readonly Dictionary<string, string> Exempt = new(StringComparer.Ordinal);

    private static string Source(string project, string file)
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "Gg.sln")))
        {
            dir = dir.Parent;
        }

        return File.ReadAllText(Path.Combine(dir!.FullName, project, file));
    }

    private static IReadOnlyList<Endpoint> Reads() =>
        [.. ProtocolSurface.Endpoints.Where(
            e => e.Method == "GET" && e.Audience == Audience.Developer)];

    /// <summary>
    /// Whether the client names this path.
    /// </summary>
    /// <remarks>
    /// <b>The whole literal, closing quote included.</b> A path is matched as
    /// the client would spell it - <c>"/v1/pools"</c>, or
    /// <c>$"/v1/airspace/strategies/{…}"</c> for a templated one - because a
    /// bare prefix search would let <c>/v1/pools/members/redeem</c> answer for
    /// <c>/v1/pools</c>, and those are two different doors.
    /// </remarks>
    private static bool Calls(string source, string path)
    {
        var spelled = Regex.Replace(Regex.Escape(path), @"\\\{[^}]*\}", "[^\"]*?");

        return Regex.IsMatch(source, "\"" + spelled + "\"");
    }

    [Test]
    public async Task Every_read_a_person_may_make_is_one_gg_can_make()
    {
        var reads = Reads();

        await Assert.That(reads).IsNotEmpty()
            .Because("no developer reads were found, so this ratchet asserted nothing. That is "
                   + "a broken scan, never a protocol with no reads in it.");

        var client = Source("Gg.Client", "ControlPlaneClient.cs");

        var unreached = reads
            .Select(e => e.Path)
            .Distinct(StringComparer.Ordinal)
            .Where(p => !Calls(client, p))
            .Where(p => !Exempt.ContainsKey(p))
            .Order(StringComparer.Ordinal)
            .ToList();

        await Assert.That(unreached).IsEmpty()
            .Because("a door the control plane serves for a PERSON and gg never opens is a "
                   + "feature that exists on the wire and nowhere a customer can see it. Add a "
                   + "method, or name it above with the reason nobody should. Found: "
                   + string.Join(", ", unreached));
    }

    [Test]
    public async Task The_exemption_list_names_nothing_that_is_reached()
    {
        // THE OTHER DIRECTION. A recorded decision not to call something gg now
        // calls is a sentence describing a product that has moved on, and it
        // reads as authoritative - ProjectionParityTests' rule about its own
        // list, one repository layer down.
        var client = Source("Gg.Client", "ControlPlaneClient.cs");

        var stale = Exempt.Keys.Where(p => Calls(client, p)).Order(StringComparer.Ordinal).ToList();

        await Assert.That(stale).IsEmpty()
            .Because("these are reached now. Delete their lines. Found: "
                   + string.Join(", ", stale));
    }

    [Test]
    public async Task The_exemption_list_names_nothing_that_was_never_declared()
    {
        var declared = Reads().Select(e => e.Path).ToHashSet(StringComparer.Ordinal);

        var ghosts = Exempt.Keys
            .Where(p => !declared.Contains(p)).Order(StringComparer.Ordinal).ToList();

        await Assert.That(ghosts).IsEmpty()
            .Because("a reason not to call a read that is no longer declared outlives the thing "
                   + "it was about. Delete their lines. Found: " + string.Join(", ", ghosts));
    }
}
