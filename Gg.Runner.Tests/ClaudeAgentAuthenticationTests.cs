using System.Diagnostics;
using Gg.Contracts;
using Gg.Runner.Execution;

namespace Gg.Runner.Tests;

/// <summary>
/// The claude adapter measures whether its agent is logged in, and recognises
/// the sentence it prints when it is not.
/// </summary>
/// <remarks>
/// <para>
/// <b><c>claude auth status --json</c> reports SOURCE, not validity.</b>
/// <c>SetupTokenSpikeTests</c>: with a bogus <c>CLAUDE_CODE_OAUTH_TOKEN</c> it
/// answers <c>loggedIn:true, authMethod:"oauth_token"</c> without validating.
/// So <c>ready</c> means "has a credential source", and a dead token is learned
/// from a real run failing with a login-shaped reason - which is what the
/// recogniser is for, and why the two halves live on one adapter.
/// </para>
/// <para>
/// <b>The JSON carries the account's email and organisation, and the
/// diagnosis never does.</b> The runner rule - the resolved secret never
/// leaves the machine - and <c>AllowanceReading</c>'s remark - <i>"a control
/// plane able to tell two subscriptions apart would be holding something about
/// them"</i> - both forbid that crossing. The adapter composes its own fixed
/// sentences; nothing the agent printed is forwarded verbatim.
/// </para>
/// <para>
/// <b>Unknown is not false, and it is not true either.</b> A probe that could
/// not run - no binary, non-JSON output, a non-zero exit - is
/// <c>needs-login</c> with a diagnosis saying it could not be measured, never
/// <c>ready</c>: a runner that flew on an unmeasured login would be the
/// move-bound probe's false pass, one credential over.
/// </para>
/// </remarks>
public class ClaudeAgentAuthenticationTests
{
    private static readonly DateTimeOffset T0 = new(2026, 9, 15, 12, 0, 0, TimeSpan.Zero);

    private const string Token = "sk-ant-oat01-a-token-for-the-probe";

    /// <summary>An agent that answers `auth status` from a script, without a process.</summary>
    private static (ClaudeAgentAuthentication Adapter, List<ProcessStartInfo> Launched) Answering(
        int exit, string output)
    {
        var launched = new List<ProcessStartInfo>();
        var adapter = new ClaudeAgentAuthentication(
            "/usr/local/bin/claude",
            run: (info, _) =>
            {
                launched.Add(info);
                return Task.FromResult((exit, output));
            },
            clock: new MovableClock(T0));
        return (adapter, launched);
    }

    [Test]
    public async Task A_token_in_the_environment_reads_as_ready_from_the_token()
    {
        var (adapter, _) = Answering(0, """{"loggedIn":true,"authMethod":"oauth_token","apiProvider":"firstParty"}""");

        var standing = await adapter.ProbeAsync(Token, CancellationToken.None);

        await Assert.That(standing.Authenticated).IsTrue();
        await Assert.That(standing.Source).IsEqualTo(AgentCredentialSources.Token);
        await Assert.That(standing.MeasuredAt).IsEqualTo(T0);
    }

    [Test]
    public async Task The_machines_own_login_reads_as_ready_from_the_machine()
    {
        // The resident today: a login somebody made as the gg user. Ready, and
        // the source says it is not gg's to revoke.
        var (adapter, _) = Answering(0,
            """{"loggedIn":true,"authMethod":"claude.ai","email":"somebody@example.test","orgId":"org_123","subscriptionType":"max"}""");

        var standing = await adapter.ProbeAsync(token: null, CancellationToken.None);

        await Assert.That(standing.Authenticated).IsTrue();
        await Assert.That(standing.Source).IsEqualTo(AgentCredentialSources.Machine);
    }

