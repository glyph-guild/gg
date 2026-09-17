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
/// <b>The runner reads the skill; the action says at which commit.</b> Decided
/// 2026-09-16 by the owner, amending 0.182.0: the control plane reads exactly
/// one thing from a customer's repository — a declared narrowings directory —
/// and no code, so it pins the commit the watch's ref resolves to and the
/// runner reads the skill there, with the customer's credential. What ran comes
/// back as the blob's digest, on the runner's word.
/// </para>
/// <para>
/// <b>What comes back is the executor's nominations, bounded.</b> Decided
/// 2026-09-16 by the owner, amending 0.182.0's sightings: <i>"the agent and/or
/// script should be doing that. that way it can be dynamic if necessary."</i>
/// So the report carries what the executor nominated - a subject, its version,
/// the kind it chose and why - and the board still decides each one. The
/// limits are what keep a runner from putting a work item's text into a store
/// by calling it a subject or a reason.
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
        Nominated =
        [
            new SweepNomination
            {
                Subject = "4242",
                Version = "7",
                IntentKey = "https://tracker.example/items/4242",
                WorkKind = "review",
                Reason = "Tagged needs-review and nobody has looked at it in a week.",
            },
        ],
        MeasuredAt = DateTimeOffset.UnixEpoch.AddYears(56),
        SkillSha = new string('b', 40),
    };

    internal static WatchAction AnAction() => new()
    {
        ActionId = V7(),
        Watch = "nightly-triage",
        WatchVersion = "nightly-triage@v3",
        Document = AWatchDeclaresReferencesTests.AWatch(),
        Executor = WatchExecutors.Instructions,
        Moves = [LoopMoves.Read, LoopMoves.Propose],
        SkillCommit = new string('a', 40),
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
                ["actionId", "watch", "watchVersion", "document", "executor", "moves",
                 "skillCommit", "diagnosis", "decidedAt"]);
        await Assert.That(ProtocolSurface.JsonMembers[typeof(WatchActionList)])
            .IsEquivalentTo((string[])["actions"]);
        await Assert.That(ProtocolSurface.JsonMembers[typeof(WatchAttestation)])
            .IsEquivalentTo((string[])
                ["attestationId", "watch", "actionId", "outcome", "nominated", "measuredAt",
                 "diagnosis", "skillSha"]);
        await Assert.That(ProtocolSurface.JsonMembers[typeof(SweepNomination)])
            .IsEquivalentTo((string[])
                ["subject", "version", "intentKey", "workKind", "reason", "note"]);
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
        // RULE 8 OF THE SLICE, and `MaintainLoop`'s scar: nominating nothing is
        // a result and silence is a fault. A validator that refused an empty
        // list would make the quiet pass indistinguishable from a dead one.
        await Assert.That(WatchAttestation.Validate(AnAttestation() with { Nominated = [] }))
            .IsNull();
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
            Nominated = [],
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
            .Because("a sweep that nominated something reached something. Half a report under "
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
    public async Task What_a_sweep_nominated_is_bounded()
    {
        static SweepNomination A(string subject = "4242") => new()
        {
            Subject = subject,
            Version = "1",
            Reason = "It needs a person.",
        };

        var tooMany = WatchAttestation.Validate(AnAttestation() with
        {
            Nominated =
            [
                .. Enumerable.Range(0, WatchAttestation.MaxNominations + 1).Select(i => A($"{i}")),
            ],
        });

        await Assert.That(tooMany).IsNotNull();

        await Assert.That(WatchAttestation.Validate(AnAttestation() with
        {
            Nominated = [A(subject: " ")],
        })).IsNotNull()
            .Because("a nomination with no subject is not about anything the board could open.");

        await Assert.That(WatchAttestation.Validate(AnAttestation() with
        {
            Nominated = [A(new string('x', SweepNomination.MaxSubject + 1))],
        })).IsNotNull()
            .Because("a subject is an identity. One the length of a paragraph is a work item's "
                   + "text arriving in a store under an identity's name.");

        await Assert.That(WatchAttestation.Validate(AnAttestation() with
        {
            Nominated = [A() with { IntentKey = new string('x', SweepNomination.MaxIntentKey + 1) }],
        })).IsNotNull();

        await Assert.That(WatchAttestation.Validate(AnAttestation() with
        {
            Nominated = [A() with { WorkKind = new string('x', FlightNomination.MaxWorkKind + 1) }],
        })).IsNotNull();
    }

    [Test]
    public async Task A_nomination_says_why_and_says_it_briefly()
    {
        // THE EXECUTOR'S JUDGMENT IS THE POINT, which is why the owner put the
        // choice with it - and a choice with no reason is one a person on the
        // board cannot weigh. The bounds are FlightNomination's own, measured
        // there, so a sweep's reason and an agent's are held to one length.
        var unsaid = WatchAttestation.Validate(AnAttestation() with
        {
            Nominated = [AnAttestation().Nominated[0] with { Reason = " " }],
        });

        await Assert.That(unsaid).IsNotNull();
        await Assert.That(unsaid!).Contains("reason");

        await Assert.That(WatchAttestation.Validate(AnAttestation() with
        {
            Nominated =
            [
                AnAttestation().Nominated[0] with
                {
                    Reason = new string('x', FlightNomination.MaxReason + 1),
                },
            ],
        })).IsNotNull();

        await Assert.That(WatchAttestation.Validate(AnAttestation() with
        {
            Nominated =
            [
                AnAttestation().Nominated[0] with
                {
                    Note = new string('x', FlightNomination.MaxNote + 1),
                },
            ],
        })).IsNotNull();

        await Assert.That(WatchAttestation.Validate(AnAttestation() with
        {
            Nominated = [AnAttestation().Nominated[0] with { WorkKind = null }],
        })).IsNull()
            .Because("the kind may be left to the bound: a menu of one names it, and a menu of "
                   + "more is the board's refusal to write, not the wire's.");
    }

    [Test]
    public async Task A_sighting_became_a_nomination_under_the_same_wire_identity()
    {
        // A RENAME MUST NOT CHANGE THE WIRE IDENTITY - the contract's own rule.
        // What the record means changed with the owner's decision; which record
        // it is did not.
        await Assert.That(typeof(SweepNomination).Assembly.GetType("Gg.Contracts.WatchSighting"))
            .IsNull();

        var pinned = typeof(SweepNomination)
            .GetCustomAttributes(typeof(PinnedIdAttribute), inherit: false)
            .Cast<PinnedIdAttribute>()
            .Single();

        await Assert.That(pinned.Id.ToString()).IsEqualTo("763495ae-bbe3-4f0e-b9b2-e991fa9e4b3d");
    }

    [Test]
    public async Task An_action_names_the_moves_its_executor_is_granted()
    {
        // THE SHIPPED `sweep` KIND'S COMPOSITION, handed to the runner so it
        // attaches exactly those servers. An unknown move is refused here
        // rather than launched as a tool nobody can name.
        var refused = WatchAction.Validate(AnAction() with { Moves = ["send"] });

        await Assert.That(refused).IsNotNull();
        await Assert.That(refused!).Contains("send");

        await Assert.That(WatchAction.Validate(AnAction() with { Moves = [] })).IsNull()
            .Because("a tenant may tighten the kind to nothing. A sweep that can do nothing "
                   + "attests that, which is the board-side answer, not a malformed action.");
    }

    [Test]
    public async Task An_action_carries_a_pinned_commit_or_says_why_it_could_not()
    {
        var neither = WatchAction.Validate(AnAction() with { SkillCommit = null });

        await Assert.That(neither).IsNotNull()
            .Because("a runner handed no commit would read the skill at whatever the ref says "
                   + "now - which is the review the pin exists to hold.");

        await Assert.That(WatchAction.Validate(AnAction() with
        {
            SkillCommit = null,
            Diagnosis = "'refs/heads/main' does not resolve to a commit",
        })).IsNull();

        await Assert.That(WatchAction.Validate(AnAction() with
        {
            Diagnosis = "both at once",
        })).IsNotNull();
    }

    [Test]
    public async Task A_pin_is_a_commit_and_not_a_ref()
    {
        var moving = WatchAction.Validate(AnAction() with { SkillCommit = "refs/heads/main" });

        await Assert.That(moving).IsNotNull()
            .Because("a ref moves. A pin that is a ref pins nothing, and the words that run "
                   + "are whatever somebody pushed after the sweep was decided.");

        await Assert.That(WatchAction.Validate(AnAction() with
        {
            SkillCommit = new string('a', 64),
        })).IsNull()
            .Because("a SHA-256 repository's commits are sixty-four hex digits, and those are "
                   + "commits too.");
    }

    [Test]
    public async Task No_member_of_the_action_can_carry_the_skills_words()
    {
        // THE OWNER'S DECISION, HELD STRUCTURALLY. 0.182.0 had a `WatchSkill`
        // with a `Content` member; the control plane was never to fill it. A
        // member that exists will be filled by somebody.
        var members = typeof(WatchAction)
            .GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Where(p => p.Name != "EqualityContract")
            .Select(p => (p.Name, p.PropertyType))
            .ToList();

        await Assert.That(members.Where(m => m.Name.Contains("Content", StringComparison.Ordinal)
                                          || m.Name.Contains("Text", StringComparison.Ordinal)))
            .IsEmpty();
        await Assert.That(typeof(WatchAction).Assembly.GetType("Gg.Contracts.WatchSkill"))
            .IsNull()
            .Because("the record that had somewhere to put the words is gone, and a second one "
                   + "under the same name would be the same member coming back.");
    }

    [Test]
    public async Task A_good_pass_reports_the_digest_of_the_skill_it_followed()
    {
        var unsaid = WatchAttestation.Validate(AnAttestation() with { SkillSha = null });

        await Assert.That(unsaid).IsNotNull()
            .Because("the digest is the record of what ran. A sweep that swept and cannot say "
                   + "which words it followed leaves the review with nothing to point at.");

        await Assert.That(WatchAttestation.Validate(AnAttestation() with { SkillSha = "latest" }))
            .IsNotNull()
            .Because("a digest is hex, forty or sixty-four digits - anything else is a label.");

        await Assert.That(WatchAttestation.Validate(AnAttestation() with
        {
            Outcome = WatchOutcomes.Unreachable,
            Nominated = [],
            Diagnosis = "the commit could not be pinned",
            SkillSha = null,
        })).IsNull()
            .Because("an unreachable sweep may never have read the skill at all.");
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

        var offending = new[] { typeof(WatchAttestation), typeof(SweepNomination) }
            .SelectMany(t => t.GetProperties(BindingFlags.Public | BindingFlags.Instance)
                .Where(p => p.Name != "EqualityContract")
                .Select(p => $"{t.Name}.{p.Name}"))
            .Where(n => forbidden.Any(w =>
                n.Split('.')[1].Contains(w, StringComparison.OrdinalIgnoreCase)))
            .ToList();

        await Assert.That(offending).IsEmpty();
    }
}
