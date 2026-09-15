using System.Diagnostics;
using System.Text.Json;
using Gg.Contracts;

namespace Gg.Runner.Execution;

/// <summary>
/// How the claude agent authenticates.
/// </summary>
/// <remarks>
/// <para>
/// <b>A subscription login, never an API key.</b> The credential is the
/// long-lived OAuth token <c>claude setup-token</c> mints, and the variable is
/// the one the binary reads it from. <c>ANTHROPIC_API_KEY</c> is not set, not
/// stored and not stripped by gg - a decision, held by a scan in
/// <c>TheAgentGetsItsTokenTests</c> - because the allowance meter and the
/// usage report both answer only for a subscription.
/// </para>
/// <para>
/// <b>Measured against Claude Code 2.1.272</b> (<c>SetupTokenSpikeTests</c>):
/// the variable takes precedence over <c>~/.claude/.credentials.json</c> even
/// under <c>--setting-sources project</c>, and a dead value is a 401 with a
/// sentence rather than a hang. It is absent from the binary's <c>--help</c>,
/// so a rename upstream would fail silently; the version is named here so the
/// next measurement knows what the last one saw.
/// </para>
/// <para>
/// <b>This class never reads the agent's own credential file.</b>
/// <c>AllowanceMeter</c>'s rule: gg asks the tool that holds the subscription's
/// credential, and <i>"reading another tool's .credentials.json and calling an
/// API with it would be a different thing entirely."</i> The token gg stores
/// came out of <c>setup-token</c>, which is the tool handing it over.
/// </para>
/// </remarks>
/// <param name="binary">Where the agent is, for the probe.</param>
/// <param name="run">
/// How a probe process is run: exit code and stdout. A seam for tests, the
/// attended executor's <c>spawn</c> shape; the default runs it.
/// </param>
/// <param name="clock">For <c>MeasuredAt</c>. The system clock by default.</param>
public sealed class ClaudeAgentAuthentication(
    string binary = "claude",
    Func<ProcessStartInfo, CancellationToken, Task<(int Exit, string Output)>>? run = null,
    IClock? clock = null) : IAuthenticateAnAgent
{
    private readonly string _binary = binary;
    private readonly Func<ProcessStartInfo, CancellationToken, Task<(int Exit, string Output)>> _run =
        run ?? RunAsync;
    private readonly IClock _clock = clock ?? new SystemClock();

    /// <summary>How long a probe may take before it is an answer of its own.</summary>
    public static readonly TimeSpan Patience = TimeSpan.FromSeconds(30);

    /// <summary>What the agent says when it could not authenticate. Its words, measured.</summary>
    private static readonly string[] LoginShaped =
    [
        "/login",
        "Not logged in",
        "OAuth access token is invalid",
        "Invalid API key",
        "Failed to authenticate",
    ];

    public string Provider => "claude";

    public string Locator { get; } = CredentialLocator.ForAgent("claude");

    public string TokenVariable => "CLAUDE_CODE_OAUTH_TOKEN";

    public async Task<AgentStanding> ProbeAsync(string? token, CancellationToken cancellationToken)
    {
        var info = new ProcessStartInfo(_binary)
        {
            ArgumentList = { "auth", "status", "--json" },
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
        };

        // THE CREDENTIAL GG HOLDS, placed the way a launch places it, so the
        // probe measures what a flight would run under and not what the
        // machine's user happens to have.
        ClaudeCodeExecutor.PlaceToken(info, this, token);

        int exit;
        string output;
        try
        {
            (exit, output) = await _run(info, cancellationToken);
        }
        catch (Exception failure) when (failure is not OperationCanceledException)
        {
            return Unmeasured($"`{Path.GetFileName(_binary)} auth status --json` could not be "
                            + $"started ({failure.GetType().Name}).");
        }

        if (exit != 0)
        {
            return Unmeasured($"`{Path.GetFileName(_binary)} auth status --json` exited {exit}.");
        }

        // PARSED, NEVER QUOTED. The JSON carries the account's email and
        // organisation, and nothing below copies a field into a sentence.
        bool loggedIn;
        string? method;
        try
        {
            using var parsed = JsonDocument.Parse(output);
            var root = parsed.RootElement;
            loggedIn = root.TryGetProperty("loggedIn", out var flag) && flag.ValueKind == JsonValueKind.True;
            method = root.TryGetProperty("authMethod", out var how) && how.ValueKind == JsonValueKind.String
                ? how.GetString()
                : null;
        }
        catch (JsonException)
        {
            return Unmeasured($"`{Path.GetFileName(_binary)} auth status --json` printed something "
                            + "other than JSON.");
        }

        if (!loggedIn)
        {
            return new AgentStanding(
                Authenticated: false,
                Source: AgentCredentialSources.None,
                Diagnosis: "the agent is not logged in: `auth status` reports no credential source"
                         + (token is { Length: > 0 }
                             ? ", and the token gg holds was not read as one."
                             : ", and gg holds no token for it."),
                MeasuredAt: _clock.UtcNow);
        }

        // "oauth_token" is what the binary reports for the variable gg places;
        // anything else is a login the machine's own user made.
        var source = string.Equals(method, "oauth_token", StringComparison.Ordinal)
            ? AgentCredentialSources.Token
            : AgentCredentialSources.Machine;

        return new AgentStanding(
            Authenticated: true, Source: source, Diagnosis: "", MeasuredAt: _clock.UtcNow);
    }

    public bool NeedsLogin(string? said) =>
        said is { Length: > 0 }
        && LoginShaped.Any(sentence => said.Contains(sentence, StringComparison.OrdinalIgnoreCase));

    private AgentStanding Unmeasured(string why) => new(
        Authenticated: false,
        Source: AgentCredentialSources.None,
        Diagnosis: "whether the agent is logged in could not be measured: " + why,
        MeasuredAt: _clock.UtcNow);

    /// <summary>The default runner: both pipes read, bounded by <see cref="Patience"/>.</summary>
    private static async Task<(int Exit, string Output)> RunAsync(
        ProcessStartInfo info, CancellationToken cancellationToken)
    {
        using var running = new Process { StartInfo = info };
        running.Start();

        var output = running.StandardOutput.ReadToEndAsync(cancellationToken);
        var error = running.StandardError.ReadToEndAsync(cancellationToken);

        using var patience = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        patience.CancelAfter(Patience);
        try
        {
            await running.WaitForExitAsync(patience.Token);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            try { running.Kill(entireProcessTree: true); } catch (InvalidOperationException) { }
            return (-1, "");
        }

        await Task.WhenAll(output, error);
        return (running.ExitCode, await output);
    }
}
