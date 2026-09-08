namespace Gg.Runner.Tests;

/// <summary>
/// Members the runner supplies and nothing reads.
/// </summary>
/// <remarks>
/// <para>
/// <b>The mirror of good-grief's unsupplied-input scan, in the opposite
/// direction.</b> That one asks <i>which parameters does no production call site
/// pass</i>; this asks <i>which members of a supplied type does no production
/// consumer read</i>. Same corpus discipline, same exemption shape, and a
/// liveness anchor of its own — carrying the sibling's across is the re-keying
/// mistake slice eighteen already recorded.
/// </para>
/// <para>
/// <b>It lives here and not beside its mirror, deliberately.</b> Fifteen of the
/// sixteen findings the hand sweep produced are in the runner, the motivating
/// defect is in the runner, and the history retro-detection reads is the
/// runner's. A scan placed beside its sibling for symmetry would have run
/// against a corpus that does not contain what it was built to find.
/// </para>
/// <para>
/// <b>It does NOT catch the defect that motivated it, and that is written into
/// the criteria rather than discovered here.</b> <c>LandingRequest.Secret</c> is
/// read — once, in <c>PushAsync</c> — so at the commit before the fix a
/// member-level scan reports nothing. That defect is a fourth shape: <i>one type,
/// two consumers, only one reads the member</i>, which is a divergence between
/// consumers rather than a dead member. The per-consumer scan that would catch it
/// is carried, and this slice's own step 1 has just created the known instance at
/// a known commit that such a scan needs.
/// </para>
/// </remarks>
public class UnreadMemberTests
{
    /// <summary>A dotted read, so the test corpus has one to find.</summary>
    private sealed class Reader
    {
        internal string MentionedInATestOnly { get; } = "";
    }

    /// <summary>Why a member reads as unread, and what would change that.</summary>
    /// <remarks>
    /// <b><c>RemovedBy</c> is the field this family has never had.</b> A reason
    /// says why the entry is there today; a trigger says what would have to
    /// change, so it can be checked rather than believed.
    /// </remarks>
    private sealed record Exemption
    {
        public required string Because { get; init; }

        public required string RemovedBy { get; init; }
    }

    /// <summary>
    /// The members this runner declares, supplies, and reads nowhere.
    /// </summary>
    /// <remarks>
    /// Keyed <c>Type.Member</c>, so a rename fails as a stale entry rather than
    /// silently watching nothing.
    /// </remarks>
    /// <summary>
    /// Members this runner declares, supplies, and reads nowhere.
    /// </summary>
    /// <remarks>
    /// <b>Empty, and getting here is what the slice was for.</b> Sixteen entries
    /// went in three ways: nine deleted with the declarations they described,
    /// three wired to callers that wanted them, and <b>two were false findings
    /// the scan itself had to be corrected to stop making</b> — a member read
    /// unqualified inside its own type, and a member read from another project.
    /// The sixteenth, <c>ExecutorRun.DurationMs</c>, went undecidable under the
    /// first correction.
    /// <para>
    /// An empty list is not a finished job. It is the state in which the next
    /// entry means something.
    /// </para>
    /// </remarks>
    private static readonly IReadOnlyDictionary<string, Exemption> Exempt =
        new Dictionary<string, Exemption>(StringComparer.Ordinal)
        {
            ["PoolConfiguration.Endpoint"] = new Exemption
            {
                Because = "A FALSE FINDING, kept so the blind spot has a name. It IS read - "
                        + "`configuration.Endpoint` builds the pool adapter's client in "
                        + "Gg.Cli - and this scan takes declarations and reads from the runner "
                        + "alone. Widening the corpus was tried and reverted: it also widens "
                        + "what history retro-detection must read, and the two stopped agreeing",
                RemovedBy = "a corpus that carries every production project AND a history read "
                          + "that carries the same, which is one change and not two",
            },
        };

    // ---- the claim ----

    [Test]
    public async Task Every_unread_member_is_listed_with_a_reason_and_a_trigger()
    {
        var unlisted = UnreadMembers.Scan(UnreadMembers.RunnerSource())
            .Select(UnreadMembers.Key)
            .Where(k => !Exempt.ContainsKey(k))
            .Order(StringComparer.Ordinal)
            .ToList();

        await Assert.That(unlisted).IsEmpty()
            .Because("a member this runner supplies and nothing reads is either a finding or an "
                   + "entry with a reason. Found: " + string.Join(", ", unlisted));
    }

