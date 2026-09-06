using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.Json.Serialization.Metadata;
using Gg.Client;
using Gg.Contracts;
using Gg.Runner;

namespace Gg.Cli.Tests;

/// <summary>
/// A member gg has no value for is left out of the request, rather than written
/// as null.
/// </summary>
/// <remarks>
/// <para>
/// <b>THIS KILLED A RUNNER ON A DEV HOST.</b> <c>LeaseClaimRequest</c> gained an
/// optional <c>FlightId</c> at contract 0.102.0. The control plane reads every
/// body with <c>JsonUnmappedMemberHandling.Disallow</c>, so a control plane that
/// had not learned that member yet answered 400 — and a runner updated ahead of
/// it probed, registered, and died unhandled on its first claim. Measured on that
/// surface: a body carrying one member the schema does not have, VALUED NULL,
/// answers 400; the same body without it answers 202.
/// </para>
/// <para>
/// <b>The member's own remark already promised this.</b> <c>FlightId</c> says
/// "it is absent on the wire rather than null so the fleet's request body is
/// byte-for-byte what it was. The two repositories are not upgraded in step." The
/// declaration under it is a bare <c>string?</c> with no ignore condition, so it
/// was written as null on every claim ever made. That is the fourth time in this
/// stretch of work that prose has asserted a property directly above code that
/// does not have it — and, as with the collections sweep, the fix is the thing
/// that compares the two.
/// </para>
/// <para>
/// <b>Why the condition and not the attribute.</b> A per-member
/// <c>[JsonIgnore(Condition = WhenWritingNull)]</c> fixes the member somebody
/// remembered and leaves the next optional member to break the next runner. The
/// member whose absence is legal is exactly the member the compiler cannot ask
/// about, so the answer belongs on the context, once, where a new member inherits
/// it by existing.
/// </para>
/// <para>
/// <b>It narrows the window; it does not close it.</b> An optional member added
/// with a NON-null value still reaches an older control plane as an unknown key
/// and still answers 400. What closes the class is the reading side being lenient
/// for the bodies our own binary composes — which is the control plane's line to
/// draw, and is drawn there.
/// </para>
/// </remarks>
public class NullIsNotOnTheWireTests
{
    /// <summary>
    /// The two contexts that address the control plane. Every other context in
    /// this repository writes a local file or talks to a third party.
    /// </summary>
    private static (string Name, JsonSerializerContext Context)[] Senders() =>
    [
        ("RunnerJsonContext", RunnerJsonContext.Default),
        ("ProtocolJsonContext", ProtocolJsonContext.Default),
    ];

    /// <summary>The minimum an envelope must carry, and nothing optional.</summary>
    private const string BareEnvelope = """
        {"context":{"scope":"src/**","constitution":"1.0.0"},
         "obligations":[{"id":"in-scope","check":"machine","rule":"no-file-outside-scope"}],
         "loops":[{"id":"implement","executor":"frontier","discharges":["in-scope"],
                   "moves":["read"],"budget":{"wallClock":"30m"},
                   "onExhaustion":"handoff-to-human"}],
         "destinations":[]}
        """;

    [Test]
    public async Task A_claim_for_whatever_is_ready_does_not_name_a_flight_at_all()
    {
        // THE BODY THAT KILLED THE RUNNER, written the way the fleet writes it:
        // no flight asked for by name, which is every claim made before 0.102.0
        // and the great majority made since.
        var claim = new LeaseClaimRequest
        {
            RunnerId = "runner-1",
            Labels = ["linux"],
            MaxWaitSeconds = 20,
            FlightId = null,
        };

        var json = JsonSerializer.Serialize(claim, RunnerJsonContext.Default.LeaseClaimRequest);

        await Assert.That(json).DoesNotContain("flightId");
        await Assert.That(json).DoesNotContain("null");
    }

    [Test]
    public async Task No_optional_member_either_sender_writes_is_written_as_null()
    {
        // THE SWEEP, AND ITS SCOPE IS A SUPERSET. It walks every shape either
        // context declares, which includes the responses gg only ever reads -
        // asserting a property about writing a type nothing writes costs nothing
        // and needs no exception list, and an exception list is how a request
        // quietly stops being swept.
        var offenders = Optional()
            .Where(member => WrittenAsNull(member.Shape, member.Member))
            .Select(member => $"{member.Sender}: {member.Shape.Type.Name}.{member.Member.Name}")
            .ToList();

        await Assert.That(offenders).IsEmpty()
            .Because("a null on the wire is an unknown key to a control plane that has "
                   + "not learned the member, and that is a 400 rather than something "
                   + "ignored. Set DefaultIgnoreCondition = WhenWritingNull on the "
                   + "context. Found: " + string.Join(", ", offenders));
    }

    [Test]
    public async Task An_empty_list_is_still_sent_because_it_is_a_decision()
    {
        // THE HALF THAT MUST NOT MOVE. Accepts is nullable because absence MEANS
        // something: null is silence and [] is a decision to accept nothing. A
        // condition that dropped empties as well as nulls would erase that
        // distinction on the way out and silently widen what a work kind takes.
        var envelope = JsonSerializer.Deserialize(BareEnvelope, ProtocolJsonContext.Default.Envelope)!;

        var decided = JsonSerializer.Serialize(
            envelope with { Accepts = [] }, ProtocolJsonContext.Default.Envelope);
        var silent = JsonSerializer.Serialize(
            envelope with { Accepts = null }, ProtocolJsonContext.Default.Envelope);

        await Assert.That(decided).Contains("\"accepts\":[]");
        await Assert.That(silent).DoesNotContain("accepts");
    }

