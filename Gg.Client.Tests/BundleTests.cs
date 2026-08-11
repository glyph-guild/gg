using Gg.Client;
using Gg.Contracts;

namespace Gg.Client.Tests;

/// <summary>
/// The document somebody sends us when it went wrong.
/// </summary>
/// <remarks>
/// <para>
/// Mandated from release one and forgotten by the build order. It is the
/// practical form of "anything that can fail inside a customer's environment
/// produces a diagnosis they can send us": we cannot look at their terminal,
/// so this has to be enough on its own.
/// </para>
/// <para>
/// <b>Three rules, and each is a test below.</b> Nothing that crossed the live
/// channel is in it. Every silent degradation writes one line. And it says
/// which half it has, rather than leaving somebody to infer that from what is
/// missing - a bundle taken while the control plane is unreachable is a
/// perfectly good bundle of local material, and indistinguishable from a
/// complete one that found nothing.
/// </para>
/// </remarks>
public class BundleTests
{
    private static readonly DateTimeOffset T0 = new(2026, 8, 11, 12, 0, 0, TimeSpan.Zero);

    private static EnvironmentIdentity AnEnvironment() => new()
    {
        HostFingerprint = "b8c1f0a9",
        Locks = [],
        Tools = [new ToolVersion { Name = "dotnet", Version = "10.0.0" }],
        Provenance = EnvironmentProvenance.Fresh,
    };

    private static DoctorReport AReport(params DoctorCheck[] checks) => new() { Checks = [.. checks] };

    private static DoctorCheck ACheck(
        string name, bool passed = true, bool blocking = false, string detail = "detail") => new()
        {
            Name = name,
            Passed = passed,
            Detail = detail,
            Blocking = blocking,
            Fixable = false,
        };

    private static FlightLog ALog() => new()
    {
        FlightNumber = "GG-1",
        FlightId = "019fefe0-39ac-77f5-b0be-fc2f856202a0",
        Entries = [],
    };

    [Test]
    public async Task A_bundle_carries_what_a_person_would_otherwise_be_asked_for()
    {
        var bundle = Bundle.Build(T0, AnEnvironment(), AReport(ACheck(DoctorChecks.ControlPlane)), ALog());

        await Assert.That(bundle.TakenAt).IsEqualTo(T0);
        await Assert.That(bundle.Binary).IsEqualTo(GgVersions.Binary);
        await Assert.That(bundle.Protocol).IsEqualTo(GgVersions.Protocol);
        await Assert.That(bundle.FactVocabulary).IsEqualTo(FactVocabulary.Version);
        await Assert.That(bundle.Environment.HostFingerprint).IsEqualTo("b8c1f0a9");
        await Assert.That(bundle.Checks).IsNotEmpty();
        await Assert.That(bundle.FlightLog).IsNotNull();
    }

    [Test]
    public async Task A_bundle_says_it_has_both_halves_when_it_does()
    {
        var bundle = Bundle.Build(T0, AnEnvironment(), AReport(ACheck(DoctorChecks.ControlPlane)), ALog());

        await Assert.That(bundle.Completeness).IsEqualTo(BundleCompleteness.Complete);
    }

    [Test]
    public async Task A_bundle_taken_without_the_control_plane_says_so_rather_than_looking_thin()
    {
        // The rule this exists for. A bundle with no flight log and no
        // statement is indistinguishable from a complete one taken on a tenant
        // that has never flown, and the two lead somewhere completely
        // different.
        var bundle = Bundle.Build(
            T0, AnEnvironment(),
            AReport(ACheck(DoctorChecks.ControlPlane, passed: false, blocking: true,
                           detail: "could not connect to https://good-grief.test/")),
            flightLog: null);

        await Assert.That(bundle.Completeness).IsEqualTo(BundleCompleteness.LocalOnly);
        await Assert.That(bundle.CompletenessDetail.ToLowerInvariant()).Contains("control plane");
        await Assert.That(bundle.FlightLog).IsNull();
    }

