using System.Reflection;
using System.Text.Json;

namespace Gg.Console.Tests;

/// <summary>
/// The state survives being serialized, because the UI is destroyed and
/// rebuilt from it.
/// </summary>
/// <remarks>
/// <para>
/// This was tested against three fields and a hand-written example. The state
/// now carries four panes' worth, and a hand-written example covers exactly
/// the fields the author remembered - so the round trip is a PROPERTY over
/// generated states, and a structural check walks the type graph for anything
/// that could not survive the trip in the first place.
/// </para>
/// <para>
/// Terminal release is what depends on this. It is an architectural
/// constraint, not a style choice: a handle in the store is a session that
/// cannot be torn down.
/// </para>
/// </remarks>
public class AppStateJsonTests
{
    /// <summary>Enough draws to hit every enum and both sides of every optional.</summary>
    private const int Draws = 400;

    [Test]
    public async Task Any_state_round_trips_through_json_unchanged()
    {
        for (var seed = 0; seed < Draws; seed++)
        {
            var state = StateGenerator.Next(new Random(seed));

            var json = AppStateJson.Serialize(state);
            var back = AppStateJson.Deserialize(json);

            // Compared as documents rather than field by field. A field-by-field
            // comparison is another list of things somebody remembered, and it
            // would pass for a property that was dropped by both the writer and
            // the reader.
            await Assert.That(AppStateJson.Serialize(back)).IsEqualTo(json)
                .Because($"seed {seed} did not survive the round trip.");
        }
    }

    [Test]
    public async Task The_generator_actually_varies_what_it_produces()
    {
        // Guards every property in this file. A generator returning the same
        // value 400 times satisfies all of them and proves nothing.
        var distinct = Enumerable.Range(0, Draws)
            .Select(seed => AppStateJson.Serialize(StateGenerator.Next(new Random(seed))))
            .Distinct()
            .Count();

        await Assert.That(distinct).IsGreaterThan(Draws / 2)
            .Because("states that are all alike make every property here vacuous.");
    }

    [Test]
    public async Task Every_mode_and_every_pane_appears_among_the_generated_states()
    {
        // The other half of the guard: variety is not coverage. A generator
        // that never produced a modal would leave the modal round trip
        // untested while looking thorough.
        var states = Enumerable.Range(0, Draws).Select(seed => StateGenerator.Next(new Random(seed))).ToList();

        foreach (var mode in Enum.GetValues<UiMode>())
        {
            await Assert.That(states.Any(s => s.Mode == mode)).IsTrue().Because($"no state had mode {mode}.");
        }
        foreach (var pane in Enum.GetValues<TabId>())
        {
            await Assert.That(states.Any(s => s.ActiveTab == pane)).IsTrue()
                .Because($"no state focused {pane}.");
        }
    }

    [Test]
    public async Task Nothing_in_the_store_could_hold_a_handle()
    {
        // The structural half, and the one that keeps terminal release
        // possible. Walked over the whole type graph rather than the top level:
        // a disposable buried three records down is exactly as fatal and much
        // easier to add by accident.
        var offenders = new List<string>();
        Walk(typeof(AppState), [], offenders);

        await Assert.That(offenders).IsEmpty()
            .Because("every non-serializable handle lives on a controller outside the store. Found: "
                   + string.Join(", ", offenders));
    }

    private static void Walk(Type type, HashSet<Type> seen, List<string> offenders, string path = "AppState")
    {
        if (!seen.Add(type))
        {
            return;
        }

        foreach (var property in type.GetProperties(BindingFlags.Public | BindingFlags.Instance))
        {
            var member = $"{path}.{property.Name}";
            var propertyType = Nullable.GetUnderlyingType(property.PropertyType) ?? property.PropertyType;

            // Collections are containers; what matters is what is inside them.
            if (propertyType != typeof(string)
                && propertyType.IsGenericType
                && propertyType.GetGenericArguments().Length == 1
                && typeof(System.Collections.IEnumerable).IsAssignableFrom(propertyType))
            {
                propertyType = propertyType.GetGenericArguments()[0];
            }

            if (Carries(propertyType))
            {
                offenders.Add($"{member} ({propertyType.Name})");
                continue;
            }

            if (!IsPlainData(propertyType))
            {
                Walk(propertyType, seen, offenders, member);
            }
        }
    }

