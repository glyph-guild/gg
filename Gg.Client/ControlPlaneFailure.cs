namespace Gg.Client;

/// <summary>
/// What a person reads when a request to the control plane did not succeed.
/// </summary>
/// <remarks>Not yet written: both members still pass the runtime's text through.</remarks>
public static class ControlPlaneFailure
{
    /// <summary>The sentence for a request that got no usable answer.</summary>
    public static string Sentence(string? baseAddress, HttpRequestException failure)
    {
        ArgumentNullException.ThrowIfNull(failure);
        return failure.Message;
    }

    /// <summary>An expected failure as a sentence a person can act on.</summary>
    public static string Said(Exception failure)
    {
        ArgumentNullException.ThrowIfNull(failure);
        return failure.Message;
    }
}
