using Gg.Contracts;
using Gg.Contracts.Authoring;

namespace Gg.Contracts.Tests;

/// <summary>
/// An envelope may set environment variables on what a flight runs, and never a
/// secret.
/// </summary>
/// <remarks>
/// <para>
/// <b>ADR-0026.</b> GG-268 fixed its work item and then could not commit,
/// because the repository's own pre-commit hook ran against a runner with
/// neither the toolchain nor the reason. Gg stopped running a scratch tree's
/// hooks the same day, which fixes that case; this is the general one. A tenant
/// whose repository needs a variable set has nowhere to say so today — not the
/// envelope, not a work kind, not a registration, not a profile.
/// </para>
/// <para>
/// <b>Union, and a contradiction refuses.</b> The whole layering rests on every
/// operator being commutative and associative, so a silent pick between two
/// values for one name is the one thing that would break it. Refusing is
/// symmetric and therefore still order-free. The same name with the same value
/// on two layers is not a contradiction.
/// </para>
/// <para>
/// <b>And never a secret.</b> <c>Credentials</c> already states the rule this
/// obeys: <i>"There is deliberately no environment-variable kind, and there
/// never will be"</i>, because environment variables leak into child processes,
/// <c>ps</c> output, crash dumps and CI logs. That is about credentials and this
/// is about configuration, so it does not forbid this field — but an envelope is
/// a document in a repository the tenant reads and reviews, so anything written
/// here is readable by everyone who can read the airspace, and the documentation
/// has to say so where somebody will find it.
/// </para>
/// <para>
/// <b>Not on a narrowing.</b> Union on <c>requires</c> is admissible for a
/// narrowing because it only ever admits less; union on variables admits more,
/// and a narrowing that widens is a contradiction in terms.
/// </para>
/// </remarks>
public class AnEnvelopeMaySetVariablesTests
{
    private static EnvelopeVariable Husky() => new() { Name = "HUSKY", Value = "0" };

    [Test]
    public async Task A_document_may_declare_them_and_they_read_back()
    {
        var read = EnvelopeYaml.Parse("""
            context:
              scope: "**"
              constitution: "1.0.0"
            variables:
              HUSKY: "0"
            obligations: {}
            """);

        await Assert.That(read.Diagnosis).IsNull();
        await Assert.That(read.Envelope!.Variables).IsNotNull();
        await Assert.That(read.Envelope.Variables!.Single()).IsEqualTo(Husky())
            .Because("a map in the YAML a person writes and a list of named records "
                   + "underneath, the way obligations, loops and destinations already are - "
                   + "the envelope has no dictionary-valued field and gains none here.");
    }

    [Test]
    public async Task Absence_and_emptiness_stay_apart()
    {
        var absent = EnvelopeYaml.Parse("""
            context:
              scope: "**"
              constitution: "1.0.0"
            obligations: {}
            """);
        var empty = EnvelopeYaml.Parse("""
            context:
              scope: "**"
              constitution: "1.0.0"
            variables: {}
            obligations: {}
            """);

        await Assert.That(absent.Envelope!.Variables).IsNull();
        await Assert.That(empty.Envelope!.Variables).IsNotNull().And.IsEmpty()
            .Because("declaring none is a decision and saying nothing is not, and three "
                   + "contract versions once parsed a field they never rendered because that "
                   + "distinction was not kept in both directions.");
    }

    [Test]
    public async Task What_is_declared_survives_the_render()
    {
        var one = EnvelopeYaml.Parse("""
            context:
              scope: "**"
              constitution: "1.0.0"
            variables:
              HUSKY: "0"
            obligations: {}
            """).Envelope!;

        var round = EnvelopeYaml.Parse(EnvelopeText.Render(one));

        await Assert.That(round.Envelope!.Variables!.Single()).IsEqualTo(Husky());
        await Assert.That(EnvelopeText.Render(one)).Contains("variables:")
            .Because("a field parsed and never rendered vanishes on show, edit, apply - and "
                   + "the next pull reports a change nobody made.");
    }

    [Test]
    public async Task A_name_that_is_not_one_is_refused()
    {
        var bad = EnvelopeYaml.Parse("""
            context:
              scope: "**"
              constitution: "1.0.0"
            variables:
              "not a name": "0"
            obligations: {}
            """);

        await Assert.That(bad.Envelope).IsNull();
        await Assert.That(bad.Diagnosis).IsNotNull();
    }

    [Test]
    public async Task A_narrowing_cannot_set_one()
    {
        await Assert.That(typeof(EnvelopeNarrowing).GetProperty("Variables")).IsNull()
            .Because("union on requires is admissible for a narrowing because it only ever "
                   + "admits less. Union on variables admits more, and a narrowing that widens "
                   + "is a contradiction in terms.");
    }

    [Test]
    public async Task Declaring_one_where_there_were_none_widens()
    {
        var was = Doc(null);
        var now = Doc([Husky()]);

        var moved = EnvelopeDirection.Widening(was, now);

        await Assert.That(moved).IsNotNull();
        await Assert.That(moved!.Value.Field).Contains("variables");
    }

    [Test]
    public async Task Withdrawing_one_widens_too_because_a_child_behaves_differently_either_way()
    {
        var moved = EnvelopeDirection.Widening(Doc([Husky()]), Doc([]));

        await Assert.That(moved).IsNotNull()
            .Because("removing a variable changes what a child process does exactly as much as "
                   + "adding one. Neither direction can be shown to reduce anything, and where "
                   + "the table declares no order the answer is widening.");
    }

    [Test]
    public async Task An_unchanged_document_has_not_moved()
    {
        // LIVENESS. Both assertions above ask for a non-null, and a comparator
        // that answered "widening" to everything would satisfy them.
        await Assert.That(EnvelopeDirection.Widening(Doc([Husky()]), Doc([Husky()]))).IsNull();
    }

    /// <summary>The smallest envelope that differs only in its variables.</summary>
    private static Envelope Doc(IReadOnlyList<EnvelopeVariable>? variables) =>
        EnvelopeYaml.Parse("""
            context:
              scope: "**"
              constitution: "1.0.0"
            obligations: {}
            """).Envelope! with { Variables = variables };
}
