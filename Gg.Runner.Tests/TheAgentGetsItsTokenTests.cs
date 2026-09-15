using System.Text.RegularExpressions;
using Gg.Contracts;
using Gg.Local;
using Gg.Runner.Execution;

namespace Gg.Runner.Tests;

/// <summary>
/// The agent's own credential is a gg credential, and the executor places it
/// in the child's environment at launch.
/// </summary>
/// <remarks>
/// <para>
/// <b>Today the agent authenticates with whatever login its OS user happens to
/// have.</b> On a laptop that is the person's; on the resident runner it is a
/// login somebody made as the <c>gg</c> user; inside a pool member there is
/// none, and the agent answers "Not logged in" in 137 ms. Nothing in gg knew
/// any of that, because nothing in gg held the credential.
/// </para>
/// <para>
/// <b>Now it is a credential like the forge's</b>: stored under a locator the
/// adapter names, read back through the same <c>secretFor</c> the tool
/// servers use, and placed where the agent reads it - its environment, under
/// the variable the adapter names. <c>SetupTokenSpikeTests</c> measured that
/// <c>CLAUDE_CODE_OAUTH_TOKEN</c> takes precedence over the machine's own
/// login, so placing it is honoured.
/// </para>
/// <para>
/// <b>The environment and nowhere else.</b> Not an argument - <i>"which every
/// <c>ps</c> on the host can read"</i> - and not the tool server's <c>env</c>
/// block, which is the server's credential, not the agent's. Nothing else about
/// the child's environment changes: the owner decided an inherited
/// <c>ANTHROPIC_API_KEY</c> is neither set by gg nor stripped by it, and the
/// scan at the bottom holds the first half structurally.
/// </para>
/// <para>
/// <b>An adapter per agent, so the second tool is a class and not a search.</b>
/// <c>IAuthenticateAnAgent</c> is what an executor asks: which provider, which
/// locator, which variable. Chosen from the same <c>ExecutorDeclaration</c> the
/// executor is, in the same default, for the reason the executor is.
/// </para>
/// </remarks>
public partial class TheAgentGetsItsTokenTests
{
    private const string Token = "sk-ant-oat01-a-token-that-must-appear-in-exactly-one-place";

    private static readonly IntentReader Tracker = new(
        "jira", "jira-mcp", ["--stdio"], "JIRA_TOKEN", null);

    private static readonly SelfInvocation Self = SelfInvocation.For("/bin/gg", null)!;

    private static readonly IAuthenticateAnAgent Claude = new ClaudeAgentAuthentication();

    private static ExecutorRequest Request() => new()
    {
        WorkingDirectory = "/work/flight",
        LoopId = "implement",
        Moves = [LoopMoves.Read, LoopMoves.Edit],
        IntentProvider = "jira",
        IntentUri = "https://example.invalid/work/1",
        WallClock = TimeSpan.FromMinutes(30),
        TranscriptPath = "/work/flight/transcript.ndjson",
    };

    // ---- what the adapter says about itself ----

    [Test]
    public async Task The_claude_adapter_names_its_provider_locator_and_variable()
    {
        await Assert.That(Claude.Provider).IsEqualTo("claude");
        await Assert.That(Claude.Locator).IsEqualTo(CredentialLocator.ForAgent("claude"))
            .Because("the console derives the locator to send to and the executor derives it "
                   + "to read from; both ask the contract so neither can spell it.");
        await Assert.That(Claude.TokenVariable).IsEqualTo("CLAUDE_CODE_OAUTH_TOKEN");
    }

    [Test]
    public async Task The_adapter_is_chosen_from_the_same_declaration_as_the_executor()
    {
        // In the default, where the vcs and destination seams learned the
        // choice has to be - and from the parse, so a runner cannot pick an
        // executor for one agent and an adapter for another.
        var agent = ExecutorConfiguration.AgentFor(
            ExecutorDeclaration.Parse("/usr/local/bin/claude", ExecutorDeclaration.Variable));

        await Assert.That(agent.Provider).IsEqualTo(ExecutorDeclaration.Claude);
    }

    // ---- the headless launch ----

    [Test]
    public async Task The_token_goes_into_the_childs_environment_under_the_adapters_variable()
    {
        var info = ClaudeCodeExecutor.StartInfoFor(
            Request(), [Tracker], secret: "jira-secret", Self, agent: Claude, token: Token);

        await Assert.That(info.Environment[Claude.TokenVariable]).IsEqualTo(Token);
    }

