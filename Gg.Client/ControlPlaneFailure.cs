namespace Gg.Client;

/// <summary>
/// What a person reads when a request to the control plane did not succeed.
/// </summary>
/// <remarks>
/// <b>The exception's own text is never shown.</b> An <see cref="HttpRequestException"/>
/// carries the runtime's wording - socket errors, status phrases - which tells
/// a person neither what happened nor what to do. Every other expected failure
/// here is one gg or the control plane already worded for a person, so it is
/// passed through unchanged.
/// </remarks>
public static class ControlPlaneFailure
{
    /// <summary>The sentence for a request that got no usable answer.</summary>
    /// <param name="baseAddress">Where the request went, when the caller knows it.</param>
    /// <param name="failure">What the request threw.</param>
    public static string Sentence(string? baseAddress, HttpRequestException failure)
    {
        ArgumentNullException.ThrowIfNull(failure);

        var at = baseAddress is { Length: > 0 } address ? $" at {address}" : "";

        return failure.StatusCode is { } status
            ? $"The control plane{at} answered with an error (HTTP {(int)status}). "
            + "Run the command again; if this keeps happening, run gg bundle and send the result "
            + "to the Good Grief team."
            : $"Could not reach the control plane{at}. Run gg doctor to check the connection.";
    }

    /// <summary>An expected failure as a sentence a person can act on.</summary>
    public static string Said(Exception failure)
    {
        ArgumentNullException.ThrowIfNull(failure);

        return failure is HttpRequestException unanswered
            ? Sentence(null, unanswered)
            : failure.Message;
    }
}
