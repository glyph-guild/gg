using Gg.Cli;

namespace Gg.Cli.Tests;

/// <summary>
/// A login code is typed, allowed to settle, and only then submitted.
/// </summary>
/// <remarks>
/// <para>
/// <b>Measured inside gg-pool-ui-1, twice.</b> Sent as one write - the code
/// with its carriage return on the end - the agent shows the code masked at
/// its prompt and does nothing with it, for ever: the ceremony waited ninety
/// seconds and gave up, three times, and the diagnosis carried ninety-two
/// asterisks sitting under "Paste code here if prompted >".
/// </para>
/// <para>
/// Sent as the code, a pause, and then the return, the same agent answers at
/// once - <i>OAuth error: Invalid code. Please make sure the full code was
/// copied. Press Enter to retry.</i> - which is the answer a deliberately
/// bogus code deserves, and proof that the submit is what was being lost.
/// </para>
/// <para>
/// <b>Why it is a list rather than two calls.</b> What a person does at a
/// terminal is paste, pause, press return; the sequence is the thing being
/// asserted, and a test that could only watch two method calls would pass on a
/// child that wrote them in one breath again.
/// </para>
/// </remarks>
public class ACodeIsTypedThenSubmittedTests
{
    [Test]
    public async Task The_code_and_the_return_are_separate_keystrokes()
    {
        var typing = SetupTokenScreen.Typing("a-code");

        await Assert.That(typing.Count).IsEqualTo(2)
            .Because("one write is what the agent ignores.");
        await Assert.That(typing[0].Text).IsEqualTo("a-code")
            .Because("the code goes in as the code, with nothing appended to it.");
        await Assert.That(typing[1].Text).IsEqualTo("\r")
            .Because("the return is the submit, and it arrives on its own.");
    }

    [Test]
    public async Task The_pause_is_between_them_and_is_real()
    {
        var typing = SetupTokenScreen.Typing("a-code");

        await Assert.That(typing[0].Settle).IsGreaterThan(TimeSpan.FromMilliseconds(250))
            .Because("the agent treats a burst as one paste and swallows the return inside it; "
                   + "the pause is what makes the return a keypress.");
        await Assert.That(typing[0].Settle).IsLessThan(TimeSpan.FromSeconds(10))
            .Because("it is inside the ninety seconds the ceremony waits, and a person is "
                   + "watching a prompt while it passes.");
        await Assert.That(typing[1].Settle).IsEqualTo(TimeSpan.Zero)
            .Because("nothing follows the return; what comes next is the agent's answer.");
    }

    [Test]
    public async Task An_empty_code_is_still_a_submit()
    {
        // NOT THIS LAYER'S REFUSAL. The contract bounds the code and the
        // dispatch drops an empty one; if one arrives here anyway, typing
        // nothing and pressing return is what a person would see, and the
        // agent's own answer is better than silence invented here.
        var typing = SetupTokenScreen.Typing("");

        await Assert.That(typing.Count).IsEqualTo(2);
        await Assert.That(typing[1].Text).IsEqualTo("\r");
    }
}