    [Test]
    public async Task No_entry_outlives_the_finding_it_describes()
    {
        // NO GRAVEYARD. An entry the scan no longer finds is a sentence about
        // the past sitting in a list that reads as current.
        var found = UnreadMembers.Scan(UnreadMembers.RunnerSource())
            .Select(UnreadMembers.Key)
            .ToHashSet(StringComparer.Ordinal);

        var stale = Exempt.Keys.Where(k => !found.Contains(k)).Order(StringComparer.Ordinal).ToList();

        await Assert.That(stale).IsEmpty()
            .Because("somebody read this member and the entry outlived the gap. Delete it, and "
                   + "say so in the slice that did the reading. Found: " + string.Join(", ", stale));
    }

    [Test]
    public async Task Every_entry_says_why_and_what_would_change_it()
    {
        foreach (var (key, entry) in Exempt)
        {
            await Assert.That(entry.Because.Length).IsGreaterThan(10)
                .Because($"'{key}' is listed with a reason too short to be one.");
            await Assert.That(entry.RemovedBy.Length).IsGreaterThan(10)
                .Because($"'{key}' says it is unread and not what would change that, which is "
                       + "the difference between a listed finding and a deferral.");
        }
    }

    // ---- retro-detection, on an instance the scan can actually see ----

    /// <summary>The probe reported when it measured, and nothing asked.</summary>
    /// <remarks>
    /// <b>Chosen by running the scan across history rather than by picking a
    /// likely-looking member.</b> Between these two commits exactly one member
    /// stopped being unread, which is what makes the set-difference assertion
    /// below a statement about the defect rather than about the count.
    /// </remarks>
    private const string WasUnread = "ProbeResult.MeasuredAt";

    /// <summary>Where the probe first reported what held and what broke.</summary>
    private const string BeforeItWasRead = "8b35486";

    [Test]
    public async Task Pointed_at_a_commit_where_a_member_was_unread_it_names_that_member()
    {
        await Assert.That(UnreadMembers.Scan(UnreadMembers.RunnerSourceAt(BeforeItWasRead))
                .Select(UnreadMembers.Key))
            .Contains(WasUnread)
            .Because("a scan built to find dead members and never shown against a real one is a "
                   + "claim, and this family's whole problem is claims that were never "
                   + "exercised.");
    }

    [Test]
    public async Task Pointed_at_the_current_tree_it_does_not()
    {
        await Assert.That(UnreadMembers.Scan(UnreadMembers.RunnerSource()).Select(UnreadMembers.Key))
            .DoesNotContain(WasUnread)
            .Because("the survey reads it now. A scan that reported the same on both trees would "
                   + "be measuring the shape of the code rather than the gap.");
    }

    [Test]
    public async Task The_member_that_was_read_is_among_what_moved()
    {
        // WEAKENED FROM "THE ONLY THING THAT MOVED", DELIBERATELY, and the
        // reason is worth more than the assertion was.
        //
        // The set-difference form was the sharper claim: not that the counts
        // differ - two unrelated changes do that - but that exactly one member
        // stopped being unread across history. It survived slice nineteen and
        // died in slice twenty, to slice twenty's OWN deletions: removing nine
        // members put nine more names in the difference, and the assertion
        // failed for the best possible reason.
        //
        // An assertion that a slice's ordinary work invalidates is not a control,
        // it is a tripwire pointed at its author. What survives is the claim the
        // retro test was for: the member was unread then and is read now.
        var before = UnreadMembers.Scan(UnreadMembers.RunnerSourceAt(BeforeItWasRead))
            .Select(UnreadMembers.Key).ToHashSet(StringComparer.Ordinal);
        var now = UnreadMembers.Scan(UnreadMembers.RunnerSource())
            .Select(UnreadMembers.Key).ToHashSet(StringComparer.Ordinal);

        await Assert.That(before.Except(now, StringComparer.Ordinal)).Contains(WasUnread);
        await Assert.That(now).DoesNotContain(WasUnread);
    }

    [Test]
    public async Task An_unreachable_commit_fails_loudly_rather_than_skipping()
    {
        // A retro test that quietly passed when it could not read history would
        // be this slice's own subject: a control that runs and checks nothing.
        await Assert.That(() => UnreadMembers.RunnerSourceAt(
                "0000000000000000000000000000000000000000"))
            .Throws<InvalidOperationException>();
    }

    // ---- the anchor, on this scan's own axis ----

