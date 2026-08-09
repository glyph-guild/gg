namespace Gg.Client;

/// <summary>
/// Time enters through this seam so polling and expiry are testable without
/// sleeping. The control-plane repo has its own; the two do not share code.
/// </summary>
public interface IClock
{
    DateTimeOffset UtcNow { get; }
}

/// <summary>The only place this binary reads the wall clock.</summary>
public sealed class SystemClock : IClock
{
    public DateTimeOffset UtcNow => DateTimeOffset.UtcNow;
}
