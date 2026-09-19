using Gg.Client;

namespace Gg.Console.Tests;

/// <summary>
/// A pasted line the console cannot read as a flight is refused in a sentence,
/// not thrown out of the loop.
/// </summary>
/// <remarks>
/// <para>
/// <b>It took the console down.</b> <c>ConsoleData.FlyAsync</c> refused a
/// half-ticket like <c>ado#</c> with an <see cref="InvalidOperationException"/>,
/// and <c>VerbConsoleActions</c> catches the refusals it expects by type - a
/// list that does not include that one, because nothing else in the client
/// throws it for a person's input. So the refusal escaped <c>Fly</c>, escaped
/// <c>ConsoleLoop.Run</c>, and ended the process over a typo.
/// </para>
/// <para>
/// <b>It is the intent being refused</b>, and the client already has a type for
/// exactly that: <see cref="FlightIntentException"/>, which the contract's own
/// validation throws for a request it would not send.
/// </para>
/// </remarks>
public class APasteTheConsoleRefusesIsSaidTests
{
    [Test]
    [Arguments("ado#")]
    [Arguments("#42")]
    public async Task A_half_ticket_is_refused_in_a_sentence(string pasted)
    {
        var (data, _) = AConsolePlane.Console();
        var actions = new VerbConsoleActions(data, new NeverAsked());

        var said = actions.Fly(pasted, [], null);

        await Assert.That(said).StartsWith("Nothing was opened")
            .Because("a typo is a sentence on the activity line, not the end of the console.");
        await Assert.That(said).Contains("looks like a ticket")
            .Because("and the sentence is the parser's, which knows what was meant.");
    }

    private sealed class NeverAsked : ISecretPrompt
    {
        public string ReadSecret(string prompt) =>
            throw new InvalidOperationException("nothing about opening a flight asks for a secret.");

        public string ReadLine(string prompt) =>
            throw new InvalidOperationException("nothing about opening a flight asks for a line.");
    }
}
