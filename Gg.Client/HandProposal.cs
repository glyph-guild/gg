using System.Text;
using Gg.Contracts;

namespace Gg.Client;

/// <summary>What the agent is asked to look at, and what it may do while looking.</summary>
/// <remarks>
/// <para>
/// <b>This is not a loop.</b> A loop discharges an obligation, and summarising
/// discharges nothing - modelling it as one would bend the envelope to fit a
/// thing the envelope is not about. So the bounds are here, in code, rather than
/// in a tenant's rules.
/// </para>
/// <para>
/// <b>Hardcoded, and that is a declared gap.</b> One fixed task with one fixed
/// shape can honestly carry its limits in the binary. The trigger for changing
/// that is written down: <b>the second utility invocation of the executor</b> is
/// when these need declaring somewhere rather than compiling in, because two
/// tasks with two shapes is a policy and one is a constant.
/// </para>
/// </remarks>
public static class HandBounds
{
    /// <summary>
    /// What the inference may do. Reading, and nothing else.
    /// </summary>
    /// <remarks>
    /// An agent reading a customer's repository with no limits is the thing this
    /// whole design exists to prevent. Outside the envelope must not mean
    /// unbounded.
    /// </remarks>
    public static IReadOnlyList<string> Moves { get; } = [LoopMoves.Read, LoopMoves.Search];

    /// <summary>
    /// How long it may take.
    /// </summary>
    /// <remarks>
    /// Two minutes. It is reading a diff and a paragraph, not doing the work; an
    /// inference that needs longer has misunderstood the task, and a person is
    /// standing at the terminal waiting for it.
    /// </remarks>
    public static readonly TimeSpan WallClock = TimeSpan.FromMinutes(2);
}

/// <summary>What the agent proposed, before anybody has agreed to it.</summary>
/// <remarks>
/// <b>A guess, and typed as one.</b> An agent's account of its OWN work is the
/// agent's claim about itself. This is the agent's claim about somebody else's
/// work, which is weaker again - and it becomes a person's assertion only when
/// they confirm it. Keeping it in its own type is what stops a proposal being
/// stored where an account belongs.
/// </remarks>
public sealed record ProposedAccount
{
    /// <summary>What the agent thinks the person did and decided.</summary>
    public required string Proposal { get; init; }

    /// <summary>Why there is no proposal, when there is none.</summary>
    /// <remarks>
    /// A failed inference is not a failed hand-back. The person still gets to
    /// write their own account - the confirmation step just starts from nothing
    /// instead of from a guess, and says which.
    /// </remarks>
    public string? Absence { get; init; }

    public bool Present => Proposal.Length > 0;
}

/// <summary>
/// Composes what the agent is asked, from material that already exists.
/// </summary>
/// <remarks>
/// <para>
/// <b>Nobody writes the summary.</b> A design that asks for one has already
/// failed: a person who has just worked for two hours will write "fixed it", and
/// the record will be worthless. So the agent reads what is there - the diff
/// since it stopped, its own prior account, and the measurements - and proposes
/// what appears to have been done.
/// </para>
/// <para>
/// <b>The prior account is the part that makes this worth building.</b> A
/// proposal built from the diff alone describes a diff, which is something git
/// already does. A proposal that can say "you addressed the float issue I
/// flagged, by rounding at the boundary" connects the two halves of the handoff
/// in a sentence neither party wrote.
/// </para>
/// <para>
/// <b>The diff does not cross.</b> The inference runs where the tree is, and
/// only the confirmed account travels.
/// </para>
/// </remarks>
public static class HandPrompt
{
    /// <summary>What the agent is asked, for one flight.</summary>
    /// <param name="flightNumber">What a person types.</param>
    /// <param name="priorAccount">What the agent said when it stopped, if anything.</param>
    /// <param name="measurements">What we counted about the agent's own run.</param>
    public static string Compose(
        string flightNumber, string? priorAccount, TakeMeasurements measurements)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(flightNumber);
        ArgumentNullException.ThrowIfNull(measurements);

        var text = new StringBuilder();

        text.AppendLine(
            "A person took over this flight after the agent stopped, and has been working in this "
          + "tree. Read `git diff` and `git status` to see what they changed.");
        text.AppendLine();
        text.AppendLine(
            "Write 3-6 sentences describing WHAT THEY APPEAR TO HAVE DONE AND DECIDED. Address it "
          + "to them as \"you\". Say what changed and, where you can tell, why - a decision they "
          + "made that is visible in the code is worth more than a list of files.");
        text.AppendLine();
        text.AppendLine(
            "Do not modify anything. Do not run tests. Do not write files. Read only.");
        text.AppendLine();

        if (priorAccount is { Length: > 0 })
        {
            // THE PART THAT EARNS THIS FEATURE. Connecting their work to what
            // the agent was worried about is the sentence neither party wrote.
            text.AppendLine("When the agent stopped, it said this about its own work:");
            text.AppendLine();
            text.AppendLine(Indent(priorAccount));
            text.AppendLine();
            text.AppendLine(
                "If their changes address something that account raised, SAY SO EXPLICITLY and say "
              + "how they addressed it. If they did something it did not anticipate, say that "
              + "instead. Do not invent a connection that is not in the diff.");
            text.AppendLine();
        }
        else
        {
            text.AppendLine(
                "The agent left no account of its own work, so there is nothing to connect theirs "
              + "to. Describe what they did without speculating about why the agent stopped.");
            text.AppendLine();
        }

        text.AppendLine($"For context, {flightNumber} stopped as '{measurements.StopReason}' after "
                      + $"{measurements.Attempts} turn(s).");

        if (measurements.FilesEdited.Count > 0)
        {
            text.AppendLine(
                "The agent itself had changed: " + string.Join(", ", measurements.FilesEdited)
              + ". Anything beyond that is the person's work.");
        }

        text.AppendLine();
        text.AppendLine(
            "Reply with the description and nothing else - no preamble, no headings, no offer to "
          + "help further. It is going to be shown to them for correction.");

        return text.ToString();
    }

    private static string Indent(string account) =>
        string.Join('\n', account.Split('\n').Select(l => "  " + l));
}