    [Test]
    public async Task The_sweep_reaches_both_senders_and_the_member_that_broke()
    {
        // LIVENESS. A sweep that quietly reached one context, or stopped at the
        // documents' top level, passes the assertion above forever.
        var swept = Optional()
            .Select(member => $"{member.Sender}: {member.Shape.Type.Name}.{member.Member.Name}")
            .ToList();

        await Assert.That(swept).Contains("RunnerJsonContext: LeaseClaimRequest.flightId");
        await Assert.That(swept).Contains("ProtocolJsonContext: Envelope.accepts");

        // AND IT REACHES A TYPE NO REQUEST NAMES DIRECTLY. SourceProvenance is
        // two documents inside a fact batch. Every shape is swept at its own top
        // level, so a member that deep is covered once, by its owner, rather
        // than by a walk that has to recurse correctly.
        await Assert.That(swept).Contains("RunnerJsonContext: SourceProvenance.forkSlug");

        // AND IT DOES NOT SWEEP WHAT CANNOT BE ABSENT. A required member is one
        // the deserializer refuses a document without, so no sender ever leaves
        // it unset and no skew produces it as null.
        await Assert.That(swept).DoesNotContain("RunnerJsonContext: LeaseClaimRequest.runnerId");
    }

    [Test]
    public async Task Dropping_a_null_can_never_remove_a_member_the_reader_demands()
    {
        // THE PROPERTY THAT MAKES THE CONDITION SAFE, and it is a fact about the
        // contract rather than about this change. System.Text.Json accepts an
        // explicit null for a required member and REFUSES a document that omits
        // one - so a member that were both required and nullable would go from
        // accepted to "required member missing" the moment it held null, and the
        // 400 this change exists to remove would come straight back wearing a
        // different message.
        //
        // Measured when this was written: no member in the contract is both.
        // That is what the assertion is for - it is one edit away from being
        // false, and nothing else in either repository would notice.
        var both = new List<string>();

        foreach (var type in Senders()
                     .SelectMany(sender => Emits(sender.Context))
                     .Select(shape => shape.Type)
                     .Where(Constructible)
                     .Distinct())
        {
            foreach (var property in type.GetProperties(BindingFlags.Public | BindingFlags.Instance))
            {
                if (property.GetCustomAttribute<RequiredMemberAttribute>() is null)
                {
                    continue;
                }

                if (Nullable.GetUnderlyingType(property.PropertyType) is not null
                    || new NullabilityInfoContext().Create(property).WriteState == NullabilityState.Nullable)
                {
                    both.Add($"{type.Name}.{property.Name}");
                }
            }
        }

        await Assert.That(both).IsEmpty()
            .Because("a required member that may hold null is accepted as an explicit "
                   + "null and refused when absent, so WhenWritingNull would turn a "
                   + "working body into a 400. Make it optional, or make it "
                   + "non-nullable. Found: " + string.Join(", ", both));
    }

    /// <summary>
    /// Every member of every shape either sender writes that a caller is allowed
    /// to leave unset.
    /// </summary>
    private static IEnumerable<(string Sender, JsonTypeInfo Shape, JsonPropertyInfo Member)> Optional() =>
        from sender in Senders()
        from shape in Emits(sender.Context)
        where Constructible(shape.Type)
        from member in shape.Properties
        where !member.IsRequired
        select (sender.Name, shape, member);

    /// <summary>
    /// The shapes a context declares, read off the generated surface itself
    /// rather than off a list this test keeps.
    /// </summary>
    private static IEnumerable<JsonTypeInfo> Emits(JsonSerializerContext context) =>
        context.GetType()
            .GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Where(property => typeof(JsonTypeInfo).IsAssignableFrom(property.PropertyType))
            .Select(property => (JsonTypeInfo)property.GetValue(context)!);

    /// <summary>
    /// What this member looks like on the wire when a caller says nothing about
    /// it.
    /// </summary>
    /// <remarks>
    /// The blank is uninitialized on purpose: every member sits at its backing
    /// field's default, which is the state a caller leaves it in by not setting
    /// it. Initializers do not run, which is the point - an absorbing accessor
    /// still answers <c>[]</c>, and a bare member still answers null.
    /// </remarks>
    private static bool WrittenAsNull(JsonTypeInfo shape, JsonPropertyInfo member)
    {
        var blank = RuntimeHelpers.GetUninitializedObject(shape.Type);

        using var written = JsonDocument.Parse(JsonSerializer.Serialize(blank, shape));

        return written.RootElement.ValueKind == JsonValueKind.Object
            && written.RootElement.TryGetProperty(member.Name, out var value)
            && value.ValueKind == JsonValueKind.Null;
    }

    /// <summary>
    /// A shape a sender can hold an instance of.
    /// </summary>
    /// <remarks>
    /// A context declares a shape for every type it reaches, members' element
    /// types included, so the list carries <c>string</c>, <c>int</c> and
    /// <c>IReadOnlyList&lt;T&gt;</c> alongside the documents. None of those is a
    /// thing a caller leaves a member unset on, and the document that owns them
    /// is swept in its own right, so nothing is lost by not building them -
    /// which is not true of skipping a record, and is why this asks what the type
    /// IS rather than naming types.
    /// </remarks>
    private static bool Constructible(Type type) =>
        type.IsClass
        && !type.IsAbstract
        && type != typeof(string)
        && !typeof(System.Collections.IEnumerable).IsAssignableFrom(type);
}
