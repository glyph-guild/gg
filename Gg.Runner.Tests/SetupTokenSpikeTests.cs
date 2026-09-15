using System.Diagnostics;

namespace Gg.Runner.Tests;

/// <summary>
/// What <c>claude setup-token</c> does when gg drives it, measured rather than
/// assumed — because where the login ceremony may live depends on the answer.
/// </summary>
/// <remarks>
/// <para>
/// <b>Excluded from CI by name</b>, as <see cref="AgainstRealAgentTests"/> is:
/// these need the executor binary and refuse loudly when it is not there.
/// What they prove is recorded here, because the decision it drives is
/// structural and cannot be re-derived from the code afterwards.
/// </para>
/// <para>
/// <b>Measured on 2026-09-15 against Claude Code 2.1.272.</b>
/// </para>
/// <list type="number">
/// <item>
/// <b>On pipes it prints nothing and does not exit.</b> With all three
/// standard streams redirected, twenty-five seconds passed with zero bytes on
/// stdout and stderr and the process still running. It wants a terminal.
/// </item>
/// <item>
/// <b>Under a pseudo-terminal it prints the login URL within eight
/// seconds.</b> The screen reads <i>"This will guide you through long-lived
/// (1-year) auth token setup for your Claude account. Claude subscription
/// required."</i>, then <i>"Opening browser to sign in…"</i> — it TRIES a
/// browser first, which on a headless runner fails — then <i>"Browser didn't
/// open? Use the url below to sign in (c to copy)"</i> and the URL, emitted as
/// an OSC-8 hyperlink (<c>ESC ] 8 ; id=… ; https://… ESC ] 8 ; ;</c>) whose
/// visible text is the same URL wrapped across lines. The hyperlink is the
/// robust thing to parse: one sequence, unwrapped.
/// </item>
/// <item>
/// <b>The URL's origin is <c>https://claude.com/cai/oauth/authorize</c></b>,
/// with <c>response_type=code</c>, <c>code_challenge_method=S256</c>, a
/// <c>code_challenge</c> and a <c>state</c>, redirecting to
/// <c>platform.claude.com/oauth/code/callback</c>. PKCE: the URL is safe to
/// show a person, and the code they bring back is useless without the verifier
/// the child holds.
/// </item>
/// <item>
/// <b><c>CLAUDE_CODE_OAUTH_TOKEN</c> takes precedence over the machine's own
/// login</b>, including under <c>--setting-sources project</c>: with a bogus
/// value, <c>claude -p</c> answers <c>is_error:true, api_error_status:401,
/// "Failed to authenticate. API Error: 401 OAuth access token is invalid."</c>
/// on a machine whose <c>~/.claude/.credentials.json</c> is valid. So placing
/// the token in the child's environment is honoured, and a dead token is a
/// sentence a recogniser can match.
/// </item>
/// </list>
/// <para>
/// <b>The decision this drives.</b> The ceremony needs a pseudo-terminal, and
/// <c>Gg.Runner</c> may not take one (<c>ProjectReferenceTests</c>,
/// <c>NoTerminalTests</c>). So the class that drives <c>setup-token</c> lives
/// in <c>Gg.Cli</c>, beside <c>PtyHost</c>'s library, and is handed to the
/// runner as a port — the identity key's and the credential keeper's shape.
/// </para>
/// <para>
/// <b>Not yet measured, because each needs a person to finish the
/// ceremony:</b> the exact shape of the "paste the code" prompt; how the token
/// is printed at the end; and whether <c>setup-token</c> ALSO writes
/// <c>~/.claude/.credentials.json</c>, in which case the token exists twice and
/// <c>Forget</c> would miss one. The first real run of the ceremony records
/// these.
/// </para>
/// </remarks>
[Category("RealAgent")]
public class SetupTokenSpikeTests
{
    private static string Binary =>
        Environment.GetEnvironmentVariable("GG_EXECUTOR_BINARY")
        ?? throw new InvalidOperationException(
            "GG_EXECUTOR_BINARY is not set. This spike drives the real binary; skipping it "
          + "would leave the ceremony's placement unmeasured.");