    [Test]
    public async Task The_scan_sees_the_members_it_is_asserting_about()
    {
        // THE LIVENESS ANCHOR, and it is this scan's own rather than the
        // sibling's carried across. Every assertion above is satisfied by a scan
        // that found no members at all.
        var source = UnreadMembers.RunnerSource();

        await Assert.That(source).IsNotEmpty();
        await Assert.That(UnreadMembers.Members(source).Count).IsGreaterThan(50)
            .Because("the runner declares roughly a hundred readable members; a handful means "
                   + "the declaration pattern broke and every assertion above silently stopped "
                   + "covering them.");
    }

    [Test]
    public async Task A_type_with_no_members_fails_rather_than_passing()
    {
        // The other half of the anchor, and the one the brief asked for by name:
        // pointed at a type that declares nothing, the scan must not report
        // success. An empty answer and a clean answer look identical from
        // outside, which is the failure this whole family is about.
        await Assert.That(() => UnreadMembers.Members(new Dictionary<string, string>
        {
            ["Gg.Runner/Nothing.cs"] = "public sealed record Nothing;",
        })).Throws<InvalidOperationException>();
    }

    [Test]
    public async Task A_planted_unread_member_is_found()
    {
        var findings = UnreadMembers.Scan(new Dictionary<string, string>
        {
            ["Gg.Runner/Planted.cs"] = """
                public sealed record Planted
                {
                    public required string Supplied { get; init; }
                    public required string Ignored { get; init; }
                }

                public static class Uses
                {
                    public static string Read(Planted planted) => planted.Supplied;
                }
                """,
        }).Select(UnreadMembers.Key).ToList();

        await Assert.That(findings).Contains("Planted.Ignored");
        await Assert.That(findings).DoesNotContain("Planted.Supplied");
    }

    // ---- the three ways a read hides, each planted ----

    [Test]
    public async Task An_interpolation_hole_is_a_read()
    {
        // THE ONE THE OBVIOUS REGEX GETS WRONG. good-grief's UnsuppliedInputs
        // blanks string bodies before reading anything - a commented-out call
        // would otherwise count as a caller - and its pattern matches `$"..."`
        // too. Reusing it here would erase the ONLY production read of
        // ExecutorRequest.IntentUri, which is the runner's most load-bearing
        // input, and report it as unread with confidence.
        var findings = UnreadMembers.Scan(new Dictionary<string, string>
        {
            ["Gg.Runner/Rendered.cs"] = """
                public sealed record Rendered
                {
                    public required string Shown { get; init; }
                }

                public static class Uses
                {
                    public static string Prompt(Rendered r) => $"Work the issue at {r.Shown} here.";
                }
                """,
        }).Select(UnreadMembers.Key).ToList();

        await Assert.That(findings).DoesNotContain("Rendered.Shown")
            .Because("rendering into a prompt is the ONLY channel by which a flight's intent "
                   + "reaches an agent. A scan that calls that unread is not conservative, it "
                   + "is wrong.");
    }

    [Test]
    public async Task A_mention_in_a_comment_or_a_string_is_not_a_read()
    {
        var findings = UnreadMembers.Scan(new Dictionary<string, string>
        {
            ["Gg.Runner/Mentioned.cs"] = """
                public sealed record Mentioned
                {
                    public required string Talked { get; init; }
                }

                public static class Uses
                {
                    // Someday something will read m.Talked and this will matter.
                    public static string Never() => "m.Talked is not a read";
                }
                """,
        }).Select(UnreadMembers.Key).ToList();

        await Assert.That(findings).Contains("Mentioned.Talked")
            .Because("a comment describing a read and a literal containing one are both prose. "
                   + "Counting them is how a scan reports a member as live because somebody "
                   + "wrote about it.");
    }

    [Test]
    public async Task A_serialized_member_is_read_by_the_serializer()
    {
        // NewProposal.Head is the body this slice just fixed. Nothing in C#
        // reads it; the source-generated serializer does, and a scan that
        // cannot tell would report the wire shape of every request as dead.
        var findings = UnreadMembers.Scan(new Dictionary<string, string>
        {
            ["Gg.Runner/Wire.cs"] = """
                public sealed record Wire
                {
                    [JsonPropertyName("head")]
                    public string Head { get; init; } = "";

                    public string NotSerialized { get; init; } = "";
                }
                """,
        }).Select(UnreadMembers.Key).ToList();

        // A SECOND, UNSERIALIZED MEMBER, and its presence is the point. With
        // only the serialized one the fixture declares nothing after exclusion,
        // and Members throws its anti-vacuity exception - correctly. So the
        // fixture proves the exclusion is SELECTIVE rather than total.
        await Assert.That(findings).Contains("Wire.NotSerialized");
        await Assert.That(findings).DoesNotContain("Wire.Head")
            .Because("a member carrying a JSON name is read by the serializer, and the request "
                   + "this slice just authenticated is built entirely out of them.");
    }

