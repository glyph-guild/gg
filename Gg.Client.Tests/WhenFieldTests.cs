using Gg.Client;
using Gg.Contracts;

namespace Gg.Client.Tests;

/// <summary>
/// `when:` survives the round trip, and a condition nothing recognises is refused.
/// </summary>
/// <remarks>
/// <para>
/// <b>This is the field where a silent failure leaves no trace.</b> A rule that
/// silently evaluates false produces a verdict somebody can be suspicious of. A
/// <c>when:</c> that silently evaluates false produces nothing at all - the
/// obligation does not attach, no verdict is written, and the flight reads as
/// governed. So a condition nobody can read must halt, and must emphatically not
/// be treated as false: false is the answer that removes the obligation.
/// </para>
/// <para>
/// <b>And it must survive the trip.</b> A field the parser reads and the emitter
/// drops is an obligation that is conditional in the file somebody wrote and
/// unconditional in the envelope that governs - or the reverse, which is worse.
/// </para>
/// </remarks>
public class WhenFieldTests
{
    private const string Conditional = """
        context:
          scope: "src/**"
          constitution: "1.0.0"
        obligations:
          in-scope:
            check: machine
            rule: no-file-outside-scope
          reversibility-plan:
            check: machine
            when: "change.manifest touches migrations/**"
            rule: no-file-outside-scope
        loops:
          implement:
            executor: frontier
            discharges:
              - in-scope
            moves:
              - read
              - edit
              - run-tests
              - search
            budget:
              wall-clock: "30m"
            on-exhaustion: handoff-to-human
        destinations:
          pull-request:
            kind: pull-request
            requires:
              - in-scope
        """;

    private static Obligation Conditioned(Envelope envelope) =>
        envelope.Obligations.Single(o => o.Id == "reversibility-plan");

    // ---- the round trip ----

    [Test]
    public async Task A_condition_parses_into_the_obligation_that_declared_it()
    {
        var parsed = EnvelopeYaml.Parse(Conditional);

        await Assert.That(parsed.Diagnosis).IsNull();
        await Assert.That(Conditioned(parsed.Envelope!).When)
            .IsEqualTo("change.manifest touches migrations/**");
    }

    [Test]
    public async Task An_obligation_with_no_condition_parses_as_null_rather_than_empty()
    {
        // Null means "declares no condition". Empty string would be a condition
        // that exists and says nothing, which is a third thing nobody wants.
        var parsed = EnvelopeYaml.Parse(Conditional);

        await Assert.That(parsed.Envelope!.Obligations.Single(o => o.Id == "in-scope").When).IsNull();
    }

    [Test]
    public async Task The_condition_survives_emit_and_reparse()
    {
        // THE ROUND TRIP. Text to model to text to model, and the condition is
        // still there and still says the same thing.
        var first = EnvelopeYaml.Parse(Conditional).Envelope!;
        var reparsed = EnvelopeYaml.Parse(EnvelopeText.Render(first));

        await Assert.That(reparsed.Diagnosis).IsNull();
        await Assert.That(Conditioned(reparsed.Envelope!).When).IsEqualTo(Conditioned(first).When);
    }

    [Test]
    public async Task Emitting_twice_gives_the_same_text()
    {
        // The ordering rule from step 1, re-asserted with the new field in play:
        // a canonical form that moved when a field was added would move every
        // envelope version for no change in what governs.
        var envelope = EnvelopeYaml.Parse(Conditional).Envelope!;
        var once = EnvelopeText.Render(envelope);

        await Assert.That(EnvelopeText.Render(EnvelopeYaml.Parse(once).Envelope!))
            .IsEqualTo(once);
    }

    [Test]
    public async Task The_condition_is_emitted_between_check_and_rule()
    {
        // A declared position, because the canonical text is what the version
        // fingerprints. "Wherever the dictionary put it" is not a position.
        var text = EnvelopeText.Render(EnvelopeYaml.Parse(Conditional).Envelope!);
        var block = text[text.IndexOf("reversibility-plan", StringComparison.Ordinal)..];

        var check = block.IndexOf("check:", StringComparison.Ordinal);
        var when = block.IndexOf("when:", StringComparison.Ordinal);
        var rule = block.IndexOf("rule:", StringComparison.Ordinal);

        await Assert.That(check).IsGreaterThanOrEqualTo(0);
        await Assert.That(when).IsGreaterThan(check);
        await Assert.That(rule).IsGreaterThan(when);
    }

    [Test]
    public async Task An_unconditional_obligation_emits_no_when_line_at_all()
    {
        // Not `when: null`, not `when: always`. The absence of the line is the
        // absence of a condition, and that is the only place absence is allowed
        // to mean something here - because the field is optional in the schema
        // and the three attachment states live elsewhere.
        var text = EnvelopeText.Render(EnvelopeYaml.Parse(Conditional).Envelope!);
        var inScope = text["  in-scope:".Length..];

        await Assert.That(text.Split("when:").Length - 1).IsEqualTo(1)
            .Because("one obligation declares a condition, so the text carries exactly one.");
        await Assert.That(inScope).IsNotEmpty();
    }

