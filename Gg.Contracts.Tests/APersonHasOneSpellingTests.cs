using Gg.Contracts;

namespace Gg.Contracts.Tests;

/// <summary>
/// A document names a person one way, and anything that claims to be a person
/// and is not well-formed is refused with what was wrong.
/// </summary>
/// <remarks>
/// <para>
/// <b>S42.1-01.</b> A watch's <c>for:</c> and an obligation's <c>approver:</c>
/// both name a person. They share one parser because two spellings of one
/// person drift, and the day they do, a gate names somebody the watch does not.
/// </para>
/// <para>
/// <b>A colon is the claim.</b> A value with a provider prefix says it is a
/// person and has to parse as one; a bare value is a role, as
/// <c>platform-owner</c> is.
/// </para>
/// <para>
/// <b>Every provider here is invented, and that is enforced.</b>
/// <c>NoSourceFileNamesAnIdentityProvider</c> refuses a real one anywhere in
/// this repository. So this parser checks only the syntax, and which providers
/// exist and what their subjects look like are the control plane's to know when
/// it resolves the person.
/// </para>
/// </remarks>
public class APersonHasOneSpellingTests
{
    /// <summary>A well-formed person with a directory-shaped subject.</summary>
    internal const string APerson =
        "a-directory:72f988bf-86f1-41af-91ab-2d7cd011db47/0b1c2d3e-4f50-6172-8394-a5b6c7d8e9f0";

    [Test]
    [Arguments(APerson)]
    [Arguments("a-forge:583231")]
    public async Task A_provider_and_a_subject_is_a_person(string value)
    {
        await Assert.That(PersonSpelling.IsPerson(value)).IsTrue();
        await Assert.That(PersonSpelling.Diagnose(value)).IsNull()
            .Because("a provider, then the subject that provider issued - the pair the "
                   + "control plane keys a principal on.");
    }

    [Test]
    [Arguments("platform-owner")]
    [Arguments("tenant")]
    [Arguments("kdeenanauth")]
    public async Task A_bare_value_is_a_role_and_not_a_person(string value)
    {
        await Assert.That(PersonSpelling.IsPerson(value)).IsFalse()
            .Because("roles have always been bare words, and a role never contains a "
                   + "provider prefix - the spelling is its own discriminator.");
    }

    [Test]
    [Arguments("a-directory:")]
    [Arguments(":583231")]
    [Arguments("a-directory: 583231")]
    [Arguments("A Directory:583231")]
    [Arguments("a-directory:5832\n31")]
    public async Task A_person_that_is_not_well_formed_is_refused(string value)
    {
        await Assert.That(PersonSpelling.IsPerson(value)).IsTrue()
            .Because("it has a colon, so it claims to be a person and is held to that.");
        await Assert.That(PersonSpelling.Diagnose(value)).IsNotNull();
    }

    [Test]
    public async Task The_refusal_quotes_what_was_written()
    {
        var refused = PersonSpelling.Diagnose("a-directory:");

        await Assert.That(refused).IsNotNull();
        await Assert.That(refused!).Contains("a-directory:")
            .Because("the author has to find the line, and 'invalid person' finds nothing.");
    }

    [Test]
    public async Task A_human_obligation_naming_a_malformed_person_is_refused()
    {
        // THE SAME PARSER, reached from the envelope, so an approver and a
        // watch's person cannot disagree about what a person is.
        var refused = Envelope.Validate(AnEnvelopeWith("a-directory:"), Roles.Root);

        await Assert.That(refused).IsNotNull();
        await Assert.That(refused!).Contains("reviewed")
            .Because("naming the obligation is how somebody finds the line.");
    }

    [Test]
    [Arguments("platform-owner")]
    [Arguments(APerson)]
    public async Task A_human_obligation_names_a_role_or_a_well_formed_person(string approver)
    {
        await Assert.That(Envelope.Validate(AnEnvelopeWith(approver), Roles.Root)).IsNull();
    }

    private static Envelope AnEnvelopeWith(string approver) => new()
    {
        Context = new ContextBinding { Scope = "src/**", Constitution = "1.0.0" },
        Obligations =
        [
            new Obligation { Id = "reviewed", Check = ObligationChecks.Human, Approver = approver },
        ],
        Loops =
        [
            new Loop
            {
                Id = "work",
                Executor = ExecutorRungs.Frontier,
                Discharges = [],
                Moves = [LoopMoves.Read, LoopMoves.Edit],
                Budget = new LoopBudget { WallClock = "30m" },
                OnExhaustion = ExhaustionPolicies.HandoffToHuman,
            },
        ],
        Destinations =
        [
            new Destination { Id = "forge", Kind = DestinationKinds.PullRequest, Requires = ["reviewed"] },
        ],
    };
}
