using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Gg.Contracts;

namespace Gg.Client;

/// <remarks>
/// <b>A member with no value is left out, not written as null</b> - the same
/// rule and the same reason as <c>RunnerJsonContext</c>, which carries it in
/// full. An empty collection is untouched: <c>Accepts</c> is nullable because
/// absence MEANS something, null being silence and <c>[]</c> a decision to
/// accept nothing, and only the first of those is what this drops.
/// </remarks>
[JsonSourceGenerationOptions(
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull)]
[JsonSerializable(typeof(DeviceAuthorizationRequest))]
[JsonSerializable(typeof(DeviceAuthorizationStarted))]
[JsonSerializable(typeof(DeviceTokenRequest))]
[JsonSerializable(typeof(SessionIssued))]
[JsonSerializable(typeof(WhoAmI))]
[JsonSerializable(typeof(InvitationRequest))]
[JsonSerializable(typeof(InvitationIssued))]
[JsonSerializable(typeof(RunnerRegistrationRequest))]
[JsonSerializable(typeof(RunnerRegistered))]
[JsonSerializable(typeof(FlightGroundingRequest))]
[JsonSerializable(typeof(FlightLaunchRequest))]
[JsonSerializable(typeof(FlightLaunched))]
[JsonSerializable(typeof(FlightSummary))]
[JsonSerializable(typeof(BoardPage))]
[JsonSerializable(typeof(NominationDecision))]
[JsonSerializable(typeof(FlightList))]
[JsonSerializable(typeof(FlightLog))]
[JsonSerializable(typeof(FlightStory))]
[JsonSerializable(typeof(TakeSeed))]
[JsonSerializable(typeof(RunnerIntroductionRequest))]
[JsonSerializable(typeof(RunnerIntroduction))]
[JsonSerializable(typeof(RunnerSealedOffer))]
[JsonSerializable(typeof(RunnerSealedAnswer))]
[JsonSerializable(typeof(RunnerRetirementRequest))]
[JsonSerializable(typeof(TenantNameRequest))]
[JsonSerializable(typeof(RunnerRetired))]
[JsonSerializable(typeof(RunnerClaimRequest))]
[JsonSerializable(typeof(EnrollmentTokenRequest))]
[JsonSerializable(typeof(EnrollmentTokenMinted))]
[JsonSerializable(typeof(EnrollmentTokenSummary))]
[JsonSerializable(typeof(EnrollmentTokenList))]
[JsonSerializable(typeof(EnrollmentTokenRevocation))]
[JsonSerializable(typeof(RunnerEnrollmentRequest))]
[JsonSerializable(typeof(RunnerEnrolled))]
[JsonSerializable(typeof(FleetProfile))]
[JsonSerializable(typeof(FleetProfileState))]
[JsonSerializable(typeof(FleetProfileList))]
[JsonSerializable(typeof(RunnerOwnershipRequest))]
[JsonSerializable(typeof(RunnerOwnership))]
[JsonSerializable(typeof(RunnerReservationRequest))]
[JsonSerializable(typeof(RunnerReserved))]
[JsonSerializable(typeof(RunnerList))]
// NEW HERE, and its absence was the restriction showing through: a person
// could never post a reading, so this context never needed to write one. The
// runner's own context has had it since the reading shipped.
[JsonSerializable(typeof(AllowanceReading))]
[JsonSerializable(typeof(AllowanceList))]
[JsonSerializable(typeof(AllowanceFloor))]
[JsonSerializable(typeof(AllowanceOverrideRequest))]
[JsonSerializable(typeof(TelemetryDisclosure))]
[JsonSerializable(typeof(CredentialRegistrationRequest))]
[JsonSerializable(typeof(CredentialRegistered))]
[JsonSerializable(typeof(CredentialList))]
[JsonSerializable(typeof(CredentialRemoved))]
[JsonSerializable(typeof(Envelope))]
[JsonSerializable(typeof(EnvelopeState))]
[JsonSerializable(typeof(FlightAttribution))]
[JsonSerializable(typeof(GateList))]
[JsonSerializable(typeof(DecisionRequest))]
[JsonSerializable(typeof(DecisionRecorded))]
[JsonSerializable(typeof(EnvelopeApplied))]
[JsonSerializable(typeof(FlightFacts))]
[JsonSerializable(typeof(Checklist))]
[JsonSerializable(typeof(EnvironmentStrategy))]
[JsonSerializable(typeof(EnvironmentStrategyState))]
[JsonSerializable(typeof(StrategyList))]
[JsonSerializable(typeof(WatchDocument))]
[JsonSerializable(typeof(WatchState))]
[JsonSerializable(typeof(WatchList))]
[JsonSerializable(typeof(WatchStandingList))]
[JsonSerializable(typeof(CurrentVersion))]
[JsonSerializable(typeof(NamedEnvelopeList))]
[JsonSerializable(typeof(NamedEnvelopeState))]
[JsonSerializable(typeof(NamedEnvelopeApply))]
[JsonSerializable(typeof(ChartEnvironmentRequest))]
[JsonSerializable(typeof(EnvironmentCharted))]
[JsonSerializable(typeof(EnvironmentChart))]
[JsonSerializable(typeof(DeclareNameRequest))]
[JsonSerializable(typeof(TopologyName))]
[JsonSerializable(typeof(RegistrationPending))]
[JsonSerializable(typeof(EnvelopeTopology))]
[JsonSerializable(typeof(RegisteredRepositories))]
// THE WRITE HALF OF THE SAME DOOR. The read was registered and the write
// was not, which is the serializer telling the same story the verb list
// did: nothing in gg had ever posted one.
[JsonSerializable(typeof(RegisterRepositoryRequest))]
[JsonSerializable(typeof(RepositoryRegistered))]
[JsonSerializable(typeof(PoolLedger))]
[JsonSerializable(typeof(PoolAction))]
[JsonSerializable(typeof(MemberCredentialRedemption))]
[JsonSerializable(typeof(MemberCredentialIssued))]
[JsonSerializable(typeof(OfferedConfiguration))]
/// <summary>
/// How this client serializes wire types.
/// </summary>
/// <remarks>
/// Public so conformance tests can read the metadata the SERIALIZER will use,
/// rather than the C# property names. A naming policy or a [JsonPropertyName]
/// changes the wire without changing a property name, and it is the wire that
/// has to match the control plane.
/// </remarks>
public sealed partial class ProtocolJsonContext : JsonSerializerContext;

/// <summary>Outcome of one poll of a pending device authorization.</summary>
public abstract record DevicePollResult
{
    public sealed record Pending : DevicePollResult;

    public sealed record Complete(SessionIssued Session) : DevicePollResult;

    /// <summary>The authorization expired or the human refused it.</summary>
    public sealed record Declined(string Reason) : DevicePollResult;
}

/// <summary>
/// Everything gg says to the control plane. It talks to nothing else.
/// </summary>
/// <remarks>
/// No identity provider appears anywhere in this client, by design: the
/// control plane brokers that exchange. When a second provider ships, this
/// file does not change - which is the entire point of the port living on the
/// server side of the boundary.
/// </remarks>
public sealed class ControlPlaneClient(HttpClient httpClient)
{
    private readonly HttpClient _httpClient = httpClient;

    /// <summary>Applies the three version headers every request must carry.</summary>
    public static void ApplyVersionHeaders(HttpRequestMessage request)
    {
        request.Headers.TryAddWithoutValidation(GgVersions.ProtocolHeader, GgVersions.Protocol.ToString());
        request.Headers.TryAddWithoutValidation(GgVersions.RunnerVersionHeader, GgVersions.Binary);
        request.Headers.TryAddWithoutValidation(GgVersions.FactVocabularyHeader, GgVersions.FactVocabulary);
    }

    private HttpRequestMessage Request(HttpMethod method, string path, string? sessionToken = null)
    {
        var request = new HttpRequestMessage(method, path);
        ApplyVersionHeaders(request);
        if (!string.IsNullOrWhiteSpace(sessionToken))
        {
            request.Headers.TryAddWithoutValidation(GgVersions.SessionHeader, sessionToken);
        }
        return request;
    }

    /// <summary>Begins a device authorization.</summary>
    public async Task<DeviceAuthorizationStarted> StartDeviceAuthorizationAsync(
        string deviceLabel, CancellationToken cancellationToken = default)
    {
        using var request = Request(HttpMethod.Post, "/v1/auth/device");
        request.Content = JsonContent.Create(
            new DeviceAuthorizationRequest { DeviceLabel = deviceLabel },
            ProtocolJsonContext.Default.DeviceAuthorizationRequest);

        using var response = await _httpClient.SendAsync(request, cancellationToken);
        await ThrowIfProtocolRefusedAsync(response, cancellationToken);
        response.EnsureSuccessStatusCode();

        return await response.Content.ReadFromJsonAsync(
            ProtocolJsonContext.Default.DeviceAuthorizationStarted, cancellationToken)
            ?? throw new InvalidOperationException("Control plane returned no authorization.");
    }

    /// <summary>
    /// Polls once. Pending is 202 - a normal wait, not an error, so it does not
    /// pollute logs and metrics with failures that aren't.
    /// </summary>
    public async Task<DevicePollResult> PollDeviceAuthorizationAsync(
        string deviceCode, CancellationToken cancellationToken = default)
    {
        using var request = Request(HttpMethod.Post, "/v1/auth/device/token");
        request.Content = JsonContent.Create(
            new DeviceTokenRequest { DeviceCode = deviceCode },
            ProtocolJsonContext.Default.DeviceTokenRequest);

        using var response = await _httpClient.SendAsync(request, cancellationToken);
        await ThrowIfProtocolRefusedAsync(response, cancellationToken);

        if (response.StatusCode == HttpStatusCode.Accepted)
        {
            return new DevicePollResult.Pending();
        }
        if (response.StatusCode == HttpStatusCode.Gone)
        {
            return new DevicePollResult.Declined("The authorization expired or was declined.");
        }

        response.EnsureSuccessStatusCode();
        var session = await response.Content.ReadFromJsonAsync(
            ProtocolJsonContext.Default.SessionIssued, cancellationToken)
            ?? throw new InvalidOperationException("Control plane returned no session.");
        return new DevicePollResult.Complete(session);
    }

    /// <summary>Who the held session belongs to.</summary>
    /// <summary>
    /// Asks for an invitation into the caller's own tenant.
    /// </summary>
    /// <remarks>
    /// The request body is empty and stays empty: an invitation names nobody,
    /// and the tenant comes from the session. The URL comes back built - where
    /// the web surface lives is deployment knowledge, and composing it here
    /// would guess wrong the first time somebody deployed it anywhere but a
    /// laptop.
    /// </remarks>
    public async Task<InvitationIssued> InviteAsync(
        string sessionToken, CancellationToken cancellationToken = default)
    {
        using var request = Request(HttpMethod.Post, "/v1/invitations", sessionToken);
        request.Content = JsonContent.Create(
            new InvitationRequest(), ProtocolJsonContext.Default.InvitationRequest);

        using var response = await _httpClient.SendAsync(request, cancellationToken);
        await ThrowIfProtocolRefusedAsync(response, cancellationToken);
        response.EnsureSuccessStatusCode();

        return await response.Content.ReadFromJsonAsync(
            ProtocolJsonContext.Default.InvitationIssued, cancellationToken)
            ?? throw new InvalidOperationException("Control plane issued no invitation.");
    }

    /// <summary>
    /// What version of gg the control plane says is current, or null if it would not say.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Returns null rather than throwing, and that is the contract of this
    /// method.</b> Every reason this can fail - the control plane is down, DNS
    /// is broken, a proxy ate it, the body was not what was expected - is the
    /// same fact to a caller: nobody said. Turning that into an exception makes
    /// every call site invent a policy, and the policy they invent under time
    /// pressure is to swallow it and carry on as though the answer were "you
    /// are fine".
    /// </para>
    /// <para>
    /// <b>No session, and no token parameter to pass one by accident.</b> What
    /// the current gg is, is not tenant knowledge - and a machine that cannot
    /// sign in is exactly the machine most likely to be far behind.
    /// </para>
    /// <para>
    /// <b>It does say which gg is asking.</b> Anonymous is about the
    /// credential and not about the version: this is the one request a binary
    /// below the floor can make, so it is the only place the callers furthest
    /// behind can be counted at all. It carried nothing until
    /// <c>EveryRequestSaysWhichGgItIsTests</c>, because the request was built
    /// by hand rather than through the helper.
    /// </para>
    /// <para>
    /// <b>It cannot be refused for being too old.</b> This door declares only
    /// 200, uniquely, because it is the remedy for being below the floor rather
    /// than something the floor governs; <c>ProtocolConformanceTests</c> holds
    /// it to those terms.
    /// </para>
    /// </remarks>
    public async Task<string?> CurrentVersionAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            // THE HELPER, AND NOTHING ABOUT THIS CALL IS AN EXCEPTION TO IT.
            // Its session token is optional, so passing none keeps this door
            // anonymous exactly as building the request by hand did - and the
            // three version headers come with it. A request built here is a
            // second place they can be forgotten, which has now happened
            // twice: see RedeemStatesItsProtocolTests for the first, where the
            // omission surfaced as "this gg is too old" about a binary built
            // minutes earlier.
            using var request = Request(HttpMethod.Get, "/v1/version");
            using var response = await _httpClient.SendAsync(request, cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                return null;
            }

