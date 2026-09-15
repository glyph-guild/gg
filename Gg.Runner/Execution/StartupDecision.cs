namespace Gg.Runner.Execution;

/// <summary>What a runner does at startup, once its agent and its bound are measured.</summary>
public enum StartupOutcome
{
    /// <summary>Take work.</summary>
    Fly,

    /// <summary>Beat, never claim, and say why - until a credential arrives.</summary>
    Hold,

    /// <summary>Exit 69: the machine's governance is unproven and no person on a gate can fix it.</summary>
    Refuse,
}

/// <summary>
/// The startup decision, pure, so the three-way rule is testable without a
/// process.
/// </summary>
/// <remarks>
/// <para>
/// <b>Holding is for the one unmeasured cause a person can fix without
/// visiting the machine.</b> An agent that cannot start for want of a login
/// leaves the move-bound probe unmeasured - the probe reads a run that never
/// happened - and gg#502's rule for an unmeasured bound is exit 69. That rule
/// stands for every other cause: a missing binary, a broken settings file, a
/// bound that BROKE. For a login, exit 69 is an exited container nobody can
/// reach over the channel to give a credential to, so the runner holds instead.
/// </para>
/// <para>
/// <b>The adapter's standing is asked first, and a probe is not run against an
/// agent that said no.</b> It would only measure the absence again, at the
/// cost of a process launch, and then need this same recogniser to read it.
/// </para>
/// </remarks>
public static class StartupDecision
{
    public static StartupOutcome Decide(
        AgentStanding? standing, ProbeResult? probe, IAuthenticateAnAgent? agent)
    {
        if (standing is { Authenticated: false })
        {
            return StartupOutcome.Hold;
        }

        if (probe is null || probe.Bound)
        {
            return StartupOutcome.Fly;
        }

        // UNMEASURED FOR WANT OF A LOGIN, and nothing else. A probe that
        // measured something - a held list, a broken list - measured the
        // bound, and a broken bound is never a hold whatever its diagnosis
        // happens to mention.
        if (agent is not null
            && probe.Held.Count == 0
            && probe.Broke.Count == 0
            && agent.NeedsLogin(probe.Diagnosis))
        {
            return StartupOutcome.Hold;
        }

        return StartupOutcome.Refuse;
    }
}
