using System.Net;
using System.Text;
using Gg.Contracts;

namespace Gg.Client.Tests;

/// <summary>
/// `gg watches`: what would run each watch, what it last said, and what it has
/// spent.
/// </summary>
/// <remarks>
/// <para>
/// <b>S39.6-01.</b> A watch is the one thing in this platform that runs with
/// nobody watching it - no lease, no flight, no queue row while it works - so
/// the only way to know it is alive is to ask. Three facts answer that: the
/// executor in force, the newest attestation, and the cost inside its window.
/// </para>
/// <para>
/// <b>A read of its own, beside the estate rather than inside it.</b>
/// `gg airspace pull` writes a working copy from the watches in force; a
/// working copy exists to be diffed against what somebody wrote, so liveness
/// in it would be churn in exactly the file that must not churn.
/// </para>
/// <para>
/// <b>Every absence is said rather than blank.</b> A watch that has never
/// swept, one whose executor nothing has declared yet, one with no budget:
/// three different states that all render as an empty column if nobody
/// decides otherwise, and a person reading a quiet board cannot tell them
/// apart.
/// </para>
/// </remarks>
public class WatchVerbTests
{
    private static readonly DateTimeOffset Noon = new(2026, 9, 17, 12, 0, 0, TimeSpan.Zero);