    [Test]
    public async Task Not_logged_in_reads_as_needs_login_with_no_source()
    {
        var (adapter, _) = Answering(0, """{"loggedIn":false,"authMethod":"none"}""");

        var standing = await adapter.ProbeAsync(token: null, CancellationToken.None);

        await Assert.That(standing.Authenticated).IsFalse();
        await Assert.That(standing.Source).IsEqualTo(AgentCredentialSources.None);
        await Assert.That(standing.Diagnosis).Contains("not logged in")
            .Because("a person reading the hold needs the plain sentence, not a JSON field.");
    }

    [Test]
    public async Task A_probe_that_could_not_run_is_needs_login_and_says_so()
    {
        // UNKNOWN IS NOT FALSE, and it is not ready either.
        foreach (var (exit, output) in new[] { (1, ""), (0, "Not logged in"), (0, "{not json") })
        {
            var (adapter, _) = Answering(exit, output);

            var standing = await adapter.ProbeAsync(Token, CancellationToken.None);

            await Assert.That(standing.Authenticated).IsFalse()
                .Because($"exit {exit} with '{output}' measured nothing, and nothing is not ready.");
            await Assert.That(standing.Diagnosis).Contains("could not be measured");
        }
    }

    [Test]
    public async Task The_diagnosis_never_carries_what_the_agent_printed()
    {
        // The fixture's email and organisation must not reach the control
        // plane through a diagnosis, however the probe went.
        var (adapter, _) = Answering(0,
            """{"loggedIn":false,"authMethod":"none","email":"somebody@example.test","orgId":"org_123"}""");

        var standing = await adapter.ProbeAsync(token: null, CancellationToken.None);

        await Assert.That(standing.Diagnosis).DoesNotContain("somebody@example.test");
        await Assert.That(standing.Diagnosis).DoesNotContain("org_123");

        var (unparsed, _) = Answering(0, "garbage somebody@example.test org_123");
        var unmeasured = await unparsed.ProbeAsync(token: null, CancellationToken.None);

        await Assert.That(unmeasured.Diagnosis).DoesNotContain("somebody@example.test")
            .Because("an output that would not parse is exactly the one a naive diagnosis "
                   + "would quote, and it is the one that may contain anything.");
    }

    [Test]
    public async Task The_probe_runs_auth_status_with_the_token_placed_and_not_as_an_argument()
    {
        var (adapter, launched) = Answering(0, """{"loggedIn":true,"authMethod":"oauth_token"}""");

        _ = await adapter.ProbeAsync(Token, CancellationToken.None);

        var info = launched.Single();
        await Assert.That(info.FileName).IsEqualTo("/usr/local/bin/claude");
        await Assert.That(info.ArgumentList).IsEquivalentTo((string[])["auth", "status", "--json"]);
        await Assert.That(info.Environment["CLAUDE_CODE_OAUTH_TOKEN"]).IsEqualTo(Token)
            .Because("the probe measures the credential gg holds, so it places it the way a "
                   + "flight's launch does.");
        await Assert.That(string.Join(" ", info.ArgumentList)).DoesNotContain(Token);
        await Assert.That(info.RedirectStandardOutput).IsTrue();
        await Assert.That(info.UseShellExecute).IsFalse();
    }

    [Test]
    public async Task The_recogniser_knows_the_sentences_a_dead_login_prints()
    {
        var adapter = new ClaudeAgentAuthentication();

        foreach (var reason in new[]
        {
            "Invalid API key · Please run /login",
            "Not logged in · Please run /login",
            "Failed to authenticate. API Error: 401 OAuth access token is invalid.",
        })
        {
            await Assert.That(adapter.NeedsLogin(reason)).IsTrue()
                .Because($"'{reason}' is what the agent said on a live member, and a runner "
                       + "that did not recognise it would exit 69 as a broken bound.");
        }

        foreach (var reason in new[] { null, "", "the tests did not compile", "exhausted its attempts" })
        {
            await Assert.That(adapter.NeedsLogin(reason)).IsFalse()
                .Because("a failure that is not about the login must not hold the runner - "
                       + "holding is how a broken flight would otherwise stop a machine.");
        }
    }
}