    /// <summary>Types that are, or could hold, something the UI must let go of.</summary>
    private static bool Carries(Type type) =>
        typeof(IDisposable).IsAssignableFrom(type)
        || typeof(IAsyncDisposable).IsAssignableFrom(type)
        || typeof(Delegate).IsAssignableFrom(type)
        || typeof(Task).IsAssignableFrom(type)
        || typeof(System.Threading.CancellationTokenSource).IsAssignableFrom(type)
        || type == typeof(System.Threading.CancellationToken)
        // An interface is a place a handle can hide behind a shape that looks
        // like data.
        || type.IsInterface && !typeof(System.Collections.IEnumerable).IsAssignableFrom(type);

    private static bool IsPlainData(Type type) =>
        type.IsPrimitive
        || type.IsEnum
        || type == typeof(string)
        || type == typeof(decimal)
        || type == typeof(DateTimeOffset)
        || type == typeof(DateTime)
        || type == typeof(TimeSpan)
        || type == typeof(Guid)
        || type == typeof(object);

    [Test]
    public async Task The_structural_check_can_actually_see_a_handle()
    {
        // Poison twin. "No handles found" is also what a walker that inspects
        // nothing returns, and this file would look diligent either way.
        var offenders = new List<string>();
        Walk(typeof(StoreWithAHandle), [], offenders, nameof(StoreWithAHandle));

        await Assert.That(offenders).IsNotEmpty()
            .Because("if the walker cannot see this, it cannot see anything.");
    }

    /// <summary>A store somebody put a handle in, so the check above has something to catch.</summary>
    private sealed record StoreWithAHandle
    {
        public Stream? Output { get; init; }

        public IReadOnlyList<StreamLine> Live { get; init; } = [];
    }

    [Test]
    public async Task Enums_travel_as_names_so_a_reordering_cannot_rewrite_state()
    {
        // Serialized as numbers, inserting a mode would silently change what
        // every stored state means. This is cheap now and unrecoverable later.
        var json = AppStateJson.Serialize(new AppState { Mode = UiMode.Help, LiveVisible = true, ActiveTab = TabId.Live });

        await Assert.That(json).Contains("\"Help\"");
        await Assert.That(json).Contains("\"Live\"");
    }

    [Test]
    public async Task The_default_state_is_the_one_the_console_should_open_with()
    {
        // Defaults are part of the model, and two of them are decisions rather
        // than conveniences.
        var fresh = new AppState();

        await Assert.That(fresh.ActiveTab).IsEqualTo(TabId.Queue)
            .Because("the queue is what a person is here for.");
        await Assert.That(fresh.LiveVisible).IsFalse()
            .Because("the live view is a trust artifact meant to decay; on by default is the opposite.");
        await Assert.That(fresh.EvidenceVisible).IsFalse()
            .Because("evidence is on demand.");
        await Assert.That(fresh.Mode).IsEqualTo(UiMode.Normal);
        await Assert.That(fresh.Frozen).IsFalse();
    }

    [Test]
    public async Task The_json_carries_no_control_sequences_it_was_handed()
    {
        // Text reaches the store already stripped, at ingress. This asserts the
        // store never becomes the place that reintroduces one.
        var state = StateGenerator.Next(new Random(7));

        await Assert.That(Gg.Contracts.ControlText.ContainsControlSequence(
                AppStateJson.Serialize(state), allowLineBreaks: true))
            .IsFalse();
    }
}
