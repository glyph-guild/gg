using Gg.Contracts;

namespace Gg.Runner.Exposures;

/// <summary>
/// Whether a loop that was supposed to serve a preview actually did.
/// </summary>
/// <remarks>
/// <para>
/// <b>Because the platform believed the loop, twice.</b> GG-522 served its
/// change and had the instance emptied a second later; GG-524 never started its
/// container, having spent its budget waiting on a background build. Both
/// reported <c>completed</c>, and gg went on to publish an address, push a
/// branch, open a human gate and hold the machine out of service for twelve
/// hours — for an address returning 502. Nothing between "the agent stopped" and
/// "a person is asked to look" checked that there was anything to look at.
/// </para>
/// <para>
/// <b>Not a wording problem.</b> GG-522's agent followed its instructions
/// exactly, including verifying the page itself; what destroyed the preview
/// happened afterwards and elsewhere. An instruction to check before finishing
/// would have passed there and changed nothing.
/// </para>
/// <para>
/// <b>The kind's own declaration is the condition.</b> A work kind that wants a
/// preview already says <c>preview.url</c> in <c>produces:</c>; one that does
/// not says nothing, and silence is not a claim to anything. So every kind in
/// the field is untouched by construction rather than by an exemption list.
/// </para>
/// <para>
/// <b>And it is the LOOP's verdict that changes, not the flight's fate.</b> What
/// this produces is a sentence for the outcome a reader sees. A flight whose
/// preview never came up has still done work worth keeping — the branch is
/// pushed, the tree is held — and the thing that must not happen is the quiet
/// claim that somebody can go and look at it.
/// </para>
/// </remarks>
public static class PreviewAnswers
{
    /// <summary>
    /// How long the probe waits before calling the origin unanswered.
    /// </summary>
    /// <remarks>
    /// Short, because this runs once at the end of a loop against a server on
    /// this same machine. A preview a person is about to open and that takes
    /// longer than this to say anything is one they would give up on too.
    /// </remarks>
    public static readonly TimeSpan Patience = TimeSpan.FromSeconds(5);

    /// <summary>
    /// The real probe: whether anything is listening at an origin on this
    /// machine.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>ANY HTTP ANSWER COUNTS, including a 500.</b> What is being measured is
    /// whether a server is there, and a preview serving an error page is a
    /// preview somebody can open and report on. Refusing a loop over the status
    /// code would make gg the judge of the work rather than of whether the work
    /// can be seen.
    /// </para>
    /// <para>
    /// <b>The first HTTP probe in this project</b>, and deliberately the whole
    /// of one: the nearest thing before it was a TCP connect with 443 hard-coded.
    /// </para>
    /// </remarks>
    public static Func<string, CancellationToken, Task<string?>> OfThisMachine() =>
        async (origin, cancellationToken) =>
        {
            using var client = new HttpClient { Timeout = Patience };

            try
            {
                using var answer = await client.GetAsync(
                    origin, HttpCompletionOption.ResponseHeadersRead, cancellationToken);

                return null;
            }
            catch (Exception failure) when (failure is HttpRequestException or TaskCanceledException
                                            or UriFormatException or InvalidOperationException)
            {
                return failure.Message;
            }
        };

    /// <summary>
    /// Why this loop may not claim success, or null when it may.
    /// </summary>
    /// <param name="produces">
    /// What the kind declared it produces. Null and empty are both silence.
    /// </param>
    /// <param name="origin">
    /// The origin the connector dials — <see cref="ExposureServed.Origin"/>.
    /// Null when no address was served, which is not this loop's failure.
    /// </param>
    /// <param name="reach">
    /// Null when the origin answers, otherwise why not. The shape
    /// <c>ProfileReadiness</c> uses, so the probe is injected and this is
    /// testable without a socket.
    /// </param>
    /// <remarks>
    /// <b>A probe that cannot reach is the answer, not an error.</b> Whatever the
    /// reason — refused, timed out, a container that exited — the consequence for
    /// the person about to open the address is identical, and the probe's own
    /// words say more than any classification here could.
    /// </remarks>
    public static async Task<string?> RefusalAsync(
        IReadOnlyList<string>? produces,
        string? origin,
        Func<string, CancellationToken, Task<string?>> reach,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(reach);

        if (produces is null
            || !produces.Contains(FactKinds.PreviewUrl, StringComparer.Ordinal))
        {
            return null;
        }

        // NO ADDRESS IS NOT A FAILED LOOP. A slot that could not be brought up is
        // narrated by PreviewUnserved before the agent ever ran, and
        // ExposureServed's own remark settles the principle: a preview that
        // cannot be served is a flight that still did its work. Refusing here
        // would fail a loop for something that happened before it started.
        if (string.IsNullOrWhiteSpace(origin))
        {
            return null;
        }

        if (await reach(origin, cancellationToken) is not { Length: > 0 } why)
        {
            return null;
        }

        return $"this kind serves a preview, and nothing answers at {origin}: {why}. The loop "
             + "changed what it was asked to change, and what it did not do is the one thing "
             + "somebody was about to be asked to look at - so this is not completed. The branch "
             + "is pushed and the tree is held; what is missing is a server on that address.";
    }
}