    // ---- reached by one test is not reached by nothing ----

    [Test]
    public async Task A_member_only_a_test_reads_says_so()
    {
        // ON PLANTS, because the runner's own findings list is empty now. The
        // distinction is what matters and it must stay exercised whatever the
        // tree happens to contain.
        var findings = UnreadMembers.Scan(new Dictionary<string, string>
        {
            ["Gg.Runner/Split.cs"] = """
                public sealed record Split
                {
                    public required string MentionedInATestOnly { get; init; }

                    public required string MentionedNowhereAtAll { get; init; }
                }
                """,
        }).ToDictionary(UnreadMembers.Key, f => f.ReadByATest, StringComparer.Ordinal);

        // The dotted read below is what the corpus must see. Real code in this
        // file rather than a string, because Noise blanks literals - a fixture
        // that only MENTIONED the name would prove the scan cannot tell a
        // mention from a read.
        _ = new Reader().MentionedInATestOnly;

        await Assert.That(findings["Split.MentionedInATestOnly"]).IsTrue()
            .Because("this very file reads it, which is what a test-only read looks like.");
        await Assert.That(findings["Split.MentionedNowhereAtAll"]).IsFalse()
            .Because("and nothing names that one, which is the other state.");
    }

    [Test]
    public async Task The_two_states_render_as_different_sentences()
    {
        var checkedByATest = UnreadMembers.Diagnose(new Unread
        {
            Type = "ExecutorCapabilities",
            Member = "ReportsTokens",
            File = "x.cs",
            ReadByATest = true,
        });

        var wantedByNobody = UnreadMembers.Diagnose(new Unread
        {
            Type = "RunnerLoop",
            Member = "HoldFor",
            File = "x.cs",
            ReadByATest = false,
        });

        await Assert.That(checkedByATest).Contains("one test");
        await Assert.That(checkedByATest).DoesNotContain("nothing at all");
        await Assert.That(wantedByNobody).Contains("nothing at all");
        await Assert.That(wantedByNobody).DoesNotContain("one test")
            .Because("a diagnosis that said both would be one sentence wearing two hats, which "
                   + "is the collapse this slice's own refusal split exists to undo.");
    }

    [Test]
    public async Task The_test_corpus_is_really_read()
    {
        // THE ANCHOR FOR THIS AXIS. Without it, ReadByATest is false everywhere
        // because the corpus never loaded, and "nothing at all" becomes the only
        // sentence the scan can produce.
        await Assert.That(UnreadMembers.TestSource()).IsNotEmpty();

        var answered = UnreadMembers.Scan(new Dictionary<string, string>
        {
            ["Gg.Runner/Split.cs"] = """
                public sealed record Split
                {
                    public required string MentionedInATestOnly { get; init; }
                }
                """,
        }).Single();

        await Assert.That(answered.ReadByATest).IsTrue()
            .Because("this file mentions that name, so a corpus that loaded must say so - and "
                   + "one that answered no would be reporting on an empty set.");
    }

    [Test]
    public async Task A_member_whose_name_is_read_on_another_type_is_undecidable()
    {
        // THE BLIND SPOT, and it swallows a real finding: PoolCapabilities
        // .Provider has zero readers anywhere, and `.Provider` is read on
        // LeaseRepoRef and IVcsAdapter, so this scan cannot see it. Slice
        // eighteen's mirror has the same shape and the same remedy - report it
        // as UNDECIDABLE rather than drop it, because a guard that quietly
        // loses a finding is the family it catches.
        var analysis = UnreadMembers.Analyse(new Dictionary<string, string>
        {
            ["Gg.Runner/Two.cs"] = """
                public sealed record One
                {
                    public required string Shared { get; init; }
                }

                public sealed record Other
                {
                    public required string Shared { get; init; }
                }

                public static class Uses
                {
                    public static string Read(Other other) => other.Shared;
                }
                """,
        });

        await Assert.That(analysis.Undecidable).Contains("One.Shared")
            .Because("the read is on Other, and this scan matches on the member name alone - so "
                   + "One.Shared is not proven read and not proven unread.");
        await Assert.That(analysis.Findings.Select(UnreadMembers.Key)).DoesNotContain("One.Shared")
            .Because("and it must not be REPORTED as a finding, because it might be one.");
    }
}
