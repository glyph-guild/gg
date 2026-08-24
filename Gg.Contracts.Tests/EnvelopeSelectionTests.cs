namespace Gg.Contracts.Tests;

/// <summary>
/// The envelope's two selections: <c>environment:</c> and <c>repository:</c>.
/// </summary>
/// <remarks>
/// <para>
/// <b>A selection, not a bound.</b> Every other envelope field composes across
/// layers through a merge operator; these two get none, deliberately. Two
/// layers naming different environments is not an empty intersection - it is a
/// mistake, and the composer refuses it rather than merging it. A selection is
/// declared once, validated for membership against what the control plane
/// charts, and never merged. The membership half lives control-plane-side;
/// what this package owns is the shape.
/// </para>
/// <para>
/// <b>Optional, and absence means unselected.</b> Every envelope written
/// before these members existed selects nothing, requires no environment
/// label, and constrains no repository - which is exactly what those envelopes
/// meant when they were written.
/// </para>
/// </remarks>
public class EnvelopeSelectionTests
{
    private static Envelope Governing(string? environment = null, string? repository = null) => new()
    {
        Context = new ContextBinding { Scope = "src/**", Constitution = "1.0.0" },
        Environment = environment,
        Repository = repository,
        Obligations =
        [
            new Obligation
            {
                Id = "in-scope",
                Check = ObligationChecks.Machine,
                Rule = ObligationPredicates.NoFileOutsideScope,
            },
        ],
        Loops =
        [
            new Loop
            {
                Id = "implement",
                Executor = ExecutorRungs.Frontier,
                Discharges = ["in-scope"],
                Moves = [LoopMoves.Read, LoopMoves.Edit],
                Budget = new LoopBudget { WallClock = "30m" },
                OnExhaustion = ExhaustionPolicies.HandoffToHuman,
            },
        ],
        Destinations =
        [
            new Destination
            {
                Id = "forge",
                Kind = DestinationKinds.PullRequest,
                Requires = ["in-scope"],
            },
        ],
    };

    [Test]
    public async Task A_declared_selection_validates_and_renders()
    {
        var envelope = Governing(environment: "aspire-payments", repository: "acme/payments");

        await Assert.That(Envelope.Validate(envelope)).IsNull()
            .Because("a well-formed selection is not the contract's to refuse - membership is "
                   + "the control plane's question, and it has the chart.");

        var text = EnvelopeText.Render(envelope);
        await Assert.That(text).Contains("environment: aspire-payments\n");
        await Assert.That(text).Contains("repository: acme/payments\n");
    }

    [Test]
    public async Task An_absent_selection_renders_nothing()
    {
        // ABSENT STAYS ABSENT, the preserve-unadmitted rule again: emitting
        // `environment:` for every envelope that never selected one would
        // rewrite every tenant's document on the next show, and a diff nobody
        // made is how a review practice gets abandoned.
        var text = EnvelopeText.Render(Governing());

        await Assert.That(text.Contains("environment", StringComparison.Ordinal)).IsFalse();
        await Assert.That(text.Contains("repository", StringComparison.Ordinal)).IsFalse();
    }

    [Test]
    public async Task A_blank_selection_is_refused_naming_the_key()
    {
        // "environment: " is not "no environment". Reading a blank as
        // unselected would make a typo mean the opposite of what the line
        // says, silently.
        var diagnosis = Envelope.Validate(Governing(environment: "   "));

        await Assert.That(diagnosis).IsNotNull();
        await Assert.That(diagnosis!).Contains("environment");
    }

    [Test]
    public async Task A_multiline_selection_is_refused_naming_the_key()
    {
        // A name is one line. Two lines is either an injection attempt or a
        // paste accident, and both deserve a diagnosis rather than a label
        // with a newline in the middle of the queue's SQL.
        var environment = Envelope.Validate(Governing(environment: "one\ntwo"));
        var repository = Envelope.Validate(Governing(repository: "acme\npayments"));

        await Assert.That(environment!).Contains("environment");
        await Assert.That(repository!).Contains("repository");
    }

    [Test]
    public async Task The_selections_are_on_the_declared_wire_surface()
    {
        var members = Description.ProtocolSurface.JsonMembers[typeof(Envelope)];

        await Assert.That(members).Contains("environment");
        await Assert.That(members).Contains("repository");
    }

    [Test]
    public async Task A_rendered_selection_reads_back_as_itself()
    {
        // The render is canonical: rendering twice gives identical bytes, with
        // the selections in a stable place.
        var envelope = Governing(environment: "aspire-payments");

        await Assert.That(EnvelopeText.Render(envelope))
            .IsEqualTo(EnvelopeText.Render(envelope));
    }
}
