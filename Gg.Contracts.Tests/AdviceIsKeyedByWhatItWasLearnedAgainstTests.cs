using Gg.Contracts;
using Gg.Contracts.Authoring;

namespace Gg.Contracts.Tests;

/// <summary>
/// Learned context is keyed by what it was learned against, not by who will read it.
/// </summary>
/// <remarks>
/// <para>
/// <b>The gap this closes was found by reading GG-330's prompt.</b>
/// <c>learn-work-kind</c> tells a flight to do the work <i>"as the kind named in
/// your task would do it"</i> — and no kind is named in the task. In that flight's
/// 5,282-character prompt, <c>learn-work-kind</c> appears ten times and every other
/// kind appears once, inside a nomination menu. <c>gg fly</c> has one
/// <c>--work-kind</c>; the lease carries no kind name; the prompt states none. So
/// when it named its own kind thirteen times, it was reading the only information
/// it had, correctly — and the refusal added for that named the only name an agent
/// can produce.
/// </para>
/// <para>
/// <b>The fault underneath is the axis.</b> Every finding the two rehearsals
/// produced is about a REPOSITORY or an IMAGE — <c>node_modules</c> absent at
/// checkout, <c>npx stylelint</c> silently fetching its own copy, jsdom performing
/// no layout so a margin assertion proves nothing, a US/UK spelling split in a
/// grep, no <c>dotnet</c> and no <c>docker</c> on the PATH. Not one is about
/// <c>ui-preview</c> versus <c>review</c>. <see cref="LearnedAgainst"/> already
/// carried repository, commit, image and envelope, so the model already said advice
/// is learned AGAINST something; composing it <c>work-kind-only</c> then bolted it
/// onto an axis nothing had measured.
/// </para>
/// <para>
/// <b>So nothing names a target any more.</b> Advice lands on the tenant's root as
/// a list, composes by <c>Append</c> exactly as instructions do, and a flight reads
/// the entries whose <c>against</c> matches its own provenance — which the runner
/// already ships as <c>source.provenance</c>. The question "which kind is this
/// for?" stops having to be answered because it stops being asked.
/// </para>
/// <para>
/// <b>Changed now because nothing reads it yet.</b> There were zero non-test
/// readers of <c>Envelope.Learned</c> in Gg.Runner, Gg.Cli, Gg.Client and Gg.Local
/// when this was written. The axis was free to move at that moment and would not
/// have been once a prompt block read it.
/// </para>
/// </remarks>
public class AdviceIsKeyedByWhatItWasLearnedAgainstTests
{
    [Test]
    public async Task A_tenant_accumulates_one_entry_per_thing_it_has_learned_about()
    {
        var read = EnvelopeYaml.Parse("""
            context:
              scope: "**"
              constitution: "1.0.0"
            learned:
              - against:
                  repository: "JDX/JDNext"
                  commit: "a1b2c3d"
                advice:
                  - "node_modules is absent at checkout; npm install takes about fifty seconds."
              - against:
                  image: "ghcr.io/acme/ci:12"
                advice:
                  - "No dotnet and no docker on PATH, so an Aspire backend cannot start here."
            obligations:
              in-scope:
                check: machine
                rule: no-file-outside-scope
            loops:
              implement:
                executor: frontier
                discharges: [in-scope]
                moves: [read, edit]
                budget:
                  wall-clock: "20m"
                on-exhaustion: handoff-to-human
            destinations:
              forge:
                kind: pull-request
                requires: [in-scope]
            """);

        await Assert.That(read.Diagnosis).IsNull().Because($"{read.Diagnosis}");
        await Assert.That(read.Envelope!.Learned!.Count).IsEqualTo(2)
            .Because("a repository and an image are two different things to have learned "
                   + "about, and a tenant flying both needs both.");
    }

    [Test]
    public async Task Advice_composes_by_append_so_it_is_never_scoped_to_one_kind()
    {
        // THE WHOLE RE-KEY, ASSERTED ON THE OPERATOR. work-kind-only was the bug:
        // it made every piece of advice belong to exactly one kind, and nothing had
        // ever measured a fact that did.
        var composed = typeof(Envelope).GetProperty(nameof(Envelope.Learned))!
            .GetCustomAttributes(typeof(ComposesAttribute), inherit: false)
            .Cast<ComposesAttribute>()
            .Single();

        await Assert.That(composed.Operator).IsEqualTo(MergeOperators.Append)
            .Because("advice accumulates across layers like instructions do. It was "
                   + "work-kind-only, which is what forced a flight to name a kind that "
                   + "nothing had told it.");
    }

    [Test]
    public async Task Two_entries_about_the_same_repository_are_refused()
    {
        // BOUNDED BY WHAT IT IS ABOUT, not by how many rehearsals have run. Two
        // entries for one repository is two answers to one question, and a reader
        // filtering by provenance would have to pick - which is a naming convention
        // doing a schema's job.
        var read = EnvelopeYaml.Parse("""
            context:
              scope: "**"
              constitution: "1.0.0"
            learned:
              - against:
                  repository: "JDX/JDNext"
                  commit: "a1b2c3d"
                advice: ["One thing."]
              - against:
                  repository: "JDX/JDNext"
                  commit: "9f8e7d6"
                advice: ["A different thing."]
            obligations:
              in-scope:
                check: machine
                rule: no-file-outside-scope
            loops:
              implement:
                executor: frontier
                discharges: [in-scope]
                moves: [read, edit]
                budget:
                  wall-clock: "20m"
                on-exhaustion: handoff-to-human
            destinations:
              forge:
                kind: pull-request
                requires: [in-scope]
            """);

        await Assert.That(read.Diagnosis).IsNotNull()
            .Because("newer advice about a repository REPLACES older advice about it; two "
                   + "entries is an accumulation nobody can read.");

        await Assert.That(read.Diagnosis!).Contains("JDX/JDNext")
            .Because("and the refusal names which one is doubled.");
    }

