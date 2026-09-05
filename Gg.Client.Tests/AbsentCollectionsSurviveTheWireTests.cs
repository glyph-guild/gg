using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text.Json;
using Gg.Client;
using Gg.Contracts;

namespace Gg.Client.Tests;

/// <summary>
/// A key a sender omits does not become a null a reader dereferences.
/// </summary>
/// <remarks>
/// <para>
/// <b>THIS SHIPPED, AND IT BROKE THE ESTATE WALK.</b> <c>Envelope.Instructions</c>
/// was declared <c>= []</c> and read as <c>envelope.Instructions.Count</c>. A
/// property initializer does not run on the deserialization path, so every
/// envelope from a control plane that had never heard of the field arrived with
/// null and <c>EnvelopeText.Render</c> threw — while the comment directly above
/// the throwing line promised those envelopes rendered byte-for-byte unchanged.
/// The comment stated the guarantee; the dereference removed it.
/// </para>
/// <para>
/// <b>It had a sibling that predated it by sixty contract versions.</b>
/// <c>Obligation.Evidence</c> carries the same declaration and the same
/// dereference, latent since 0.44.0 because every producer happens to send the
/// key. "Nobody omits it yet" is not a property; it is a habit of the current
/// senders.
/// </para>
/// <para>
/// <b>Which is why this is a sweep and not two fixes.</b> The next optional
/// collection on a wire type will be declared the same way by somebody reading
/// the ones already there, and the failure only appears against a sender that
/// does not write the key — which is precisely the sender you get during a
/// version skew, and never the one in your own tests.
/// </para>
/// </remarks>
public class AbsentCollectionsSurviveTheWireTests
{
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
    public async Task An_envelope_from_a_sender_that_omits_every_optional_key_still_renders()
    {
        // THE PRODUCTION PATH, and the exact configuration that failed: a CLI
        // one contract version ahead of the control plane it is talking to,
        // which is the ordinary state between a merge and the next pin bump.
        var envelope = JsonSerializer.Deserialize(
            BareEnvelope, ProtocolJsonContext.Default.Envelope)!;

        await Assert.That(() => EnvelopeText.Render(envelope)).ThrowsNothing();
    }

    [Test]
    public async Task An_explicit_null_is_the_same_as_an_absent_key()
    {
        // A sender that writes the key as null is as real as one that omits
        // it, and Gg.Contracts writes null for every optional member because
        // it declares no JsonIgnore anywhere.
        var envelope = JsonSerializer.Deserialize(
            BareEnvelope.Replace("{\"context\"", "{\"instructions\":null,\"context\"",
                StringComparison.Ordinal),
            ProtocolJsonContext.Default.Envelope)!;

        await Assert.That(envelope.Instructions).IsNotNull();
        await Assert.That(() => EnvelopeText.Render(envelope)).ThrowsNothing();
    }

    [Test]
    public async Task No_optional_collection_anywhere_in_the_contract_reads_as_null()
    {
        // THE SWEEP, AND IT IS THE WHOLE CONTRACT RATHER THAN ONE GRAPH. The
        // first version of this walked an Envelope and found the two members
        // that had already broken. Ten more carry the same declaration on types
        // an Envelope never reaches - WhoAmI, FlightSummary, RunnerSummary,
        // Reason - and a sweep scoped to the type that happened to fail is a
        // sweep that finds the bug you already know about.
        var offenders = new List<string>();

        foreach (var type in CrossesTheBoundary())
        {

            foreach (var property in Optional(type))
            {
                // WHAT A DESERIALIZER LEAVES BEHIND. These members are
                // init-only, so System.Text.Json cannot assign them after
                // construction and builds the object through a creator that
                // assigns EVERY member at once from an argument array - the
                // optional ones included, as null when the key is absent, over
                // the initializer. The backing field of an uninitialized object
                // is in that same state, so this reads what a reader reads after
                // a sender one version behind omits the key. The test above
                // checks that premise rather than trusting this paragraph.
                var blank = RuntimeHelpers.GetUninitializedObject(type);

                object? read;
                try
                {
                    read = property.GetValue(blank);
                }
                catch (TargetInvocationException error)
                {
                    offenders.Add($"{type.Name}.{property.Name} (threw {error.InnerException?.GetType().Name})");
                    continue;
                }

                if (read is null)
                {
                    offenders.Add($"{type.Name}.{property.Name}");
                }
            }
        }

        await Assert.That(offenders).IsEmpty()
            .Because("a non-nullable collection is a promise the type makes to every caller, "
                   + "and an absent key must not break it. Either absorb the null in the "
                   + "accessor, or declare the member nullable because absence MEANS "
                   + "something - and check that a context actually emits the type "
                   + "first, because this scope is a deliberate superset of what is "
                   + "serialized. Found: " + string.Join(", ", offenders));
    }

