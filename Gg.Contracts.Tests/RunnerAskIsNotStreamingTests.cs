using System.Collections;
using System.Reflection;
using Gg.Contracts;

namespace Gg.Contracts.Tests;

/// <summary>
/// A request and a bounded response, so a second transport can carry it whole.
/// </summary>
/// <remarks>
/// <para>
/// <b>ADR-0013's first constraint on Decision 3, asserted.</b> "Designing it
/// around streaming would mean a second transport later cannot carry it, and
/// adding the fallback becomes a rewrite rather than a registration." The
/// dead-drop is store-and-forward: it can move one answer, once. A continuation
/// token or a callback on this surface would quietly make it WebRTC-only.
/// </para>
/// <para>
/// <b>A shape test rather than a reading.</b> The property this protects is
/// about every future member as much as today's, and "we remembered" is not a
/// mechanism.
/// </para>
/// </remarks>
public class RunnerAskIsNotStreamingTests
{
    private static readonly Type[] Surface =
    [
        typeof(RunnerAsk), typeof(TailLogAsk), typeof(StatusAsk),
        typeof(RunnerSaid), typeof(LogTail), typeof(RunnerStatusReport),
    ];

    [Test]
    public async Task Nothing_on_the_surface_is_an_asynchronous_sequence()
    {
        var streaming = Surface
            .SelectMany(t => t.GetProperties().Select(p => (Type: t, Property: p)))
            .Where(x => x.Property.PropertyType.FullName?.Contains(
                "IAsyncEnumerable", StringComparison.Ordinal) == true)
            .Select(x => $"{x.Type.Name}.{x.Property.Name}")
            .ToList();

        await Assert.That(streaming).IsEmpty()
            .Because("a store-and-forward transport cannot carry a sequence that arrives "
                   + "over time. Found: " + string.Join(", ", streaming));
    }

    [Test]
    public async Task Nothing_on_the_surface_is_a_callback()
    {
        var callbacks = Surface
            .SelectMany(t => t.GetProperties().Select(p => (Type: t, Property: p)))
            .Where(x => typeof(Delegate).IsAssignableFrom(x.Property.PropertyType))
            .Select(x => $"{x.Type.Name}.{x.Property.Name}")
            .ToList();

        await Assert.That(callbacks).IsEmpty()
            .Because("a delegate cannot be serialized, so a member holding one would be a "
                   + "member only a live connection could carry. Found: "
                   + string.Join(", ", callbacks));
    }

    [Test]
    public async Task Nothing_on_the_surface_is_a_continuation_token()
    {
        // BY NAME, because a continuation token is a string and no type test
        // finds it. The names are the ones this codebase and every HTTP API
        // reach for; a fifth spelling arriving is a conversation worth having
        // rather than something to catch by regex.
        string[] paging = ["cursor", "continuation", "continuationtoken", "pagetoken",
                           "nexttoken", "nextpage", "offset", "skip", "after"];

        var paged = Surface
            .SelectMany(t => t.GetProperties().Select(p => (Type: t, Property: p)))
            .Where(x => paging.Contains(x.Property.Name.ToLowerInvariant(), StringComparer.Ordinal))
            .Select(x => $"{x.Type.Name}.{x.Property.Name}")
            .ToList();

        await Assert.That(paged).IsEmpty()
            .Because("paging is streaming with extra steps: the second page needs the peer "
                   + "still there, which is the assumption the dead-drop cannot make. "
                   + "Found: " + string.Join(", ", paged));
    }

    [Test]
    public async Task The_scan_can_actually_fail()
    {
        // The poison twin. Every assertion above passes on a surface the scan
        // never reached, and a wrong type array is exactly how that happens.
        await Assert.That(Surface).IsNotEmpty();
        await Assert.That(Surface.SelectMany(t => t.GetProperties())).IsNotEmpty()
            .Because("a scan over types with no properties asserts nothing at all.");

        // And it finds what it is looking for when it is really there.
        var pageable = typeof(Paged).GetProperties()
            .Where(p => string.Equals(p.Name, "Cursor", StringComparison.Ordinal))
            .ToList();
        await Assert.That(pageable).IsNotEmpty();
    }

    private sealed record Paged
    {
        public string? Cursor { get; init; }
    }
}
