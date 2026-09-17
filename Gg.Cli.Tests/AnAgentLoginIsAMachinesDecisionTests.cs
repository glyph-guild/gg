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
/// <b>Open on members, by a later decision that reversed the first.</b> A
/// member's first start opened <c>accept-configured</c> on the authority of its
/// nonce and nothing else, so a member took its token only by
/// <c>gg credential send --agent</c>. On 2026-09-17 a person pressed Login on an
/// agent-login gate for a pool member, was told it was closed by decision, and
/// decided it should not be: a member is the machine most in need of the
/// ceremony, because it is the one nobody can open a shell on. So a member's
/// first start opens this too, on the same authority, IN ITS OWN FILE - there is
/// still no variable and the control plane still cannot offer it.
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
    public async Task A_member_opens_both_doors_at_first_start()
    {
        // THE REVERSAL, AND IT IS STILL TWO DOORS. Accept_configured_does_not_
        // open_it above still holds for a machine a person opens by hand; what
        // changed is that the member, which nobody can open by hand, opens both
        // on the nonce's authority - written, so `gg config show` inside the
        // container says so.
        var opened = LocalCredentialKeeper.Opened(new Configuration());

        await Assert.That(opened.AcceptConfigured).IsTrue();
        await Assert.That(opened.AcceptAgentLogin).IsTrue()
            .Because("a member with a Login button that answers 'closed' is a member nobody "
                   + "can log in from a console, and that was decided to be wrong.");
    }

    [Test]
    public async Task Both_ways_a_runner_comes_up_ask_the_file_through_the_one_gate()
    {
        // STILL THE DANGEROUS DIRECTION, and now it guards both paths: a member
        // or a resident that built the ceremony's ports directly would be a
        // machine starting programs on a channel with nothing written down
        // saying it may. Both go through LocalAgentLogin.For, which reads the
        // file and nothing else.
        foreach (var path in (string[])["MemberUpAsync", "RunnerUpAsync"])
        {
            var body = Body(path);

            await Assert.That(body).Contains("login:")
                .Because($"{path} has to hand the runner the ceremony's ports, or a console "
                       + "pressing Login is told the machine is closed.");
            await Assert.That(body).Contains("LocalAgentLogin.For")
                .Because($"{path} asks its file, through the one gate, and nowhere else.");
        }
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
