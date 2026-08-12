using System.Collections;
using System.Reflection;
using System.Runtime.CompilerServices;
using Gg.Contracts.Description;

namespace Gg.Contracts.Tests;

/// <summary>
/// The credential surface is structurally incapable of carrying a secret.
/// </summary>
/// <remarks>
/// <para>
/// Constitution Article VIII: the control plane stores references and facts,
/// never secrets. The developer-side half of that claim is that the request
/// registering a credential has nowhere to put one - not that whoever wrote the
/// client remembered not to.
/// </para>
/// <para>
/// So this asserts over the SHAPE of the types, three ways, because each way
/// misses something the others catch:
/// </para>
/// <list type="number">
/// <item>the member set is closed, so adding any field at all fails here;</item>
/// <item>no member is NAMED for secret material, which is the field somebody
/// adds in a hurry;</item>
/// <item>no member's TYPE is a free-form container, because a
/// <c>Dictionary&lt;string, string&gt;</c> called <c>Metadata</c> passes both
/// of the above and carries anything at all.</item>
/// </list>
/// <para>
/// The walk is recursive: a credential type reached through another credential
/// type is on the wire just the same.
/// </para>
/// </remarks>
public class CredentialContainmentTests
{
    /// <summary>
    /// Every type on the credential path, in both directions.
    /// </summary>
    /// <remarks>
    /// The lease response is in this list deliberately. It is the one place a
    /// secret could be sent the other way - control plane to runner - which
    /// would satisfy "the secret never enters a request body" while breaking
    /// the thing that sentence is shorthand for.
    /// </remarks>
    private static IReadOnlyList<Type> CredentialTypes { get; } =
    [
        typeof(CredentialReference),
        typeof(CredentialRegistrationRequest),
        typeof(CredentialRegistered),
        typeof(CredentialSummary),
        typeof(CredentialList),
        typeof(CredentialRemoved),
        typeof(CredentialResolutionFailure),
        typeof(LeaseGranted),
        typeof(LeaseReleaseRequest),
    ];

    /// <summary>
    /// Words that name secret material rather than a reference to it.
    /// </summary>
    /// <remarks>
    /// Not the same list the control plane uses on column names, and
    /// deliberately narrower: <c>credential</c> is a fine word for a member
    /// that identifies one, and <c>CredentialId</c> must not be an offence. The
    /// list names the thing, not the topic.
    /// </remarks>
    private static readonly string[] SecretShapedWords =
        ["token", "secret", "password", "passphrase", "bearer", "apikey", "privatekey", "accesskey"];

    /// <summary>The only property types a credential-path member may have.</summary>
    /// <remarks>
    /// A closed set, because the hole is not a badly named string - it is
    /// <c>object</c>, <c>JsonElement</c>, or a string-to-string map, any of
    /// which carries a secret while passing a name check.
    /// </remarks>
    private static bool IsAllowedShape(Type type) =>
        type == typeof(string)
        || type == typeof(int)
        || type == typeof(bool)
        || type == typeof(DateTimeOffset)
        || Nullable.GetUnderlyingType(type) is { } inner && IsAllowedShape(inner)
        || IsContractType(type)
        || (type.IsGenericType
            && type.GetGenericTypeDefinition() == typeof(IReadOnlyList<>)
            && IsAllowedShape(type.GetGenericArguments()[0]));

    private static bool IsContractType(Type type) =>
        type.Namespace == typeof(CredentialReference).Namespace && type.IsClass;

    private static IEnumerable<PropertyInfo> MembersOf(Type type) =>
        type.GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Where(p => p.Name != "EqualityContract");

