using System.Net;
using System.Text.RegularExpressions;
using System.Text;
using System.Text.Json;
using Gg.Client;
using Gg.Contracts;
using Gg.Contracts.Description;
using Gg.Local;

namespace Gg.Cli.Tests;

/// <summary>
/// A person mints an enrollment token, and a machine redeems it with nobody at
/// it: gg's half of slice forty-three's step 4 (rules 17-19).
/// </summary>
/// <remarks>
/// <para>
/// <b>The decision moves from typing at the machine to minting the token</b>
/// (ADR-0025 section 1). <c>gg login</c> at every new machine is what kept a
/// person at a keyboard for each one; a token is that person's decision made
/// once, bounded by a count and seven days, and redeemed by the machine itself.
/// </para>
/// <para>
/// <b>The secret appears once.</b> Minting prints it; listing never can, because
/// the listed type has no member for it to travel in.
/// </para>
/// </remarks>
public class AMachineEnrollsItselfTests
{
    // ---- minting: the command line ----

    [Test]
    public async Task A_token_is_minted_for_a_profile_a_count_and_a_duration()
    {
        var parsed = CliArgs.Parse(["fleet", "enroll", "--profile", "dev-worker", "--uses", "3", "--expires", "24h"]);

        await Assert.That(parsed).IsTypeOf<CliAction.FleetEnroll>();
        var asked = ((CliAction.FleetEnroll)parsed).Request;
        await Assert.That(asked.Profile).IsEqualTo("dev-worker");
        await Assert.That(asked.Uses).IsEqualTo(3);
        await Assert.That(asked.ExpiresInSeconds).IsEqualTo(24 * 3600);
        await Assert.That(asked.Ownership).IsEqualTo(RunnerOwnerships.Open)
            .Because("an enrolled machine is open unless its minter says otherwise.");
    }

    [Test]
    public async Task Whose_it_starts_as_is_the_minters_to_say()
    {
        var tenant = (CliAction.FleetEnroll)CliArgs.Parse(
            ["fleet", "enroll", "--tenant", "--profile", "p", "--uses", "1", "--expires", "1h"]);
        var mine = (CliAction.FleetEnroll)CliArgs.Parse(
            ["fleet", "enroll", "--profile", "p", "--uses", "1", "--expires", "7d", "--claim", "--reserve"]);

        await Assert.That(tenant.Request.Ownership).IsEqualTo(RunnerOwnerships.Tenant);
        await Assert.That(mine.Request.Ownership).IsEqualTo(RunnerOwnerships.Claimed);
        await Assert.That(mine.Request.Reserve).IsTrue();
    }

    [Test]
    [Arguments(new[] { "--uses", "1", "--expires", "1h" }, "--profile")]
    [Arguments(new[] { "--profile", "p", "--expires", "1h" }, "--uses")]
    [Arguments(new[] { "--profile", "p", "--uses", "1" }, "--expires")]
    [Arguments(new[] { "--profile", "p", "--uses", "1", "--expires", "1h", "--tenant", "--claim" }, "Say one")]
    [Arguments(new[] { "--profile", "p", "--uses", "1", "--expires", "1h", "--reserve" }, "--claim")]
    [Arguments(new[] { "--profile", "p", "--uses", "1", "--expires", "soon" }, "30m, 24h or 7d")]
    public async Task A_token_with_no_bound_or_two_owners_is_refused_before_anything_is_asked(
        string[] flags, string said)
    {
        var parsed = CliArgs.Parse(["fleet", "enroll", .. flags]);

        await Assert.That(parsed).IsTypeOf<CliAction.Unknown>();
        await Assert.That(((CliAction.Unknown)parsed).Message).Contains(said);
    }

