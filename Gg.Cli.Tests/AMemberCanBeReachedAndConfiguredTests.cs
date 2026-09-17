using Gg.Client;
using Gg.Cli;
using Gg.Local;

namespace Gg.Cli.Tests;

/// <summary>
/// One place decides whether a runner may be given a credential, and a pool
/// member can be reached at all.
/// </summary>
/// <remarks>
/// <para>
/// <b>The gate is a seam rather than a condition in the root.</b> Two
/// composition paths bring a runner up - a person's <c>gg runner up</c> and a
/// member's nonce redemption - and a rule written twice is a rule that drifts.
/// This is the same discipline the configuration reader already has: one place
/// reads, nothing downstream reaches a second answer.
/// </para>
/// <para>
/// <b>And a member had no key, which made all of this unreachable where it
/// matters most.</b> <c>MemberUpAsync</c> called <c>RunnerHost.RunAsync</c>
/// without an <c>identityKey</c>, so <c>attendedSessions</c> was null, so no
/// channel existed and no dispatch was ever constructed. A pool member is the
/// one machine class with no other way to receive a credential - no bind, no
/// file, no operator - and it was the one that could not be reached.
/// </para>
/// </remarks>
public partial class AMemberCanBeReachedAndConfiguredTests
{
    private sealed class ANullStore : ICredentialStore
    {
        public string Root => "/nowhere";

        public string Protection => "nothing, this is a test";

        public string PathFor(string locator) => "/nowhere/x";

        public void Write(string locator, string secret) { }

        public string? Read(string locator) => null;

        // Presence without resolving; this double holds nothing to resolve.
        public bool Holds(string locator) => false;

        public bool Remove(string locator) => false;
    }

    [Test]
    public async Task A_machine_that_has_not_opted_in_is_handed_nowhere_to_keep_one()
    {
        // NULL IS THE GATE, which is why there is no permission check anywhere
        // below this line. A runner handed no port refuses for want of a port,
        // exactly as one handed no private key is unreachable - one mechanism,
        // asserted once, rather than a flag consulted in two places.
        await Assert.That(LocalCredentialKeeper.For(null, new ANullStore())).IsNull();
        await Assert.That(LocalCredentialKeeper.For(new Configuration(), new ANullStore()))
            .IsNull();
        await Assert.That(LocalCredentialKeeper.For(
                new Configuration { AcceptConfigured = false }, new ANullStore()))
            .IsNull();
    }

    [Test]
    public async Task A_machine_that_has_opted_in_is_handed_the_store_a_person_writes_to()
    {
        await Assert.That(LocalCredentialKeeper.For(
                new Configuration { AcceptConfigured = true }, new ANullStore()))
            .IsNotNull();
    }

    [Test]
    public async Task A_member_opting_itself_in_keeps_the_rest_of_its_file()
    {
        // MERGE, NOT REPLACE. ConfigurationFile.Write replaces the whole
        // document, so a member that wrote a fresh Configuration at first start
        // would silently drop whatever its image had baked in - which is the
        // sort of loss nobody notices until a flight cannot reach a forge.
        var opened = LocalCredentialKeeper.Opened(
            new Configuration { RunnerLabels = "linux-x64", AcceptOffered = true });

        await Assert.That(opened.AcceptConfigured).IsTrue();
        await Assert.That(opened.RunnerLabels).IsEqualTo("linux-x64");
        await Assert.That(opened.AcceptOffered).IsTrue();
    }

    [Test]
    public async Task A_member_brings_up_a_key_so_a_console_can_reach_it_at_all()
    {
        // A RATCHET, because the gap it closes was invisible: everything else
        // about the channel was built, tested and correct, and a member simply
        // never got a key - so it read as a machine that would not answer
        // rather than as one nobody wired. Source, because the root is
        // top-level statements and there is nothing to construct.
        var member = Body("MemberUpAsync");

        await Assert.That(member).Contains("identityKey:", StringComparison.Ordinal)
            .Because("a member handed no key has no AttendedSession, so no channel and no "
                   + "dispatch - and a pool member is the one machine class with no other "
                   + "way to be given a credential.");

        await Assert.That(member).Contains("stunServers:", StringComparison.Ordinal)
            .Because("a member behind a NAT with no relay to ask ends in NoRoute, which "
                   + "reads exactly like a machine that is refusing.");
    }