    /// <summary>Every (type, property) on the credential path, following references.</summary>
    private static List<(Type Owner, PropertyInfo Property)> Walk(IEnumerable<Type> roots)
    {
        var seen = new HashSet<Type>();
        var found = new List<(Type Owner, PropertyInfo Property)>();
        var queue = new Queue<Type>(roots);

        while (queue.Count > 0)
        {
            var type = queue.Dequeue();
            if (!seen.Add(type))
            {
                continue;
            }

            foreach (var property in MembersOf(type))
            {
                found.Add((type, property));

                var element = property.PropertyType.IsGenericType
                    ? property.PropertyType.GetGenericArguments()[0]
                    : property.PropertyType;

                if (IsContractType(element) && element != typeof(string))
                {
                    queue.Enqueue(element);
                }
            }
        }

        return found;
    }

    private static string Normalized(string name) =>
        new string([.. name.Where(char.IsLetterOrDigit)]).ToLowerInvariant();

    [Test]
    public async Task No_member_on_the_credential_path_is_named_for_secret_material()
    {
        var offenders = Walk(CredentialTypes)
            .Where(m => SecretShapedWords.Any(word => Normalized(m.Property.Name).Contains(word, StringComparison.Ordinal)))
            .Select(m => $"{m.Owner.Name}.{m.Property.Name}")
            .ToList();

        await Assert.That(offenders).IsEmpty()
            .Because("a member named for a secret is a member that will eventually hold one. Found: "
                   + string.Join(", ", offenders));
    }

    [Test]
    public async Task No_member_on_the_credential_path_is_a_free_form_container()
    {
        var offenders = Walk(CredentialTypes)
            .Where(m => !IsAllowedShape(m.Property.PropertyType))
            .Select(m => $"{m.Owner.Name}.{m.Property.Name} ({m.Property.PropertyType.Name})")
            .ToList();

        await Assert.That(offenders).IsEmpty()
            .Because("a map or an object carries a secret while passing every name check. Found: "
                   + string.Join(", ", offenders));
    }

    [Test]
    public async Task The_registration_request_carries_exactly_the_reference_and_the_repository()
    {
        // The closed-set assertion, and the one that fails on ANY new field
        // rather than only on a badly named one. Adding a member here is meant
        // to be a deliberate, reviewable act - that is the whole mechanism.
        var members = MembersOf(typeof(CredentialRegistrationRequest)).Select(p => p.Name).OrderBy(n => n).ToList();

        await Assert.That(members).IsEquivalentTo((string[])["Reference", "Repo"]);
    }

    [Test]
    public async Task A_reference_is_a_kind_a_locator_an_identity_and_scopes_and_nothing_else()
    {
        var members = MembersOf(typeof(CredentialReference)).Select(p => p.Name).OrderBy(n => n).ToList();

        await Assert.That(members).IsEquivalentTo((string[])["Identity", "Kind", "Locator", "Scopes"]);
    }

    [Test]
    public async Task The_lease_response_carries_references_and_cannot_carry_a_secret()
    {
        // The reference joins the lease response at this step: the runner is
        // told WHICH credential to resolve and resolves it itself. The same
        // walk covers it, so a secret arriving on the way back fails here too.
        var reference = MembersOf(typeof(LeaseGranted)).SingleOrDefault(p => p.Name == "Credentials");

        await Assert.That(reference).IsNotNull()
            .Because("a runner that is not told which credential to resolve cannot resolve one.");
        await Assert.That(reference!.PropertyType).IsEqualTo(typeof(IReadOnlyList<CredentialReference>));
    }

    // ---- the poison twins ----
    //
    // Every assertion above is an ABSENCE, and an absence passes on a walk that
    // inspects nothing, over a type list that is empty, with a word list that
    // matches nothing. So each check is pointed at something planted.

    /// <summary>What somebody adds "for scripting". The walk must see it.</summary>
    private sealed record SmugglesAToken
    {
        public required string Locator { get; init; }

        public required string Token { get; init; }
    }

    /// <summary>The subtler one: a name check alone lets this through.</summary>
    private sealed record SmugglesAMap
    {
        public required string Locator { get; init; }

        public required Dictionary<string, string> Metadata { get; init; }
    }

