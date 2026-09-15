using Gg.Contracts;

namespace Gg.Contracts.Tests;

/// <summary>
/// <c>run-tests</c> is classified record-only, and it grants an unbounded
/// shell. Both halves are true and the second one is not written down.
/// </summary>
/// <remarks>
/// <para>
/// <b>Measured, from inside a pool member.</b> The browser-environment spike
/// asked whether a reviewer could open a URL and see a change running, and the
/// answer was yes, from inside a container with no published port and no
/// inbound rule anywhere:
/// </para>
/// <para>
/// <c>cloudflared tunnel --url http://localhost:PORT</c> dials out, and the URL
/// it prints is public to anybody who has it. Nothing was opened on the host,
/// nothing was asked of a firewall, and no envelope was consulted — because the
/// whole thing ran under <c>Bash</c>, which is the tool <c>run-tests</c> grants.
/// </para>
/// <para>
/// <b>What the classification claims.</b> <c>MoveKinds</c> is explicit about the
/// stakes of getting this wrong: <i>"record-only is the answer that lets an
/// unrecallable act be granted"</i>, and record-only means <i>"its product is
/// still gated at a destination before anything leaves"</i>. A public URL is not
/// gated at a destination and cannot be unpublished. So the classification is a
/// claim this test exists to stop anybody believing.
/// </para>
/// <para>
/// <b>And it is not reclassified here, deliberately.</b> An outward act <i>"whose
/// enforcement nothing this product has lets a probe confirm is refused at
/// authoring"</i>, so moving <c>run-tests</c> to <c>outward-act</c> would refuse
/// every envelope that declares it — which is every envelope that runs a test.
/// The asymmetry is load-bearing and already stated one consequence deep:
/// <i>"run-tests maps onto Bash, which can also edit files."</i> Reaching the
/// network is the sharper consequence and belongs in the same breath.
/// </para>
/// <para>
/// <b>What would have to exist first.</b> Something a probe can confirm while the
/// tool is GRANTED — which is the exact thing <c>MoveKinds</c> says the
/// confirmable-outward set is empty for want of. The pool proxy is the shape:
/// <c>ScopeProbe</c> reaches outside its prefix and requires a refusal, so the
/// bound is measured rather than asserted. A network bound with that property is
/// a sandbox, and gg does not have one.
/// </para>
/// </remarks>
public class ADeclaredMoveDoesNotBoundTheNetworkTests
{
    [Test]
    public async Task Run_tests_is_record_only()
    {
        // Half one of the contradiction, and the half that is a promise.
        await Assert.That(MoveKinds.Of(LoopMoves.RunTests)).IsEqualTo(MoveKinds.RecordOnly);
    }

    [Test]
    public async Task Nothing_in_the_vocabulary_is_an_outward_act()
    {
        // The enforced set is correctly empty, and that is the reason the
        // classification above cannot simply be corrected: there is no arm for
        // a move whose bound nothing can confirm, so the only available answer
        // is the one that is not quite true.
        var outward = LoopMoves.All
            .Where(m => string.Equals(
                MoveKinds.Of(m), MoveKinds.OutwardAct, StringComparison.Ordinal))
            .ToList();

        await Assert.That(outward).IsEmpty()
            .Because("an outward act whose enforcement no probe can confirm is refused at "
                   + "authoring, so a non-empty set here would be a mechanism waiting for a "
                   + "member - and would refuse every envelope that declared one.");
    }

    [Test]
    public async Task The_classification_says_what_the_move_actually_grants()
    {
        // ANCHORED IN THE SOURCE, because there is nothing else to anchor it
        // in: no code in this repository reaches the network, so the gap cannot
        // be asserted as behaviour. What CAN be held is that a reader who
        // arrives at the line calling run-tests record-only is told, there,
        // what it grants.
        //
        // The words rather than a word: "network" alone appears in this file
        // nowhere else, but a guard that passed on a coincidence is a guard
        // this repository has already been bitten by once.
        var source = Source("MoveKinds.cs");

        var classification = source[source.IndexOf(
            "[LoopMoves.RunTests]", StringComparison.Ordinal)..];

        await Assert.That(source).Contains("Bash", StringComparison.Ordinal)
            .Because("the tool is what makes the claim untrue, and naming the move without "
                   + "naming the tool leaves a reader to go and find the mapping.");

        await Assert.That(source).Contains("tunnel", StringComparison.Ordinal)
            .Because("the measured consequence is a public URL that cannot be unpublished, "
                   + "which is the definition of the kind this is not classified as.");

        await Assert.That(classification.Length).IsGreaterThan(0)
            .Because("the run-tests classification was not found at all, so the assertions "
                   + "above are about a file that no longer says what they assume.");
    }

    [Test]
    public async Task And_the_reason_it_is_not_reclassified_is_written_down_too()
    {
        // A GAP DECLARED WITHOUT ITS REASON READS AS AN OVERSIGHT, and the next
        // person to find it would do the obvious thing: change record-only to
        // outward-act, and refuse every envelope in the product.
        var source = Source("MoveKinds.cs");

        await Assert.That(source).Contains("sandbox", StringComparison.Ordinal)
            .Because("what would have to exist before the classification could honestly "
                   + "change is the useful half of knowing it is wrong.");
    }

    private static string Source(string file)
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);

        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "Gg.sln")))
        {
            directory = directory.Parent;
        }

        return directory is null
            ? throw new InvalidOperationException(
                "The repository root was not found above the test binary, and this guard "
              + "reads the contract's source deliberately.")
            : File.ReadAllText(Path.Combine(directory.FullName, "Gg.Contracts", file));
    }
}
