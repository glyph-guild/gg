using Gg.Contracts;
using System.Reflection;

namespace Gg.Contracts.Tests;

/// <summary>
/// Every flight state this build declares reaches the list the fingerprint reads.
/// </summary>
/// <remarks>
/// <para>
/// <b>0.65.0's lesson applied on the way in rather than after.</b>
/// <c>stale-working-copy</c> shipped as a constant with a sentence and no entry
/// in <c>ReasonKinds.All</c>. The suite stayed green, because the
/// closed-vocabulary fingerprint reads the LIST — so a value that never reaches
/// the list is invisible to the guard whose entire job is noticing new values —
/// and it surfaced as a 500 in the control plane, where a governed refusal
/// became an internal error.
/// </para>
/// <para>
/// <b>Walked rather than listed</b>, so a seventh state added next slice is
/// covered by this test on the day somebody types it, rather than on the day
/// somebody remembers to come back here.
/// <see cref="ClosedVocabularyTotalityTests"/> is the general form of the same
/// walk; this one is the specific claim slice fourteen's criteria name, and it
/// fails with a message about flight states rather than about vocabularies.
/// </para>
/// </remarks>
public class FlightStateTotalityTests
{
    private static IReadOnlyList<FieldInfo> Constants() =>
        [.. typeof(FlightStates)
            .GetFields(BindingFlags.Public | BindingFlags.Static)
            .Where(f => f is { IsLiteral: true, IsInitOnly: false })
            .Where(f => f.FieldType == typeof(string))
            .OrderBy(f => f.Name, StringComparer.Ordinal)];

    [Test]
    public async Task Every_declared_state_is_in_All()
    {
        var missing = Constants()
            .Where(f => f.GetValue(null) is string v
                     && !FlightStates.All.Contains(v, StringComparer.Ordinal))
            .Select(f => f.Name)
            .ToList();

        await Assert.That(missing).IsEmpty()
            .Because("All is what the closed-vocabulary fingerprint hashes, so a state that "
                   + "never reaches it moves no hash, forces no conversation, and arrives in "
                   + "somebody's control plane as an unknown value. Found: "
                   + string.Join(", ", missing));
    }

    [Test]
    public async Task The_walk_finds_the_states_it_is_walking()
    {
        // LIVENESS. The assertion above is an absence, and an absence passes
        // trivially if reflection returned nothing at all - which is exactly
        // what a rename of the vocabulary would cause.
        await Assert.That(Constants().Select(f => f.Name)).Contains(nameof(FlightStates.Withdrawn));
        await Assert.That(Constants().Count).IsEqualTo(FlightStates.All.Count)
            .Because("a constant that is not a state, or a state with no constant, both mean "
                   + "the list and the declarations have stopped being the same thing.");
    }

    [Test]
    public async Task The_vocabulary_crosses_the_wire_and_says_so()
    {
        // The attribute is what puts these values inside the contract
        // fingerprint. Without it the vocabulary is closed in prose only, and a
        // seventh value would ship without moving a hash - which is the defect
        // ClosedVocabularies was written to close for every vocabulary here.
        var membership = typeof(FlightStates).GetCustomAttribute<VocabularyOfAttribute>();

        await Assert.That(membership).IsNotNull()
            .Because("a closed vocabulary nobody fingerprints is a naming convention.");
        await Assert.That(membership!.Fingerprint).IsEqualTo(VocabularyFingerprints.Contract);
    }
}