    [Test]
    public async Task An_amendment_is_a_list_too_so_there_is_one_shape_to_learn()
    {
        // UNIFORM WITH THE ENVELOPE'S OWN KEY. One rehearsal usually learns about one
        // thing, but `learned:` meaning a block here and a list there is a second
        // shape for one key name - and GG-330 spent nine calls on shape alone.
        var read = EnvelopeYaml.ParseLearning("""
            learned:
              - against:
                  repository: "JDX/JDNext"
                  commit: "a1b2c3d"
                advice:
                  - "jsdom performs no layout, so a margin assertion proves nothing here."
            """);

        await Assert.That(read.Diagnosis).IsNull().Because($"{read.Diagnosis}");
        await Assert.That(read.Learned!.Count).IsEqualTo(1);
        await Assert.That(read.Learned![0].Against.Repository).IsEqualTo("JDX/JDNext");
    }

    [Test]
    public async Task The_root_document_has_a_name_both_sides_can_say()
    {
        // THE RUNNER HAS TO NAME THE DOCUMENT IT IS AMENDING, and until now the only
        // "root" in the contract was a ROLE. The control plane had its own
        // EnvelopeNames.Root and the runner had no way to say it, so the alternative
        // was a literal on one side agreeing with a constant on the other by
        // coincidence - which is the drift this exists to prevent.
        await Assert.That(EnvelopeNames.Root).IsEqualTo("root");

        // NOT ASSERTED EQUAL TO Roles.Root, though it is the same string today. They
        // are a document's name and a document's role, and a test pinning the
        // coincidence is a test that forbids ever fixing it - which is the shape of
        // guard that makes a rename impossible rather than visible.
        await Assert.That(typeof(EnvelopeNames)).IsNotEqualTo(typeof(Roles))
            .Because("two ideas, declared separately, so a runner naming the document it "
                   + "amends is not relying on a role's value.");
    }

    [Test]
    public async Task Advice_arriving_replaces_advice_about_the_same_subject()
    {
        // THE MERGE RULE, IN THE CONTRACT rather than in whichever side folds. The
        // control plane does the folding today because the document being amended
        // lives there - but what "replace" MEANS is a schema question, and a rule
        // written on one side is a rule the other can come to disagree with.
        var current = (IReadOnlyList<LearnedContext>)
        [
            Advice("JDX/JDNext", "npm install takes about fifty seconds."),
            Advice("acme/web", "The dev server needs ninety seconds."),
        ];

        var folded = Envelope.Fold(current,
        [
            Advice("JDX/JDNext", "jsdom performs no layout."),
            Advice("ghcr.io/acme/ci:12", "No dotnet and no docker on PATH.", image: true),
        ]);

        await Assert.That(folded.Count).IsEqualTo(3)
            .Because("one subject was already known and was replaced; one is new and "
                   + "was appended.");

        await Assert.That(folded.Single(e => e.Against.Repository == "JDX/JDNext").Advice.Single())
            .IsEqualTo("jsdom performs no layout.")
            .Because("newer advice about a repository REPLACES older advice about it - a "
                   + "rehearsal that ran again learned the environment as it now is.");

        await Assert.That(folded.Single(e => e.Against.Repository == "acme/web").Advice.Single())
            .IsEqualTo("The dev server needs ninety seconds.")
            .Because("and a subject nobody rehearsed this time is left exactly alone.");
    }

    [Test]
    public async Task The_order_advice_was_learned_in_is_kept()
    {
        // A REPLACEMENT HAPPENS IN PLACE, and an arrival goes on the end. Not
        // cosmetic: the stored document is serialized and its bytes are what a
        // version's digest is over, so a fold that reordered would mint a new
        // version out of advice that had not changed.
        var current = (IReadOnlyList<LearnedContext>)
        [
            Advice("one/a", "First."),
            Advice("two/b", "Second."),
        ];

        var folded = Envelope.Fold(current, [Advice("one/a", "Replaced.")]);

        await Assert.That(string.Join(",", folded.Select(e => e.Against.Repository)))
            .IsEqualTo("one/a,two/b")
            .Because("replacing the first entry must not move it behind the second.");
    }

    [Test]
    public async Task Folding_onto_nothing_is_what_arrived()
    {
        // A TENANT THAT HAS BEEN TAUGHT NOTHING YET. Absence and an empty list are
        // the same thing to arrive into, and neither is an error.
        await Assert.That(Envelope.Fold(null, [Advice("one/a", "First.")]).Count).IsEqualTo(1);
        await Assert.That(Envelope.Fold([], [Advice("one/a", "First.")]).Count).IsEqualTo(1);
    }

    private static LearnedContext Advice(string subject, string advice, bool image = false) => new()
    {
        Against = image
            ? new LearnedAgainst { Image = subject }
            : new LearnedAgainst { Repository = subject },
        Advice = [advice],
    };
}