    [Test]
    public async Task The_premise_the_sweep_rests_on_is_checked_rather_than_asserted()
    {
        // THE SIMULATION IS ONLY FAITHFUL FOR ONE KIND OF MEMBER, and until now
        // this file stated that in a comment - which is the shape of defect the
        // whole class exists to catch, sitting in the class itself.
        //
        // An init-only property cannot be assigned after construction, so
        // System.Text.Json builds the object through a creator that assigns
        // EVERY member at once from an argument array. An absent key contributes
        // default(T), so the assignment writes null OVER the initializer, and an
        // uninitialized object is in that same state. A settable property is
        // different: the deserializer can set it afterwards and simply does not
        // when the key is absent, so the initializer survives and reading an
        // uninitialized one would report a defect that cannot happen.
        //
        // I GOT THIS WRONG TWICE BEFORE CHECKING IT. First "the initializer does
        // not run on the deserialization path" - it runs and is overwritten.
        // Then "every wire type has a required member, which is what forces the
        // creator" - NamedEnvelopeApply has none and takes that path anyway,
        // because init-only is what forces it. Both were stated confidently in
        // prose and neither was measured; a peer sweeping the same bug next door
        // made the per-type version of the same mistake. The unit here turns out
        // to be smaller than either of us assumed: it is the member.
        var settable = CrossesTheBoundary()
            .SelectMany(type => Optional(type).Select(p => (Type: type, Property: p)))
            .Where(m => !IsInitOnly(m.Property))
            .Select(m => $"{m.Type.Name}.{m.Property.Name}")
            .ToList();

        await Assert.That(settable).IsEmpty()
            .Because("this sweep reads an uninitialized object to stand for one the "
                   + "deserializer built, which holds only where the member is assigned "
                   + "by the creator. A settable member keeps its initializer across an "
                   + "absent key, so sweeping one would report a defect it does not have "
                   + "- it must be excluded deliberately rather than swept by accident. "
                   + "Found: " + string.Join(", ", settable));
    }

    /// <summary>
    /// Whether the deserializer must assign this member at construction, which
    /// is what makes an uninitialized object stand for a deserialized one.
    /// </summary>
    private static bool IsInitOnly(PropertyInfo property) =>
        property.SetMethod is { } setter
        && setter.ReturnParameter.GetRequiredCustomModifiers()
            .Any(modifier => modifier.Name == "IsExternalInit");

    [Test]
    public async Task The_sweep_reaches_types_an_envelope_never_touches()
    {
        // LIVENESS, AND IT NAMES THE BLIND SPOT THE FIRST VERSION HAD. A sweep
        // that quietly stopped at the Envelope graph would pass this file's
        // other tests forever while the contract grew members it never saw.
        var swept = CrossesTheBoundary()
            .SelectMany(type => Optional(type).Select(p => $"{type.Name}.{p.Name}"))
            .ToList();

        await Assert.That(swept).Contains("Envelope.Instructions");
        await Assert.That(swept).Contains("WhoAmI.Notices");
        await Assert.That(swept).Contains("FlightSummary.RequiredLabels");

        // AND IT DOES NOT SWEEP WHAT IT MUST NOT. Required members cannot be
        // absent - the deserializer refuses the document - and nullable ones
        // tell their caller to check, which is how this codebase already
        // records that absence means something.
        await Assert.That(swept).DoesNotContain("Envelope.Obligations");
        await Assert.That(swept).DoesNotContain("Envelope.Accepts");
        await Assert.That(swept).DoesNotContain("Envelope.Produces");
    }