            var current = await response.Content.ReadFromJsonAsync(
                ProtocolJsonContext.Default.CurrentVersion, cancellationToken);

            return current?.Version is { Length: > 0 } version ? version : null;
        }
        catch (HttpRequestException)
        {
            return null;
        }
        catch (TaskCanceledException)
        {
            // A TIMEOUT IS AN ABSENCE, not a failure of the verb. Asking what is
            // current must never be the reason a person's command hangs or dies.
            return null;
        }
    }

    public async Task<WhoAmI?> WhoAmIAsync(string sessionToken, CancellationToken cancellationToken = default)
    {
        using var request = Request(HttpMethod.Get, "/v1/auth/whoami", sessionToken);
        using var response = await _httpClient.SendAsync(request, cancellationToken);

        // THE ONE DOOR WHERE A REFUSAL IS THE ANSWER. This is the probe
        // `gg doctor` uses to ask whether a session is still good, and it says
        // so with null - throwing here would take the question away from the
        // one command whose job is to ask it.
        await ThrowIfProtocolRefusedAsync(response, cancellationToken, refusalIsAnAnswer: true);

        return response.StatusCode == HttpStatusCode.Unauthorized
            ? null
            : await response.Content.ReadFromJsonAsync(ProtocolJsonContext.Default.WhoAmI, cancellationToken);
    }

    /// <summary>
    /// Registers a runner and returns its credential, shown once.
    /// </summary>
    /// <remarks>
    /// A person does this, with their session. The credential that comes back
    /// is handed to the runner process and is the only thing it ever holds -
    /// attribution stays with the developer, authority is the runner protocol
    /// alone.
    /// </remarks>
    /// <summary>
    /// Exchanges a member's single-use nonce for its own runner credential, or
    /// null when the nonce buys nothing.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>The one call gg makes with no credential at all.</b> A member has none
    /// yet - that is the whole point of redeeming - so the nonce IS the
    /// authorization. It came from the resident runner that created this
    /// container, and it is spent by the first redemption.
    /// </para>
    /// <para>
    /// <b>Null on refusal rather than a throw.</b> Never minted, expired, and
    /// already redeemed all answer the same way on purpose: telling them apart
    /// would let anything probe for live nonces. The caller reports a member
    /// that could not start; it does not retry, because a spent nonce does not
    /// become unspent.
    /// </para>
    /// </remarks>
    public async Task<MemberCredentialIssued?> RedeemMemberAsync(
        string nonce, CancellationToken cancellationToken = default)
    {
        // THROUGH THE HELPER, whose session token has always been optional. The
        // first version built this message by hand because a member presents no
        // credential - and skipped the protocol header along with it, so the
        // control plane refused at the floor and every member died before it
        // could become anybody. The refusal read "this gg is too old" about a
        // binary built minutes earlier from the same commit.
        using var request = Request(HttpMethod.Post, "/v1/pools/members/redeem");
        request.Content = JsonContent.Create(
            new MemberCredentialRedemption { Nonce = nonce },
            ProtocolJsonContext.Default.MemberCredentialRedemption);

        using var response = await _httpClient.SendAsync(request, cancellationToken);
        await ThrowIfProtocolRefusedAsync(response, cancellationToken);

        return response.IsSuccessStatusCode
            ? await response.Content.ReadFromJsonAsync(
                ProtocolJsonContext.Default.MemberCredentialIssued, cancellationToken)
            : null;
    }

    /// <param name="publicKey">
    /// What a console will seal an introduction to, or null when this caller has
    /// no key to offer.
    /// </param>
    /// <param name="machine">
    /// The host this runner runs on, or null when the caller has none to name.
    /// The label says how a runner was started; this says where, which is what
    /// lets a maintainer group with the machine it maintains.
    /// </param>
    public async Task<RunnerRegistered> RegisterRunnerAsync(
        string sessionToken, string label, CancellationToken cancellationToken = default,
        string? publicKey = null, string? machine = null)
    {
        using var request = Request(HttpMethod.Post, "/v1/runners", sessionToken);
        request.Content = JsonContent.Create(
            new RunnerRegistrationRequest
            {
                Label = label,
                ProtocolVersion = GgVersions.Protocol,
                PublicKey = publicKey,
                Machine = machine,
            },
            ProtocolJsonContext.Default.RunnerRegistrationRequest);

        using var response = await _httpClient.SendAsync(request, cancellationToken);
        await ThrowIfProtocolRefusedAsync(response, cancellationToken);
        response.EnsureSuccessStatusCode();

        return await response.Content.ReadFromJsonAsync(
            ProtocolJsonContext.Default.RunnerRegistered, cancellationToken)
            ?? throw new InvalidOperationException("Control plane registered no runner.");
    }

    /// <summary>
    /// The cheapest call that reaches the protocol floor.
    /// </summary>
    /// <remarks>
    /// Anonymous on purpose. The floor is checked BEFORE authentication
    /// server-side, so an unauthenticated request still gets a 426 - which
    /// means one call answers both "is it up" and "will it talk to this
    /// binary", and answers the second even for somebody who is not signed in.
    /// </remarks>
    public async Task PingAsync(CancellationToken cancellationToken = default)
    {
        using var request = Request(HttpMethod.Get, "/v1/auth/whoami");
        using var response = await _httpClient.SendAsync(request, cancellationToken);
        await ThrowIfProtocolRefusedAsync(response, cancellationToken);
        // Any other status means the server answered, which is the question.
    }

    /// <summary>The tenant's flights that are still in the air, or all of them.</summary>
    /// <remarks>
    /// The default is the queue's own question. Everything was the only answer
    /// available while nothing recorded an ending, and it made this verb's
    /// one-line description aspirational.
    /// </remarks>
    public async Task<FlightList> ListFlightsAsync(
        string sessionToken,
        bool all = false,
        CancellationToken cancellationToken = default,
        string? intent = null)
    {
        // BUILT AS QUERY PARAMETERS, and the work item token is re-joined with
        // its separator escaped: `#` unescaped in a uri starts a fragment,
        // which never leaves the client, so an unescaped id would arrive as
        // nothing at all and the correlation would silently answer about every
        // flight instead of one work item.
        var query = new List<string>();
        if (all)
        {
            query.Add("all=true");
        }

        if (intent is { Length: > 0 })
        {
            // ESCAPED WHOLE, which is what makes one parameter serve both
            // shapes. `#` unescaped starts a fragment that never leaves the
            // client, so a work item would arrive as its provider alone and the
            // correlation would answer about every flight in that tracker; a
            // uri's own separators need the same treatment for the same reason.
            query.Add("intent=" + Uri.EscapeDataString(intent));
        }

        using var request = Request(
            HttpMethod.Get,
            query.Count == 0 ? "/v1/flights" : "/v1/flights?" + string.Join('&', query),
            sessionToken);
        using var response = await _httpClient.SendAsync(request, cancellationToken);
        await ThrowIfProtocolRefusedAsync(response, cancellationToken);
        response.EnsureSuccessStatusCode();

        return await response.Content.ReadFromJsonAsync(ProtocolJsonContext.Default.FlightList, cancellationToken)
            ?? throw new InvalidOperationException("Control plane returned no flight list.");
    }

    /// <summary>
    /// The board: what this tenant's agents have nominated, and what became of it.
    /// </summary>
    /// <param name="includeEnded">
    /// Whether to include rows that have ended. False answers with what is
    /// still standing, which is what somebody deciding is asking for.
    /// </param>
    /// <remarks>
    /// <b>The page says which of those it gave back</b>, rather than leaving a
    /// reader to infer it. A page of standing rows and a page that happens to
    /// contain no ended ones look identical, and reading "nothing was declined"
    /// off the second would be reading a filter rather than a fact.
    /// </remarks>
    public async Task<BoardPage> GetBoardAsync(
        string sessionToken,
        bool includeEnded = false,
        CancellationToken cancellationToken = default)
    {
        using var request = Request(
            HttpMethod.Get,
            includeEnded ? "/v1/board?ended=true" : "/v1/board",
            sessionToken);
        using var response = await _httpClient.SendAsync(request, cancellationToken);
        await ThrowIfProtocolRefusedAsync(response, cancellationToken);
        response.EnsureSuccessStatusCode();

        return await response.Content.ReadFromJsonAsync(
                   ProtocolJsonContext.Default.BoardPage, cancellationToken)
            ?? throw new InvalidOperationException("Control plane returned no board.");
    }

    /// <summary>
    /// Answers a nomination that is waiting for somebody.
    /// </summary>
    /// <param name="outcome">
    /// One of <see cref="NominationDecisions.All"/> - the two endings a person
    /// may cause. The other four belong to the board, the world, the clock and
    /// the rules.
    /// </param>
    /// <param name="because">
    /// Why. Required, because every ending carries a sentence and a decision is
    /// the one most worth being able to ask about.
    /// </param>
    /// <remarks>
    /// <b>202 and nothing back</b>, on ADR-0012: the write is a command, and
    /// the caller learns what happened by reading the board. A 409 means
    /// somebody already answered it, which is a different fact from a refusal.
    /// </remarks>
    public async Task DecideNominationAsync(
        string sessionToken,
        Guid nomination,
        string outcome,
        string because,
        CancellationToken cancellationToken = default)
    {
        using var request = Request(
            HttpMethod.Post, $"/v1/board/{nomination}/decisions", sessionToken);
        request.Content = JsonContent.Create(
            new NominationDecision { Outcome = outcome, Because = because },
            ProtocolJsonContext.Default.NominationDecision);

        using var response = await _httpClient.SendAsync(request, cancellationToken);
        await ThrowIfProtocolRefusedAsync(response, cancellationToken);
        response.EnsureSuccessStatusCode();
    }

    /// <summary>
    /// The tenant's envelope, or null when it has never applied one.
    /// </summary>
    /// <remarks>
    /// Null and "an envelope that governs nothing" are different answers, and
    /// the endpoint keeps them apart with a 404. A tenant that has never set
    /// one up should be told to; one whose envelope is deliberately permissive
    /// should not.
    /// </remarks>
    public async Task<EnvelopeState?> GetEnvelopeAsync(
        string sessionToken, CancellationToken cancellationToken = default)
    {
        using var request = Request(HttpMethod.Get, "/v1/envelope", sessionToken);
        using var response = await _httpClient.SendAsync(request, cancellationToken);
        await ThrowIfProtocolRefusedAsync(response, cancellationToken);

        if (response.StatusCode == HttpStatusCode.NotFound)
        {
            return null;
        }

        response.EnsureSuccessStatusCode();

        return await response.Content.ReadFromJsonAsync(
            ProtocolJsonContext.Default.EnvelopeState, cancellationToken)
            ?? throw new InvalidOperationException("Control plane returned no envelope.");
    }

    /// <summary>
    /// Writes the envelope back, and answers with the version it became.
    /// </summary>
    /// <remarks>
    /// The wire is JSON even though the thing a person edited was YAML. The
    /// control plane holds no YAML parser at all - that is a property worth
    /// having deliberately rather than by accident, since it is the service that
    /// holds the platform's own signing keys - so the format is translated
    /// here, on the side that already has the grammar.
    /// </remarks>
    public async Task<EnvelopeApplied> ApplyEnvelopeAsync(
        string sessionToken, Envelope envelope, CancellationToken cancellationToken = default)
    {
        using var request = Request(HttpMethod.Put, "/v1/envelope", sessionToken);
        request.Content = JsonContent.Create(envelope, ProtocolJsonContext.Default.Envelope);

        using var response = await _httpClient.SendAsync(request, cancellationToken);
        await ThrowIfProtocolRefusedAsync(response, cancellationToken);

        if (response.StatusCode == HttpStatusCode.BadRequest)
        {
            // The control plane's own diagnosis, carried through unchanged. It
            // validated on its own terms rather than trusting that gg did, and
            // if the two disagree the person needs to see which one refused.
            throw new EnvelopeRefusedException(
                await response.Content.ReadAsStringAsync(cancellationToken));
        }

        response.EnsureSuccessStatusCode();

        return await response.Content.ReadFromJsonAsync(
            ProtocolJsonContext.Default.EnvelopeApplied, cancellationToken)
            ?? throw new InvalidOperationException("Control plane acknowledged nothing.");
    }

    /// <summary>
    /// Applies a document to its topology name, optionally stating what it was
    /// based on.
    /// </summary>
    /// <remarks>
    /// <b>The precondition is a query parameter and never a body member.</b> The
    /// body's stored form is the idempotence key, its field-by-field comparison
    /// decides whether an apply gates, and its bytes are what the composition
    /// digest hashes — so a member that changed on every pull would mint a
    /// version per document per pull, divert every one to a gate, and move every
    /// pin. It is stated by the applier rather than required of them: a
    /// hand-written document has no version it was based on.
    /// </remarks>
    /// <summary>
    /// Declares a name in the topology, so a document can be applied to it.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Two success codes, and both are answers rather than one being a
    /// consolation.</b> A 202 carries the flight the declaration rides and who
    /// decides; a 200 carries the entry, and means the name was already there.
    /// ADR-0016 § 6 makes a registration a widening unconditionally - reach
    /// that did not exist a moment ago has no prior version to sit below - so
    /// the 202 is the ordinary path and the 200 is the narrow one.
    /// </para>
    /// <para>
    /// <b>The refusals are the useful part of this door</b> - reserved,
    /// malformed, an unknown role, a parent that does not exist - and each is
    /// composed at the control plane naming the value. They come through
    /// unchanged, because rewording them would be a second opinion about what
    /// is wrong with a name.
    /// </para>
    /// </remarks>
    /// <returns>
    /// The entry when the name is live, or null with <paramref name="pending"/>
    /// set when it rode a flight. Exactly one of the two.
    /// </returns>
    /// <summary>
    /// Charts an environment name, so an envelope may select it.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>202 is the ordinary answer and 200 is the narrow one</b>, read by
    /// status code rather than by which body parsed - <see cref="DeclareNameAsync"/>'s
    /// rule, for its reason: the two bodies are different types and a reader
    /// that tried the live one first would deserialize a pending answer into a
    /// shape with none of its fields set, and report a name as charted.
    /// </para>
    /// <para>
    /// <b>400 carries the door's own sentence.</b> Its declaration says why it
    /// matters: <i>"the registry is what apply refusals point people at, and a
    /// chart that could hold a blank line would make that advice a trap."</i>
    /// Rewording it here would be a second opinion about what is wrong with a
    /// name.
    /// </para>
    /// </remarks>
    /// <returns>
    /// The entry when the name is charted, or null with the pending answer when
    /// it rode a flight. Exactly one of the two.
    /// </returns>
    public async Task<(EnvironmentCharted? Live, RegistrationPending? Pending)>
        ChartEnvironmentAsync(
            string sessionToken,
            ChartEnvironmentRequest body,
            CancellationToken cancellationToken = default)
    {
        using var request = Request(HttpMethod.Post, "/v1/environments", sessionToken);
        request.Content = JsonContent.Create(
            body, ProtocolJsonContext.Default.ChartEnvironmentRequest);

        using var response = await _httpClient.SendAsync(request, cancellationToken);
        await ThrowIfProtocolRefusedAsync(response, cancellationToken);

        if (response.StatusCode == HttpStatusCode.BadRequest)
        {
            throw new EnvelopeRefusedException(
                await response.Content.ReadAsStringAsync(cancellationToken));
        }

        if (response.StatusCode == HttpStatusCode.Forbidden)
        {
            throw new PermissionRefusedException(
                await RefusalAsync(response, cancellationToken)
              + " Charting an environment widens what every envelope in this tenant may "
              + "select, so it is an administrator's act.");
        }

        response.EnsureSuccessStatusCode();

        return response.StatusCode == HttpStatusCode.Accepted
            ? (null, await response.Content.ReadFromJsonAsync(
                  ProtocolJsonContext.Default.RegistrationPending, cancellationToken))
            : (await response.Content.ReadFromJsonAsync(
                  ProtocolJsonContext.Default.EnvironmentCharted, cancellationToken), null);
    }

    public async Task<(TopologyName? Live, RegistrationPending? Pending)> DeclareNameAsync(
        string sessionToken,
        DeclareNameRequest body,
        CancellationToken cancellationToken = default)
    {
        using var request = Request(HttpMethod.Post, "/v1/airspace/names", sessionToken);
        request.Content = JsonContent.Create(body, ProtocolJsonContext.Default.DeclareNameRequest);

        using var response = await _httpClient.SendAsync(request, cancellationToken);
        await ThrowIfProtocolRefusedAsync(response, cancellationToken);

        if (response.StatusCode == HttpStatusCode.BadRequest)
        {
            throw new EnvelopeRefusedException(
                await response.Content.ReadAsStringAsync(cancellationToken));
        }

        response.EnsureSuccessStatusCode();

        // 202 IS READ BY ITS CODE, not by which member came back non-null. The
        // two bodies are different types and a reader that tried the entry
        // first would deserialize a pending answer into a shape with none of
        // its fields set and report a name as live.
        if (response.StatusCode == HttpStatusCode.Accepted)
        {
            return (null, await response.Content.ReadFromJsonAsync(
                ProtocolJsonContext.Default.RegistrationPending, cancellationToken)
                ?? throw new InvalidOperationException("Control plane acknowledged nothing."));
        }

        return (await response.Content.ReadFromJsonAsync(
            ProtocolJsonContext.Default.TopologyName, cancellationToken)
            ?? throw new InvalidOperationException("Control plane acknowledged nothing."), null);
    }

    /// <summary>
    /// Retires a name by applying a terminal version of it.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>A VERSION, NOT A DELETION</b> (ADR-0014). Retiring by removing a
    /// topology entry would be a governance-critical change wearing
    /// bookkeeping's clothes: the constraint stops attaching and no version
    /// records that it did.
    /// </para>
    /// <para>
    /// <b>NO 200 EXISTS ON THIS DOOR.</b> A document that stops applying
    /// removes every constraint in it at once, so it is a widening by
    /// construction and always rides the gate. The answer is 202 and it names
    /// the flight and the approver - so the name still governs when this
    /// returns, which the caller has to say out loud.
    /// </para>
    /// <para>
    /// <b>No body.</b> The name is the whole request, in the path.
    /// </para>
    /// </remarks>
    public async Task<EnvelopeApplied> RetireNamedAsync(
        string sessionToken,
        string name,
        CancellationToken cancellationToken = default)
    {
        using var request = Request(
            HttpMethod.Post,
            $"/v1/airspace/envelopes/{Uri.EscapeDataString(name)}/retirement",
            sessionToken);

        using var response = await _httpClient.SendAsync(request, cancellationToken);
        await ThrowIfProtocolRefusedAsync(response, cancellationToken);

        if (response.StatusCode is HttpStatusCode.BadRequest or HttpStatusCode.Conflict)
        {
            throw new EnvelopeRefusedException(
                await response.Content.ReadAsStringAsync(cancellationToken));
        }

        response.EnsureSuccessStatusCode();

        return await response.Content.ReadFromJsonAsync(
            ProtocolJsonContext.Default.EnvelopeApplied, cancellationToken)
            ?? throw new InvalidOperationException("Control plane acknowledged nothing.");
    }

    public async Task<EnvelopeApplied> ApplyNamedAsync(
        string sessionToken,
        string name,
        NamedEnvelopeApply body,
        string? basedOn = null,
        CancellationToken cancellationToken = default)
    {
        var path = $"/v1/airspace/envelopes/{Uri.EscapeDataString(name)}";
        if (basedOn is { Length: > 0 })
        {
            path += $"?based-on={Uri.EscapeDataString(basedOn)}";
        }

        using var request = Request(HttpMethod.Put, path, sessionToken);
        request.Content = JsonContent.Create(body, ProtocolJsonContext.Default.NamedEnvelopeApply);

        using var response = await _httpClient.SendAsync(request, cancellationToken);
        await ThrowIfProtocolRefusedAsync(response, cancellationToken);

        if (response.StatusCode is HttpStatusCode.BadRequest or HttpStatusCode.Conflict)
        {
            // The control plane's own diagnosis, carried through unchanged - and
            // a 409 is the stale-working-copy refusal, which names both versions
            // because how far behind you are decides what you do next.
            throw new EnvelopeRefusedException(
                await response.Content.ReadAsStringAsync(cancellationToken));
        }

        response.EnsureSuccessStatusCode();

        return await response.Content.ReadFromJsonAsync(
            ProtocolJsonContext.Default.EnvelopeApplied, cancellationToken)
            ?? throw new InvalidOperationException("Control plane acknowledged nothing.");
    }

    /// <summary>
    /// Applies a strategy to its topology name. The wire is JSON for the same
    /// reason the envelope's is - the text form and its parser stay on this
    /// side of the boundary.
    /// </summary>
    public async Task<EnvelopeApplied> ApplyStrategyAsync(
        string sessionToken, string name, EnvironmentStrategy strategy,
        CancellationToken cancellationToken = default)
    {
        using var request = Request(
            HttpMethod.Put, $"/v1/airspace/strategies/{Uri.EscapeDataString(name)}", sessionToken);
        request.Content = JsonContent.Create(strategy, ProtocolJsonContext.Default.EnvironmentStrategy);

        using var response = await _httpClient.SendAsync(request, cancellationToken);
        await ThrowIfProtocolRefusedAsync(response, cancellationToken);

        if (response.StatusCode == HttpStatusCode.BadRequest)
        {
            // The control plane's own diagnosis, carried through unchanged -
            // both sides fail closed on their own format.
            throw new StrategyRefusedException(
                await response.Content.ReadAsStringAsync(cancellationToken));
        }

        response.EnsureSuccessStatusCode();

        return await response.Content.ReadFromJsonAsync(
            ProtocolJsonContext.Default.EnvelopeApplied, cancellationToken)
            ?? throw new InvalidOperationException("Control plane acknowledged nothing.");
    }

    /// <summary>The strategy in force for a name, or null when none is.</summary>
    public async Task<EnvironmentStrategyState?> GetStrategyAsync(
        string sessionToken, string name, CancellationToken cancellationToken = default)
    {
        using var request = Request(
            HttpMethod.Get, $"/v1/airspace/strategies/{Uri.EscapeDataString(name)}", sessionToken);

        using var response = await _httpClient.SendAsync(request, cancellationToken);
        await ThrowIfProtocolRefusedAsync(response, cancellationToken);

        if (response.StatusCode == HttpStatusCode.NotFound)
        {
            return null;
        }

        response.EnsureSuccessStatusCode();

        return await response.Content.ReadFromJsonAsync(
            ProtocolJsonContext.Default.EnvironmentStrategyState, cancellationToken);
    }

    /// <summary>Every strategy in force for the tenant.</summary>
    /// <summary>
    /// Every named envelope document in force — what <c>pull</c> reads.
    /// </summary>
    /// <remarks>
    /// Strategies are a second read, because they have their own door and their
    /// own shape. <see cref="ReadEstateAsync"/> joins the two.
    /// </remarks>
    public async Task<NamedEnvelopeList> ListEnvelopesAsync(
        string sessionToken, CancellationToken cancellationToken = default)
    {
        using var request = Request(HttpMethod.Get, "/v1/airspace/envelopes", sessionToken);

        using var response = await _httpClient.SendAsync(request, cancellationToken);
        await ThrowIfProtocolRefusedAsync(response, cancellationToken);
        response.EnsureSuccessStatusCode();

        return await response.Content.ReadFromJsonAsync(
            ProtocolJsonContext.Default.NamedEnvelopeList, cancellationToken)
            ?? throw new InvalidOperationException("Control plane acknowledged nothing.");
    }

    /// <summary>The whole estate: every document class, in one answer.</summary>
    public async Task<AirspaceEstate> ReadEstateAsync(
        string sessionToken, CancellationToken cancellationToken = default) =>
        new()
        {
            Documents = (await ListEnvelopesAsync(sessionToken, cancellationToken)).Documents,
            Strategies = (await ListStrategiesAsync(sessionToken, cancellationToken)).Strategies,
            // A THIRD READ, because a watch has its own door too. An estate that
            // never carried watches rendered a working copy with none in it, and
            // the apply after that pull would have read as a person deleting
            // every one.
            Watches = (await ListWatchesAsync(sessionToken, cancellationToken)).Watches,
            // AND A FOURTH, for fleet profiles (slice forty-three).
            Profiles = (await ListFleetProfilesAsync(sessionToken, cancellationToken)).Profiles,
        };

    /// <summary>Every fleet profile in force for the tenant.</summary>
    /// <remarks>
    /// <b>Empty from a control plane that has no such door</b> - a 404 on the
    /// list itself, which is how one pinned below 0.200.0 answers. A pull must
    /// keep working against it, and it holds no profiles to lose.
    /// </remarks>
    public async Task<FleetProfileList> ListFleetProfilesAsync(
        string sessionToken, CancellationToken cancellationToken = default)
    {
        using var request = Request(HttpMethod.Get, "/v1/airspace/fleet", sessionToken);

        using var response = await _httpClient.SendAsync(request, cancellationToken);
        await ThrowIfProtocolRefusedAsync(response, cancellationToken);

        if (response.StatusCode == HttpStatusCode.NotFound)
        {
            return new FleetProfileList { Profiles = [] };
        }

        response.EnsureSuccessStatusCode();

        return await response.Content.ReadFromJsonAsync(
            ProtocolJsonContext.Default.FleetProfileList, cancellationToken)
            ?? throw new InvalidOperationException("Control plane acknowledged nothing.");
    }

    /// <summary>Applies a fleet profile to its topology name, through the fleet door.</summary>
    /// <remarks>JSON on the wire, as a strategy's is; a 400 carries the control plane's own diagnosis.</remarks>
    public async Task<EnvelopeApplied> ApplyFleetProfileAsync(
        string sessionToken, string name, FleetProfile profile,
        CancellationToken cancellationToken = default)
    {
        using var request = Request(
            HttpMethod.Put, $"/v1/airspace/fleet/{Uri.EscapeDataString(name)}", sessionToken);
        request.Content = JsonContent.Create(profile, ProtocolJsonContext.Default.FleetProfile);

        using var response = await _httpClient.SendAsync(request, cancellationToken);
        await ThrowIfProtocolRefusedAsync(response, cancellationToken);

        if (response.StatusCode == HttpStatusCode.BadRequest)
        {
            throw new StrategyRefusedException(
                await response.Content.ReadAsStringAsync(cancellationToken));
        }

        response.EnsureSuccessStatusCode();

        return await response.Content.ReadFromJsonAsync(
            ProtocolJsonContext.Default.EnvelopeApplied, cancellationToken)
            ?? throw new InvalidOperationException("Control plane acknowledged nothing.");
    }

    public async Task<StrategyList> ListStrategiesAsync(
        string sessionToken, CancellationToken cancellationToken = default)
    {
        using var request = Request(HttpMethod.Get, "/v1/airspace/strategies", sessionToken);

        using var response = await _httpClient.SendAsync(request, cancellationToken);
        await ThrowIfProtocolRefusedAsync(response, cancellationToken);
        response.EnsureSuccessStatusCode();

        return await response.Content.ReadFromJsonAsync(
            ProtocolJsonContext.Default.StrategyList, cancellationToken)
            ?? throw new InvalidOperationException("Control plane acknowledged nothing.");
    }

    /// <summary>
    /// Applies a watch to its topology name, through the watch door.
    /// </summary>
    /// <remarks>
    /// <b>JSON on the wire, as a strategy's is</b> - the text form and its
    /// parser stay on this side of the boundary. A 400 carries the control
    /// plane's own diagnosis, which for a document the schema refuses is
    /// <c>WatchDocument.Validate</c>'s sentence: the same one
    /// <c>gg envelope validate</c> would have given.
    /// </remarks>
    public async Task<EnvelopeApplied> ApplyWatchAsync(
        string sessionToken, string name, WatchDocument watch,
        CancellationToken cancellationToken = default)
    {
        using var request = Request(
            HttpMethod.Put, $"/v1/airspace/watches/{Uri.EscapeDataString(name)}", sessionToken);
        request.Content = JsonContent.Create(watch, ProtocolJsonContext.Default.WatchDocument);

        using var response = await _httpClient.SendAsync(request, cancellationToken);
        await ThrowIfProtocolRefusedAsync(response, cancellationToken);

        if (response.StatusCode == HttpStatusCode.BadRequest)
        {
            throw new EnvelopeRefusedException(
                await response.Content.ReadAsStringAsync(cancellationToken));
        }

        response.EnsureSuccessStatusCode();

        return await response.Content.ReadFromJsonAsync(
            ProtocolJsonContext.Default.EnvelopeApplied, cancellationToken)
            ?? throw new InvalidOperationException("Control plane acknowledged nothing.");
    }

    /// <summary>The watch in force for a name, or null when none is.</summary>
    public async Task<WatchState?> GetWatchAsync(
        string sessionToken, string name, CancellationToken cancellationToken = default)
    {
        using var request = Request(
            HttpMethod.Get, $"/v1/airspace/watches/{Uri.EscapeDataString(name)}", sessionToken);

        using var response = await _httpClient.SendAsync(request, cancellationToken);
        await ThrowIfProtocolRefusedAsync(response, cancellationToken);

        if (response.StatusCode == HttpStatusCode.NotFound)
        {
            return null;
        }

        response.EnsureSuccessStatusCode();

        return await response.Content.ReadFromJsonAsync(
            ProtocolJsonContext.Default.WatchState, cancellationToken);
    }

    /// <summary>Every watch in force for the tenant. Empty is a state, not an error.</summary>
    public async Task<WatchList> ListWatchesAsync(
        string sessionToken, CancellationToken cancellationToken = default)
    {
        using var request = Request(HttpMethod.Get, "/v1/airspace/watches", sessionToken);

        using var response = await _httpClient.SendAsync(request, cancellationToken);
        await ThrowIfProtocolRefusedAsync(response, cancellationToken);
        response.EnsureSuccessStatusCode();

        return await response.Content.ReadFromJsonAsync(
            ProtocolJsonContext.Default.WatchList, cancellationToken)
            ?? throw new InvalidOperationException("Control plane acknowledged nothing.");
    }

    /// <summary>
    /// How every watch in force is doing: what would run it, what it last
    /// said, and what it has spent.
    /// </summary>
    /// <remarks>
    /// <b>A second read beside <see cref="ListWatchesAsync"/>, not a widening
    /// of it.</b> That one answers what is in force and is what an estate is
    /// pulled from; this one answers how it is going, and the two change on
    /// completely different clocks.
    /// </remarks>
    public async Task<WatchStandingList> WatchStandingsAsync(
        string sessionToken, CancellationToken cancellationToken = default)
    {
        using var request = Request(HttpMethod.Get, "/v1/airspace/watch-standings", sessionToken);

        using var response = await _httpClient.SendAsync(request, cancellationToken);
        await ThrowIfProtocolRefusedAsync(response, cancellationToken);
        response.EnsureSuccessStatusCode();

        return await response.Content.ReadFromJsonAsync(
            ProtocolJsonContext.Default.WatchStandingList, cancellationToken)
            ?? throw new InvalidOperationException("Control plane acknowledged nothing.");
    }

    /// <summary>
    /// The tenant-level plan: what a flight opened now would need, priced
    /// against the live fleet. Null when no envelope has ever been applied.
    /// </summary>
    public async Task<Checklist?> GetPlanAsync(
        string sessionToken, CancellationToken cancellationToken = default)
    {
        using var request = Request(HttpMethod.Get, "/v1/envelope/plan", sessionToken);
        using var response = await _httpClient.SendAsync(request, cancellationToken);
        await ThrowIfProtocolRefusedAsync(response, cancellationToken);

        if (response.StatusCode == HttpStatusCode.NotFound)
        {
            return null;
        }

        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync(
            ProtocolJsonContext.Default.Checklist, cancellationToken);
    }

    /// <summary>The tenant's topology: every envelope name that exists, root first.</summary>
    /// <remarks>Never null and never empty - root is synthesized by the read.</remarks>
    public async Task<EnvelopeTopology> GetTopologyAsync(
        string sessionToken, CancellationToken cancellationToken = default)
    {
        using var request = Request(HttpMethod.Get, "/v1/airspace/topology", sessionToken);
        using var response = await _httpClient.SendAsync(request, cancellationToken);
        await ThrowIfProtocolRefusedAsync(response, cancellationToken);
        response.EnsureSuccessStatusCode();

        return await response.Content.ReadFromJsonAsync(
            ProtocolJsonContext.Default.EnvelopeTopology, cancellationToken)
            ?? throw new InvalidOperationException("Control plane answered with no topology.");
    }

    /// <summary>
    /// The repositories this tenant has registered.
    /// </summary>
    /// <remarks>
    /// <b>The control plane has served this the whole time and nothing here
    /// asked.</b> <c>GET /v1/airspace/repositories</c> exists and returns
    /// <see cref="RegisteredRepositories"/>; the console wraps
    /// <c>AirspaceAsync</c>, which is the TOPOLOGY - envelope names and roles -
    /// and answers a different question. A person wanting to know what they can
    /// fly against had no way to ask.
    /// </remarks>
    /// <summary>
    /// Registers a repository, or rides the gate that decides whether it may be.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>The door three refusals already point at.</b> A flight whose intent
    /// names an unregistered repository is refused pointing HERE, and until now
    /// nothing in gg could knock on it — so the refusal named a door only
    /// something else could open.
    /// </para>
    /// <para>
    /// <b>202 is the ordinary answer.</b> Registering a NEW repository widens
    /// what the tenant can reach, so it rides a flight and the answer names who
    /// decides. The one 200 left is an entry already live and identical, which
    /// is the idempotent re-run.
    /// </para>
    /// <para>
    /// <b>Read by STATUS CODE rather than by which body parsed</b>, which is
    /// <see cref="DeclareNameAsync"/>'s rule: the two bodies are different
    /// types, and a reader that tried the live one first would deserialize a
    /// pending answer into a shape with none of its fields set and report a
    /// repository as registered.
    /// </para>
    /// <para>
    /// <b>400 carries the door's own sentence.</b> It names which of provider,
    /// forge id and path was blank; rewording it here would be a second opinion
    /// about somebody else's rule.
    /// </para>
    /// </remarks>
    /// <returns>
    /// The entry when it is live, or null with the pending answer when it rode
    /// a flight. Exactly one of the two.
    /// </returns>
    public async Task<(RepositoryRegistered? Live, RegistrationPending? Pending)>
        RegisterRepositoryAsync(
            string sessionToken,
            RegisterRepositoryRequest body,
            CancellationToken cancellationToken = default)
    {
        using var request = Request(HttpMethod.Post, "/v1/airspace/repositories", sessionToken);
        request.Content = JsonContent.Create(
            body, ProtocolJsonContext.Default.RegisterRepositoryRequest);

        using var response = await _httpClient.SendAsync(request, cancellationToken);
        await ThrowIfProtocolRefusedAsync(response, cancellationToken);

        if (response.StatusCode == HttpStatusCode.BadRequest)
        {
            throw new EnvelopeRefusedException(
                await response.Content.ReadAsStringAsync(cancellationToken));
        }

        // THE ROLE ANSWER, AND IT IS AN ANSWER RATHER THAN A FAULT. Left to
        // EnsureSuccessStatusCode this became an HttpRequestException, which
        // the emitter prints as "could not reach the control plane - try gg
        // doctor": a person told to diagnose a network that just answered
        // them. Who may register is decided there and never here, so gg's job
        // is to carry the sentence and say who can.
        if (response.StatusCode == HttpStatusCode.Forbidden)
        {
            throw new PermissionRefusedException(
                await RefusalAsync(response, cancellationToken)
              + " Registering a repository widens what this tenant can reach, so it is an "
              + "administrator's to do - ask one of yours to run it, or to grant you the role.");
        }

        response.EnsureSuccessStatusCode();

        if (response.StatusCode == HttpStatusCode.Accepted)
        {
            return (null, await response.Content.ReadFromJsonAsync(
                ProtocolJsonContext.Default.RegistrationPending, cancellationToken)
                ?? throw new InvalidOperationException("Control plane acknowledged nothing."));
        }

        return (await response.Content.ReadFromJsonAsync(
            ProtocolJsonContext.Default.RepositoryRegistered, cancellationToken)
            ?? throw new InvalidOperationException("Control plane acknowledged nothing."), null);
    }

    public async Task<RegisteredRepositories> ListRepositoriesAsync(
        string sessionToken, CancellationToken cancellationToken = default)
    {
        using var request = Request(HttpMethod.Get, "/v1/airspace/repositories", sessionToken);
        using var response = await _httpClient.SendAsync(request, cancellationToken);
        await ThrowIfProtocolRefusedAsync(response, cancellationToken);
        response.EnsureSuccessStatusCode();

        return await response.Content.ReadFromJsonAsync(
            ProtocolJsonContext.Default.RegisteredRepositories, cancellationToken)
            ?? throw new InvalidOperationException("Control plane answered with no repositories.");
    }

    /// <summary>One flight's checklist, from the envelope version it pinned, or null.</summary>
    public async Task<Checklist?> GetChecklistAsync(
        string sessionToken, string reference, CancellationToken cancellationToken = default)
    {
        using var request = Request(
            HttpMethod.Get, $"/v1/flights/{reference}/checklist", sessionToken);
        using var response = await _httpClient.SendAsync(request, cancellationToken);
        await ThrowIfProtocolRefusedAsync(response, cancellationToken);

        if (response.StatusCode == HttpStatusCode.NotFound)
        {
            return null;
        }

        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync(
            ProtocolJsonContext.Default.Checklist, cancellationToken);
    }

    /// <summary>One flight, or null if the reference names none.</summary>
    public async Task<FlightSummary?> GetFlightAsync(
        string sessionToken, string reference, CancellationToken cancellationToken = default)
    {
        using var request = Request(HttpMethod.Get, $"/v1/flights/{reference}", sessionToken);
        using var response = await _httpClient.SendAsync(request, cancellationToken);
        await ThrowIfProtocolRefusedAsync(response, cancellationToken);

        if (response.StatusCode == HttpStatusCode.NotFound)
        {
            return null;
        }

        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync(
            ProtocolJsonContext.Default.FlightSummary, cancellationToken);
    }

    /// <summary>
    /// What a flight tried and ruled out, or null if the reference names none.
    /// </summary>
    /// <remarks>
    /// <b>Fetched rather than composed here, and that is the whole change.</b> The
    /// seed used to be built on the machine that ran the flight, from a digest on
    /// its own disk, which is why a stopped flight was resumable by whoever was
    /// sitting at that keyboard and by nobody else. It is composed from facts the
    /// control plane already holds now, so any machine can ask.
    /// </remarks>
    public async Task<TakeSeed?> GetSeedAsync(
        string sessionToken, string reference, CancellationToken cancellationToken = default)
    {
        using var request = Request(HttpMethod.Get, $"/v1/flights/{reference}/seed", sessionToken);
        using var response = await _httpClient.SendAsync(request, cancellationToken);
        await ThrowIfProtocolRefusedAsync(response, cancellationToken);

        if (response.StatusCode == HttpStatusCode.NotFound)
        {
            return null;
        }

        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync(
            ProtocolJsonContext.Default.TakeSeed, cancellationToken);
    }

    /// <summary>A flight's log, or null if the reference names no flight.</summary>
    /// <summary>
    /// A flight's whole story, or null if the reference names none.
    /// </summary>
    /// <remarks>
    /// <b>A second route beside the log, not a replacement for it.</b> The control
    /// plane composes this FROM the log and four other reads; the log stays the
    /// exact record, because three walks grep it and a support bundle carries it.
    /// </remarks>
    public async Task<FlightStory?> GetFlightStoryAsync(
        string sessionToken, string reference, CancellationToken cancellationToken = default)
    {
        using var request = Request(HttpMethod.Get, $"/v1/flights/{reference}/story", sessionToken);
        using var response = await _httpClient.SendAsync(request, cancellationToken);
        await ThrowIfProtocolRefusedAsync(response, cancellationToken);

        if (response.StatusCode == HttpStatusCode.NotFound)
        {
            return null;
        }

        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync(
            ProtocolJsonContext.Default.FlightStory, cancellationToken);
    }

    /// <summary>What this flight actually recorded, or null when there is no such flight.</summary>
    /// <remarks>
    /// <b>The third read about one flight, and the only one a customer audits.</b>
    /// The log is what the control plane did to it and the story folds that for a
    /// reader; this is what the RUNNER shipped. Two of thirteen fact kinds reached
    /// the story and the rest had no reader at all - including the proposal a
    /// scoring flight exists to make, so a flight that claimed a score and
    /// recorded none read exactly like one that did.
    /// </remarks>
    public async Task<FlightFacts?> GetFlightFactsAsync(
        string sessionToken, string reference, CancellationToken cancellationToken = default)
    {
        using var request = Request(HttpMethod.Get, $"/v1/flights/{reference}/facts", sessionToken);
        using var response = await _httpClient.SendAsync(request, cancellationToken);
        await ThrowIfProtocolRefusedAsync(response, cancellationToken);

        if (response.StatusCode == HttpStatusCode.NotFound)
        {
            return null;
        }

        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync(
            ProtocolJsonContext.Default.FlightFacts, cancellationToken);
    }

    public async Task<FlightLog?> GetFlightLogAsync(
        string sessionToken, string reference, CancellationToken cancellationToken = default)
    {
        using var request = Request(HttpMethod.Get, $"/v1/flights/{reference}/log", sessionToken);
        using var response = await _httpClient.SendAsync(request, cancellationToken);
        await ThrowIfProtocolRefusedAsync(response, cancellationToken);

        if (response.StatusCode == HttpStatusCode.NotFound)
        {
            return null;
        }

        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync(ProtocolJsonContext.Default.FlightLog, cancellationToken);
    }

    /// <summary>The tenant's runners, with the state the control plane derived.</summary>
    public async Task<RunnerList> ListRunnersAsync(
        string sessionToken, CancellationToken cancellationToken = default)
    {
        using var request = Request(HttpMethod.Get, "/v1/runners", sessionToken);
        using var response = await _httpClient.SendAsync(request, cancellationToken);
        await ThrowIfProtocolRefusedAsync(response, cancellationToken);
        response.EnsureSuccessStatusCode();

        return await response.Content.ReadFromJsonAsync(ProtocolJsonContext.Default.RunnerList, cancellationToken)
            ?? throw new InvalidOperationException("Control plane returned no runner list.");
    }

    /// <summary>
    /// The tenant's chart: every environment name an envelope may select.
    /// </summary>
    /// <remarks>
    /// <b>Never 404 and never null.</b> A tenant that has charted nothing
    /// answers 200 with an empty list, which the endpoint declares in as many
    /// words: charting nothing is a tenant that is set up and has said nothing,
    /// and that is a different fact from a tenant that does not exist.
    /// </remarks>
    public async Task<EnvironmentChart> ChartAsync(
        string sessionToken, CancellationToken cancellationToken = default)
    {
        using var request = Request(HttpMethod.Get, "/v1/environments", sessionToken);
        using var response = await _httpClient.SendAsync(request, cancellationToken);
        await ThrowIfProtocolRefusedAsync(response, cancellationToken);
        response.EnsureSuccessStatusCode();

        return await response.Content.ReadFromJsonAsync(
                   ProtocolJsonContext.Default.EnvironmentChart, cancellationToken)
            ?? throw new InvalidOperationException("Control plane returned no environment chart.");
    }

    /// <summary>Every managed pool's latest attestation, per pool and action.</summary>
    /// <remarks>
    /// <b>An empty ledger is a state rather than an error</b>, and it is two of
    /// them: a tenant managing no pools is the null strategy, and a pool that
    /// has not come up has attested nothing. The endpoint declares 200 for
    /// both, and what tells them apart is whether a strategy is in force.
    /// </remarks>
    /// <summary>
    /// Asks for a build of a strategy's recipe, and returns what was decided.
    /// </summary>
    /// <remarks>
    /// <b>Slice forty-one.</b> 404 is a strategy that names no recipe and 409 a
    /// build already standing for it; both are facts only the control plane
    /// holds, so both come back as its own sentence rather than ours.
    /// </remarks>
    public async Task<PoolAction> RequestBuildAsync(
        string sessionToken, string strategy, CancellationToken cancellationToken = default)
    {
        using var request = Request(
            HttpMethod.Post,
            $"/v1/airspace/strategies/{Uri.EscapeDataString(strategy)}/builds",
            sessionToken);

        using var response = await _httpClient.SendAsync(request, cancellationToken);
        await ThrowIfProtocolRefusedAsync(response, cancellationToken);

        if (response.StatusCode is HttpStatusCode.NotFound or HttpStatusCode.Conflict)
        {
            throw new EnvelopeRefusedException(
                await response.Content.ReadAsStringAsync(cancellationToken));
        }

        response.EnsureSuccessStatusCode();

        return await response.Content.ReadFromJsonAsync(
                   ProtocolJsonContext.Default.PoolAction, cancellationToken)
            ?? throw new InvalidOperationException("Control plane decided nothing it would name.");
    }

    public async Task<PoolLedger> PoolsAsync(
        string sessionToken, CancellationToken cancellationToken = default)
    {
        using var request = Request(HttpMethod.Get, "/v1/pools", sessionToken);
        using var response = await _httpClient.SendAsync(request, cancellationToken);
        await ThrowIfProtocolRefusedAsync(response, cancellationToken);
        response.EnsureSuccessStatusCode();

        return await response.Content.ReadFromJsonAsync(
                   ProtocolJsonContext.Default.PoolLedger, cancellationToken)
            ?? throw new InvalidOperationException("Control plane returned no pool ledger.");
    }

    /// <summary>Makes a principal an administrator of their tenant.</summary>
    /// <remarks>
    /// <b>204 and nothing back.</b> The interesting answers are refusals - 403
    /// once the tenant already has one, 404 for a principal it does not have -
    /// and they arrive as the status with the server's own sentence.
    /// </remarks>
    public async Task GrantAdminAsync(
        string sessionToken, string principalId, bool granted,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(principalId);

        using var request = Request(
            granted ? HttpMethod.Post : HttpMethod.Delete,
            $"/v1/principals/{Uri.EscapeDataString(principalId)}/admin",
            sessionToken);

        using var response = await _httpClient.SendAsync(request, cancellationToken);
        await ThrowIfProtocolRefusedAsync(response, cancellationToken);

        // THE REFUSAL IS THE ANSWER ON THIS ROUTE. Success is 204 and no body,
        // so everything worth reading arrives as a status with a sentence
        // beside it - and the three sentences are three different
        // instructions. EnsureSuccessStatusCode keeps the number and discards
        // the part a person can act on.
        if (!response.IsSuccessStatusCode)
        {
            throw new AdminRefusedException(
                await RefusalAsync(response, cancellationToken));
        }
    }

    /// <summary>
    /// The control plane's own sentence, or the status when it sent none.
    /// </summary>
    /// <remarks>
    /// <b>ProblemDetails' <c>detail</c>, which is where <c>Results.Problem</c>
    /// puts it.</b> A body that is not one - stripped by a proxy, or an older
    /// control plane answering with nothing - falls back to naming the status,
    /// because an empty message would be worse than the bare code this
    /// replaced.
    /// </remarks>
    private static async Task<string> RefusalAsync(
        HttpResponseMessage response, CancellationToken cancellationToken)
    {
        var body = await response.Content.ReadAsStringAsync(cancellationToken);

        try
        {
            using var read = System.Text.Json.JsonDocument.Parse(body);

            if (read.RootElement.ValueKind == System.Text.Json.JsonValueKind.Object
                && read.RootElement.TryGetProperty("detail", out var detail)
                && detail.GetString() is { Length: > 0 } said)
            {
                return said;
            }
        }
        catch (System.Text.Json.JsonException)
        {
            // Not a problem document. Fall through to the status, which is
            // still more than nothing.
        }

        return $"The control plane refused with {(int)response.StatusCode} and said nothing "
             + "about why.";
    }

    /// <summary>What each allowance the fleet spends from has left.</summary>
    /// <remarks>
    /// <b>The fleet's, not this machine's.</b> Every machine reports what its
    /// own transcripts say; this is the sum, per allowance, including machines
    /// the caller has never seen.
    /// </remarks>
    public async Task<AllowanceList> ListAllowancesAsync(
        string sessionToken, CancellationToken cancellationToken = default)
    {
        using var request = Request(HttpMethod.Get, "/v1/allowances", sessionToken);
        using var response = await _httpClient.SendAsync(request, cancellationToken);
        await ThrowIfProtocolRefusedAsync(response, cancellationToken);
        response.EnsureSuccessStatusCode();

        return await response.Content.ReadFromJsonAsync(
            ProtocolJsonContext.Default.AllowanceList, cancellationToken)
            ?? throw new InvalidOperationException("Control plane returned no allowance list.");
    }

    /// <summary>
    /// Reports what the subscription this person speaks for has spent.
    /// </summary>
    /// <remarks>
    /// <b>The same document a runner posts, on the person's own credential.</b>
    /// An allowance is a subscription, and a machine reading its own meter
    /// learns something about the PLAN rather than about itself - so a laptop
    /// that never takes a flight can still say what is left. 202 and nothing
    /// back, like the machine's route.
    /// </remarks>
    public async Task ReportMyAllowanceAsync(
        string sessionToken, AllowanceReading reading,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(reading);

        using var request = Request(
            HttpMethod.Post, "/v1/allowances/readings/mine", sessionToken);
        request.Content = JsonContent.Create(
            reading, ProtocolJsonContext.Default.AllowanceReading);

        using var response = await _httpClient.SendAsync(request, cancellationToken);
        await ThrowIfProtocolRefusedAsync(response, cancellationToken);
        response.EnsureSuccessStatusCode();
    }

    /// <summary>Sets what an allowance's owners keep back.</summary>
    /// <remarks>
    /// <b>204 and nothing back</b>, so a caller that wants to see the effect
    /// reads the list afterwards. That is one extra round trip and it is worth
    /// it: the answer a person wants is what the fleet looks like now, not an
    /// echo of what they typed.
    /// </remarks>
    public async Task SetFloorAsync(
        string sessionToken, string allowance, AllowanceFloor floor,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(allowance);
        ArgumentNullException.ThrowIfNull(floor);

        using var request = Request(
            HttpMethod.Put, $"/v1/allowances/{Uri.EscapeDataString(allowance)}/floor",
            sessionToken);
        request.Content = JsonContent.Create(floor, ProtocolJsonContext.Default.AllowanceFloor);

        using var response = await _httpClient.SendAsync(request, cancellationToken);
        await ThrowIfProtocolRefusedAsync(response, cancellationToken);
        response.EnsureSuccessStatusCode();
    }

    /// <summary>Clears a floor. Clearing one nobody set is the asked-for state.</summary>
    public async Task ClearFloorAsync(
        string sessionToken, string allowance, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(allowance);

        using var request = Request(
            HttpMethod.Delete, $"/v1/allowances/{Uri.EscapeDataString(allowance)}/floor",
            sessionToken);

        using var response = await _httpClient.SendAsync(request, cancellationToken);
        await ThrowIfProtocolRefusedAsync(response, cancellationToken);
        response.EnsureSuccessStatusCode();
    }

    /// <summary>Spends a floor somebody else set, for a while, with a reason.</summary>
    public async Task OverrideFloorAsync(
        string sessionToken, string allowance, AllowanceOverrideRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(allowance);
        ArgumentNullException.ThrowIfNull(request);

        using var message = Request(
            HttpMethod.Post, $"/v1/allowances/{Uri.EscapeDataString(allowance)}/override",
            sessionToken);
        message.Content = JsonContent.Create(
            request, ProtocolJsonContext.Default.AllowanceOverrideRequest);

        using var response = await _httpClient.SendAsync(message, cancellationToken);
        await ThrowIfProtocolRefusedAsync(response, cancellationToken);
        response.EnsureSuccessStatusCode();
    }

    /// <summary>Opens a flight. Answers 202: the number is minted afterwards.</summary>
    public async Task<FlightLaunched> LaunchFlightAsync(
        string sessionToken, FlightLaunchRequest launch, CancellationToken cancellationToken = default)
    {
        using var request = Request(HttpMethod.Post, "/v1/flights", sessionToken);
        request.Content = JsonContent.Create(launch, ProtocolJsonContext.Default.FlightLaunchRequest);

        using var response = await _httpClient.SendAsync(request, cancellationToken);
        await ThrowIfProtocolRefusedAsync(response, cancellationToken);

        if (response.StatusCode == HttpStatusCode.BadRequest)
        {
            // Article XI reaching the person: the control plane refused with a
            // diagnosis, and swallowing it into "bad request" would lose the
            // only part they can act on.
            throw new FlightIntentException(
                (await response.Content.ReadAsStringAsync(cancellationToken)).Trim());
        }

        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync(
            ProtocolJsonContext.Default.FlightLaunched, cancellationToken)
            ?? throw new InvalidOperationException("Control plane opened no flight.");
    }

    /// <summary>
    /// Registers a reference to a credential the developer stored locally.
    /// </summary>
    /// <remarks>
    /// The request type has no field capable of carrying secret material, so
    /// there is nothing this method could send even if somebody wanted it to.
    /// A 400 is the control plane refusing the reference - a kind that is not
    /// local, a scope wider than read - and the diagnosis is the actionable
    /// part, so it is carried through rather than collapsed into a status.
    /// </remarks>
    public async Task<CredentialRegistered> RegisterCredentialAsync(
        string sessionToken, CredentialRegistrationRequest registration,
        CancellationToken cancellationToken = default)
    {
        using var request = Request(HttpMethod.Post, "/v1/credentials", sessionToken);
        request.Content = JsonContent.Create(
            registration, ProtocolJsonContext.Default.CredentialRegistrationRequest);

        using var response = await _httpClient.SendAsync(request, cancellationToken);
        await ThrowIfProtocolRefusedAsync(response, cancellationToken);

        if (response.StatusCode == HttpStatusCode.BadRequest)
        {
            throw new CredentialRefusedException(
                (await response.Content.ReadAsStringAsync(cancellationToken)).Trim());
        }

        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync(
            ProtocolJsonContext.Default.CredentialRegistered, cancellationToken)
            ?? throw new InvalidOperationException("Control plane registered no credential.");
    }

    /// <summary>Every credential reference this tenant has registered.</summary>
    public async Task<CredentialList> ListCredentialsAsync(
        string sessionToken, CancellationToken cancellationToken = default)
    {
        using var request = Request(HttpMethod.Get, "/v1/credentials", sessionToken);
        using var response = await _httpClient.SendAsync(request, cancellationToken);
        await ThrowIfProtocolRefusedAsync(response, cancellationToken);
        response.EnsureSuccessStatusCode();

        return await response.Content.ReadFromJsonAsync(
            ProtocolJsonContext.Default.CredentialList, cancellationToken)
            ?? throw new InvalidOperationException("Control plane returned no credential list.");
    }

    /// <summary>Deregisters a credential, or null if the id names none.</summary>
    /// <summary>
    /// Takes a runner out of the fleet. Null when this tenant has no such runner.
    /// </summary>
    /// <remarks>
    /// <b>Idempotent, and it answers with WHEN.</b> Retiring one already retired
    /// is the state the caller asked for rather than a conflict, and the instant
    /// that comes back is the existing one - which is how a person finds out
    /// somebody else got there first.
    /// </remarks>
    public async Task<RunnerRetired?> RetireRunnerAsync(
        string sessionToken, string runnerId, CancellationToken cancellationToken = default)
    {
        using var request = Request(
            HttpMethod.Post, $"/v1/runners/{runnerId}/retirement", sessionToken);
        request.Content = JsonContent.Create(
            new RunnerRetirementRequest(), ProtocolJsonContext.Default.RunnerRetirementRequest);

        using var response = await _httpClient.SendAsync(request, cancellationToken);
        await ThrowIfProtocolRefusedAsync(response, cancellationToken);

        if (response.StatusCode == HttpStatusCode.NotFound)
        {
            return null;
        }

        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync(
            ProtocolJsonContext.Default.RunnerRetired, cancellationToken);
    }

    /// <summary>Says what this tenant is called.</summary>
    /// <remarks>
    /// <b>The tenant is the session's and is named nowhere in this call.</b> A
    /// caller may be a tenant and may never name one, so there is no id in the
    /// path and none in the body - which also means there is no way to ask
    /// this question about somebody else's tenant.
    /// </remarks>
    public async Task RenameTenantAsync(
        string sessionToken, string name, CancellationToken cancellationToken = default)
    {
        using var request = Request(HttpMethod.Put, "/v1/tenant/name", sessionToken);
        request.Content = JsonContent.Create(
            new TenantNameRequest { Name = name },
            ProtocolJsonContext.Default.TenantNameRequest);

        using var response = await _httpClient.SendAsync(request, cancellationToken);
        await ThrowIfProtocolRefusedAsync(response, cancellationToken);

        response.EnsureSuccessStatusCode();
    }

    /// <summary>Mints an enrollment token (slice forty-three, rules 17 and 18).</summary>
    /// <remarks>A refusal - a bound, a profile not in force, a tenant token from a non-admin - is the control plane's sentence.</remarks>
    public async Task<EnrollmentTokenMinted> MintEnrollmentTokenAsync(
        string sessionToken, EnrollmentTokenRequest asked, CancellationToken cancellationToken = default)
    {
        using var request = Request(HttpMethod.Post, "/v1/fleet/tokens", sessionToken);
        request.Content = JsonContent.Create(asked, ProtocolJsonContext.Default.EnrollmentTokenRequest);

        using var response = await _httpClient.SendAsync(request, cancellationToken);
        await ThrowIfProtocolRefusedAsync(response, cancellationToken);

        if (response.StatusCode is HttpStatusCode.BadRequest or HttpStatusCode.Forbidden
                                or HttpStatusCode.NotFound)
        {
            throw new EnrollmentRefusedException(await RefusalAsync(response, cancellationToken));
        }

        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync(
            ProtocolJsonContext.Default.EnrollmentTokenMinted, cancellationToken)
            ?? throw new InvalidOperationException("Control plane minted nothing.");
    }

    /// <summary>This tenant's enrollment tokens, never their secrets.</summary>
    public async Task<EnrollmentTokenList> ListEnrollmentTokensAsync(
        string sessionToken, CancellationToken cancellationToken = default)
    {
        using var request = Request(HttpMethod.Get, "/v1/fleet/tokens", sessionToken);

        using var response = await _httpClient.SendAsync(request, cancellationToken);
        await ThrowIfProtocolRefusedAsync(response, cancellationToken);
        response.EnsureSuccessStatusCode();

        return await response.Content.ReadFromJsonAsync(
            ProtocolJsonContext.Default.EnrollmentTokenList, cancellationToken)
            ?? new EnrollmentTokenList { Tokens = [] };
    }

    /// <summary>Revokes an enrollment token. Null when this tenant has no such token.</summary>
    public async Task<EnrollmentTokenSummary?> RevokeEnrollmentTokenAsync(
        string sessionToken, string tokenId, CancellationToken cancellationToken = default)
    {
        using var request = Request(
            HttpMethod.Post, $"/v1/fleet/tokens/{Uri.EscapeDataString(tokenId)}/revocation", sessionToken);
        request.Content = JsonContent.Create(
            new EnrollmentTokenRevocation(), ProtocolJsonContext.Default.EnrollmentTokenRevocation);

        using var response = await _httpClient.SendAsync(request, cancellationToken);
        await ThrowIfProtocolRefusedAsync(response, cancellationToken);

        if (response.StatusCode == HttpStatusCode.NotFound)
        {
            return null;
        }

        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync(
            ProtocolJsonContext.Default.EnrollmentTokenSummary, cancellationToken);
    }

    /// <summary>
    /// Enrolls this machine with a token: anonymous, authorized by the token
    /// alone (rule 19). A refusal is one sentence whatever the reason, so a
    /// token cannot be probed.
    /// </summary>
    public async Task<RunnerEnrolled> EnrollAsync(
        string token, string label, CancellationToken cancellationToken = default,
        string? publicKey = null, string? machine = null)
    {
        using var request = Request(HttpMethod.Post, "/v1/runners/enrollments");
        request.Content = JsonContent.Create(
            new RunnerEnrollmentRequest
            {
                Token = token,
                Label = label,
                ProtocolVersion = GgVersions.Protocol,
                PublicKey = publicKey,
                Machine = machine,
            },
            ProtocolJsonContext.Default.RunnerEnrollmentRequest);

        using var response = await _httpClient.SendAsync(request, cancellationToken);
        await ThrowIfProtocolRefusedAsync(response, cancellationToken);

        if (response.StatusCode is HttpStatusCode.Forbidden or HttpStatusCode.BadRequest)
        {
            throw new EnrollmentRefusedException(await RefusalAsync(response, cancellationToken));
        }

        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync(
            ProtocolJsonContext.Default.RunnerEnrolled, cancellationToken)
            ?? throw new InvalidOperationException("Control plane enrolled nothing.");
    }

    /// <summary>Claims a runner for the caller. Null when this tenant has no such runner.</summary>
    /// <remarks>
    /// <b>A refusal is an answer, and it carries the control plane's sentence</b>
    /// - a tenant runner (403) or somebody else's (409) - because the two are
    /// different things to do next and the status alone says neither.
    /// </remarks>
    public Task<RunnerOwnership?> ClaimRunnerAsync(
        string sessionToken, string runnerId, CancellationToken cancellationToken = default) =>
        OwnershipAsync(
            HttpMethod.Post, $"/v1/runners/{Uri.EscapeDataString(runnerId)}/claim", sessionToken,
            JsonContent.Create(new RunnerClaimRequest(), ProtocolJsonContext.Default.RunnerClaimRequest),
            ProtocolJsonContext.Default.RunnerOwnership, cancellationToken);

    /// <summary>Gives a runner up, back to open. Null when this tenant has no such runner.</summary>
    public Task<RunnerOwnership?> UnclaimRunnerAsync(
        string sessionToken, string runnerId, CancellationToken cancellationToken = default) =>
        OwnershipAsync(
            HttpMethod.Delete, $"/v1/runners/{Uri.EscapeDataString(runnerId)}/claim", sessionToken,
            content: null, ProtocolJsonContext.Default.RunnerOwnership, cancellationToken);

    /// <summary>An admin's word: the tenant's, or open. Null when this tenant has no such runner.</summary>
    public Task<RunnerOwnership?> SetRunnerOwnershipAsync(
        string sessionToken, string runnerId, string ownership,
        CancellationToken cancellationToken = default) =>
        OwnershipAsync(
            HttpMethod.Put, $"/v1/runners/{Uri.EscapeDataString(runnerId)}/ownership", sessionToken,
            JsonContent.Create(
                new RunnerOwnershipRequest { Ownership = ownership },
                ProtocolJsonContext.Default.RunnerOwnershipRequest),
            ProtocolJsonContext.Default.RunnerOwnership, cancellationToken);

    /// <summary>Keeps the caller's own runner to the caller's flights. Null when there is no such runner.</summary>
    public Task<RunnerReserved?> ReserveRunnerAsync(
        string sessionToken, string runnerId, CancellationToken cancellationToken = default) =>
        OwnershipAsync(
            HttpMethod.Post, $"/v1/runners/{Uri.EscapeDataString(runnerId)}/reservation", sessionToken,
            JsonContent.Create(
                new RunnerReservationRequest(), ProtocolJsonContext.Default.RunnerReservationRequest),
            ProtocolJsonContext.Default.RunnerReserved, cancellationToken);

    /// <summary>Lets the caller's runner take the tenant's work again. Null when there is no such runner.</summary>
    public Task<RunnerReserved?> ReleaseRunnerAsync(
        string sessionToken, string runnerId, CancellationToken cancellationToken = default) =>
        OwnershipAsync(
            HttpMethod.Delete, $"/v1/runners/{Uri.EscapeDataString(runnerId)}/reservation", sessionToken,
            content: null, ProtocolJsonContext.Default.RunnerReserved, cancellationToken);

    /// <summary>One of the five ownership doors: null for 404, the sentence for 403 and 409.</summary>
    private async Task<T?> OwnershipAsync<T>(
        HttpMethod method, string path, string sessionToken, HttpContent? content,
        System.Text.Json.Serialization.Metadata.JsonTypeInfo<T> answer,
        CancellationToken cancellationToken)
        where T : class
    {
        using var request = Request(method, path, sessionToken);
        request.Content = content;

        using var response = await _httpClient.SendAsync(request, cancellationToken);
        await ThrowIfProtocolRefusedAsync(response, cancellationToken);

        if (response.StatusCode == HttpStatusCode.NotFound)
        {
            return null;
        }

        if (response.StatusCode is HttpStatusCode.Forbidden or HttpStatusCode.Conflict
                                or HttpStatusCode.BadRequest)
        {
            throw new RunnerOwnershipRefusedException(await RefusalAsync(response, cancellationToken));
        }

        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync(answer, cancellationToken);
    }

    /// <summary>
    /// Asks to be introduced to one runner, for one short conversation.
    /// </summary>
    /// <param name="ephemeralPublicKey">
    /// The console's public key for this introduction and no other. The control
    /// plane stores its hash against the row, so whatever seals the offer has to
    /// be the private half of THIS.
    /// </param>
    /// <remarks>
    /// <b>The control plane says WHERE and WHETHER, never WHAT.</b> What comes
    /// back names the runner, says this caller may reach it, and says for how
    /// long. Nothing that passes between the two ends afterwards is readable
    /// here, which is the whole of ADR-0013's decision 3.
    /// </remarks>
    public async Task<Introduced> IntroduceRunnerAsync(
        string sessionToken,
        string runnerId,
        string ephemeralPublicKey,
        // WHAT THE CAPABILITY SHOULD AUTHORISE. Required of the caller rather
        // than defaulted here: every reach knows what it is for, and a default
        // is how the next one is minted for tailing a log while it places a
        // credential. The contract decides what absence means; this side never
        // sends absence.
        string purpose,
        CancellationToken cancellationToken = default)
    {
        using var request = Request(
            HttpMethod.Post, $"/v1/runners/{runnerId}/introduction", sessionToken);
        request.Content = JsonContent.Create(
            new RunnerIntroductionRequest
            {
                EphemeralPublicKey = ephemeralPublicKey,
                Purpose = purpose,
            },
            ProtocolJsonContext.Default.RunnerIntroductionRequest);

        using var response = await _httpClient.SendAsync(request, cancellationToken);
        await ThrowIfProtocolRefusedAsync(response, cancellationToken);

        // THREE REFUSALS, THREE SENTENCES - and the third is the control
        // plane's own, because 409 is TWO refusals rather than one. A runner
        // with no key and a machine that says it will not keep a credential
        // are both answered 409, and this arm used to tell the first story
        // about both: somebody was sent to restart a pool member whose key
        // was registered and fine, over a setting on the machine. Only the
        // far end knows which happened, and it says so in the body.
        switch (response.StatusCode)
        {
            case HttpStatusCode.NotFound:
                return new Introduced(
                    null, IntroductionRefusal.NoSuchRunner,
                    $"There is no runner {runnerId} here. It may have been retired, or the id "
                  + "may belong to another tenant - `gg runners` lists the ones you can see.");

            case HttpStatusCode.Forbidden:
                return new Introduced(
                    null, IntroductionRefusal.NotYoursToReach,
                    $"Runner {runnerId} is in your tenant and somebody else registered it. "
                  + "Reaching a machine means reading what it is doing, so that is theirs to "
                  + "allow - `gg runners` names who registered it.");

            case HttpStatusCode.Conflict:
                return new Introduced(
                    null, IntroductionRefusal.Refused,
                    await RefusalAsync(response, cancellationToken));
        }

        response.EnsureSuccessStatusCode();

        var introduction = await response.Content.ReadFromJsonAsync(
            ProtocolJsonContext.Default.RunnerIntroduction, cancellationToken)
            ?? throw new InvalidOperationException("Control plane introduced nothing.");

        return new Introduced(introduction, IntroductionRefusal.None, "introduced");
    }

    /// <summary>
    /// Leaves a sealed offer for the runner's next heartbeat to take.
    /// </summary>
    /// <returns>False when the introduction is gone.</returns>
    /// <remarks>
    /// <b>202, and nothing here waits for the far end.</b> A route that waited
    /// would put the relay inside the conversation, which is the one thing the
    /// whole design is arranged to prevent.
    /// </remarks>
    public async Task<bool> LeaveOfferAsync(
        string sessionToken,
        string introductionId,
        RunnerSealedOffer offer,
        CancellationToken cancellationToken = default)
    {
        using var request = Request(
            HttpMethod.Post, $"/v1/introductions/{introductionId}", sessionToken);
        request.Content = JsonContent.Create(
            offer, ProtocolJsonContext.Default.RunnerSealedOffer);

        using var response = await _httpClient.SendAsync(request, cancellationToken);
        await ThrowIfProtocolRefusedAsync(response, cancellationToken);

        if (response.StatusCode == HttpStatusCode.NotFound)
        {
            return false;
        }

        response.EnsureSuccessStatusCode();
        return true;
    }

    /// <summary>
    /// Asks whether the runner has answered yet.
    /// </summary>
    /// <remarks>
    /// <b>204 and 404 are different facts and stay different here.</b> Not
    /// answered yet is a reason to ask again; no such introduction is a reason
    /// to stop. A method returning a nullable answer would make the caller wait
    /// out its patience on a conversation that had already ended, and then
    /// report the runner as silent - blaming a machine for a clock.
    /// </remarks>
    public async Task<Collected> CollectAnswerAsync(
        string sessionToken,
        string introductionId,
        CancellationToken cancellationToken = default)
    {
        using var request = Request(
            HttpMethod.Get, $"/v1/introductions/{introductionId}", sessionToken);

        using var response = await _httpClient.SendAsync(request, cancellationToken);
        await ThrowIfProtocolRefusedAsync(response, cancellationToken);

        if (response.StatusCode == HttpStatusCode.NoContent)
        {
            return Collected.NotYet;
        }

        if (response.StatusCode == HttpStatusCode.NotFound)
        {
            return Collected.Gone;
        }

        response.EnsureSuccessStatusCode();

        var answer = await response.Content.ReadFromJsonAsync(
            ProtocolJsonContext.Default.RunnerSealedAnswer, cancellationToken)
            ?? throw new InvalidOperationException("Control plane answered with nothing.");

        return new Collected(answer, AnswerState.Arrived);
    }

    public async Task<CredentialRemoved?> RemoveCredentialAsync(
        string sessionToken, string credentialId, CancellationToken cancellationToken = default)
    {
        using var request = Request(HttpMethod.Delete, $"/v1/credentials/{credentialId}", sessionToken);
        using var response = await _httpClient.SendAsync(request, cancellationToken);
        await ThrowIfProtocolRefusedAsync(response, cancellationToken);

        if (response.StatusCode == HttpStatusCode.NotFound)
        {
            return null;
        }

        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync(
            ProtocolJsonContext.Default.CredentialRemoved, cancellationToken);
    }

    /// <summary>
    /// Why each obligation applied to a flight. Null when there is no such flight.
    /// </summary>
    /// <remarks>
    /// A read, and nothing more. Everything in the answer was decided by the
    /// Engine before it was serialized.
    /// </remarks>
    /// <summary>
    /// Everything waiting on a person.
    /// </summary>
    /// <remarks>
    /// A read, and there is no method beside it that answers one. The absence is the
    /// point: nothing in this client can unstick a flight.
    /// </remarks>
    /// <summary>
    /// Posts a decision, and returns what the control plane made of it.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Posts, never decides.</b> Nothing here marks an obligation satisfied or infers
    /// an admission: the answer comes back with whatever the Engine re-evaluated, and the
    /// client renders it. ADR-0011 - a decision is an input to evaluation, never a
    /// substitute for admission.
    /// </para>
    /// <para>
    /// A 409 means the work moved between being shown and being decided. Surfaced as a
    /// diagnosis rather than swallowed, because the caller approved something specific
    /// and the honest answer is that it is no longer what is there.
    /// </para>
    /// </remarks>
    public async Task<DecisionRecorded?> DecideAsync(
        string sessionToken,
        string reference,
        DecisionRequest decision,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(decision);

        using var request = Request(
            HttpMethod.Post, $"/v1/flights/{reference}/decisions", sessionToken);
        // THE SOURCE-GENERATED CONTEXT, because the reflection overload is refused here:
        // this assembly ships inside an AOT binary, and JsonContent.Create<T> cannot be
        // statically analysed. The build rejects it rather than producing something that
        // works in Debug and throws in the published binary.
        request.Content = JsonContent.Create(
            decision, ProtocolJsonContext.Default.DecisionRequest);

        using var response = await _httpClient.SendAsync(request, cancellationToken);
        await ThrowIfProtocolRefusedAsync(response, cancellationToken);

        if (response.StatusCode == HttpStatusCode.NotFound)
        {
            // RAISED RATHER THAN RETURNED AS NULL. Null now means "accepted, and
            // there is nothing to answer with", so a missing flight needs a value
            // of its own or the two collapse - and the one that collapses quietly
            // is the one that reports success.
            throw new FlightNotFoundException(
                $"No flight {reference}. Run gg flights to see what is there.");
        }

        if (response.StatusCode == HttpStatusCode.Conflict)
        {
            throw new DecisionRefusedException(
                "The work changed while this decision was being made, so it was not recorded. "
              + "What you were shown is not what is there now - read it again with `gg why` and "
              + "decide against the work as it stands.");
        }

        if (response.StatusCode == HttpStatusCode.BadRequest)
        {
            // THE DIAGNOSIS, CARRIED THROUGH. This used to fall to
            // EnsureSuccessStatusCode and leave as an HttpRequestException saying
            // "400 (Bad Request)" - the control plane's sentence, which is the only
            // part anybody can act on, was thrown away one line before it was read.
            throw new DecisionRefusedException(
                (await response.Content.ReadAsStringAsync(cancellationToken)).Trim());
        }

        response.EnsureSuccessStatusCode();

        // ACCEPTED, WITH NOTHING TO SAY. ADR-0012: the write is a command, so the
        // control plane takes the decision and the caller learns what happened by
        // looking. Null is the answer rather than an error - `gg decide` already
        // observes, and the record it used to render was carried beside the
        // observation and consulted by nothing.
        //
        // The 200 branch below is TOLERATED while a control plane that still
        // answers inline exists, which is what lets the two repositories land this
        // in either order. When none does, it is dead and deleting it is a change
        // with its own reason.
        if (response.StatusCode == HttpStatusCode.Accepted)
        {
            return null;
        }

        return await response.Content.ReadFromJsonAsync(
            ProtocolJsonContext.Default.DecisionRecorded, cancellationToken);
    }

    public async Task<GateList> GatesAsync(
        string sessionToken, CancellationToken cancellationToken = default)
    {
        using var request = Request(HttpMethod.Get, "/v1/gates", sessionToken);
        using var response = await _httpClient.SendAsync(request, cancellationToken);
        await ThrowIfProtocolRefusedAsync(response, cancellationToken);

        response.EnsureSuccessStatusCode();

        // An empty list rather than null. "Nothing is waiting" is an answer every
        // caller can render, and a null would make it a case each of them handles.
        return await response.Content.ReadFromJsonAsync(
            ProtocolJsonContext.Default.GateList, cancellationToken)
            ?? new GateList { Gates = [] };
    }

    /// <summary>
    /// What this tenant's control plane is offering, or null for nothing.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>A read a PERSON starts, which is what makes it allowed.</b> An offer
    /// rides a runner's heartbeat because nothing connects inbound to a
    /// machine; this adds no inbound reach, because gg dials out here exactly
    /// as it does for every other verb. The route answers a session and refuses
    /// a runner credential, so what a runner may have is still only what rode
    /// the poll it was already making.
    /// </para>
    /// <para>
    /// <b>Null is 204, and it is the fleet's ordinary state.</b> Unlike
    /// <c>GatesAsync</c> above, an absence cannot be flattened into an empty
    /// document here: an offer with no settings HAS a version and is a control
    /// plane that withdrew what it was offering, which is something a person
    /// can still accept. "Nothing is offered" and "nothing is offered any more"
    /// are different facts and stay different.
    /// </para>
    /// <para>
    /// <b>And a 404 is the far end being too old</b>, not an empty answer.
    /// The declaration says this route has no 404, so one means a control plane
    /// that predates the contract — which is a different fact from "nothing is
    /// offered" and must not be reported as it. That is
    /// <see cref="TelemetryAsync"/>'s own warning, and it bites harder here:
    /// told "nothing is offered", a person concludes their fleet is as
    /// configured as it looks.
    /// </para>
    /// </remarks>
    public async Task<OfferedConfiguration?> OfferedConfigurationAsync(
        string sessionToken, CancellationToken cancellationToken = default)
    {
        using var request = Request(HttpMethod.Get, "/v1/configuration/offered", sessionToken);
        using var response = await _httpClient.SendAsync(request, cancellationToken);
        await ThrowIfProtocolRefusedAsync(response, cancellationToken);

        if (response.StatusCode == HttpStatusCode.NoContent)
        {
            return null;
        }

        if (response.StatusCode == HttpStatusCode.NotFound)
        {
            throw new ControlPlaneTooOldException(
                "this control plane does not serve offered configuration - it is older than "
              + "the contract this gg was built against. Nothing is wrong with your machine, "
              + "and nothing here can tell you what is offered until the control plane is "
              + "updated.");
        }

        response.EnsureSuccessStatusCode();

        return await response.Content.ReadFromJsonAsync(
            ProtocolJsonContext.Default.OfferedConfiguration, cancellationToken);
    }

    /// <summary>
    /// Stops a flight that could still have been done.
    /// </summary>
    /// <remarks>
    /// <b>Nothing comes back but the fact that it worked.</b> The door answers
    /// 202 with no body - the answer is that the flight is over, and what a
    /// caller does next is read it. A 409 is a flight that has already ended,
    /// refused rather than allowed to appear to rewrite an ending that
    /// happened.
    /// </remarks>
    public async Task GroundAsync(
        string sessionToken,
        string reference,
        string because,
        CancellationToken cancellationToken = default)
    {
        using var request = Request(
            HttpMethod.Post, $"/v1/flights/{reference}/grounding", sessionToken);

        request.Content = JsonContent.Create(
            new FlightGroundingRequest { Because = because },
            ProtocolJsonContext.Default.FlightGroundingRequest);

        using var response = await _httpClient.SendAsync(request, cancellationToken);
        await ThrowIfProtocolRefusedAsync(response, cancellationToken);

        if (response.StatusCode == HttpStatusCode.NotFound)
        {
            throw new FlightNotFoundException(
                $"No flight {reference}. Run gg flights to see what is there.");
        }

        // A FLIGHT THAT HAS ALREADY ENDED, said as what it is. The door refuses
        // this rather than accepting it, because accepting would let a
        // grounding appear to rewrite an ending that already happened - and a
        // caller told only "409" would have to guess which of the two endings
        // it now has.
        if (response.StatusCode == HttpStatusCode.Conflict)
        {
            throw new DecisionRefusedException(
                $"Flight {reference} has already ended. Run gg show {reference} to see how.");
        }

        response.EnsureSuccessStatusCode();
    }

    public async Task<FlightAttribution?> WhyAsync(
        string sessionToken, string reference, CancellationToken cancellationToken = default)
    {
        using var request = Request(HttpMethod.Get, $"/v1/flights/{reference}/why", sessionToken);
        using var response = await _httpClient.SendAsync(request, cancellationToken);
        await ThrowIfProtocolRefusedAsync(response, cancellationToken);

        if (response.StatusCode == HttpStatusCode.NotFound)
        {
            return null;
        }

        response.EnsureSuccessStatusCode();

        return await response.Content.ReadFromJsonAsync(
            ProtocolJsonContext.Default.FlightAttribution, cancellationToken);
    }

    /// <summary>
    /// Claims a flight for a takeover, or reports who already holds it.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>A claim rather than a record, and the difference is when it happens.</b>
    /// What this replaced posted a <c>TakeoverRecord</c> when somebody had already
    /// finished, so two people on two machines could both take one stopped flight
    /// and both find out afterwards. This asks first, and exactly one of two
    /// simultaneous claimants is granted.
    /// </para>
    /// <para>
    /// <b>A refusal is returned rather than thrown.</b> Somebody else holding the
    /// flight is the ordinary case this exists for, not an error - and what the
    /// caller has to do with it is print who holds it and since when.
    /// </para>
    /// </remarks>
    public async Task<TakeoverClaim> ClaimTakeoverAsync(
        string sessionToken, string reference, CancellationToken cancellationToken = default)
    {
        using var request = Request(
            HttpMethod.Post, $"/v1/flights/{reference}/takeover:claim", sessionToken);

        using var response = await _httpClient.SendAsync(request, cancellationToken);
        await ThrowIfProtocolRefusedAsync(response, cancellationToken);

        if (response.StatusCode == HttpStatusCode.NotFound)
        {
            return new TakeoverClaim.NoSuchFlight();
        }

        if (response.StatusCode == HttpStatusCode.Conflict)
        {
            var held = await response.Content.ReadFromJsonAsync(
                TakeoverJson.Default.TakeoverHeld, cancellationToken);

            // A 409 with an unreadable body still means refused. Reporting it as
            // granted would be the worst possible reading of a refusal.
            return held is null
                ? new TakeoverClaim.Refused(null)
                : new TakeoverClaim.Refused(held);
        }

        response.EnsureSuccessStatusCode();

        var claimed = await response.Content.ReadFromJsonAsync(
            TakeoverJson.Default.TakeoverClaimed, cancellationToken);

        return claimed is null
            ? new TakeoverClaim.Refused(null)
            : new TakeoverClaim.Granted(claimed);
    }

    /// <summary>Keeps a hold, or reports that it is no longer this caller's.</summary>
    /// <remarks>
    /// <b>The generation is the fence.</b> A holder who stopped renewing long
    /// enough for the hold to lapse, and whose flight was then claimed by somebody
    /// else, is told so rather than handed it back - which is the same arrangement
    /// a lease renewal uses.
    /// </remarks>
    public async Task<TakeoverRenewed?> RenewTakeoverAsync(
        string sessionToken, string reference, int generation,
        CancellationToken cancellationToken = default)
    {
        using var request = Request(
            HttpMethod.Post, $"/v1/flights/{reference}/takeover:renew", sessionToken);
        request.Content = JsonContent.Create(
            new TakeoverRenewalRequest { Generation = generation },
            TakeoverJson.Default.TakeoverRenewalRequest);

        using var response = await _httpClient.SendAsync(request, cancellationToken);
        await ThrowIfProtocolRefusedAsync(response, cancellationToken);

        return response.IsSuccessStatusCode
            ? await response.Content.ReadFromJsonAsync(
                TakeoverJson.Default.TakeoverRenewed, cancellationToken)
            : null;
    }

    /// <summary>
    /// Hands a flight back with a decision, against the hold that made it.
    /// </summary>
    /// <remarks>
    /// <b>Refusal is a real answer and is reported as one.</b> A decision arriving
    /// against a hold that has moved to somebody else is not applied - putting one
    /// person's decision on another person's work is worse than losing it, which
    /// is the same argument <see cref="TakeoverReturn.Validate"/> makes about a
    /// leftover file.
    /// </remarks>
    public async Task<bool> ReturnTakeoverAsync(
        string sessionToken,
        string reference,
        TakeoverReturnRequest decision,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(decision);

        using var request = Request(
            HttpMethod.Post, $"/v1/flights/{reference}/takeover:return", sessionToken);
        request.Content = JsonContent.Create(
            decision, TakeoverJson.Default.TakeoverReturnRequest);

        using var response = await _httpClient.SendAsync(request, cancellationToken);
        await ThrowIfProtocolRefusedAsync(response, cancellationToken);

        return response.IsSuccessStatusCode;
    }

    /// <summary>
    /// What the control plane says it transmits. Null if it is too old to say.
    /// </summary>
    /// <remarks>
    /// A 404 means an older control plane that predates the disclosure, which
    /// is a different fact from "exports nothing" and must not be reported as
    /// it - the whole point is to stop a silent transmission looking like
    /// silence.
    /// </remarks>
    public async Task<TelemetryDisclosure?> TelemetryAsync(
        string sessionToken, CancellationToken cancellationToken = default)
    {
        using var request = Request(HttpMethod.Get, "/v1/telemetry", sessionToken);
        using var response = await _httpClient.SendAsync(request, cancellationToken);
        await ThrowIfProtocolRefusedAsync(response, cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            return null;
        }

        return await response.Content.ReadFromJsonAsync(
            ProtocolJsonContext.Default.TelemetryDisclosure, cancellationToken);
    }

    /// <summary>Revokes the session server-side. Returns false if it was already gone.</summary>
    public async Task<bool> RevokeSessionAsync(string sessionToken, CancellationToken cancellationToken = default)
    {
        using var request = Request(HttpMethod.Post, "/v1/auth/logout", sessionToken);
        using var response = await _httpClient.SendAsync(request, cancellationToken);
        return response.IsSuccessStatusCode;
    }

    /// <summary>
    /// Surfaces a refusal as something actionable rather than a bare status
    /// code.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Here because every call comes through it.</b> Thirty-six doors, and a
    /// per-door check would be thirty-six chances to forget - the one forgotten
    /// being whichever verb somebody happened to press.
    /// </para>
    /// </remarks>
    /// <param name="refusalIsAnAnswer">
    /// Set where a 401 is what the caller ASKED - <c>WhoAmIAsync</c> is the
    /// probe `gg doctor` uses to find out whether a session is still good, and
    /// it answers null. Throwing there would take the question away from the
    /// one command whose job is to ask it.
    /// </param>
    private static async Task ThrowIfProtocolRefusedAsync(
        HttpResponseMessage response,
        CancellationToken cancellationToken,
        bool refusalIsAnAnswer = false)
    {
        if (response.StatusCode == HttpStatusCode.UpgradeRequired)
        {
            var detail = await response.Content.ReadAsStringAsync(cancellationToken);
            throw new ProtocolTooOldException(
                $"This gg is too old for the control plane. {detail}".Trim());
        }

        // A SESSION THE CONTROL PLANE WILL NOT TAKE, which is the same fact as
        // having none and was reaching a person as a status code. Every verb
        // checks that a session FILE exists and none can know whether it is
        // still good; this is the far side saying so, and it is the only
        // authority on it - an expiry read off the stored ExpiresAt would miss
        // a session revoked, a tenant removed, and a laptop whose clock is
        // wrong.
        //
        // ONLY WHERE ONE WAS SENT, and the sentence is why: "this session is no
        // longer valid" is false about a call that carried none. The ping, the
        // version read and the device-authorization pair are all
        // unauthenticated, and a 401 on one of those is a different thing that
        // signing in would not fix.
        //
        // NOT 403 either. Signed in and not allowed is a different fact, and
        // offering to sign in again would send somebody round a loop that
        // cannot help them - a runner token on a developer door is refused for
        // a reason signing in does not change.
        if (!refusalIsAnAnswer
            && response.StatusCode == HttpStatusCode.Unauthorized
            && response.RequestMessage?.Headers.Contains(GgVersions.SessionHeader) == true)
        {
            throw new NotSignedInException(
                "This session is no longer valid. Run gg login.");
        }
    }
}