    [Test]
    public async Task The_poison_twin_a_planted_token_member_is_caught_by_the_name_check()
    {
        var offenders = Walk([typeof(SmugglesAToken)])
            .Where(m => SecretShapedWords.Any(word => Normalized(m.Property.Name).Contains(word, StringComparison.Ordinal)))
            .ToList();

        await Assert.That(offenders.Select(m => m.Property.Name)).Contains("Token")
            .Because("if the walk cannot see this, the assertion above proves nothing.");
    }

    [Test]
    public async Task The_poison_twin_a_planted_map_is_caught_by_the_shape_check()
    {
        var offenders = Walk([typeof(SmugglesAMap)])
            .Where(m => !IsAllowedShape(m.Property.PropertyType))
            .ToList();

        await Assert.That(offenders.Select(m => m.Property.Name)).Contains("Metadata");

        // And the name check does NOT catch it, which is why there are two of
        // them. Computed rather than asserted as a literal, so the claim is
        // about the word list rather than about this sentence.
        var caughtByName = Walk([typeof(SmugglesAMap)])
            .Where(m => SecretShapedWords.Any(w => Normalized(m.Property.Name).Contains(w, StringComparison.Ordinal)))
            .Select(m => m.Property.Name)
            .ToList();
        await Assert.That(caughtByName).IsEmpty();
    }

    [Test]
    public async Task The_walk_actually_reaches_the_types_it_claims_to_cover()
    {
        // The emptiest failure of all: a walk that returns nothing passes every
        // absence assertion in this file.
        var walked = Walk(CredentialTypes);

        await Assert.That(walked).IsNotEmpty();
        await Assert.That(walked.Select(m => m.Owner).Distinct()).Contains(typeof(CredentialReference))
            .Because("the reference is reached THROUGH the request; if it is not, the recursion is dead.");
        await Assert.That(walked.Count).IsGreaterThan(CredentialTypes.Count)
            .Because("one member per type would mean the property enumeration is returning almost nothing.");
    }
}

/// <summary>
/// A credential reference is <c>local</c>, read-only, and says where it lives.
/// </summary>
/// <remarks>
/// The rule lives in the contract, so gg and the control plane cannot disagree
/// about it - the same reason <see cref="FlightIntent.Validate"/> does. The
/// control plane refuses independently; this is not the only gate.
/// </remarks>
public class CredentialReferenceTests
{
    private static CredentialReference AReference(
        string? kind = null, string? locator = null, IReadOnlyList<string>? scopes = null) => new()
    {
        Kind = kind ?? CredentialKinds.Local,
        Locator = locator ?? CredentialLocator.ForRepo("acme/widgets"),
        Identity = "acme-bot",
        Scopes = scopes ?? [CredentialScopes.Read],
    };

    [Test]
    public async Task Local_is_the_only_kind_slice_one_has()
    {
        await Assert.That(CredentialKinds.All).IsEquivalentTo((string[])[CredentialKinds.Local])
            .Because("an enum with unused members that quietly work is how a shortcut is inherited.");
    }

    [Test]
    public async Task A_local_reference_with_read_scope_validates()
    {
        await Assert.That(CredentialReference.Validate(AReference())).IsNull();
    }

    [Test]
    public async Task An_environment_variable_locator_is_not_a_kind_that_exists()
    {
        // Named rather than merely absent. Env vars leak into child processes,
        // ps output, crash dumps and CI logs; for a product about credential
        // containment it is the one adapter that would undercut the pitch.
        await Assert.That(CredentialKinds.All).DoesNotContain("env");
        await Assert.That(CredentialReference.Validate(AReference(kind: "env"))).IsNotNull();
    }

    [Test]
    public async Task An_unknown_kind_is_refused_with_a_diagnosis_naming_it()
    {
        var diagnosis = CredentialReference.Validate(AReference(kind: "keychain"));

        await Assert.That(diagnosis).IsNotNull();
        await Assert.That(diagnosis!).Contains("keychain")
            .Because("a refusal that does not name what was sent makes somebody read their own code to find out.");
    }