    /// <summary>
    /// The types a document is made of, which is the repository's own answer
    /// rather than this test's guess.
    /// </summary>
    /// <remarks>
    /// <b>Scoped by <see cref="PinnedIdAttribute"/>, not by "everything in the
    /// assembly".</b> A type nothing serializes is built by code that always runs
    /// its initializers, so an absent key is a state it never reaches, and
    /// rewriting its declaration would be a change with no defect behind it.
    /// Measured when this was written: 163 types are emitted across the five
    /// contexts and every one of them carries a pinned id, so the four
    /// collections this scope excludes - the three <c>Notes</c> on the YAML parse
    /// results and <c>Endpoint.RequiredHeaders</c> - are on types no context
    /// emits.
    /// </remarks>
    /// <remarks>
    /// <b>IT IS A SUPERSET, DELIBERATELY, AND THE EARLIER WORDING READ AS IF IT
    /// WERE EXACT.</b> Every emitted type carries a pinned id; the converse is
    /// false. <c>Composition</c>, <c>RunnerParkRequest</c> and
    /// <c>RunnerReservationRequest</c> carry one and are serialized by nothing,
    /// so a member flagged on a type like those is not a live defect - check
    /// whether any context emits it before treating a report as one.
    /// </remarks>
    /// <remarks>
    /// <b>Why the superset is still the right default.</b> The two errors are not
    /// symmetric. Over-reporting costs one harmless line - an accessor that
    /// returns the same empty list the initializer would have - while
    /// under-reporting is a NullReferenceException against a sender one version
    /// behind, which is the failure this whole file exists for. When the scope
    /// has to be wrong, it should be wrong in the direction that is cheap.
    /// </remarks>
    /// <remarks>
    /// <b>And the defect needs a source-generated context, not just init-only.</b>
    /// A peer measured the full table next door: init-only decides it and
    /// <c>required</c> is irrelevant, but plain reflection-based
    /// <c>JsonSerializer</c> keeps the initializer in every shape - only a
    /// generated context nulls it. That is invisible here because this repository
    /// is AOT-published and has no reflection-based path, and it is the reason
    /// their first probe of the same bug found nothing.
    /// </remarks>
    private static IEnumerable<Type> CrossesTheBoundary() =>
        typeof(Envelope).Assembly.GetExportedTypes()
            .Where(type => type.IsClass && !type.IsAbstract && !type.ContainsGenericParameters)
            .Where(type => type.GetCustomAttribute<PinnedIdAttribute>() is not null);

    /// <summary>
    /// The members a sender one version behind can leave out: a collection the
    /// type promises is never null, that the document is allowed to omit.
    /// </summary>
    private static IEnumerable<PropertyInfo> Optional(Type type)
    {
        foreach (var property in type.GetProperties(BindingFlags.Public | BindingFlags.Instance))
        {
            if (!property.PropertyType.IsGenericType)
            {
                continue;
            }

            var shape = property.PropertyType.GetGenericTypeDefinition();
            if (shape != typeof(IReadOnlyList<>)
                && shape != typeof(IReadOnlyDictionary<,>)
                && shape != typeof(IReadOnlySet<>))
            {
                continue;
            }

            // REQUIRED IS NOT OPTIONAL. System.Text.Json refuses a document that
            // omits one, so no skew produces an absent key here. (An explicit
            // null in the document still assigns null, but that is a malformed
            // sender rather than an old one, and the type does not defend
            // against it today.)
            if (property.GetCustomAttribute<RequiredMemberAttribute>() is not null)
            {
                continue;
            }

            // A NULLABLE ONE IS NOT A DEFECT. It tells its caller to check, and
            // Accepts and Produces are nullable precisely because absence means
            // something there.
            if (new NullabilityInfoContext().Create(property).ReadState == NullabilityState.Nullable)
            {
                continue;
            }

            yield return property;
        }
    }
}
