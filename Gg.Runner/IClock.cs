namespace Gg.Runner;

/// <summary>
/// Time, injected.
/// </summary>
/// <remarks>
/// The runner's own seam, so this assembly depends on nothing but the wire
/// contract. That narrowness is the point: a runner that cannot reach the
/// developer client cannot accidentally hold a developer's session, and the
/// dependency graph says so rather than a comment.
/// </remarks>
public interface IClock
{
    DateTimeOffset UtcNow { get; }
}

/// <inheritdoc />
public sealed class SystemClock : IClock
{
    public DateTimeOffset UtcNow => DateTimeOffset.UtcNow;
}
