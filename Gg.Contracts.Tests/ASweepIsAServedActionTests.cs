using System.Reflection;
using Gg.Contracts;
using Gg.Contracts.Description;

namespace Gg.Contracts.Tests;

/// <summary>
/// A sweep is a decided action a runner is served, and what it saw comes back
/// as an attestation.
/// </summary>
/// <remarks>
/// <para>
/// <b>Slice thirty-nine, step 2, on the pools surface's shape and for its
/// reason.</b> A sweep has no flight, so the fact pipeline — lease-welded at
/// four points — cannot carry it. A decided row served to exactly one runner,
/// and an attestation idempotent on the runner's own id, is the shape a routine
/// action already has, and ADR-0022 § 5 says a sweep is one.
/// </para>
/// <para>
/// <b>The skill travels in the answer, with the record of which one it
/// was.</b> ADR-0023 § 1: the control plane fetches it and hands the content
/// to the runner. The commit the ref resolved to and the blob's digest travel
/// beside it, because those are what the control plane keeps — rule 16 of the
/// slice, and <c>PolicyFile</c>'s own rule that the words never reach a store.
/// </para>
/// <para>
/// <b>What comes back is identities and versions, bounded.</b> The runner
/// reports; the control plane nominates (Article IX). So the report carries
/// only what a nomination would be keyed by, and the limits are what keep a
/// runner from putting a work item's text into a store by calling it a
/// subject.
/// </para>
/// </remarks>
public class ASweepIsAServedActionTests
{
    private static Endpoint Declared(string method, string path) =>
        ProtocolSurface.Endpoints.Single(e => e.Method == method && e.Path == path);

    private static Guid V7() => Guid.CreateVersion7(DateTimeOffset.UnixEpoch.AddYears(56));

    internal static WatchAttestation AnAttestation() => new()
    {
        AttestationId = V7(),
        Watch = "nightly-triage",
        ActionId = V7(),
        Outcome = WatchOutcomes.Swept,
        Saw =
        [
            new WatchSighting
            {
                Subject = "4242",
                Version = "7",
                IntentKey = "https://tracker.example/items/4242",
            },
        ],
        MeasuredAt = DateTimeOffset.UnixEpoch.AddYears(56),
    };

    internal static WatchAction AnAction() => new()
    {
        ActionId = V7(),
        Watch = "nightly-triage",
        WatchVersion = "nightly-triage@v3",
        Document = AWatchDeclaresReferencesTests.AWatch(),
        Executor = WatchExecutors.Instructions,
        Skill = new WatchSkill
        {
            Path = ".goodgrief/skills/triage.md",
            Commit = new string('a', 40),
            Sha = new string('b', 40),
            Content = "Look at each item and say whether it needs a person.",
        },
        DecidedAt = DateTimeOffset.UnixEpoch.AddYears(56),
    };

    [Test]
    public async Task The_pull_point_is_declared_and_serving_is_the_claim()
    {
        var pull = Declared("GET", "/v1/watches/{name}/actions");

        await Assert.That(pull.Audience).IsEqualTo(Audience.Runner)
            .Because("a sweep is performed by a runner and decided by nobody on this route.");
        await Assert.That(pull.Response).IsEqualTo(typeof(WatchActionList));
        await Assert.That(pull.RequiredHeaders).Contains(ProtocolSurface.RunnerHeader);
        await Assert.That(pull.Statuses).DoesNotContain(404)
            .Because("nothing decided is an empty list rather than an error - the pool pull "
                   + "point's answer, for its reason.");
    }

    [Test]
    public async Task The_attestation_is_a_command_and_the_answer_says_so()
    {
        var attest = Declared("POST", "/v1/watches/{name}/attestations");

        await Assert.That(attest.Audience).IsEqualTo(Audience.Runner);
        await Assert.That(attest.Request).IsEqualTo(typeof(WatchAttestation));
        await Assert.That(attest.Statuses).Contains(202);
        await Assert.That(attest.Statuses).Contains(400)
            .Because("the contract's own Validate refuses, on both sides, so a runner and the "
                   + "control plane cannot disagree about what a valid report is.");
    }