    [Test]
    public async Task Completeness_is_read_from_the_checks_rather_than_from_what_is_missing()
    {
        // The twin of the assertion above, and the interesting half: a bundle
        // with no flight log is NOT automatically local-only. Somebody who has
        // never opened a flight has an empty log and a working control plane,
        // and telling them their bundle is half a bundle would send them
        // debugging a network that is fine.
        var bundle = Bundle.Build(
            T0, AnEnvironment(), AReport(ACheck(DoctorChecks.ControlPlane)), flightLog: null);

        await Assert.That(bundle.Completeness).IsEqualTo(BundleCompleteness.Complete);
    }

    [Test]
    public async Task Every_failing_check_becomes_one_degradation_line()
    {
        // One line each, in the bundle and in doctor, from the same source.
        // A degradation visible in only one of the two is one somebody reports
        // and we cannot reproduce.
        var bundle = Bundle.Build(T0, AnEnvironment(), AReport(
            ACheck(DoctorChecks.ControlPlane),
            ACheck(DoctorChecks.Telemetry, passed: false, detail: "exports to https://collector.test/"),
            ACheck(DoctorChecks.Credentials, passed: false, blocking: true,
                   detail: "acme/widgets resolves to nothing on this machine")), ALog());

        await Assert.That(bundle.Degradations.Select(d => d.Name))
            .IsEquivalentTo((string[])[DoctorChecks.Telemetry, DoctorChecks.Credentials]);
    }

    [Test]
    public async Task A_healthy_machine_reports_no_degradations()
    {
        // The twin. A bundle that always listed something would make the list
        // the thing people skip.
        var bundle = Bundle.Build(T0, AnEnvironment(), AReport(ACheck(DoctorChecks.ControlPlane)), ALog());

        await Assert.That(bundle.Degradations).IsEmpty();
    }

    [Test]
    public async Task A_degradation_carries_its_remedy_when_the_check_had_one()
    {
        var bundle = Bundle.Build(T0, AnEnvironment(), AReport(new DoctorCheck
        {
            Name = DoctorChecks.Credentials,
            Passed = false,
            Detail = "acme/widgets resolves to nothing",
            Blocking = true,
            Fixable = true,
            Fix = "gg credential add --repo acme/widgets",
        }), ALog());

        await Assert.That(bundle.Degradations.Single().Remedy).IsEqualTo("gg credential add --repo acme/widgets");
    }

    [Test]
    public async Task A_bundle_has_nowhere_to_put_a_line_of_runner_output()
    {
        // Structural, and the strongest form of the redaction rule: there is
        // no member on this document capable of carrying what a runner
        // printed, so a future caller cannot put one there by accident. The
        // behavioural twin lives in the console tests, where the live channel
        // actually exists.
        var members = typeof(DiagnosticsBundle).GetProperties().Select(p => p.Name).ToList();

        await Assert.That(members).IsNotEmpty();
        foreach (var forbidden in (string[])["Live", "Stream", "Output", "Stdout", "Stderr", "Held"])
        {
            await Assert.That(members.Where(m => m.Contains(forbidden, StringComparison.Ordinal))).IsEmpty()
                .Because($"'{forbidden}' is where a line of somebody's build log would arrive.");
        }
    }

    [Test]
    public async Task The_bundle_goes_out_through_the_verb_path_like_everything_else()
    {
        // No second way to print. That is what keeps --json and the console
        // rendering the same document.
        var result = new VerbResult.Bundle(
            Bundle.Build(T0, AnEnvironment(), AReport(ACheck(DoctorChecks.ControlPlane)), ALog()));

        var json = VerbOutput.ToJson(result);
        var rendered = VerbOutput.ToText(result);

        await Assert.That(result.Kind).IsEqualTo(VerbResultKinds.Bundle);
        await Assert.That(json).Contains("hostFingerprint");
        await Assert.That(rendered).IsNotEmpty();
    }

    [Test]
    public async Task A_bundle_read_back_renders_the_same_way()
    {
        // The property every other verb has: we can re-render a payload
        // somebody sent us, which is the whole reason this document exists.
        var result = new VerbResult.Bundle(
            Bundle.Build(T0, AnEnvironment(), AReport(
                ACheck(DoctorChecks.Telemetry, passed: false, detail: "exports somewhere")), ALog()));

        var round = VerbOutput.Parse(VerbResultKinds.Bundle, VerbOutput.ToJson(result));

        await Assert.That(VerbOutput.ToText(round)).IsEqualTo(VerbOutput.ToText(result));
    }
}