    [Test]
    public async Task Scopes_are_what_the_developer_registered_and_nothing_widens_them()
    {
        // AMENDED IN SLICE TWO, STEP 4, and the amendment is the point.
        //
        // This asserted that read was the only scope, which was true and useful
        // while nothing could write: a wider scope was a request nobody had a
        // use for. Write now exists, registered by the developer in their own
        // store - so the claim narrows to the one that still matters and is the
        // one that was always doing the work.
        //
        // AN ENVELOPE DECLARES THAT A FLIGHT MAY LAND SOMEWHERE; IT CANNOT
        // GRANT THE ABILITY TO. A control plane that could escalate a
        // credential would make the customer's own store advisory, which is the
        // layering model - lower layers narrow, never widen - reaching across
        // the boundary.
        await Assert.That(CredentialScopes.All)
            .IsEquivalentTo((string[])[CredentialScopes.Read, CredentialScopes.Write]);

        foreach (var invented in (string[])["admin", "repo", "read:write", "delete", "force-push"])
        {
            await Assert.That(CredentialReference.Validate(AReference(scopes: [invented]))).IsNotNull()
                .Because($"'{invented}' is not a scope this contract knows, and a scope nobody can "
                       + "name is one nobody can audit.");
        }

        await Assert.That(CredentialReference.Validate(AReference(scopes: [CredentialScopes.Read])))
            .IsNull();
        await Assert.That(CredentialReference.Validate(AReference(scopes: [CredentialScopes.Write])))
            .IsNull();
    }

    [Test]
    public async Task A_reference_carries_no_member_that_could_widen_its_own_scopes()
    {
        // Structural, because this is the property the two-control design rests
        // on: whatever is registered is what is held, and there is nowhere for a
        // later "grant" or "elevate" to arrive. An escalation that has no field
        // to travel in cannot be added by forgetting a rule.
        var members = typeof(CredentialReference).GetProperties().Select(p => p.Name).ToList();

        foreach (var widening in (string[])
                 ["Grant", "Grants", "Elevate", "Elevated", "Escalate", "Additional",
                  "ExtraScopes", "EffectiveScopes", "GrantedScopes"])
        {
            await Assert.That(members).DoesNotContain(widening)
                .Because($"'{widening}' is where a server-side widening would end up.");
        }

        await Assert.That(members).Contains(nameof(CredentialReference.Scopes))
            .Because("the scan has to be looking at the type that holds them.");
    }

    [Test]
    public async Task A_reference_with_no_scopes_is_refused_rather_than_defaulted()
    {
        // Article XI. Defaulting an empty list to read would make "scopes are
        // requested read-only" true by our own generosity rather than by what
        // the caller asked for.
        await Assert.That(CredentialReference.Validate(AReference(scopes: []))).IsNotNull();
    }

    [Test]
    public async Task A_locator_names_a_place_rather_than_carrying_a_value()
    {
        // Shape, not intent - and said out loud, because a charset rule does
        // not stop somebody pasting a lowercase token in. What it does stop is
        // the accident: a locator is short, lowercase and path-shaped, and a
        // bearer value is not.
        await Assert.That(CredentialLocator.Validate("local:acme/widgets")).IsNull();

        foreach (var malformed in (string[])
                 ["", "acme/widgets", "local:", "local:/leading", "local:UPPER",
                  "local:has space", "local:tab\tseparated", "env:A_PROVIDER_TOKEN"])
        {
            await Assert.That(CredentialLocator.Validate(malformed)).IsNotNull()
                .Because($"'{malformed}' is not a locator this contract accepts.");
        }
    }

    [Test]
    public async Task A_locator_is_bounded_so_a_pasted_value_does_not_fit()
    {
        var overlong = "local:" + new string('a', CredentialLocator.MaxLength);

        await Assert.That(CredentialLocator.Validate(overlong)).IsNotNull();

        // Shorter than any provider token anyone actually issues. Asserted
        // against the longest of them rather than against a bare number, so
        // the bound says what it is for.
        const int ShortestRealisticProviderToken = 36;
        await Assert.That(overlong.Length).IsGreaterThan(ShortestRealisticProviderToken);
    }