    [Test]
    public async Task The_watches_prefix_is_governed()
    {
        await Assert.That(ProtocolSurface.GovernedPrefixes).Contains("/v1/watches")
            .Because("a runner-audience route nobody declared would be an unaudited way for a "
                   + "runner to reach the control plane - the pools prefix's argument, and a "
                   + "sweep's report is the input to what gets nominated.");
    }

    [Test]
    public async Task The_wire_members_are_declared()
    {
        await Assert.That(ProtocolSurface.JsonMembers[typeof(WatchAction)])
            .IsEquivalentTo((string[])
                ["actionId", "watch", "watchVersion", "document", "executor", "skill",
                 "diagnosis", "decidedAt"]);
        await Assert.That(ProtocolSurface.JsonMembers[typeof(WatchSkill)])
            .IsEquivalentTo((string[])["path", "commit", "sha", "content"]);
        await Assert.That(ProtocolSurface.JsonMembers[typeof(WatchActionList)])
            .IsEquivalentTo((string[])["actions"]);
        await Assert.That(ProtocolSurface.JsonMembers[typeof(WatchAttestation)])
            .IsEquivalentTo((string[])
                ["attestationId", "watch", "actionId", "outcome", "saw", "measuredAt",
                 "diagnosis"]);
        await Assert.That(ProtocolSurface.JsonMembers[typeof(WatchSighting)])
            .IsEquivalentTo((string[])["subject", "version", "intentKey"]);
    }

    [Test]
    public async Task One_executor_exists_and_the_script_is_slice_forties()
    {
        await Assert.That(WatchExecutors.All).IsEquivalentTo((string[])["instructions"])
            .Because("ADR-0023's second executor arrives with the crystalize flight that "
                   + "writes a script, and declaring it first would be a word nothing performs.");
    }

    [Test]
    public async Task A_whole_attestation_and_a_whole_action_are_valid()
    {
        await Assert.That(WatchAttestation.Validate(AnAttestation())).IsNull();
        await Assert.That(WatchAction.Validate(AnAction())).IsNull();
    }

    [Test]
    public async Task An_empty_sweep_is_a_valid_report()
    {
        // RULE 8 OF THE SLICE, and `MaintainLoop`'s scar: found-nothing is a
        // result and silence is a fault. A validator that refused an empty
        // list would make the quiet pass indistinguishable from a dead one.
        await Assert.That(WatchAttestation.Validate(AnAttestation() with { Saw = [] })).IsNull();
    }

    [Test]
    public async Task The_attestation_id_is_a_uuid_v7()
    {
        var refused = WatchAttestation.Validate(AnAttestation() with { AttestationId = Guid.NewGuid() });

        await Assert.That(refused).IsNotNull();
        await Assert.That(refused!).Contains("UUIDv7");
    }

    [Test]
    public async Task An_attestation_names_its_watch_and_a_known_outcome()
    {
        await Assert.That(WatchAttestation.Validate(AnAttestation() with { Watch = " " }))
            .IsNotNull();

        var unknown = WatchAttestation.Validate(AnAttestation() with { Outcome = "partial" });

        await Assert.That(unknown).IsNotNull();
        await Assert.That(unknown!).Contains("partial");
    }

    [Test]
    public async Task An_unreachable_sweep_says_why_and_reports_nothing_seen()
    {
        var silent = WatchAttestation.Validate(AnAttestation() with
        {
            Outcome = WatchOutcomes.Unreachable,
            Saw = [],
        });

        await Assert.That(silent).IsNotNull()
            .Because("a sweep that could not do its job escalates to a person, and a person "
                   + "handed no reason has nothing to act on.");

        var seeing = WatchAttestation.Validate(AnAttestation() with
        {
            Outcome = WatchOutcomes.Unreachable,
            Diagnosis = "the tracker refused the credential",
        });

        await Assert.That(seeing).IsNotNull()
            .Because("a sweep that reports what it saw reached something. Half a report under "
                   + "an unreachable outcome is two answers, and the control plane would have "
                   + "to pick one.");
    }