    /// <summary>A launch with no token in the environment, whatever this shell has.</summary>
    private static ProcessStartInfo Clean(string file, params string[] arguments)
    {
        var info = new ProcessStartInfo(file)
        {
            RedirectStandardInput = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            WorkingDirectory = Path.GetTempPath(),
        };

        foreach (var argument in arguments)
        {
            info.ArgumentList.Add(argument);
        }

        info.Environment.Remove("CLAUDE_CODE_OAUTH_TOKEN");
        info.Environment.Remove("ANTHROPIC_API_KEY");
        return info;
    }

    [Test]
    public async Task On_pipes_setup_token_prints_nothing_and_does_not_exit()
    {
        // FINDING 1. The bounded wait is real time, deliberately: this is a
        // measurement of another program, and there is no clock to inject
        // into it.
        using var child = Process.Start(Clean(Binary, "setup-token"))!;
        var exited = child.WaitForExit(TimeSpan.FromSeconds(10));

        var seen = exited ? await child.StandardOutput.ReadToEndAsync() : "";
        if (!exited)
        {
            child.Kill(entireProcessTree: true);
        }

        await Assert.That(exited).IsFalse()
            .Because("it neither finished nor refused: it is waiting for a terminal it does "
                   + "not have.");
        await Assert.That(seen).IsEmpty()
            .Because("nothing reaches a pipe - not the welcome, not the URL - so a ceremony on "
                   + "pipes would wait for ever for a URL that is never coming.");
    }

    [Test]
    public async Task Under_a_pseudo_terminal_setup_token_prints_a_login_url()
    {
        // FINDING 2 and 3, using `script` to lend the child a pty without a
        // library in this project. Killed once the URL has been seen: nothing
        // is typed, so no code is entered and no token is minted.
        var script = File.Exists("/usr/bin/script") ? "/usr/bin/script" : "script";
        var info = Clean(script, "-q", "/dev/null", Binary, "setup-token");
        info.Environment["TERM"] = "xterm-256color";

        using var child = Process.Start(info)!;
        var screen = new System.Text.StringBuilder();
        var deadline = DateTimeOffset.UtcNow.AddSeconds(30);
        var buffer = new char[4096];

        while (DateTimeOffset.UtcNow < deadline
               && !screen.ToString().Contains("code_challenge_method=S256", StringComparison.Ordinal))
        {
            var read = await child.StandardOutput.ReadAsync(buffer, 0, buffer.Length);
            if (read <= 0)
            {
                break;
            }

            screen.Append(buffer, 0, read);
        }

        child.Kill(entireProcessTree: true);
        var text = screen.ToString();

        await Assert.That(text).Contains("https://claude.com/cai/oauth/authorize", StringComparison.Ordinal)
            .Because("the URL a person is sent to has one origin, and the console will only ever "
                   + "show a URL from it.");
        await Assert.That(text).Contains("code_challenge_method=S256", StringComparison.Ordinal)
            .Because("PKCE is what makes the URL safe to show and the code useless to anyone "
                   + "but the child holding the verifier.");
        await Assert.That(text).Contains("]8;", StringComparison.Ordinal)
            .Because("the URL arrives as an OSC-8 hyperlink, which is the one unwrapped copy on "
                   + "the screen and the thing a driver should parse.");
    }

    [Test]
    public async Task A_token_in_the_environment_takes_precedence_and_a_dead_one_is_a_401()
    {
        // FINDING 4. The bogus token costs no inference: the request is refused
        // before a model is reached.
        var info = Clean(
            Binary, "-p", "say OK", "--output-format", "json",
            "--setting-sources", "project", "--max-turns", "1");
        info.Environment["CLAUDE_CODE_OAUTH_TOKEN"] = "sk-ant-oat01-this-is-a-bogus-probe-token-000000";

        using var child = Process.Start(info)!;
        var answer = await child.StandardOutput.ReadToEndAsync();
        child.WaitForExit(TimeSpan.FromSeconds(60));

        await Assert.That(answer).Contains("\"is_error\":true", StringComparison.Ordinal);
        await Assert.That(answer).Contains("\"api_error_status\":401", StringComparison.Ordinal)
            .Because("the machine's own login is valid here, so a 401 proves the environment "
                   + "variable won - and that a dead token is a sentence, not a hang.");
        await Assert.That(answer).Contains("OAuth access token is invalid", StringComparison.Ordinal)
            .Because("this is the sentence NeedsLogin will recognise.");
    }
}
