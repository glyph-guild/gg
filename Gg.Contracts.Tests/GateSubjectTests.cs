namespace Gg.Contracts.Tests;

/// <summary>
/// A gate about something that is not a repository.
/// </summary>
/// <remarks>
/// <para>
/// <b>The coupling found by flying a flight with no repository.</b> Every gate
/// this contract could describe carried a branch and a commit as required
/// members, which was true of every gate that existed - and made <i>flight</i>
/// mean <i>agent run against a branch</i> in the one type a person reads when
/// they are asked to decide something.
/// </para>
/// <para>
/// <b>Nullable, not an empty string.</b> A gate with <c>commit: ""</c> makes "no
/// commit" and "a commit nobody recorded" the same value, which is Article XI's
/// failure with the fields swapped. Absent has to be representable for a reader
/// to be able to say so.
/// </para>
/// <para>
/// <b>The rule they encoded survives.</b> A gate about work in a repository still
/// waits for the push - a decision about a tree on somebody's machine is a
/// decision nobody can act on - and that is now a rule about repository
/// destinations rather than a rule about gates.
/// </para>
/// </remarks>
public class GateSubjectTests
{
    private static PendingGate Gate(string? branch = null, string? commit = null) => new()
    {
        FlightNumber = "GG-42",
        ObligationId = "envelope-change-approved",
        Approver = "platform-oncall",
        Branch = branch,
        Commit = commit,
        ManifestHash = new string('0', 64),
        Because = "this obligation declares no condition, so it always applies.",
        AwaitingSince = DateTimeOffset.UnixEpoch,
        Attempt = 0,
    };

    [Test]
    public async Task A_gate_can_be_about_something_with_no_commit()
    {
        var gate = Gate();

        await Assert.That(gate.Commit).IsNull();
        await Assert.That(gate.Branch).IsNull();
    }

    [Test]
    public async Task A_gate_about_a_repository_still_carries_both()
    {
        // THE OLD SHAPE, SURVIVING AS A CASE OF THE NEW ONE rather than being
        // replaced by it. Every gate that has ever been opened looks like this.
        var gate = Gate(branch: "gg/42-add-discount", commit: new string('a', 40));

        await Assert.That(gate.Branch).IsEqualTo("gg/42-add-discount");
        await Assert.That(gate.Commit).IsEqualTo(new string('a', 40));
    }

    [Test]
    public async Task Absent_and_empty_are_different_values()
    {
        // The distinction the nullability exists for, stated so that anybody
        // tempted to default these to "" has to delete an assertion that says why.
        await Assert.That(Gate(branch: "", commit: "").Commit).IsNotEqualTo(Gate().Commit);
    }
}