    [Test]
    public async Task And_not_into_its_arguments_or_the_tool_servers_configuration()
    {
        // The arguments include the --mcp-config JSON, which carries the
        // TRACKER's secret in its env block; the agent's must not be beside it.
        var info = ClaudeCodeExecutor.StartInfoFor(
            Request(), [Tracker], secret: "jira-secret", Self, agent: Claude, token: Token);

        var arguments = string.Join("\n", info.ArgumentList);

        await Assert.That(arguments).DoesNotContain(Token)
            .Because("an argument is readable by every `ps` on the host.");
        await Assert.That(arguments).Contains("jira-secret")
            .Because("the anchor: the tracker's secret still travels in the server's env "
                   + "block, so the assertion above is about placement rather than "
                   + "about secrets being absent from arguments in general.");
    }

    [Test]
    public async Task No_token_leaves_the_variable_exactly_as_the_parent_had_it()
    {
        // NOT "absent": a ProcessStartInfo's environment is a copy of this
        // process's, and this process may carry the variable. Untouched is the
        // property - a runner with nothing stored does not clear a login the
        // machine's operator exported.
        var info = ClaudeCodeExecutor.StartInfoFor(
            Request(), [Tracker], secret: null, Self, agent: Claude, token: null);

        var parent = Environment.GetEnvironmentVariable(Claude.TokenVariable);

        await Assert.That(info.Environment.TryGetValue(Claude.TokenVariable, out var child) ? child : null)
            .IsEqualTo(parent);
    }

    [Test]
    public async Task An_inherited_api_key_is_neither_set_nor_removed()
    {
        // THE OWNER'S DECISION, both halves: gg never sets ANTHROPIC_API_KEY,
        // and gg does not strip one the operator exported. So the child's
        // value is the parent's, whatever the token is.
        var info = ClaudeCodeExecutor.StartInfoFor(
            Request(), [Tracker], secret: null, Self, agent: Claude, token: Token);

        var parent = Environment.GetEnvironmentVariable("ANTHROPIC_API_KEY");

        await Assert.That(info.Environment.TryGetValue("ANTHROPIC_API_KEY", out var child) ? child : null)
            .IsEqualTo(parent);
    }

    [Test]
    public async Task Without_an_adapter_the_launch_is_what_it_always_was()
    {
        // The anchor for every runner that declares no agent: nothing new is
        // placed, because nothing knows which variable to place it under.
        var info = ClaudeCodeExecutor.StartInfoFor(
            Request(), [Tracker], secret: null, Self, agent: null, token: Token);

        var parent = Environment.GetEnvironmentVariable(Claude.TokenVariable);

        await Assert.That(info.Environment.TryGetValue(Claude.TokenVariable, out var child) ? child : null)
            .IsEqualTo(parent)
            .Because("a token handed to a launch with no adapter has nowhere to go, and "
                   + "guessing the variable would be the assumption the adapter exists to end.");
    }

    [Test]
    public async Task The_executor_reads_the_token_through_secretFor_under_the_agents_locator()
    {
        // The same lookup the tool servers use, keyed by the adapter's locator
        // - so the store the CLI writes and the store the runner reads are one.
        var asked = new List<string>();
        string? SecretFor(string locator)
        {
            asked.Add(locator);
            return locator == Claude.Locator ? Token : null;
        }

        await Assert.That(ClaudeCodeExecutor.TokenFor(Claude, SecretFor)).IsEqualTo(Token);
        await Assert.That(asked).IsEquivalentTo([Claude.Locator]);
        await Assert.That(ClaudeCodeExecutor.TokenFor(null, SecretFor)).IsNull()
            .Because("no adapter, no locator, no lookup.");
    }

    // ---- the attended launch, and the meter ----

    [Test]
    public async Task The_attended_launch_places_it_the_same_way()
    {
        // A laptop with a stored token behaves like a member: the person's
        // hand-flight runs under the credential gg holds, not the one their
        // shell happens to have.
        var info = AttendedExecutor.StartInfoFor(
            Request(), [Tracker], secret: null, Self, agent: Claude, token: Token);

        await Assert.That(info.Environment[Claude.TokenVariable]).IsEqualTo(Token);
        await Assert.That(string.Join("\n", info.ArgumentList)).DoesNotContain(Token);
        await Assert.That(info.RedirectStandardOutput).IsFalse()
            .Because("placing a variable must not have turned the attended launch into a "
                   + "redirected one; that property has its own test and this repeats it "
                   + "at the one place both changed.");
    }