    [Test]
    public async Task Tokens_and_revoke_are_verbs_too()
    {
        await Assert.That(CliArgs.Parse(["fleet", "tokens"])).IsEqualTo(new CliAction.FleetTokens(false));
        await Assert.That(CliArgs.Parse(["fleet", "revoke", "t-1"])).IsEqualTo(new CliAction.FleetRevoke("t-1", false));
        await Assert.That(((CliAction.Unknown)CliArgs.Parse(["fleet", "revoke"])).Message).Contains("gg fleet tokens");
    }

    // ---- the bounds, stated once for both repositories ----

    [Test]
    public async Task Seven_days_is_the_longest_a_token_lasts()
    {
        var asked = new EnrollmentTokenRequest { Profile = "p", Uses = 1, ExpiresInSeconds = (int)TimeSpan.FromDays(8).TotalSeconds };

        await Assert.That(EnrollmentBounds.Validate(asked)).Contains("seven days");
        await Assert.That(EnrollmentBounds.Validate(asked with { ExpiresInSeconds = (int)TimeSpan.FromDays(7).TotalSeconds }))
            .IsNull();
        await Assert.That(EnrollmentBounds.Validate(asked with { ExpiresInSeconds = 60, Uses = 0 })).IsNotNull();
    }

    // ---- the secret, once ----

    [Test]
    public async Task The_secret_is_printed_once_and_a_list_has_nowhere_to_put_it()
    {
        var minted = VerbOutput.ToText(new VerbResult.EnrollmentMinted(new EnrollmentTokenMinted
        {
            TokenId = "t-1", Token = "ggenroll_s3cret", Profile = "dev-worker", Uses = 2,
            ExpiresAt = DateTimeOffset.UnixEpoch, Ownership = RunnerOwnerships.Tenant,
        }));

        await Assert.That(minted).Contains("ggenroll_s3cret");
        await Assert.That(minted).Contains("Shown once");
        await Assert.That(ProtocolSurface.JsonMembers[typeof(EnrollmentTokenSummary)]).DoesNotContain("token")
            .Because("a listed token has no member its secret could travel in, so no bug can print it.");
    }

    // ---- the doors ----

    [Test]
    public async Task Redeeming_is_anonymous_and_everything_else_is_a_persons()
    {
        await Assert.That(ProtocolSurface.Endpoints.Single(e => e.Path == "/v1/runners/enrollments").Audience)
            .IsEqualTo(Audience.Anonymous)
            .Because("the machine has no session, and needing one is the thing enrollment ends.");
        await Assert.That(ProtocolSurface.Endpoints
                .Where(e => e.Path.StartsWith("/v1/fleet/", StringComparison.Ordinal))
                .All(e => e.Audience == Audience.Developer))
            .IsTrue();
        await Assert.That(ProtocolSurface.GovernedPrefixes).Contains("/v1/fleet");
    }

    private sealed class Answering(HttpStatusCode status, string? body = null) : HttpMessageHandler
    {
        internal HttpRequestMessage? Seen { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Seen = request;
            return Task.FromResult(new HttpResponseMessage(status)
            {
                Content = new StringContent(body ?? "", Encoding.UTF8, "application/json"),
            });
        }
    }

    private static ControlPlaneClient Against(Answering handler) =>
        new(new HttpClient(handler) { BaseAddress = new Uri("https://control.example.invalid") });

    [Test]
    public async Task A_machine_enrolls_with_the_token_and_no_session()
    {
        var handler = new Answering(HttpStatusCode.OK, """
            {"runnerId":"r-1","runnerToken":"rt","expiresAt":"2026-10-19T00:00:00+00:00",
             "profile":"dev-worker","ownership":"tenant"}
            """);

        var enrolled = await Against(handler).EnrollAsync("ggenroll_s3cret", "vmlinux002");

        await Assert.That(handler.Seen!.RequestUri!.AbsolutePath).IsEqualTo("/v1/runners/enrollments");
        await Assert.That(handler.Seen.Headers.Contains(GgVersions.SessionHeader)).IsFalse();
        await Assert.That(enrolled.Profile).IsEqualTo("dev-worker");
    }