/// <summary>
/// Raised when a grant or revocation was refused, carrying the reason.
/// </summary>
/// <remarks>
/// <b>Its own type, beside the other per-subject refusals.</b> The console and
/// the command line both render an exception's message, so what matters is that
/// the message is the control plane's sentence rather than a status code - and a
/// named type is what lets a caller tell "you may not" from "the network is
/// down".
/// </remarks>
public sealed class AdminRefusedException(string message) : Exception(message);

/// <summary>
/// Raised when the control plane refuses a claim, an unclaim, a reservation or
/// an ownership change, carrying its own sentence.
/// </summary>
/// <remarks>
/// <b>403 and 409 both arrive here</b>, and the sentence is what tells them
/// apart: not you - a tenant runner, somebody else's - or not now - claim it
/// first, unclaim it first. Each is a different thing to do next.
/// </remarks>
public sealed class RunnerOwnershipRefusedException(string message) : Exception(message);

/// <summary>Raised when the control plane refuses to mint, or to redeem, an enrollment token - with its own sentence.</summary>
public sealed class EnrollmentRefusedException(string message) : Exception(message);

/// <summary>
/// Raised when the control plane checked the principal and said no.
/// </summary>
/// <remarks>
/// <para>
/// <b>An answer, never a fault.</b> A 403 left to
/// <c>EnsureSuccessStatusCode</c> becomes an <c>HttpRequestException</c>, and
/// every emitter renders one of those as <i>"could not reach the control plane
/// … try gg doctor"</i> - which is false in the one way that costs a person the
/// most time: the control plane was reached, it understood the request, and it
/// decided.
/// </para>
/// <para>
/// <b>Distinct from a refusal a person can act on.</b> A 400 names a value to
/// fix and the same person fixes it; this one has nothing to fix and has to
/// point at somebody who holds the role. Two instructions, so two types - the
/// argument <see cref="AdminRefusedException"/> already makes for telling "you
/// may not" from "the network is down".
/// </para>
/// </remarks>
public sealed class PermissionRefusedException(string message) : Exception(message);

/// <summary>Raised when the control plane refuses this binary's protocol version.</summary>
public sealed class ProtocolTooOldException(string message) : Exception(message);

/// <summary>
/// Raised when the control plane does not serve a route this gg is sure of.
/// </summary>
/// <remarks>
/// <para>
/// <b>The mirror of <see cref="ProtocolTooOldException"/>, and there was no
/// name for it.</b> A 426 is the far end saying this gg is behind, and it says
/// so out loud. The reverse arrives as a bare 404, which is indistinguishable
/// from a domain answer at the transport and is exactly how "this cannot be
/// asked" comes to be read as "the answer is nothing".
/// </para>
/// <para>
/// <b>Only for a route whose declaration has no 404.</b> Where 404 is a real
/// answer — no such flight, no such credential — it stays one. This is for the
/// doors where the declaration says the question is always askable, so a 404 can
/// only mean the far half of the protocol has not arrived yet.
/// </para>
/// </remarks>
public sealed class ControlPlaneTooOldException(string message) : Exception(message);