    [Test]
    public async Task A_member_reads_back_the_opt_in_it_just_wrote()
    {
        // MEASURED ON gg-pool-ui-1 AND -2. Both refused a configure
        // introduction - "there is nothing to seal an introduction to" - while
        // their own `gg config show` said a credential may be placed on them.
        // A member writes accept-configured when it redeems its nonce and then
        // asks InForce, which memoizes for the process and had already been
        // read: so the keeper was built from the answer from before the write.
        //
        // AND ITS FIRST LIFE IS ITS WHOLE LIFE. Forget() is called on the
        // console's boot path only, and a member never restarts - it is created
        // warm and replaced when its credential ends. So every fresh member was
        // unreachable, and a member is the one machine class with no other way
        // to be given a credential.
        var member = Body("MemberUpAsync");

        var wrote = member.IndexOf("LocalCredentialKeeper.Opened(", StringComparison.Ordinal);
        var forgot = member.IndexOf("InForce.Forget()", StringComparison.Ordinal);
        var keeps = member.IndexOf("keepCredential:", StringComparison.Ordinal);

        await Assert.That(wrote).IsGreaterThan(-1)
            .Because("this asserts over the member's own opt-in, so the write has to be here.");
        await Assert.That(keeps).IsGreaterThan(-1);

        await Assert.That(forgot).IsGreaterThan(wrote)
            .Because("the cached configuration is the answer from before the write, so what "
                   + "this member may do has to be re-read after it opts itself in.");
        await Assert.That(keeps).IsGreaterThan(forgot)
            .Because("re-reading after the keeper is built changes nothing, and the keeper is "
                   + "what the heartbeat's accepts-configuration is derived from.");
    }

    [Test]
    public async Task Both_ways_a_runner_comes_up_ask_the_same_question()
    {
        // THE DRIFT THIS PREVENTS IS THE DANGEROUS DIRECTION. A member path
        // that constructed a keeper directly would be a machine accepting
        // credentials with nothing written down saying it may.
        foreach (var path in (string[])["RunnerUpAsync", "MemberUpAsync"])
        {
            var body = Body(path);

            if (!body.Contains("LocalCredentialKeeper", StringComparison.Ordinal))
            {
                continue;
            }

            await Assert.That(body).Contains("LocalCredentialKeeper.For", StringComparison.Ordinal)
                .Because($"{path} builds a keeper without going through the one gate, so "
                       + "whether this machine may be given a credential is decided twice.");
        }
    }

    /// <summary>The source of one method in the composition root.</summary>
    /// <remarks>
    /// Braces counted rather than matched by a pattern, because a regex that
    /// tried to span a method body is a regex that stops at the first nested
    /// one and quietly asserts over half of what it named.
    /// </remarks>
    private static string Body(string method)
    {
        var source = File.ReadAllText(RootPath());
        var at = source.IndexOf(
            $"static async Task<int> {method}(", StringComparison.Ordinal);

        if (at < 0)
        {
            // LOUD, because a walk that found nothing and asserted over an
            // empty string would pass and mean nothing - which is the shape
            // every source ratchet in this repository is written to avoid.
            throw new InvalidOperationException(
                $"'{method}' is not in the composition root any more, so this test is "
              + "asserting over nothing. Rename it here, or find where it went.");
        }

        var open = source.IndexOf('{', at);
        var depth = 0;

        for (var i = open; i < source.Length; i++)
        {
            depth += source[i] switch { '{' => 1, '}' => -1, _ => 0 };

            if (depth == 0)
            {
                return source[open..i];
            }
        }

        return source[open..];
    }

    private static string RootPath()
    {
        var here = new DirectoryInfo(AppContext.BaseDirectory);

        while (here is not null && !File.Exists(Path.Combine(here.FullName, "Gg.Cli", "Program.cs")))
        {
            here = here.Parent;
        }

        return Path.Combine(here!.FullName, "Gg.Cli", "Program.cs");
    }
}