    [Test]
    public async Task A_refused_token_arrives_as_the_one_sentence_the_control_plane_gives()
    {
        const string said = "That enrollment token cannot enroll a machine.";
        var handler = new Answering(
            HttpStatusCode.Forbidden, JsonSerializer.Serialize(new Dictionary<string, string> { ["detail"] = said }));

        var refused = await Assert.ThrowsAsync<EnrollmentRefusedException>(
            () => Against(handler).EnrollAsync("spent", "vmlinux002"));

        await Assert.That(refused!.Message).IsEqualTo(said);
    }

    // ---- the seed an install leaves ----

    [Test]
    public async Task The_seed_is_beside_the_configuration_read_once_and_spent()
    {
        var home = Directory.CreateTempSubdirectory("gg-seed-");
        try
        {
            var configuration = Path.Combine(home.FullName, "config.json");
            await Assert.That(EnrollmentSeed.Read(configuration)).IsNull();

            await File.WriteAllTextAsync(Path.Combine(home.FullName, "enrollment"), "ggenroll_s3cret\n");
            await Assert.That(EnrollmentSeed.Read(configuration)).IsEqualTo("ggenroll_s3cret");

            EnrollmentSeed.Spend(configuration);
            await Assert.That(EnrollmentSeed.Read(configuration)).IsNull()
                .Because("a redeemed token is spent, and a spent one is kept nowhere.");
        }
        finally
        {
            home.Delete(recursive: true);
        }
    }

    [Test]
    public async Task Runner_up_redeems_the_seed_before_asking_for_a_session_and_writes_down_its_profile()
    {
        // THE WIRING, which is where enrollment would otherwise be a feature
        // nothing calls: runner up reads the seed, redeems it, records the
        // profile the machine agreed to, and spends the seed.
        var here = new DirectoryInfo(AppContext.BaseDirectory);
        while (here is not null && !Directory.Exists(Path.Combine(here.FullName, "Gg.Cli")))
        {
            here = here.Parent;
        }

        var program = await File.ReadAllTextAsync(Path.Combine(here!.FullName, "Gg.Cli", "Program.cs"));
        var up = program[program.IndexOf("static async Task<int> RunnerUpAsync()", StringComparison.Ordinal)..];
        up = up[..up.IndexOf("var inForce = InForce.Configuration;", StringComparison.Ordinal)];

        // THE PROFILE IS RECORDED THROUGH THE DOOR THAT ALSO OPENS THE MACHINE,
        // rather than by setting the one member: a machine that wrote down which
        // profile it agreed to and stayed closed to both of that profile's
        // remedies is the vmlinux002 walk, and it is the reason this names a
        // call rather than an assignment.
        foreach (var step in new[] { "EnrollmentSeed.Read(", ".EnrollAsync(", "LocalCredentialKeeper.EnrolledUnder(", "EnrollmentSeed.Spend(" })
        {
            await Assert.That(up).Contains(step);
        }

        await Assert.That(up.IndexOf("EnrollAsync(", StringComparison.Ordinal))
            .IsLessThan(up.IndexOf("RegisterRunnerAsync(", StringComparison.Ordinal))
            .Because("an install that left a token said how this machine joins.");
    }

    // ---- redeeming: what the machine writes about itself ----

    [Test]
    public async Task An_enrolled_machine_opens_both_doors_on_the_authority_of_its_token()
    {
        // MEASURED ON vmlinux002 (S43.8-01), and it is the member's lesson
        // arriving at the machine class the member's argument was about. A
        // member opts itself in on its nonce because nobody can open a shell
        // on it; an enrolled machine redeems a token and nobody is at it
        // either - the install leaves the token and walks away.
        //
        // With neither door open, rule 25's own bring-up ask cannot be
        // answered: `gg agent login` is refused for want of accept-configured,
        // and `gg config set` refuses both keys because neither is a setting.
        var opened = LocalCredentialKeeper.EnrolledUnder(new Configuration(), "dev-worker");

        await Assert.That(opened.EnrolledProfile).IsEqualTo("dev-worker");
        await Assert.That(opened.AcceptConfigured).IsTrue()
            .Because("a machine that will not keep a credential cannot be sent one, and "
                   + "sending one is the other half of the agent remedy.");
        await Assert.That(opened.AcceptAgentLogin).IsTrue()
            .Because("the ceremony is the remedy a bring-up gate names, and a machine that "
                   + "answers 'closed' to it is a machine nobody can bring up.");
    }

