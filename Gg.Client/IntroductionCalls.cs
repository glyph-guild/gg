using Gg.Contracts;

namespace Gg.Client;

/// <summary>Why a person may not be introduced to a runner.</summary>
/// <remarks>
/// <b>Three refusals rather than one, because they send somebody three
/// places.</b> A runner that is not there is a typo or a retirement; one
/// somebody else registered is a conversation with that person; one that
/// registered before keys existed is a machine to restart. A single "cannot
/// introduce" would send them through all three in turn, starting with the
/// wrong one.
/// </remarks>
public enum IntroductionRefusal
{
    /// <summary>It worked.</summary>
    None,

    /// <summary>No runner by that id, in this tenant.</summary>
    NoSuchRunner,

    /// <summary>A runner in this tenant that this person did not register.</summary>
    NotYoursToReach,

    /// <summary>A runner that registered before it could offer a key.</summary>
    RegisteredBeforeKeys,
}

/// <summary>What asking to be introduced produced.</summary>
public sealed record Introduced(
    RunnerIntroduction? Introduction, IntroductionRefusal Refusal, string Said);

/// <summary>Whether an answer is here yet, not here yet, or never coming.</summary>
/// <remarks>
/// <b>The distinction this type exists for is <see cref="NotYet"/> against
/// <see cref="Gone"/>.</b> The route answers 204 while there is no answer and
/// 404 for an introduction that never existed or has expired, and the contract
/// says why: a console polling has to tell WAITING from WRONG. A nullable answer
/// would collapse them, and a caller that cannot tell them apart waits out its
/// whole patience on a conversation that ended - then reports the runner as
/// silent, which points at the wrong machine.
/// </remarks>
public enum AnswerState
{
    /// <summary>The runner has not answered yet. Keep asking.</summary>
    NotYet,

    /// <summary>Here it is.</summary>
    Arrived,

    /// <summary>No such introduction. It expired, or it never was.</summary>
    Gone,
}

/// <summary>What one collection found.</summary>
public sealed record Collected(RunnerSealedAnswer? Answer, AnswerState State)
{
    /// <summary>Nothing yet.</summary>
    public static readonly Collected NotYet = new(null, AnswerState.NotYet);

    /// <summary>Nothing ever.</summary>
    public static readonly Collected Gone = new(null, AnswerState.Gone);
}