    [Test]
    public async Task The_meters_refresh_runs_the_agent_under_the_same_token()
    {
        // Or a member never refreshes its meter: `claude -p /usage` needs a
        // login exactly as a flight does, and a refresh that silently failed
        // would leave a reading that is merely older.
        var info = MeterAsk.StartInfoFor("/usr/local/bin/claude", Claude.TokenVariable, Token);

        await Assert.That(info.Environment[Claude.TokenVariable]).IsEqualTo(Token);
        await Assert.That(string.Join("\n", info.ArgumentList)).DoesNotContain(Token);

        var without = MeterAsk.StartInfoFor("/usr/local/bin/claude", Claude.TokenVariable, null);
        var parent = Environment.GetEnvironmentVariable(Claude.TokenVariable);
        await Assert.That(without.Environment.TryGetValue(Claude.TokenVariable, out var child) ? child : null)
            .IsEqualTo(parent);
    }

    // ---- structural: the two things gg never does ----

    private static string Root()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "Gg.sln")))
        {
            directory = directory.Parent;
        }

        return (directory ?? throw new InvalidOperationException("Gg.sln not found")).FullName;
    }

    /// <summary>Every source file in the shipping projects, never the tests.</summary>
    private static IEnumerable<string> ShippingSources() =>
        new[] { "Gg.Runner", "Gg.Local", "Gg.Cli", "Gg.Client", "Gg.Console", "Gg.Contracts" }
            .SelectMany(project => Directory.EnumerateFiles(
                Path.Combine(Root(), project), "*.cs", SearchOption.AllDirectories))
            .Where(f => !f.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}")
                     && !f.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}"));

    /// <summary>The lines that compile, with comments left out.</summary>
    private static IEnumerable<string> CodeLines(string source) =>
        source.Split('\n').Where(line => !line.TrimStart().StartsWith("//", StringComparison.Ordinal));

    [Test]
    public async Task Nothing_in_gg_sets_the_api_key()
    {
        // THE OWNER'S FIRST HALF, held by the build. The agent authenticates
        // with a subscription login, never an API key, and gg naming the
        // variable anywhere it could set one is the door this keeps shut.
        var offenders = ShippingSources()
            .Where(f => CodeLines(File.ReadAllText(f)).Any(line => ApiKey().IsMatch(line)))
            .Select(f => Path.GetRelativePath(Root(), f))
            .ToList();

        await Assert.That(offenders).IsEmpty()
            .Because("a code line naming ANTHROPIC_API_KEY is a line that could set it: "
                   + string.Join(", ", offenders));
    }

    [Test]
    public async Task The_adapter_never_reads_the_agents_own_credential_file()
    {
        // AllowanceMeter's guest rule, made structural: gg asks the tool that
        // holds the subscription's credential; "reading another tool's
        // .credentials.json and calling an API with it would be a different
        // thing entirely." The token this slice stores came out of setup-token,
        // which is the tool handing it over.
        var offenders = ShippingSources()
            .Where(f => CodeLines(File.ReadAllText(f)).Any(line => CredentialsFile().IsMatch(line)))
            .Select(f => Path.GetRelativePath(Root(), f))
            .ToList();

        await Assert.That(offenders).IsEmpty()
            .Because("these name the agent's own credential file on a code line: "
                   + string.Join(", ", offenders));
    }

    [Test]
    public async Task The_two_scans_would_notice()
    {
        // LIVENESS, both ways: a planted code line is caught, and the same
        // text in a comment is not - which is what lets MeterAsk's remark
        // explain the rule without tripping it.
        await Assert.That(CodeLines("var k = Environment.GetEnvironmentVariable(\"ANTHROPIC_API_KEY\");")
            .Any(l => ApiKey().IsMatch(l))).IsTrue();
        await Assert.That(CodeLines("    // never ANTHROPIC_API_KEY, by decision")
            .Any(l => ApiKey().IsMatch(l))).IsFalse();

        await Assert.That(CodeLines("var path = Path.Combine(home, \".claude\", \".credentials.json\");")
            .Any(l => CredentialsFile().IsMatch(l))).IsTrue();
        await Assert.That(CodeLines("    /// reading another tool's .credentials.json is a different thing")
            .Any(l => CredentialsFile().IsMatch(l))).IsFalse();

        await Assert.That(ShippingSources().Any()).IsTrue();
    }

    [GeneratedRegex("ANTHROPIC_API_KEY")]
    private static partial Regex ApiKey();

    [GeneratedRegex(@"\.credentials\.json")]
    private static partial Regex CredentialsFile();
}