    [Test]
    public async Task The_locator_for_a_repository_is_derived_the_same_way_everywhere()
    {
        // Both halves of gg compute it and the control plane stores it. Two
        // derivations that agree today is how a runner ends up looking for a
        // file the CLI never wrote.
        await Assert.That(CredentialLocator.ForRepo("Acme/Widgets"))
            .IsEqualTo(CredentialLocator.ForRepo("acme/widgets"));
        await Assert.That(CredentialLocator.Validate(CredentialLocator.ForRepo("acme/widgets"))).IsNull();
    }

    [Test]
    public async Task A_resolution_failure_names_the_reference_and_what_went_wrong()
    {
        // ADR-0004 named this failure before it existed: a runner that cannot
        // read a secret produces a stalled flight that looks like a broken
        // product. The diagnosis is a feature, so it is a wire type.
        var failure = new CredentialResolutionFailure
        {
            Reference = AReference(),
            Problem = "no file at the locator",
        };

        await Assert.That(failure.Reference.Locator).IsNotEmpty();
        await Assert.That(failure.Problem).IsNotEmpty();
    }
}

/// <summary>The credential surface, as the protocol declares it.</summary>
public class CredentialSurfaceDeclarationTests
{
    private static Endpoint Declared(string method, string path) =>
        ProtocolSurface.Endpoints.Single(e => e.Method == method && e.Path == path);

    [Test]
    public async Task The_credential_prefix_is_governed_and_therefore_closed()
    {
        // Without this the control plane could grow a credential route gg knows
        // nothing about, inside the very area this step is about.
        await Assert.That(ProtocolSurface.GovernedPrefixes).Contains("/v1/credentials");
    }

    [Test]
    public async Task Registering_a_credential_is_a_developer_action()
    {
        var endpoint = Declared("POST", "/v1/credentials");

        await Assert.That(endpoint.Audience).IsEqualTo(Audience.Developer)
            .Because("a person registers a credential; a runner resolves one. A runner that could "
                   + "register would be a runner that could point a flight at a secret of its choosing.");
        await Assert.That(endpoint.Request).IsEqualTo(typeof(CredentialRegistrationRequest));
        await Assert.That(endpoint.Response).IsEqualTo(typeof(CredentialRegistered));
        await Assert.That(endpoint.Statuses).Contains(400)
            .Because("a kind the control plane refuses is a diagnosis, not a 500.");
    }

    [Test]
    public async Task Listing_and_removing_are_declared_too()
    {
        // A store you cannot inspect or clean is a store people work around.
        await Assert.That(Declared("GET", "/v1/credentials").Response).IsEqualTo(typeof(CredentialList));
        await Assert.That(Declared("DELETE", "/v1/credentials/{id}").Statuses).Contains(404);
    }

    [Test]
    public async Task Every_credential_type_is_in_the_vocabulary_and_pinned()
    {
        // VocabularyTests asserts this for the whole manifest; this says out
        // loud that the credential types belong to it, so a type added to the
        // file and forgotten in the list fails with a message about
        // credentials rather than about a list.
        foreach (var type in (Type[])
                 [typeof(CredentialReference), typeof(CredentialRegistrationRequest),
                  typeof(CredentialRegistered), typeof(CredentialSummary), typeof(CredentialList),
                  typeof(CredentialRemoved), typeof(CredentialResolutionFailure)])
        {
            await Assert.That(Vocabulary.Types).Contains(type);
            await Assert.That(ProtocolSurface.JsonMembers.ContainsKey(type)).IsTrue()
                .Because($"{type.Name} crosses the boundary, so its member names are declared.");
        }
    }

    [Test]
    public async Task The_declared_members_of_a_reference_carry_no_secret()
    {
        // The wire spelling, not the C# name: a [JsonPropertyName] could add a
        // member the shape assertions above never see.
        await Assert.That(ProtocolSurface.JsonMembers[typeof(CredentialReference)])
            .IsEquivalentTo((string[])["kind", "locator", "identity", "scopes"]);
    }
}