    [Test]
    public async Task Redeeming_keeps_what_the_file_already_said()
    {
        // The member's other lesson: ConfigurationFile.Write replaces the whole
        // document, so an enrolled machine's control plane - written by the
        // installer, before any of this - must survive the redemption that
        // follows it.
        var opened = LocalCredentialKeeper.EnrolledUnder(
            new Configuration { ControlPlane = "https://control.example", AcceptOffered = true },
            "dev-worker");

        await Assert.That(opened.ControlPlane).IsEqualTo("https://control.example");
        await Assert.That(opened.AcceptOffered).IsTrue();
    }

    [Test]
    public async Task An_enrolled_machine_must_name_the_profile_it_agreed_to()
    {
        // The consent is to a NAME. A blank one would open both doors on a
        // machine that agreed to nothing, which is the one thing the token's
        // authority does not extend to.
        await Assert.That(() => LocalCredentialKeeper.EnrolledUnder(new Configuration(), " "))
            .Throws<ArgumentException>();
    }

    [Test]
    public async Task The_enrolling_start_re_reads_the_file_it_just_wrote()
    {
        // gg-pool-ui-1 and -2 again, one machine class along: the configuration
        // is memoized for the process, and the enrolling start writes the file
        // AFTER the process began. Everything composed from the stale answer -
        // the keeper, the login door - says this machine is closed, which is
        // exactly what it was before it enrolled.
        var body = Body("RunnerUpAsync");

        var wrote = body.IndexOf("LocalCredentialKeeper.EnrolledUnder(", StringComparison.Ordinal);
        var forgot = body.IndexOf("InForce.Forget()", StringComparison.Ordinal);
        var keeps = body.IndexOf("keepCredential:", StringComparison.Ordinal);

        await Assert.That(wrote).IsGreaterThan(-1)
            .Because("the doors are opened where the token is redeemed, and nowhere else.");
        await Assert.That(forgot).IsGreaterThan(wrote)
            .Because("a re-read before the write reads the file the machine arrived with.");
        await Assert.That(keeps).IsGreaterThan(forgot)
            .Because("the keeper is what the heartbeat's accepts-configuration is derived "
                   + "from, so it has to be built after the re-read.");
    }

    /// <summary>The source of one method in the composition root.</summary>
    private static string Body(string method)
    {
        var source = File.ReadAllText(Path.Combine(Root(), "Gg.Cli", "Program.cs"));
        var at = Regex.Match(source, $@"static async Task<int> {Regex.Escape(method)}\(");
        if (!at.Success)
        {
            throw new InvalidOperationException($"{method} was not found in Program.cs");
        }

        var open = source.IndexOf('{', at.Index);
        var depth = 0;
        for (var i = open; i < source.Length; i++)
        {
            depth += source[i] switch { '{' => 1, '}' => -1, _ => 0 };
            if (depth == 0)
            {
                return source[at.Index..(i + 1)];
            }
        }

        throw new InvalidOperationException($"{method}'s body never closed");
    }

    private static string Root()
    {
        var here = new DirectoryInfo(AppContext.BaseDirectory);
        while (here is not null && !File.Exists(Path.Combine(here.FullName, "Gg.sln")))
        {
            here = here.Parent;
        }

        return here?.FullName ?? throw new InvalidOperationException("Gg.sln not found");
    }
}
