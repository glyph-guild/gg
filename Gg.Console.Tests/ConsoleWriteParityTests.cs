using Gg.Client;
using Gg.Contracts;

namespace Gg.Console.Tests;

/// <summary>
/// The writes the console can half do.
/// </summary>
/// <remarks>
/// <para>
/// <b>A credential the console registers can only ever read.</b>
/// <c>ConsoleData.AddAsync</c> hard-codes <c>[read]</c>, so a runner that must
/// land work needs a credential this console silently cannot grant - and
/// nothing says so. The person registers one, the flight runs, and the push at
/// the end fails at the credential.
/// </para>
/// <para>
/// <b>And a store you cannot clean is a store people work around.</b>
/// <c>RemoveCredentialAsync</c> has been on <c>ConsoleData</c> with no caller
/// since it was written, which is the half of credential management that
/// matters when one leaks.
/// </para>
/// <para>
/// <b>Rule 7 throughout.</b> The value is never a parameter and never reaches
/// the model: <c>CredentialCommands</c> prompts for it, so no frame in this
/// project holds it. What the console keeps is the REFERENCE - kind, locator,
/// identity, scopes - which is what crosses the wire anyway.
/// </para>
/// </remarks>
public class ConsoleWriteParityTests
{
    private sealed class Held : ISessionStore
    {
        public StoredSession? Read() => new()
        {
            SessionToken = "t",
            ExpiresAt = new DateTimeOffset(2099, 1, 1, 0, 0, 0, TimeSpan.Zero),
            TenantId = "tenant",
            PrincipalDisplay = "a-person",
        };

        public void Write(StoredSession session) { }

        public void Clear() { }
    }

    private sealed class NoStore : ICredentialStore
    {
        public string Root => "(none)";

        public string Protection => "nothing is stored";

        public string PathFor(string locator) => "(none)";

        public void Write(string locator, string secret) { }

        public string? Read(string locator) => null;

        public bool Remove(string locator) => false;
    }

    /// <summary>Answers each prompt by what it asked for.</summary>
    private sealed class Asked(params string[] lines) : ISecretPrompt
    {
        private int _at;

        internal List<string> Prompts { get; } = [];

        public string ReadSecret(string prompt) => "s3cret-value";

        public string ReadLine(string prompt)
        {
            Prompts.Add(prompt);
            return _at < lines.Length ? lines[_at++] : "";
        }
    }

    private static ConsoleData Unreachable(ISecretPrompt prompt) =>
        new(new FlightCommands(Client(), new Held()),
            new CredentialCommands(Client(), new Held(), new NoStore(), prompt),
            new TakeCommands(Client(), new Held()),
            new IdentityCommands(Client(), new Held()),
            new EnvelopeCommands(Client(), new Held()));

    private static ControlPlaneClient Client() =>
        new(new HttpClient { BaseAddress = new Uri("http://127.0.0.1:1/") });

    [Test]
    public async Task A_credential_can_be_registered_with_a_write_scope()
    {
        // S28.5-03. The console could grant read and nothing else, so a runner
        // that must land work needed a credential registered from somewhere
        // this console is not.
        var prompt = new Asked("acme/widgets", "write", "a-bot");
        var actions = new VerbConsoleActions(Unreachable(prompt), prompt);

        _ = actions.AddCredential();

        await Assert.That(prompt.Prompts.Any(p =>
                p.Contains("scope", StringComparison.OrdinalIgnoreCase))).IsTrue()
            .Because("the scope is a decision a person makes, and one nobody was offered.");
    }

    [Test]
    public async Task A_scope_nobody_asked_for_is_refused_by_name()
    {
        // Not silently narrowed to read. A person who typed `admin` and got a
        // read credential would find out at the push, one flight later.
        var prompt = new Asked("acme/widgets", "admin", "a-bot");
        var actions = new VerbConsoleActions(Unreachable(prompt), prompt);

        var said = actions.AddCredential();

        await Assert.That(said).Contains("admin")
            .Because("the refusal names what was refused, or it reads as a bug.");
        await Assert.That(said).Contains("read")
            .Because("and what could have been asked for instead.");
    }

    [Test]
    public async Task Nothing_typed_at_the_scope_prompt_registers_a_reading_credential()
    {
        // The narrow answer is the default, and pressing return is how a person
        // says "the ordinary one".
        var prompt = new Asked("acme/widgets", "", "a-bot");
        var actions = new VerbConsoleActions(Unreachable(prompt), prompt);

        var said = actions.AddCredential();

        await Assert.That(said).DoesNotContain("is not a scope")
            .Because("an empty answer is not a wrong answer.");
    }

    [Test]
    public async Task A_credential_can_be_forgotten()
    {
        // S28.5-04, and it is the half that matters when one leaks.
        await Assert.That(ShellCommands.Handled).Contains(Command.ForgetCredential)
            .Because("it talks to the control plane, so its effect belongs where the "
                   + "terminal is free.");

        var prompt = new Asked("acme/widgets");
        var actions = new VerbConsoleActions(Unreachable(prompt), prompt);

        var said = actions.ForgetCredential();

        await Assert.That(said).IsNotEmpty()
            .Because("a key that says nothing is a key that looks broken.");
    }

    [Test]
    public async Task Neither_write_puts_a_secret_anywhere_the_model_can_reach()
    {
        // Rule 7. The failure path, because a diagnostic is where a secret
        // leaks: every call below fails, and the sentence it returns is the one
        // a person is shown and a bundle records.
        var prompt = new Asked("acme/widgets", "write", "a-bot");
        var actions = new VerbConsoleActions(Unreachable(prompt), prompt);

        foreach (var said in (string[])[actions.AddCredential(), actions.ForgetCredential()])
        {
            await Assert.That(said).DoesNotContain("s3cret-value")
                .Because("AppState is written to disk under GG_STATE_DUMP and handed to the "
                       + "diagnostics bundle, so a value in a sentence is a value in both.");
        }
    }
}