    // ---- a condition nothing recognises ----

    [Test]
    public async Task A_misspelled_condition_is_refused_and_names_itself()
    {
        // PROVEN WITH A MISSPELLING, because that is how this arrives in
        // practice: not an attack, a typo in a file somebody hand-wrote.
        var parsed = EnvelopeYaml.Parse(
            Conditional.Replace("manifest touches", "manifest touchs", StringComparison.Ordinal));

        await Assert.That(parsed.Envelope).IsNull();
        await Assert.That(parsed.Diagnosis!).Contains("touchs")
            .Because("quoting back what was written is what makes a typo findable.");
        await Assert.That(parsed.Diagnosis!).Contains(AttachmentConditions.TouchesPrefix)
            .Because("and naming the form it should have taken is what makes it fixable.");
    }

    [Test]
    public async Task The_refusal_says_why_false_was_not_the_answer()
    {
        // The reasoning is in the message on purpose. Somebody reading this
        // diagnosis is one keystroke from "just skip conditions you don't
        // understand", and that keystroke silently unenforces the obligation.
        var parsed = EnvelopeYaml.Parse(
            Conditional.Replace("change.manifest", "chnage.manifest", StringComparison.Ordinal));

        await Assert.That(parsed.Diagnosis!).Contains("cannot be treated as false");
        await Assert.That(parsed.Diagnosis!).Contains("nothing would be recorded");
    }

    [Test]
    public async Task A_condition_with_the_right_prefix_and_nothing_after_it_is_refused()
    {
        // `change.manifest touches ` with no glob matches nothing, so it would
        // attach nothing, forever, quietly.
        var parsed = EnvelopeYaml.Parse(Conditional.Replace(
            "change.manifest touches migrations/**", "change.manifest touches",
            StringComparison.Ordinal));

        await Assert.That(parsed.Envelope).IsNull();
        await Assert.That(parsed.Diagnosis!).Contains("when");
    }

    [Test]
    public async Task A_condition_naming_another_obligation_is_refused()
    {
        // NOT BUILT, deliberately, and refused rather than left to fail later.
        // `when: obligations.x == violated` makes one obligation's attachment
        // depend on another's verdict - which is no longer a pure function of the
        // fact set. It turns evaluation into a fixed-point computation and
        // reintroduces exactly the ordering dependence step 1 proved absent.
        var parsed = EnvelopeYaml.Parse(Conditional.Replace(
            "change.manifest touches migrations/**", "obligations.in-scope == violated",
            StringComparison.Ordinal));

        await Assert.That(parsed.Envelope).IsNull()
            .Because("a condition reading a verdict is not a condition over facts.");
    }

    [Test]
    public async Task A_duplicate_when_key_is_refused()
    {
        var parsed = EnvelopeYaml.Parse(Conditional.Replace(
            "    when: \"change.manifest touches migrations/**\"\n",
            "    when: \"change.manifest touches migrations/**\"\n"
          + "    when: \"change.manifest touches src/**\"\n",
            StringComparison.Ordinal));

        await Assert.That(parsed.Envelope).IsNull();
        await Assert.That(parsed.Diagnosis!).Contains("when");
    }

    [Test]
    public async Task A_misspelling_of_the_key_itself_is_refused_rather_than_ignored()
    {
        // The worst case in the whole field: `whn:` parsed as an unknown key and
        // ignored would leave an obligation that its author believes is
        // conditional and that in fact always applies. That direction is at least
        // safe. `wehn:` on an obligation somebody meant to be conditional the
        // other way round is not - and neither is acceptable silently.
        var parsed = EnvelopeYaml.Parse(
            Conditional.Replace("    when:", "    whn:", StringComparison.Ordinal));

        await Assert.That(parsed.Envelope).IsNull();
        await Assert.That(parsed.Diagnosis!).Contains("whn")
            .Because("an unknown key on an obligation is refused by name, not dropped.");
    }

    // ---- the known forms are a closed list ----

    [Test]
    public async Task The_known_forms_are_declared_and_the_predicate_agrees_with_them()
    {
        // Two things that could drift: the list somebody is shown in a diagnosis,
        // and the check that actually runs. A form advertised and not accepted is
        // a diagnosis that lies.
        await Assert.That(AttachmentConditions.Forms).IsNotEmpty();

        foreach (var form in AttachmentConditions.Forms)
        {
            await Assert.That(AttachmentConditions.IsKnown(
                    form.Replace("<glob>", "migrations/**", StringComparison.Ordinal)))
                .IsTrue()
                .Because($"'{form}' is advertised as a form, so a filled-in instance of it parses.");
        }

        await Assert.That(AttachmentConditions.IsKnown("something else entirely")).IsFalse()
            .Because("and the predicate can still say no, or the loop above proves nothing.");
    }
}
