using Gg.Client;

namespace Gg.Client.Tests;

/// <summary>
/// What a person pastes into the secret prompt is what the far end is handed,
/// and they can tell that something arrived without it being echoed.
/// </summary>
/// <remarks>
/// <para>
/// <b>Bracketed paste is not a control character.</b> A terminal wraps a paste
/// in <c>ESC[200~</c> and <c>ESC[201~</c>; the reader drops the ESC because it
/// is a control character and keeps <c>[200~</c>, so the secret a person pasted
/// arrives with five characters on the front and five on the back. An agent
/// login code mangled that way is refused by the agent, and the refusal says
/// only that no token was printed.
/// </para>
/// <para>
/// <b>And a count is not an echo.</b> A person pasting into a prompt that shows
/// nothing cannot tell a paste that landed from one that did not - which is how
/// three attempts went by without anybody being able to say which was being
/// tested.
/// </para>
/// </remarks>
public class TheSecretPromptTakesAPasteTests
{
    [Test]
    public async Task A_bracketed_paste_arrives_as_what_was_pasted()
    {
        await Assert.That(ConsoleSecretPrompt.Pasted("[200~sk-ant-oat01-abc[201~"))
            .IsEqualTo("sk-ant-oat01-abc");
    }

    [Test]
    public async Task Text_that_merely_looks_like_a_marker_is_left_alone()
    {
        // NOT A BLIND STRIP. A secret is somebody else's bytes, and cutting
        // five characters off one that happens to start with a bracket would
        // be this side corrupting what it was handed.
        await Assert.That(ConsoleSecretPrompt.Pasted("[200~only-the-front"))
            .IsEqualTo("only-the-front");
        await Assert.That(ConsoleSecretPrompt.Pasted("[2001~keep-this"))
            .IsEqualTo("[2001~keep-this");
        await Assert.That(ConsoleSecretPrompt.Pasted("plain")).IsEqualTo("plain");
        await Assert.That(ConsoleSecretPrompt.Pasted("")).IsEqualTo("");
    }

    [Test]
    public async Task What_arrived_is_counted_rather_than_echoed()
    {
        var said = ConsoleSecretPrompt.Received(96);

        await Assert.That(said).Contains("96");
        await Assert.That(said).DoesNotContain("sk-ant");
    }

    [Test]
    public async Task Nothing_pasted_says_so()
    {
        await Assert.That(ConsoleSecretPrompt.Received(0)).Contains("nothing")
            .Because("an empty paste is the case a person most needs told about, and it is "
                   + "the one a count of zero would whisper.");
    }
}
