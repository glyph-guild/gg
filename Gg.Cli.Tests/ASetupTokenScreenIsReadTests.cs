using Gg.Runner;

namespace Gg.Cli.Tests;

/// <summary>
/// <c>claude setup-token</c> wants a terminal, so gg gives it one it owns and
/// reads two things off the screen: the login URL, which it prints as an
/// OSC-8 hyperlink, and the token it mints once the code is typed.
/// </summary>
/// <remarks>
/// <para>
/// <b>The hyperlink is the robust thing to parse.</b> Measured in the spike:
/// the visible text is the same URL wrapped across lines, and a wrapped URL
/// is a URL with a line break in it. The OSC-8 sequence carries it once,
/// unwrapped, between <c>ESC ] 8 ; params ;</c> and its terminator.
/// </para>
/// <para>
/// <b>The token is recognised by its shape</b>, because how the agent prints
/// it is the one thing the spike could not measure without a person finishing
/// the ceremony. A long-lived claude token begins <c>sk-ant-oat01-</c>; the
/// recogniser reads that and nothing near it.
/// </para>
/// <para>
/// <b>And a real child under a real pseudo-terminal</b>, because the parsing
/// above proves nothing about whether bytes reach the reader or keystrokes
/// reach the child. A shell script plays the agent: prints a hyperlink, reads
/// a line, prints a token containing it.
/// </para>
/// </remarks>
public class ASetupTokenScreenIsReadTests
{
    private const string Esc = "\u001b";
    private const string Bel = "\u0007";

    [Test]
    public async Task The_url_is_read_from_the_hyperlink_not_the_wrapped_text()
    {
        var url = "https://claude.com/cai/oauth/authorize?code=true&code_challenge=abc&state=xyz";
        var screen =
            "Browser didn't open? Use the url below to sign in (c to copy)\r\n"
          + $"{Esc}]8;id=1;{url}{Esc}\\https://claude.com/cai/oauth/authorize?code=true&code_\r\n"
          + $"challenge=abc&state=xyz{Esc}]8;;{Esc}\\\r\n"
          + "Paste code here if prompted > ";

        await Assert.That(SetupTokenScreen.Url(screen)).IsEqualTo(url);
    }

    [Test]
    public async Task A_bell_terminated_hyperlink_is_read_too()
    {
        var screen = $"{Esc}]8;;https://example.test/a{Bel}https://example.test/a{Esc}]8;;{Bel}";

        await Assert.That(SetupTokenScreen.Url(screen)).IsEqualTo("https://example.test/a");
    }

    [Test]
    public async Task No_hyperlink_is_no_url()
    {
        await Assert.That(SetupTokenScreen.Url("Opening browser to sign in…\r\n")).IsNull();
        await Assert.That(SetupTokenScreen.Url($"{Esc}]8;;{Esc}\\")).IsNull()
            .Because("the closing sequence carries an empty target, and empty is not a URL.");
        await Assert.That(SetupTokenScreen.Url("")).IsNull();
    }

    [Test]
    public async Task The_token_is_read_by_its_shape()
    {
        var screen =
            "Long-lived authentication token created.\r\n"
          + "\r\n"
          + "  sk-ant-oat01-AbC_dEf-123456789abcdefghijklmnop\r\n"
          + "\r\n"
          + "Store it securely.";

        await Assert.That(SetupTokenScreen.Token(screen))
            .IsEqualTo("sk-ant-oat01-AbC_dEf-123456789abcdefghijklmnop");
    }

    [Test]
    public async Task What_is_not_a_token_is_not_read_as_one()
    {
        await Assert.That(SetupTokenScreen.Token("Paste code here if prompted > ")).IsNull();
        await Assert.That(SetupTokenScreen.Token("sk-ant-oat01-")).IsNull()
            .Because("a prefix with nothing after it is the agent starting a line, not a token.");
        await Assert.That(SetupTokenScreen.Token("sk-ant-api03-not-an-oauth-token-1234567890")).IsNull()
            .Because("an API key is the thing gg never sets, and reading one here would place it.");
    }

    [Test]
    public async Task A_child_under_a_pseudo_terminal_is_read_and_typed_to()
    {
        // A SHELL PLAYS THE AGENT: a hyperlink, then a line read, then a token
        // that proves the line arrived. `sh` is on every runner gg has.
        var script =
            "printf '\\033]8;;https://example.test/authorize\\033\\\\https://example.test/authorize\\033]8;;\\033\\\\\\n'; "
          + "read code; "
          + "printf 'Token: sk-ant-oat01-%s-minted\\n' \"$code\"";
        var login = new SetupTokenLogin("sh", ["-c", script]);
        using var patience = new CancellationTokenSource(TimeSpan.FromSeconds(20));

        using var child = await login.StartAsync(patience.Token);

        await Assert.That(await child.UrlAsync(patience.Token))
            .IsEqualTo("https://example.test/authorize");
        await Assert.That(await child.TokenAsync("the-code", patience.Token))
            .IsEqualTo("sk-ant-oat01-the-code-minted");
    }

    [Test]
    public async Task A_child_that_prints_no_hyperlink_yields_no_url_once_it_has_exited()
    {
        var login = new SetupTokenLogin("sh", ["-c", "echo nothing to see; exit 3"]);
        using var patience = new CancellationTokenSource(TimeSpan.FromSeconds(20));

        using var child = await login.StartAsync(patience.Token);

        await Assert.That(await child.UrlAsync(patience.Token)).IsNull()
            .Because("a child that has exited will never print one, and waiting on is a hang.");
    }

    [Test]
    public async Task The_ceremony_child_is_started_with_no_token_of_its_own()
    {
        // THE CHILD MINTS A TOKEN; IT MUST NOT INHERIT ONE. A stored token in
        // its environment would make setup-token report the login it already
        // has, and the ceremony would end with the old token kept as new.
        var options = SetupTokenLogin.OptionsFor("claude", ["setup-token"]);

        await Assert.That(options.Environment.ContainsKey("CLAUDE_CODE_OAUTH_TOKEN")).IsFalse();
        await Assert.That(options.Environment.ContainsKey("ANTHROPIC_API_KEY")).IsFalse()
            .Because("never set by gg, and never handed to the one child whose job is to log in.");
        await Assert.That(options.App).IsEqualTo("claude");
        await Assert.That(options.CommandLine).IsEquivalentTo((string[])["setup-token"]);
        await Assert.That(options.Cols).IsGreaterThanOrEqualTo(200)
            .Because("a token is a hundred characters and a wrapped one is two lines.");
    }
}
