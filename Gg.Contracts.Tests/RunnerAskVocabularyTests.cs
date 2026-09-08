using System.Reflection;
using Gg.Contracts;
using Gg.Contracts.Description;

namespace Gg.Contracts.Tests;

/// <summary>
/// The ask vocabulary is closed the way the fact vocabulary is.
/// </summary>
/// <remarks>
/// <para>
/// <b>ADR-0013's read-only constraint, made structural.</b> The ADR says a
/// channel that can only answer <c>tail-log</c> and <c>status</c> is defensible
/// where a general one is not, and calls that "a constraint written down now
/// rather than a discovery made when somebody proposes <c>RunCommand</c>". A
/// constraint written down is a comment. These tests are the difference.
/// </para>
/// <para>
/// <b>Four registrations, the same four a fact needs</b>: a pinned id, an entry
/// in <see cref="RunnerAskKinds"/>, declared JSON members, and a slot on the
/// envelope. Three of the four produces an ask that serializes to a kind and
/// nothing, or one the other side cannot name.
/// </para>
/// </remarks>
public class RunnerAskVocabularyTests
{
    private static IReadOnlyList<Type> AskTypes =>
        [.. typeof(RunnerAsk).Assembly.GetExportedTypes()
            .Where(t => t.GetCustomAttribute<RunnerAskKindAttribute>() is not null)
            .OrderBy(t => t.Name, StringComparer.Ordinal)];

    [Test]
    public async Task Every_ask_payload_carries_a_pinned_id()
    {
        await Assert.That(AskTypes).IsNotEmpty()
            .Because("no ask payloads were found, so every assertion here is about nothing.");

        var unpinned = AskTypes
            .Where(t => t.GetCustomAttribute<PinnedIdAttribute>() is null)
            .Select(t => t.Name)
            .ToList();

        await Assert.That(unpinned).IsEmpty()
            .Because("an id that moves is a type the other side cannot recognise across a "
                   + "rename. Found: " + string.Join(", ", unpinned));
    }

    [Test]
    public async Task Every_ask_payload_names_a_kind_the_vocabulary_has()
    {
        var strangers = AskTypes
            .Select(t => (t.Name, Kind: t.GetCustomAttribute<RunnerAskKindAttribute>()!.Kind))
            .Where(x => !RunnerAskKinds.All.Contains(x.Kind, StringComparer.Ordinal))
            .Select(x => $"{x.Name} says '{x.Kind}'")
            .ToList();

        await Assert.That(strangers).IsEmpty()
            .Because("a kind the list does not contain is exactly the widening this "
                   + "vocabulary is closed to prevent. Found: " + string.Join(", ", strangers));
    }

    [Test]
    public async Task Every_kind_has_exactly_one_payload()
    {
        // BOTH DIRECTIONS. A kind with no payload cannot be asked; two payloads
        // for one kind makes the envelope's slot ambiguous.
        var byKind = AskTypes
            .GroupBy(t => t.GetCustomAttribute<RunnerAskKindAttribute>()!.Kind, StringComparer.Ordinal)
            .ToDictionary(g => g.Key, g => g.Count(), StringComparer.Ordinal);

        foreach (var kind in RunnerAskKinds.All)
        {
            await Assert.That(byKind.TryGetValue(kind, out var count) ? count : 0)
                .IsEqualTo(1)
                .Because($"'{kind}' is in the vocabulary, so exactly one payload declares it.");
        }
    }

    [Test]
    public async Task Every_ask_payload_has_a_slot_on_the_envelope()
    {
        // THE FOURTH REGISTRATION. A payload with no slot cannot arrive, and the
        // kind would name something the envelope has nowhere to put.
        var slots = typeof(RunnerAsk).GetProperties()
            .Select(p => Nullable.GetUnderlyingType(p.PropertyType) ?? p.PropertyType)
            .ToHashSet();

        var homeless = AskTypes.Where(t => !slots.Contains(t)).Select(t => t.Name).ToList();

        await Assert.That(homeless).IsEmpty()
            .Because("an ask with nowhere to arrive serializes to a kind and nothing. "
                   + "Found: " + string.Join(", ", homeless));
    }

    [Test]
    public async Task Every_ask_and_answer_type_has_declared_member_names()
    {
        // THE THIRD REGISTRATION, and it is what the other side reads by.
        var declared = ProtocolSurface.JsonMembers;

        var undeclared = new[]
            {
                typeof(RunnerAsk), typeof(TailLogAsk), typeof(StatusAsk),
                typeof(RunnerSaid), typeof(LogTail), typeof(RunnerStatusReport),
            }
            .Where(t => !declared.ContainsKey(t))
            .Select(t => t.Name)
            .ToList();

        await Assert.That(undeclared).IsEmpty()
            .Because("a wire type whose members are not declared is one the surface "
                   + "fingerprint does not cover. Found: " + string.Join(", ", undeclared));
    }

    [Test]
    public async Task The_vocabulary_belongs_to_the_contract_ledger_not_the_fact_one()
    {
        // Values on the wire that never reach a fact. Getting this wrong is the
        // defect VocabularyOfAttribute's own remark describes: a gate payload's
        // vocabulary moved the FACT fingerprint while no fact kind changed.
        var membership = typeof(RunnerAskKinds).GetCustomAttribute<VocabularyOfAttribute>();

        await Assert.That(membership).IsNotNull();
        await Assert.That(membership!.Fingerprint).IsEqualTo(VocabularyFingerprints.Contract);
    }
}
