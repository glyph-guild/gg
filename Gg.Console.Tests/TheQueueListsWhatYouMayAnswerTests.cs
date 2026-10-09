using Gg.Contracts;

namespace Gg.Console.Tests;

/// <summary>
/// The queue lists a gate as needing you only when you may answer it: a gate naming a role is
/// anybody entitled's, a gate naming a person is theirs alone (owner, 2026-10-09: GG-1044, about
/// Phil's machine, sat in Kevin's queue - "isolate gates which are pertinent to a specific runner to
/// the owner"). The gates tab still lists every gate; only "needs you" narrows.
/// </summary>
public class TheQueueListsWhatYouMayAnswerTests
{
    private const string Kevin = "fake:kevin";
    private const string Phil = "fake:phil";

    private static PendingGate Gate(string flight, string approver) => new()
    {
        FlightNumber = flight,
        ObligationId = "maintenance-oncall",
        Approver = approver,
        ManifestHash = "sha256:none",
        Because = "this obligation declares no condition",
        AwaitingSince = DateTimeOffset.UnixEpoch,
        Attempt = 1,
    };

    private static readonly GateList Three = new()
    {
        Gates = [Gate("GG-1043", "platform-oncall"), Gate("GG-1044", Phil), Gate("GG-1045", Kevin)],
    };

    [Test]
    public async Task A_role_gate_and_your_own_are_listed_and_somebody_elses_is_not()
    {
        var mine = QueueGates.Answerable(Three, Kevin)!;

        await Assert.That(mine.Gates.Select(g => g.FlightNumber)).IsEquivalentTo(["GG-1043", "GG-1045"])
            .Because("GG-1044 is Phil's to answer, and the server refuses anybody else.");
        await Assert.That(QueueGates.Answerable(Three, Phil)!.Gates.Select(g => g.FlightNumber))
            .IsEquivalentTo(["GG-1043", "GG-1044"]);
    }

    [Test]
    public async Task With_nobody_signed_in_only_role_gates_need_you()
    {
        await Assert.That(QueueGates.Answerable(Three, "")!.Gates.Select(g => g.FlightNumber))
            .IsEquivalentTo(["GG-1043"]);
        await Assert.That(QueueGates.Answerable(null, Kevin)).IsNull();
    }

    [Test]
    public async Task A_gate_naming_your_subject_is_announced_to_you()
    {
        var watching = new AppState { Principal = "kdeenanauth", Subject = Kevin, Gates = new GateList { Gates = [] } };
        var booted = Announcements.Folded(watching);
        var arrived = Announcements.Folded(booted with { Gates = new GateList { Gates = [Gate("GG-1045", Kevin)] } });

        await Assert.That(arrived.Notifications.Any(n => n.FlightNumber == "GG-1045")).IsTrue()
            .Because("a gate that names you by subject is yours, and announcing only display-name matches missed it.");
    }
}
