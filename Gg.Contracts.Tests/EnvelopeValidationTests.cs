using Gg.Contracts;

namespace Gg.Contracts.Tests;

/// <summary>
/// What an envelope's instructions may not be.
/// </summary>
/// <remarks>
/// <para>
/// <b>S30.1-03 and -04. Refused at apply, never truncated at use.</b> An agent
/// reading half a policy is worse than an author being told to shorten one: the
/// half it read looks complete, and nothing downstream can tell that a sentence
/// was cut. So the bound is a validation failure on the document, at the moment
/// somebody applies it, while they can still edit it.
/// </para>
/// <para>
/// <b>And the refusal names both numbers</b> — what was written and what is
/// allowed. "Too long" without them is a message that sends an author to count
/// characters by hand.
/// </para>
/// </remarks>
public class EnvelopeValidationTests
{
    private static Envelope With(params string[] instructions) => new()
    {
        Context = new ContextBinding { Scope = "src/**", Constitution = "1.0.0" },
        Obligations = [],
        Loops =
        [
            new Loop
            {
                Id = "implement",
                Executor = ExecutorRungs.Frontier,
                Discharges = [],
                Moves = [LoopMoves.Read],
                Budget = new LoopBudget { WallClock = "30m" },
                OnExhaustion = ExhaustionPolicies.HandoffToHuman,
            },
        ],
        Destinations = [],
        Instructions = [.. instructions.Select(text => new EnvelopeInstruction { Text = text })],
    };

    [Test]
    public async Task An_envelope_with_no_instructions_is_valid()
    {
        // THE ANCHOR. Every envelope that exists today has none, and this
        // feature must not refuse a single one of them.
        await Assert.That(Envelope.Validate(With())).IsNull();
    }

    [Test]
    public async Task An_ordinary_instruction_is_valid()
    {
        await Assert.That(Envelope.Validate(With("reproduce a bug in a test first"))).IsNull();
    }

    [Test]
    public async Task An_empty_block_is_refused()
    {
        // A block that parses and says nothing is a policy somebody believes
        // they wrote.
        await Assert.That(Envelope.Validate(With(""))).IsNotNull();
    }

    [Test]
    public async Task A_whitespace_block_is_refused()
    {
        await Assert.That(Envelope.Validate(With("   \t  "))).IsNotNull();
    }

    [Test]
    public async Task The_total_is_bounded_across_blocks_rather_than_per_block()
    {
        // THE BOUND IS ON WHAT THE AGENT READS, which is the concatenation.
        // Bounding each block would let a document carry fifty short ones and
        // land the same wall of text in the prompt.
        var justUnder = new string('a', Envelope.InstructionsBound - 10);

        await Assert.That(Envelope.Validate(With(justUnder))).IsNull();
        await Assert.That(Envelope.Validate(With(justUnder, justUnder))).IsNotNull()
            .Because("two blocks that each fit still exceed the total the agent reads.");
    }

    [Test]
    public async Task The_refusal_names_what_was_written_and_what_is_allowed()
    {
        var over = new string('a', Envelope.InstructionsBound + 1);

        var refused = Envelope.Validate(With(over));

        await Assert.That(refused).IsNotNull();
        await Assert.That(refused!).Contains(
            (Envelope.InstructionsBound + 1).ToString(null as IFormatProvider));
        await Assert.That(refused).Contains(
            Envelope.InstructionsBound.ToString(null as IFormatProvider))
            .Because("'too long' without both numbers sends an author to count by hand.");
    }

    [Test]
    public async Task Nothing_is_truncated()
    {
        // The other half of "refused at apply": an over-long document does not
        // compose to a shortened one. It does not compose.
        var over = new string('a', Envelope.InstructionsBound + 1);
        var envelope = With(over);

        await Assert.That(Envelope.Validate(envelope)).IsNotNull();
        await Assert.That(envelope.Instructions[0].Text.Length)
            .IsEqualTo(Envelope.InstructionsBound + 1)
            .Because("validation reports; it never edits the document it was handed.");
    }
}
