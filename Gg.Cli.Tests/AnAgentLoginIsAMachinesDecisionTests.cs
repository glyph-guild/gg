using System.Text.RegularExpressions;
using Gg.Client;
using Gg.Contracts;
using Gg.Local;
using Gg.Runner;

namespace Gg.Cli.Tests;

/// <summary>
/// Whether a runner will START its agent's login ceremony when asked to over
/// the channel is a decision made on that machine, in its own file, under its
/// own key: <c>accept-agent-login</c>.
/// </summary>
/// <remarks>
/// <para>
/// <b>Its own key, because this is the first SPAWNING verb.</b>
/// <c>accept-configured</c> lets a person put a secret on this machine;
/// this lets a person make this machine run a program that mints one. The
/// channel's own tests say a fourth verb must not inherit the third's gate
/// <i>"without somebody deciding it is enough"</i>, and it is not: a machine
/// that agreed to be HANDED a token has not agreed to START anything.
/// </para>
/// <para>
/// <b>Closed on members, by decision.</b> A member's first start opens
/// <c>accept-configured</c> on the authority of its nonce; it opens nothing
/// else. Members receive the token by <c>gg credential send --agent</c>; the
/// ceremony runs on residents and laptops, whose files a person can open.
/// </para>
/// </remarks>
public class AnAgentLoginIsAMachinesDecisionTests
{
    private sealed class ANullStore : ICredentialStore
    {
        public string Root => "/nowhere";

        public string Protection => "nothing, this is a test";

        public string PathFor(string locator) => "/nowhere/x";

        public void Write(string locator, string value) { }

        public string? Read(string locator) => null;

        public bool Holds(string locator) => false;

        public bool Remove(string locator) => false;
    }

    private static readonly ExecutorDeclaration Claude = ExecutorDeclaration.Parse("claude", "GG_EXECUTOR_BINARY");

    [Test]
    public async Task A_machine_is_closed_until_its_file_says_otherwise()
    {
        await Assert.That(new Configuration().AcceptAgentLogin).IsNull()
            .Because("absent is off. A program started on a machine nobody opened is the "
                   + "standing grant this path exists to not be.");

        await Assert.That(LocalAgentLogin.For(null, new ANullStore(), Claude)).IsNull();
        await Assert.That(LocalAgentLogin.For(new Configuration(), new ANullStore(), Claude)).IsNull();
        await Assert.That(LocalAgentLogin.For(
                new Configuration { AcceptAgentLogin = false }, new ANullStore(), Claude))
            .IsNull();
    }

    [Test]
    public async Task Accept_configured_does_not_open_it()
    {
        // THE WHOLE POINT OF A SECOND KEY. A machine that takes credentials
        // over the channel has not agreed to start programs over it.
        await Assert.That(LocalAgentLogin.For(
                new Configuration { AcceptConfigured = true }, new ANullStore(), Claude))
            .IsNull()
            .Because("being handed a secret and being made to mint one are two decisions.");
    }

    [Test]
    public async Task A_machine_that_has_opted_in_is_handed_the_ceremonys_ports()
    {
        var ports = LocalAgentLogin.For(
            new Configuration { AcceptAgentLogin = true }, new ANullStore(), Claude);

        await Assert.That(ports).IsNotNull();
        await Assert.That(ports!.Runs).IsTypeOf<SetupTokenLogin>()
            .Because("the adapter that drives claude's own setup-token, under the binary the "
                   + "declaration named.");
        await Assert.That(ports.Keeps).IsNotNull()
            .Because("what the child mints has to land somewhere this runner reads at launch.");
    }

    [Test]
    public async Task It_survives_the_file_it_is_written_to()
    {
        var parsed = ConfigurationFile.Parse(
            ConfigurationFile.Render(new Configuration { AcceptAgentLogin = true }));

        await Assert.That(parsed.Configuration!.AcceptAgentLogin).IsTrue();
    }

    [Test]
    public async Task Nothing_but_the_file_can_turn_it_on()
    {
        // NO VARIABLE, for accept-configured's reason: a variable is a second
        // way to turn it on, and one a container image or a unit file could
        // carry without anybody reading it.
        await Assert.That(Configuration.Members.Any(
                m => string.Equals(m.Key, "accept-agent-login", StringComparison.Ordinal)
                  && m.Variable is { Length: > 0 }))
            .IsFalse()
            .Because("a setting with a variable is one an image can carry silently.");

        // AND IT CANNOT BE OFFERED. A control plane able to set this could make
        // a machine start a program that mints a credential - Article VIII by
        // a longer route.
        await Assert.That(OfferableKeys.All).DoesNotContain("accept-agent-login");
    }

    [Test]
    public async Task A_member_opening_accept_configured_does_not_open_this()
    {
        var opened = LocalCredentialKeeper.Opened(new Configuration());

        await Assert.That(opened.AcceptConfigured).IsTrue();
        await Assert.That(opened.AcceptAgentLogin).IsNull()
            .Because("the nonce bought one door, and this is the other one.");
    }

    [Test]
    public async Task A_member_is_never_handed_the_ceremony_and_a_resident_is_asked_the_one_question()
    {
        // THE DRIFT THIS PREVENTS IS THE DANGEROUS DIRECTION: a member path
        // that built the ceremony's ports would be a container starting
        // programs on a channel with nothing written down saying it may.
        var member = Body("MemberUpAsync");
        await Assert.That(member).DoesNotContain("LocalAgentLogin")
            .Because("closed on members by decision, and the decision is that the member "
                   + "path never asks.");
        await Assert.That(member).DoesNotContain("login:")
            .Because("the port's default is null, and a member leaves it there.");

        var resident = Body("RunnerUpAsync");
        await Assert.That(resident).Contains("LocalAgentLogin.For")
            .Because("a resident asks its file, through the one gate, and nowhere else.");
    }

    /// <summary>The source of one method in the composition root.</summary>
    private static string Body(string method)
    {
        var source = File.ReadAllText(Path.Combine(Root(), "Gg.Cli", "Program.cs"));
        var at = Regex.Match(source, $@"static async Task<int> {Regex.Escape(method)}\(");
        if (!at.Success)
        {
            throw new InvalidOperationException($"{method} was not found in Program.cs");
        }

        var open = source.IndexOf('{', at.Index);
        var depth = 0;
        for (var i = open; i < source.Length; i++)
        {
            depth += source[i] switch { '{' => 1, '}' => -1, _ => 0 };
            if (depth == 0)
            {
                return source[at.Index..(i + 1)];
            }
        }

        throw new InvalidOperationException($"{method}'s body never closed");
    }

    private static string Root()
    {
        var here = new DirectoryInfo(AppContext.BaseDirectory);
        while (here is not null && !File.Exists(Path.Combine(here.FullName, "Gg.sln")))
        {
            here = here.Parent;
        }

        return here?.FullName ?? throw new InvalidOperationException("Gg.sln not found");
    }
}