    private sealed class Answering(string body) : HttpMessageHandler
    {
        internal HttpRequestMessage? Seen { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Seen = request;

            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(body, Encoding.UTF8, "application/json"),
            });
        }
    }

    private static WatchStanding AStanding(
        string name = "nightly-triage",
        string? executor = WatchExecutors.Instructions,
        DateTimeOffset? lastHeardAt = null,
        // A NULL LAST-HEARD CANNOT BE SPELLED BY OMISSION, which is the first
        // thing this fixture got wrong: `null` is also the "use the default"
        // value, so a test asking for "never reported" quietly got twenty
        // minutes ago. Never is its own argument.
        bool everHeard = true,
        string? outcome = WatchOutcomes.Swept,
        int nominated = 2,
        string? diagnosis = null,
        DateTimeOffset? quietSince = null,
        int opened = 3,
        string window = "24h",
        int? budgeted = 5) => new()
    {
        Name = name,
        Version = $"{name}@v1",
        Executor = executor,
        LastHeardAt = everHeard ? lastHeardAt ?? Noon.AddMinutes(-20) : null,
        Outcome = outcome,
        Nominated = nominated,
        Diagnosis = diagnosis,
        QuietSince = quietSince,
        Opened = opened,
        Window = window,
        Budgeted = budgeted,
    };

    private static string Json(params WatchStanding[] standings) =>
        System.Text.Json.JsonSerializer.Serialize(
            new WatchStandingList { Standings = standings },
            ProtocolJsonContext.Default.WatchStandingList);

    private static StoredSession SignedIn { get; } = new()
    {
        SessionToken = "stub-session",
        ExpiresAt = new DateTimeOffset(2027, 1, 1, 0, 0, 0, TimeSpan.Zero),
        TenantId = "stub-tenant",
        PrincipalDisplay = "someone@example.test",
    };

    private static async Task<(VerbResult Result, Answering Handler)> WatchesAsync(
        params WatchStanding[] standings)
    {
        var handler = new Answering(Json(standings));
        using var http = new HttpClient(handler)
        {
            BaseAddress = new Uri("https://cp.invalid/"),
        };

        var commands = new FlightCommands(
            new ControlPlaneClient(http), new HeldSessionStore(SignedIn));

        return (await commands.WatchesAsync(), handler);
    }

    [Test]
    public async Task The_verb_reads_the_standings_and_says_what_each_watch_is_doing()
    {
        var (result, handler) = await WatchesAsync(AStanding());

        await Assert.That(handler.Seen!.RequestUri!.AbsolutePath)
            .IsEqualTo("/v1/airspace/watch-standings")
            .Because("a separate read from the estate, because what is in force and how it is "
                   + "going change on completely different clocks.");
        await Assert.That(result.Kind).IsEqualTo(VerbResultKinds.Watches);

        var text = VerbOutput.ToText(result);

        await Assert.That(text).Contains("nightly-triage");
        await Assert.That(text).Contains(WatchExecutors.Instructions)
            .Because("the executor in force is the first of the three facts the criterion "
                   + "names: it is what would run the next sweep.");
        await Assert.That(text).Contains(WatchOutcomes.Swept);
        await Assert.That(text).Contains("3 of 5 in 24h")
            .Because("the cost per window needs its scale beside it - `3` says nothing and "
                   + "`3 of 5 in 24h` says whether the next nomination will stand.");
    }

    [Test]
    public async Task A_watch_with_no_budget_shows_its_cost_and_no_bound()
    {
        var (result, _) = await WatchesAsync(AStanding(opened: 7, budgeted: null));

        var text = VerbOutput.ToText(result);

        await Assert.That(text).Contains("7 in 24h");
        await Assert.That(text).DoesNotContain("of 0")
            .Because("unbounded is a state rather than a bound of zero, and a watch written "
                   + "before budgets existed says exactly that.");
    }

    [Test]
    public async Task A_watch_that_has_gone_quiet_says_so_and_says_since_when()
    {
        var (result, _) = await WatchesAsync(
            AStanding(quietSince: Noon.AddHours(-5), lastHeardAt: Noon.AddHours(-5)));

        var text = VerbOutput.ToText(result);

        await Assert.That(text.Contains("quiet", StringComparison.OrdinalIgnoreCase)).IsTrue()
            .Because("rule 11: the board must never look quiet when it is blind, and the verb "
                   + "a person types to check on a watch is the other half of that.");
        await Assert.That(text).Contains("07:00")
            .Because("since when is the fact somebody acts on - it says whether this started "
                   + "before or after the deploy they are thinking about.");
    }

    [Test]
    public async Task A_watch_that_could_not_sweep_shows_the_runners_own_reason()
    {
        var (result, _) = await WatchesAsync(AStanding(
            outcome: WatchOutcomes.Unreachable,
            nominated: 0,
            diagnosis: "the tracker refused all 3 of this sweep's reads"));

        var text = VerbOutput.ToText(result);

        await Assert.That(text).Contains(WatchOutcomes.Unreachable);
        await Assert.That(text).Contains("refused all 3")
            .Because("the diagnosis was written on the machine that tried, and it is the only "
                   + "thing anybody can act on.");
    }

    [Test]
    public async Task A_watch_that_has_never_swept_says_that_rather_than_nothing()
    {
        var (result, _) = await WatchesAsync(
            AStanding(executor: null, everHeard: false, outcome: null, nominated: 0,
                      opened: 0, budgeted: null));

        var text = VerbOutput.ToText(result);

        await Assert.That(text.Contains("never", StringComparison.OrdinalIgnoreCase)).IsTrue()
            .Because("a watch applied a minute ago, a watch whose runner never came, and a "
                   + "watch that swept and found nothing are three states that all render as "
                   + "an empty column unless somebody decides otherwise.");
    }

    [Test]
    public async Task A_tenant_watching_nothing_is_told_that_and_not_shown_a_table()
    {
        var (result, _) = await WatchesAsync();

        await Assert.That(VerbOutput.ToText(result).Contains("no watch", StringComparison.OrdinalIgnoreCase))
            .IsTrue()
            .Because("empty is a state and not an error - and the sentence has to be "
                   + "distinguishable from a control plane that answered nothing.");
    }

    [Test]
    public async Task The_json_form_is_the_contracts_own_document()
    {
        var (result, _) = await WatchesAsync(AStanding());

        var json = VerbOutput.ToJson(result);

        await Assert.That(json).Contains("\"standings\"")
            .Because("the json form is the wire type, so a script reads what the control plane "
                   + "said rather than what this renderer thought of it.");
        await Assert.That(json).Contains("\"lastHeardAt\"");
    }
}