    [Test]
    public async Task A_good_pass_carries_no_diagnosis()
    {
        var refused = WatchAttestation.Validate(AnAttestation() with
        {
            Diagnosis = "all fine",
        });

        await Assert.That(refused).IsNotNull()
            .Because("the diagnosis is the discriminator a person's escalation reads, and one "
                   + "on a good pass is a sentence nobody is asked to read.");
    }

    [Test]
    public async Task What_a_sweep_saw_is_bounded()
    {
        var tooMany = WatchAttestation.Validate(AnAttestation() with
        {
            Saw =
            [
                .. Enumerable.Range(0, WatchAttestation.MaxSightings + 1)
                    .Select(i => new WatchSighting { Subject = $"{i}", Version = "1" }),
            ],
        });

        await Assert.That(tooMany).IsNotNull();

        var blank = WatchAttestation.Validate(AnAttestation() with
        {
            Saw = [new WatchSighting { Subject = " ", Version = "1" }],
        });

        await Assert.That(blank).IsNotNull()
            .Because("a sighting with no subject is not a thing anybody could nominate.");

        var prose = WatchAttestation.Validate(AnAttestation() with
        {
            Saw =
            [
                new WatchSighting
                {
                    Subject = new string('x', WatchSighting.MaxSubject + 1),
                    Version = "1",
                },
            ],
        });

        await Assert.That(prose).IsNotNull()
            .Because("a subject is an identity. One the length of a paragraph is a work item's "
                   + "text arriving in a store under an identity's name.");

        var longKey = WatchAttestation.Validate(AnAttestation() with
        {
            Saw =
            [
                new WatchSighting
                {
                    Subject = "4242",
                    Version = "1",
                    IntentKey = new string('x', WatchSighting.MaxIntentKey + 1),
                },
            ],
        });

        await Assert.That(longKey).IsNotNull();
    }

    [Test]
    public async Task An_action_carries_a_skill_or_says_why_it_could_not()
    {
        var neither = WatchAction.Validate(AnAction() with { Skill = null });

        await Assert.That(neither).IsNotNull()
            .Because("an instructions executor with no instructions and no reason would run an "
                   + "agent on nothing, or do nothing and say nothing.");

        await Assert.That(WatchAction.Validate(AnAction() with
        {
            Skill = null,
            Diagnosis = "the skill is not in the repository's declared directory",
        })).IsNull();

        await Assert.That(WatchAction.Validate(AnAction() with
        {
            Diagnosis = "both at once",
        })).IsNotNull();
    }

    [Test]
    public async Task A_skill_travels_with_the_record_of_which_one_it_was()
    {
        var unpinned = WatchAction.Validate(AnAction() with
        {
            Skill = AnAction().Skill! with { Commit = " " },
        });

        await Assert.That(unpinned).IsNotNull()
            .Because("the commit is what the control plane keeps instead of the words, so a "
                   + "skill without one ran and left no record of what it said.");

        await Assert.That(WatchAction.Validate(AnAction() with
        {
            Skill = AnAction().Skill! with { Sha = "" },
        })).IsNotNull();
    }

    [Test]
    public async Task An_action_names_an_executor_this_version_knows()
    {
        var refused = WatchAction.Validate(AnAction() with { Executor = "script" });

        await Assert.That(refused).IsNotNull();
        await Assert.That(refused!).Contains("script");
    }

    [Test]
    public async Task Nothing_that_comes_back_is_named_for_a_secret()
    {
        // THE POOL ATTESTATION'S SCAN, for the report's two types. The ACTION
        // carries the watch document, and so a credential LOCATOR, on purpose
        // - the runner resolves it. What comes BACK must carry none.
        string[] forbidden =
            ["host", "token", "secret", "password", "credential", "apikey", "content", "text",
             "body", "title", "description"];

        var offending = new[] { typeof(WatchAttestation), typeof(WatchSighting) }
            .SelectMany(t => t.GetProperties(BindingFlags.Public | BindingFlags.Instance)
                .Where(p => p.Name != "EqualityContract")
                .Select(p => $"{t.Name}.{p.Name}"))
            .Where(n => forbidden.Any(w =>
                n.Split('.')[1].Contains(w, StringComparison.OrdinalIgnoreCase)))
            .ToList();

        await Assert.That(offending).IsEmpty();
    }
}
