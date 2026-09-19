using Gg.Client;
using Gg.Contracts;
using Gg.Contracts.Authoring;

namespace Gg.Client.Tests;

/// <summary>
/// Nobody types a subject: <c>gg whoami</c> prints it, and a personal name's
/// declaration writes it.
/// </summary>
/// <remarks>
/// <para>
/// <b>S42.1-06.</b> ADR-0024 spells a person as their provider subject, because
/// that pair is the one thing a principal is unique on. It is also an opaque
/// string nobody knows by heart, so the spelling is only usable if gg supplies
/// it: printed where a person looks for who they are, and written where a
/// personal watch needs it.
/// </para>
/// <para>
/// <b>Personal is a flag on the wire, never a person.</b> The control plane
/// binds the name to whoever asked. What comes back says whose it is, and that
/// answer is what gets written - gg never decides which person a name belongs
/// to.
/// </para>
/// </remarks>
public class WhoAmISaysYourSubjectTests
{
    private const string Me =
        "a-directory:72f988bf-86f1-41af-91ab-2d7cd011db47/0b1c2d3e-4f50-6172-8394-a5b6c7d8e9f0";

    private static StoredSession SignedIn { get; } = new()
    {
        SessionToken = "stub-session",
        ExpiresAt = new DateTimeOffset(2027, 1, 1, 0, 0, 0, TimeSpan.Zero),
        TenantId = "stub-tenant",
        PrincipalDisplay = "someone@example.test",
    };

    private static FlightCommands Against(StubControlPlane stub, HttpClient http)
    {
        http.BaseAddress = new Uri(stub.BaseAddress);
        return new FlightCommands(new ControlPlaneClient(http), new HeldSessionStore(SignedIn));
    }

    private static WhoAmI Who(string? subject) => new()
    {
        PrincipalId = "01a062f3-42a5-73a4-8c01-ec248bfe5237",
        PrincipalDisplay = "someone@example.test",
        TenantId = "stub-tenant",
        ExpiresAt = new DateTimeOffset(2027, 1, 1, 0, 0, 0, TimeSpan.Zero),
        Subject = subject,
    };

    private static WatchDocument AWatch() => new()
    {
        Shape = WatchShapes.WorkItems,
        Trigger = new WatchTrigger { Every = "1h" },
        Host = "tracker.example",
        Credential = "op://vault/tracker/token",
        Filter = "SELECT [System.Id] FROM WorkItems WHERE [System.AssignedTo] = @Me",
        Repository = "payments",
        Skill = ".goodgrief/skills/triage.md",
        Ref = "refs/heads/main",
        Mapping = new WatchMapping { Subject = "id", Version = "rev", IntentKey = "url" },
        PullPoint = PullPoints.ResidentRunner,
        Nominates = new Destination
        {
            Id = "what-a-sweep-opens",
            Kind = DestinationKinds.Flight,
            Requires = [],
            Opens = ["review"],
        },
    };

    private static string AnEstate()
    {
        var root = Path.Combine(Path.GetTempPath(), $"gg-personal-{Guid.NewGuid():N}");
        Directory.CreateDirectory(Path.Combine(root, "airspace", "watches"));
        return root;
    }

    private static string WatchFile(string root) =>
        Path.Combine(root, "airspace", "watches", "my-queue.yaml");

    private static StubControlPlane Personal() => new()
    {
        NameLive = new TopologyName
        {
            Name = "my-queue",
            Role = Roles.Watch,
            Parent = Roles.Root,
            DeclaredAt = new DateTimeOffset(2026, 9, 19, 0, 0, 0, TimeSpan.Zero),
            DeclaredBy = "someone@example.test",
            For = Me,
        },
    };

    [Test]
    public async Task Whoami_prints_the_subject_the_control_plane_sent()
    {
        var text = VerbOutput.ToText(new VerbResult.Identity(Who(Me)));

        await Assert.That(text).Contains("Subject:");
        await Assert.That(text).Contains(Me)
            .Because("this is the line a person copies into nothing - it is what they are "
                   + "called in every document that names them.");
    }

    [Test]
    public async Task An_older_control_plane_that_sends_none_prints_no_subject_line()
    {
        var text = VerbOutput.ToText(new VerbResult.Identity(Who(subject: null)));

        await Assert.That(text).DoesNotContain("Subject:")
            .Because("a blank line would read as having no subject, which is not the same "
                   + "as a control plane that does not say.");
    }

    [Test]
    public async Task Declaring_a_personal_name_asks_for_one_and_names_nobody()
    {
        await using var stub = Personal();
        using var http = new HttpClient();

        _ = await Against(stub, http).DeclareNameAsync(
            Roles.Watch, "my-queue", Roles.Root, personal: true, estateRoot: AnEstate());

        await Assert.That(stub.DeclaredName!.Personal).IsTrue();
    }

    [Test]
    public async Task A_declared_personal_name_writes_its_person_into_the_watch_file()
    {
        await using var stub = Personal();
        using var http = new HttpClient();
        var root = AnEstate();
        await File.WriteAllTextAsync(WatchFile(root), EnvelopeText.Render(AWatch()));

        _ = await Against(stub, http).DeclareNameAsync(
            Roles.Watch, "my-queue", Roles.Root, personal: true, estateRoot: root);

        var parsed = EnvelopeYaml.ParseWatch(await File.ReadAllTextAsync(WatchFile(root)));

        await Assert.That(parsed.Diagnosis).IsNull();
        await Assert.That(parsed.Watch!.For).IsEqualTo(Me)
            .Because("the person the control plane bound, written where the watch needs it - "
                   + "so nobody types a subject.");
        await Assert.That(EnvelopeText.Render(parsed.Watch! with { For = null }))
            .IsEqualTo(EnvelopeText.Render(AWatch()))
            .Because("only whose it is changed; the rest of the watch is as it was written.");
    }

    [Test]
    public async Task With_no_watch_file_yet_the_line_to_write_is_said()
    {
        await using var stub = Personal();
        using var http = new HttpClient();

        var declared = await Against(stub, http).DeclareNameAsync(
            Roles.Watch, "my-queue", Roles.Root, personal: true, estateRoot: AnEstate());

        await Assert.That(((VerbResult.NameDeclared)declared).Value.For).IsEqualTo(Me);
        await Assert.That(VerbOutput.ToText(declared)).Contains($"for: {Me}")
            .Because("the watch is not written yet, so the answer says what its first line is.");
    }

    [Test]
    public async Task A_tenant_name_is_declared_as_it_always_was()
    {
        await using var stub = new StubControlPlane
        {
            NameLive = new TopologyName
            {
                Name = "nightly",
                Role = Roles.Watch,
                Parent = Roles.Root,
                DeclaredAt = new DateTimeOffset(2026, 9, 19, 0, 0, 0, TimeSpan.Zero),
                DeclaredBy = "someone@example.test",
            },
        };
        using var http = new HttpClient();
        var root = AnEstate();

        _ = await Against(stub, http).DeclareNameAsync(
            Roles.Watch, "nightly", Roles.Root, estateRoot: root);

        await Assert.That(stub.DeclaredName!.Personal).IsFalse();
        await Assert.That(Directory.EnumerateFiles(root, "*", SearchOption.AllDirectories))
            .IsEmpty()
            .Because("declaring the tenant's name writes nothing, exactly as before.");
    }
}
