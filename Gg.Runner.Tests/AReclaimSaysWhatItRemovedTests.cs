using Gg.Contracts;
using Gg.Contracts.Description;

namespace Gg.Runner.Tests;

/// <summary>
/// What a reclaim removed is on the flight's evidence, and a reclaim that found
/// nothing says so.
/// </summary>
/// <remarks>
/// <para>
/// <b>Slice fifty-six rule 5, which is slice fifty-four's rule 8 one level
/// down:</b> a reclaim that cannot prove it happened is a failure. When a preview
/// flight dies on a port already bound, the question a person needs answered is
/// <i>was this instance emptied first, and what was in it?</i> — and nothing else
/// in the system can answer it afterwards, because the containers are gone.
/// </para>
/// <para>
/// <b>Its own kind rather than <c>environment.identity</c>.</b> That fact is
/// "what ran, and where" about the WORKER — host fingerprint, image digest, tool
/// versions — and this is about the environment the worker's stack runs in.
/// Folding them would conflate the two environments this slice family spent eight
/// rounds separating, in the one payload a reader goes to for either.
/// </para>
/// <para>
/// <b>Zero is a value, not an absence.</b> An instance that was already clean is
/// the ordinary state of a fresh slot and of one whose last flight brought its own
/// stack down politely. A fact shipped only when something was removed would make
/// "already clean" and "never reclaimed" the same reading, and only one of those
/// means the environment is trustworthy.
/// </para>
/// <para>
/// <b>And a flight hosted nowhere ships none.</b> There was no daemon to empty,
/// so a fact saying it removed nothing would be a claim about work that was never
/// attempted.
/// </para>
/// </remarks>
public class AReclaimSaysWhatItRemovedTests
{
    [Test]
    public async Task The_kind_is_in_the_pinned_vocabulary()
    {
        // A FACT THE LIST DOES NOT CONTAIN IS REJECTED LOUDLY, which is the
        // point of the list - so a payload shipped under an unregistered kind
        // arrives as a refusal rather than as evidence.
        await Assert.That(FactKinds.All).Contains(FactKinds.EnvironmentReclaimed);
    }

    [Test]
    public async Task It_says_which_instance_and_what_went()
    {
        var members = ProtocolSurface.JsonMembers[typeof(EnvironmentReclaimed)];

        await Assert.That(members).Contains("instance")
            .Because("a fact that cannot say which instance it emptied is unreadable on a host "
                   + "with two, and the grant that would have answered it is released by the "
                   + "time anybody asks.");
        await Assert.That(members).Contains("containers");
        await Assert.That(members).Contains("networks");
        await Assert.That(members).Contains("volumes");
    }

    [Test]
    public async Task The_envelope_carries_it()
    {
        // THE SLOT THE DIGEST DOES NOT COVER. A payload type declared with no
        // slot on FactEnvelope serializes to nothing, and Gg.Contracts.Tests
        // stays green while the fact arrives empty.
        await Assert.That(ProtocolSurface.JsonMembers[typeof(FactEnvelope)])
            .Contains("reclaimed");
    }

    [Test]
    public async Task A_reclaim_that_removed_nothing_is_still_a_fact()
    {
        // ZERO IS A VALUE. "Already clean" and "never reclaimed" must not read
        // the same, because only one of them means this flight's environment is
        // trustworthy.
        var nothing = new EnvironmentReclaimed
        {
            Instance = "gg-env-1",
            Containers = 0,
            Networks = 0,
            Volumes = 0,
        };

        await Assert.That(EnvironmentReclaimed.Validate(nothing)).IsNull();
    }

    [Test]
    public async Task A_negative_count_is_refused()
    {
        // The machine reading's rule: no quantity of anything is negative, and a
        // count that could be is a count nobody can add up.
        await Assert.That(EnvironmentReclaimed.Validate(new EnvironmentReclaimed
        {
            Instance = "gg-env-1",
            Containers = -1,
            Networks = 0,
            Volumes = 0,
        })).IsNotNull();
    }

    [Test]
    public async Task A_reclaim_of_no_instance_is_refused()
    {
        // A fact naming no instance is one nobody can act on - and it is what a
        // flight hosted nowhere would ship if it shipped anything at all, which
        // is why it ships nothing.
        await Assert.That(EnvironmentReclaimed.Validate(new EnvironmentReclaimed
        {
            Instance = "  ",
            Containers = 0,
            Networks = 0,
            Volumes = 0,
        })).IsNotNull();
    }
}
