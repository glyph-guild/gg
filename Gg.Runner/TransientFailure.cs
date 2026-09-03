using System.Net;

namespace Gg.Runner;

/// <summary>
/// Whether a failure is the control plane's to fix rather than this machine's,
/// and how long to wait before asking again.
/// </summary>
/// <remarks>
/// <para>
/// <b>One declaration, because two loops ask.</b> <see cref="RunnerLoop"/> claims
/// flights and <c>MaintainLoop</c> pulls pool actions, and both die pointlessly
/// on a deploy without this. The rule lived inside the first one, which is
/// exactly how the second kept crash-looping after the first was fixed - the
/// issue named both loops and only one was changed. A third caller gets the rule
/// by construction rather than by somebody remembering this paragraph.
/// </para>
/// <para>
/// <b>No status at all is transient.</b> A refused connection, a reset, a DNS
/// failure mid-deploy - the request never reached anything that could have an
/// opinion, so there is nothing to conclude about this machine.
/// </para>
/// <para>
/// <b>A 4xx is ours and is fatal.</b> 401 and 403 say this machine's credential is
/// wrong, and no amount of waiting fixes a credential; retrying would be a
/// misconfigured machine hammering a control plane forever, which is worse than
/// stopping where somebody can see it. <b>408 and 429 are the exceptions</b>: a
/// timeout and a rate limit both explicitly mean try again.
/// </para>
/// </remarks>
public static class TransientFailure
{
    /// <summary>The first wait after a refusal.</summary>
    public static readonly TimeSpan FirstRetry = TimeSpan.FromSeconds(2);

    /// <summary>
    /// The longest either loop will ever wait between attempts.
    /// </summary>
    /// <remarks>
    /// Bounded, and not large. A loop that backed off to minutes would be the
    /// outage this prevents wearing a longer timer: the control plane comes back
    /// and nothing asks it for another five minutes.
    /// </remarks>
    public static readonly TimeSpan SlowestRetry = TimeSpan.FromSeconds(30);

    /// <summary>Whether waiting could plausibly help.</summary>
    public static bool IsTransient(HttpRequestException failure)
    {
        ArgumentNullException.ThrowIfNull(failure);

        return failure.StatusCode switch
        {
            null => true,
            HttpStatusCode.RequestTimeout or HttpStatusCode.TooManyRequests => true,
            var status => (int)status >= 500,
        };
    }

    /// <summary>The next wait: doubling from <see cref="FirstRetry"/>, capped.</summary>
    public static TimeSpan Next(TimeSpan current) =>
        current == TimeSpan.Zero
            ? FirstRetry
            : current + current > SlowestRetry ? SlowestRetry : current + current;

    /// <summary>What to say, when there is somebody to say it to.</summary>
    /// <remarks>
    /// Composed here so both loops report a refusal the same way. Naming the
    /// status when there is one and the reach when there is not is the whole
    /// difference between "the control plane is unwell" and "this machine cannot
    /// see it".
    /// </remarks>
    public static string Diagnose(HttpRequestException failure, TimeSpan retryIn)
    {
        ArgumentNullException.ThrowIfNull(failure);

        return failure.StatusCode is { } status
            ? $"the control plane answered {(int)status}; asking again in {retryIn.TotalSeconds:0}s"
            : $"the control plane could not be reached ({failure.Message}); asking again in "
            + $"{retryIn.TotalSeconds:0}s";
    }
}
