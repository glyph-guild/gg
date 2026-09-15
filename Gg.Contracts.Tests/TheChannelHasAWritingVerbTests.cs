using Gg.Contracts;

namespace Gg.Contracts.Tests;

/// <summary>
/// The channel's verbs, split into the ones that read and the one that writes.
/// </summary>
/// <remarks>
/// <para>
/// <b>Two correct changes landed a week apart and their conjunction is
/// something neither argued for.</b> <c>configure-credential</c> was added with
/// a safety argument that leaned on a lease - <i>reachable only inside a
/// lease</i> - because at the time a channel existed only while a flight did. In
/// the same stretch the channel's lifetime moved to the conversation, so that a
/// person could attach to a runner in the one state they most want to: waiting
/// for work. That change's own argument was that <i>what can be READ did not
/// widen</i>, which was true of the two verbs its author was looking at.
/// </para>
/// <para>
/// <b>The result is a verb that WRITES, reachable on an idle runner, with no
/// lease anywhere.</b> Nothing was wrong with either change. What was missing is
/// anything that would notice the combination, which is what this file is.
/// </para>
/// <para>
/// <b>And the combination is defensible - it just has to be argued rather than
/// inherited.</b> What restrains a write now is four things and none of them is
/// a flight: the control plane introduces only the principal who REGISTERED the
/// runner; the machine must have been wired with a private key at all; a
/// conversation nobody has asked anything of for <c>AttendedSession.QuietFor</c>
/// is dropped, which bounds how long a write stays reachable after somebody
/// walks away; and the machine's own file must say <c>accept-configured</c>.
/// </para>
/// <para>
/// <b>That last one is a DEFECT rather than a design, and saying so is the
/// point.</b> It is the only restraint in the list that lives on the machine
/// being written to rather than in the introduction that authorised the write -
/// so it answers "may anyone put a secret here" and not "may THIS caller". It
/// ended up load-bearing because the lease it stood beside went away, not
/// because anybody chose it for the job. A fourth verb should not inherit it
/// without somebody deciding it is enough.
/// </para>
/// <para>
/// <b>Named here rather than derived, because the point is that a new value
/// forces somebody to say which half it joins.</b> A partition computed from an
/// attribute would put a third writing verb on the writing side automatically,
/// which is the opposite of what this is for.
/// </para>
/// </remarks>
public class TheChannelHasAWritingVerbTests
{
    /// <summary>Verbs that answer with something and change nothing.</summary>
    private static readonly string[] Reads =
        [RunnerAskKinds.TailLog, RunnerAskKinds.Status];

    /// <summary>Verbs that change this machine.</summary>
    /// <remarks>
    /// <b>One, and it should stay hard to add to.</b> ADR-0013's whole argument
    /// for a closed vocabulary is that a general data channel to a component
    /// <c>CLAUDE.md</c> calls hostile is a bad idea. A second writing verb is a
    /// much larger decision than a second reading one, and nothing else in the
    /// build distinguishes them.
    /// </remarks>
    private static readonly string[] Writes =
        [RunnerAskKinds.ConfigureCredential];

    /// <summary>Verbs that make this machine START a program.</summary>
    /// <remarks>
    /// <b>A third side, because a fourth verb must not inherit the third's
    /// gate without somebody deciding it is enough</b> - and it is not. A
    /// machine that agreed to be HANDED a secret (<c>accept-configured</c>)
    /// has not agreed to run its agent's login ceremony on a console's say-so.
    /// Both halves of the ceremony are here: the begin starts the child and
    /// the finish types into it, and neither is answerable without the port
    /// <c>accept-agent-login</c> hands the dispatch.
    /// </remarks>
    private static readonly string[] Spawns =
        [RunnerAskKinds.BeginAgentLogin, RunnerAskKinds.FinishAgentLogin];

    [Test]
    public async Task Every_verb_is_on_exactly_one_side()
    {
        foreach (var kind in RunnerAskKinds.All)
        {
            var sides = new[] { Reads, Writes, Spawns }
                .Count(side => side.Contains(kind, StringComparer.Ordinal));
            await Assert.That(sides == 1)
                .IsTrue()
                .Because($"'{kind}' is in the vocabulary and this file does not say whether it "
                       + "reads or writes. That sentence is the one a reviewer needs, and it "
                       + "is the one nobody wrote when the channel's lifetime changed.");
        }

        await Assert.That(Reads.Length + Writes.Length + Spawns.Length).IsEqualTo(RunnerAskKinds.All.Count)
            .Because("a verb named here and retired from the vocabulary leaves a claim about "
                   + "a channel that no longer has it.");
    }

    [Test]
    public async Task A_spawning_verb_needs_its_own_port()
    {
        // THE DECISION THE THIRD SIDE RECORDS. A spawning verb is not a writing
        // verb wearing a new name: it is gated on accept-agent-login, a key of
        // its own, so a machine that opened accept-configured is still closed
        // to it - asserted against the gate in AnAgentLoginIsAMachinesDecisionTests
        // and against the dispatch in ARunnerLogsItsAgentInOverTheChannelTests.
        await Assert.That(Spawns).Contains(RunnerAskKinds.BeginAgentLogin);
        await Assert.That(Spawns).Contains(RunnerAskKinds.FinishAgentLogin)
            .Because("the finish types into the child the begin started; it is the same "
                   + "program, and it needs the same port.");

        foreach (var spawning in Spawns)
        {
            await Assert.That(Writes).DoesNotContain(spawning)
                .Because("a spawning verb listed as a writing one would read as answerable "
                       + "wherever accept-configured is, which is the inheritance this side "
                       + "exists to refuse.");
        }
    }

    [Test]
    public async Task The_channel_is_no_longer_read_only_and_nothing_may_say_it_is()
    {
        // THE SENTENCE THIS FILE EXISTS TO RETIRE. It was true, it was written
        // down in four places, and it stopped being true without any of them
        // changing. Asserted as a fact about the vocabulary so that the day it
        // becomes true again - if a writing verb is ever removed - somebody has
        // to come back here rather than discovering it by reading.
        await Assert.That(Writes).IsNotEmpty()
            .Because("if this is ever empty again, the four remarks that used to say "
                   + "'read-only' can say it once more - and until then none of them may.");
    }

    [Test]
    public async Task A_writing_verb_is_answerable_only_where_the_machine_agreed()
    {
        // WHAT ACTUALLY RESTRAINS IT, now that a lease does not. The runner
        // refuses for want of a PORT rather than for want of a permission, and
        // the port exists only when the machine's own file says so - which is
        // asserted against the dispatch in ARunnerKeepsACredentialItIsGivenTests
        // and against the gate in AMemberCanBeReachedAndConfiguredTests.
        //
        // Here, the half those two cannot see: that the writing verb is the one
        // that needs it, and the reading verbs do not.
        await Assert.That(Writes).Contains(RunnerAskKinds.ConfigureCredential);

        foreach (var read in Reads)
        {
            await Assert.That(read).IsNotEqualTo(RunnerAskKinds.ConfigureCredential)
                .Because("a reading verb gated on accept-configured would make a machine "
                       + "that declined to be configured also unwatchable, which is two "
                       + "permissions wearing one name.");
        }
    }
}
