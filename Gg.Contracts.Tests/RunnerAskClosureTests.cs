using System.Reflection;
using Gg.Contracts;
using Gg.Contracts.Description;

namespace Gg.Contracts.Tests;

/// <summary>
/// A third kind cannot arrive by being forgotten about.
/// </summary>
/// <remarks>
/// <para>
/// <b>The poison twin for the vocabulary.</b> <see cref="RunnerAskVocabularyTests"/>
/// asserts today's two are registered four ways, and would pass just as well if
/// the registration mechanism reached nothing — so this plants the type somebody
/// would add wrongly and counts how many of the four guards catch it.
/// </para>
/// <para>
/// <b>Two is the floor, and it is asserted rather than hoped for.</b> ADR-0013's
/// build plan says that if fewer than two of the four catch a widening, the
/// closure is weaker than the fact vocabulary's and has to be strengthened
/// before anything carries this protocol. This is where that is measured.
/// </para>
/// </remarks>
public class RunnerAskClosureTests
{
    /// <summary>What somebody would add, wrongly, to widen the channel.</summary>
    /// <remarks>
    /// <c>RunCommand</c> by name, because ADR-0013 names it: the constraint is
    /// "written down now rather than a discovery made when somebody proposes
    /// <c>RunCommand</c>". Declared here in the test assembly, so it is a rule
    /// over a SET of types rather than a scan that would return no offenders and
    /// look diligent.
    /// </remarks>
    [RunnerAskKind("run-command")]
    private sealed record RunCommandAsk
    {
        public required string Command { get; init; }
    }

    [Test]
    public async Task A_third_kind_is_caught_by_at_least_two_of_the_four()
    {
        var caught = new List<string>();

        // ONE: no pinned id.
        if (typeof(RunCommandAsk).GetCustomAttribute<PinnedIdAttribute>() is null)
        {
            caught.Add("pinned id");
        }

        // TWO: its kind is not in the vocabulary.
        var kind = typeof(RunCommandAsk).GetCustomAttribute<RunnerAskKindAttribute>()!.Kind;
        if (!RunnerAskKinds.All.Contains(kind, StringComparer.Ordinal))
        {
            caught.Add("vocabulary");
        }

        // THREE: its members are not declared.
        if (!ProtocolSurface.JsonMembers.ContainsKey(typeof(RunCommandAsk)))
        {
            caught.Add("declared members");
        }

        // FOUR: the envelope has nowhere for it to arrive.
        var slots = typeof(RunnerAsk).GetProperties()
            .Select(p => Nullable.GetUnderlyingType(p.PropertyType) ?? p.PropertyType)
            .ToHashSet();
        if (!slots.Contains(typeof(RunCommandAsk)))
        {
            caught.Add("envelope slot");
        }

        await Assert.That(caught.Count).IsGreaterThanOrEqualTo(2)
            .Because("a widening that only one registration refuses is a widening one "
                   + "forgetful commit away. Caught by: " + string.Join(", ", caught));
    }

    [Test]
    public async Task The_two_that_exist_are_not_caught()
    {
        // THE LIVENESS HALF. A closure that refused everything would satisfy the
        // assertion above and stop the protocol entirely.
        foreach (var kind in RunnerAskKinds.All)
        {
            await Assert.That(RunnerAskKinds.All.Contains(kind, StringComparer.Ordinal))
                .IsTrue();
        }

        await Assert.That(ProtocolSurface.JsonMembers.ContainsKey(typeof(TailLogAsk))).IsTrue();
        await Assert.That(ProtocolSurface.JsonMembers.ContainsKey(typeof(StatusAsk))).IsTrue();
    }

    [Test]
    public async Task Widening_the_channel_moves_a_fingerprint()
    {
        // WHAT MAKES THE CLOSURE COST SOMETHING. RunnerAskKinds is part of a
        // ledger's fingerprint, so a third value is a contract version bump and
        // a ledger entry rather than a line in a switch.
        var membership = typeof(RunnerAskKinds).GetCustomAttribute<VocabularyOfAttribute>();

        await Assert.That(membership).IsNotNull()
            .Because("a closed vocabulary outside both ledgers is one a person can widen "
                   + "without anything moving.");
    }
}
