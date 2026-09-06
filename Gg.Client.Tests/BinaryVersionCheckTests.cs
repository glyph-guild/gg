using Gg.Client;

namespace Gg.Client.Tests;

/// <summary>
/// The third signal, and the one that has never existed.
/// </summary>
/// <remarks>
/// <para>
/// <b>Three versions travel with every request and two of them are watched.</b>
/// The protocol has a floor and a 426 that enforces it. The fact vocabulary has
/// a header printed on purpose. The BINARY has had neither — nothing anywhere
/// compares it to anything — which is exactly why it is the one that drifts.
/// </para>
/// <para>
/// <b>The remedy for a 426 was a shrug.</b> <i>"Install a newer gg"</i> is true
/// and is not an instruction: the command differs by install shape, and on a
/// pool host the obvious guess is wrong in a way that reports success. Now that
/// <c>gg update</c> exists, the refusal can name it.
/// </para>
/// </remarks>
public class BinaryVersionCheckTests
{
    [Test]
    public async Task Doctor_reports_the_binary_version_beside_the_two_it_already_checks()
    {
        // S32.2-01. Protocol and fact vocabulary are both checked here; the
        // binary is the one that is only ever printed by --version, where
        // nothing compares it to anything.
        await using var stub = new StubControlPlane();

        var report = await new Doctor(
            new ControlPlaneClient(new HttpClient { BaseAddress = new Uri(stub.BaseAddress) }),
            new HeldSession(DoctorTests.AValidSession()),
            DoctorTests.ScratchStore(),
            new Uri(stub.BaseAddress)).RunAsync();

        var binary = report.Checks.SingleOrDefault(c => c.Name == DoctorChecks.Binary);

        await Assert.That(binary).IsNotNull()
            .Because("the binary is the third version and the only one nothing watches, which is "
                   + "why it is the one that drifts.");
        await Assert.That(binary!.Detail).Contains(GgVersions.Binary.Split('+')[0])
            .Because("a check that does not say which version is running cannot be acted on.");
    }

    [Test]
    public async Task Being_behind_never_blocks()
    {
        // S32.2-04 and rule 6. The protocol floor already refuses with a 426
        // and that stays the only thing that does. A blocking binary check
        // would stop somebody working over a number.
        await using var stub = new StubControlPlane();

        var report = await new Doctor(
            new ControlPlaneClient(new HttpClient { BaseAddress = new Uri(stub.BaseAddress) }),
            new HeldSession(DoctorTests.AValidSession()),
            DoctorTests.ScratchStore(),
            new Uri(stub.BaseAddress)).RunAsync();

        var binary = report.Checks.Single(c => c.Name == DoctorChecks.Binary);

        await Assert.That(binary.Blocking).IsFalse()
            .Because("being behind is reported, never blocking - the 426 is the only refusal in "
                   + "this design and it is about the protocol, not the version.");
    }

    [Test]
    public async Task The_protocol_refusal_names_the_verb_that_fixes_it()
    {
        // S32.2-02. "install a newer gg" is true and is not an instruction:
        // the command differs by install shape, and the obvious guess is wrong
        // on a pool host in a way that reports success. `gg update` knows which
        // shape this is, so the refusal can point at it instead of guessing.
        await using var stub = new StubControlPlane { ProtocolFloorMessage = "this gg is too old for the control plane" };

        var report = await new Doctor(
            new ControlPlaneClient(new HttpClient { BaseAddress = new Uri(stub.BaseAddress) }),
            new HeldSession(DoctorTests.AValidSession()),
            DoctorTests.ScratchStore(),
            new Uri(stub.BaseAddress)).RunAsync();

        var protocol = report.Checks.Single(c => c.Name == DoctorChecks.Protocol);

        await Assert.That(protocol.Passed).IsFalse();
        await Assert.That(protocol.Fix).IsNotNull();
        await Assert.That(protocol.Fix!).Contains("gg update")
            .Because("a remedy a person cannot type is a shrug with punctuation.");
    }

    private sealed class HeldSession(StoredSession? session) : ISessionStore
    {
        public StoredSession? Read() => session;
        public void Write(StoredSession value) { }
        public void Clear() { }
    }
}
