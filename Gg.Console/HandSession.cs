using Gg.Client;
using Gg.Contracts;

namespace Gg.Console;

/// <summary>What a hand-back needs to run.</summary>
public sealed record HandRequest
{
    public required string FlightId { get; init; }

    public required string FlightNumber { get; init; }

    /// <summary>The tree the person worked in. The inference runs here.</summary>
    public required string TreePath { get; init; }

    /// <summary>Who is asserting the account. The session's principal, never typed in.</summary>
    public required string By { get; init; }

    /// <summary>What the agent said when it stopped, if anything.</summary>
    public string? PriorAccount { get; init; }

    public required TakeMeasurements Measurements { get; init; }
}

/// <summary>
/// Runs the inference and takes a person's confirmation.
/// </summary>
/// <remarks>
/// A port because the inference spawns an agent and the confirmation reads a
/// terminal, and neither is something a test of the trust boundary should have
/// to do.
/// </remarks>
public interface IHandSession
{
    HandOutcome Hand(HandRequest request);
}

/// <summary>
/// Asks an agent what appears to have been done, then asks the person.
/// </summary>
/// <remarks>
/// <para>
/// <b>It always ends in a confirmation, and never in an empty box.</b> A person
/// who has just worked for two hours writes "fixed it"; the whole design is that
/// they get something to correct instead. When the inference fails they are told
/// so and asked to write their own - which is the fallback, not the primary path,
/// and it says which.
/// </para>
/// <para>
/// <b>Nothing is recorded until they answer.</b> Closing the terminal leaves no
/// account, because an unconfirmed proposal stored as somebody's words is
/// attributing a guess to them under Article XII.
/// </para>
/// </remarks>
public sealed class HandSession(
    Func<HandRequest, string, string?> infer,
    Func<string?, HandChoice> ask,
    TimeProvider? time = null) : IHandSession
{
    private readonly TimeProvider _time = time ?? TimeProvider.System;

    public HandOutcome Hand(HandRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        var prompt = HandPrompt.Compose(
            request.FlightNumber, request.PriorAccount, request.Measurements);

        ProposedAccount proposed;
        try
        {
            proposed = infer(request, prompt) is { Length: > 0 } text
                ? new ProposedAccount { Proposal = text.Trim() }
                : new ProposedAccount
                {
                    Proposal = "",
                    Absence = "the agent produced nothing",
                };
        }
        catch (Exception failure)
        {
            // A failed inference is not a failed hand-back. The person still
            // gets to record what they did; they just start from nothing, and
            // are told that is why.
            proposed = new ProposedAccount
            {
                Proposal = "",
                Absence = $"the agent could not be run ({failure.GetType().Name})",
            };
        }

        return HandConfirmation.Confirm(
            request.By,
            proposed,
            ask(proposed.Present ? proposed.Proposal : null),
            _time.GetUtcNow());
    }
}
