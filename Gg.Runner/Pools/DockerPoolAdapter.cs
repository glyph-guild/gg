using System.Net;
using System.Text;
using System.Text.Json;
using Gg.Contracts;

namespace Gg.Runner.Pools;

/// <summary>
/// The Docker adapter: the Engine API over HTTP, against the endpoint the
/// deployment configured — the scope-enforcing proxy, never the raw socket.
/// </summary>
/// <remarks>
/// <para>
/// <b>A raw HttpClient, deliberately.</b> No Docker client package: the
/// surface this needs is five endpoints, and a dependency that can do
/// everything the daemon can is a capability nobody granted waiting for a
/// call site. There is no socket path anywhere in this file — the endpoint
/// is a URL from <see cref="PoolConfiguration"/>, and what that URL refuses
/// is the scope (§ 12: enforced by the provider, not by us).
/// </para>
/// <para>
/// The spec.Image digest attested is the daemon's own spec.Image id from inspect —
/// what the member actually runs, never what was asked for.
/// </para>
/// </remarks>
public sealed class DockerPoolAdapter(HttpClient httpClient) : IPoolAdapter, IImageBuilder
{
    private readonly HttpClient _httpClient = httpClient;

    public PoolCapabilities Capabilities { get; } = new() { Provider = "docker" };

    public async Task<IReadOnlyList<PoolMember>> ListAsync(
        string pool, CancellationToken cancellationToken = default)
    {
        var filters = Uri.EscapeDataString($$"""{"name":["{{pool}}-"]}""");
        using var response = await _httpClient.GetAsync(
            $"/containers/json?all=true&filters={filters}", cancellationToken);
        response.EnsureSuccessStatusCode();

        using var listed = JsonDocument.Parse(
            await response.Content.ReadAsStringAsync(cancellationToken));

        var members = new List<PoolMember>();
        foreach (var container in listed.RootElement.EnumerateArray())
        {
            // The daemon's name filter is a substring match; the prefix is
            // re-checked here so a stranger whose name merely contains the
            // pool's never enters the inventory.
            var name = container.GetProperty("Names")[0].GetString()!.TrimStart('/');
            if (name.StartsWith($"{pool}-", StringComparison.Ordinal))
            {
                // THE DAEMON HAS ALWAYS SAID THIS and the listing threw it
                // away. "running" is the daemon's own word, and anything else -
                // exited, created, paused, dead - is a member not doing the one
                // thing a member is for.
                members.Add(new PoolMember
                {
                    Name = name,
                    Running = string.Equals(
                        container.GetProperty("State").GetString(),
                        "running",
                        StringComparison.Ordinal),

                    // AND SO HAS THIS. The listing carries the reference each
                    // container was made from - the same string Config.Image
                    // reports on an inspect - so a roll can tell which members
                    // drifted from one request instead of one per member.
                    MadeFrom = container.TryGetProperty("Image", out var madeFrom)
                        ? madeFrom.GetString()
                        : null,
                });
            }
        }

        return members;
    }

    public async Task<PoolObservation> VerifyAsync(
        PoolMember member, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(member);

        var inspected = await InspectAsync(member.Name, cancellationToken);
        if (inspected is not { } state)
        {
            return new PoolObservation
            {
                Outcome = PoolOutcomes.Failed,
                Diagnosis = $"'{member.Name}' is in the pool's listing and will not inspect - "
                          + "the daemon knows a name it cannot describe.",
            };
        }

        return state.Running
            ? new PoolObservation
            {
                Outcome = PoolOutcomes.Verified,
                ImageDigest = state.ImageDigest,
                Provenance = EnvironmentProvenance.Reused,
            }
            : new PoolObservation
            {
                Outcome = PoolOutcomes.Failed,
                ImageDigest = state.ImageDigest,
                Diagnosis = $"'{member.Name}' exists and is not running (status: {state.Status}).",
            };
    }

    public async Task<PoolObservation> RefreshAsync(
        string pool, string member, MemberSpec spec, CancellationToken cancellationToken = default)
    {
        // UNKNOWN IS NOT FALSE, AND IT IS NOT AN EXCEPTION EITHER. Convergence
        // made this inspect load-bearing on every sweep, and a non-404 answer
        // used to throw out of here, out of the maintain loop's ExecuteAsync
        // and out of its cycle - BEFORE the attestation. So the pool shipped
        // nothing, and nothing already means something else: the staleness arm
        // reads silence as "the pull point stopped attesting" and sends a
        // person to check a runner that is fine.
        //
        // 404 is not this. Absent is a real answer with its own branch below.
        (bool Running, string Status, int ExitCode, string? ImageDigest, string? MadeFrom)? inspected;

        try
        {
            inspected = await InspectAsync(member, cancellationToken);
        }
        catch (HttpRequestException unreachable)
        {
            return new PoolObservation
            {
                Outcome = PoolOutcomes.Failed,
                Diagnosis = $"'{member}' could not be inspected: {unreachable.Message}. "
                          + "Nothing was created and nothing was converged - the daemon "
                          + "knows a name it will not describe, which is not the same as "
                          + "a member that is absent.",
            };
        }

        if (inspected is null)
        {
            return await CreateAndStartAsync(pool, member, spec, cancellationToken);
        }

        // A MEMBER THAT FINISHED HAS NOTHING LEFT TO START. Exit 0 out of a
        // member's loop means its credential ended - the one condition starting
        // it again cannot fix, because a member token is not renewable and the
        // nonce it was bought with is spent. The process would read an ended
        // credential and exit, and this would attest Verified every sweep: a
        // restart loop wearing the costume of a repair.
        //
        // Reset rather than refuse, because the slot is the thing the pool
        // wants. Removing and recreating puts a WORKING member back in the name
        // that just died, which is the whole of "the pool refills without
        // intervention" on this side.
        //
        // NOT EVERY STOPPED MEMBER, though. A daemon restart or a reboot stops
        // one whose stored identity is still good, and that member comes back
        // by being started - cheaper than a replacement and exactly as warm.
        // The exit code is what tells them apart, and it only became worth
        // reading when a credential ending stopped arriving as a signal.
        if (!inspected.Value.Running && inspected.Value.ExitCode == 0)
        {
            return await ResetAsync(member, spec, cancellationToken);
        }

        if (!inspected.Value.Running)
        {
            using var started = await _httpClient.PostAsync(
                $"/containers/{member}/start", content: null, cancellationToken);
            if (started.StatusCode is not (HttpStatusCode.NoContent or HttpStatusCode.NotModified))
            {
                return new PoolObservation
                {
                    Outcome = PoolOutcomes.Failed,
                    Diagnosis = $"'{member}' exists and will not start: the daemon answered "
                              + $"{(int)started.StatusCode}.",
                };
            }

            var restarted = await InspectAsync(member, cancellationToken);
            return new PoolObservation
            {
                Outcome = PoolOutcomes.Verified,
                ImageDigest = restarted?.ImageDigest,
                Provenance = EnvironmentProvenance.Reused,
            };
        }

        // RUNNING ALREADY, WHICH IS NOT THE SAME AS CURRENT. Refresh means
        // current AND running; a member made from something the strategy does
        // not name is not made current by being described as current, and this
        // branch used to describe it and stop.
        //
        // The comparison is what the container says it was made FROM against
        // what the strategy pins - never the resolved spec.Image id, which is a
        // different thing and would differ from a reference every time. Both
        // sides are digest-pinned by EnvironmentStrategy.Validate, so exact
        // equality is the right operator: an approximate drift check resets
        // every sweep, forever, and that is a bill rather than a bug.
        //
        // NOTHING OUTSIDE /containers/ IS ASKED. The pull point refuses images,
        // volumes, build and networks, so resolving the strategy's spec.Image
        // through the daemon would 403 - and a 403 read as drift is the same
        // billing incident by another route.
        if (!string.Equals(inspected.Value.MadeFrom, spec.Image, StringComparison.Ordinal))
        {
            return await ResetAsync(member, spec, cancellationToken);
        }

        return new PoolObservation
        {
            Outcome = PoolOutcomes.Verified,
            ImageDigest = inspected.Value.ImageDigest,
            Provenance = EnvironmentProvenance.Reused,
        };
    }

    public async Task<PoolObservation> ResetAsync(
        string member, MemberSpec spec, CancellationToken cancellationToken = default)
    {
        // Stop-and-remove tolerates absence: a reset of a member that is
        // already gone is a create, not an error.
        using var stopped = await _httpClient.PostAsync(
            $"/containers/{member}/stop", content: null, cancellationToken);
        using var removed = await _httpClient.DeleteAsync(
            $"/containers/{member}", cancellationToken);

        var pool = member[..member.LastIndexOf('-')];
        return await CreateAndStartAsync(pool, member, spec, cancellationToken);
    }

    public async Task<ScopeProbe> ProbeScopeAsync(CancellationToken cancellationToken = default)
    {
        // A name no pool owns: the reach the proxy exists to refuse. An
        // answer OTHER than a refusal - a 200, a 404 from the daemon itself -
        // means the request got past the scope, which is the broken bound.
        var outside = $"gg-scope-probe-{Guid.NewGuid():N}";
        var probedAt = DateTimeOffset.UtcNow;

        try
        {
            using var response = await _httpClient.GetAsync(
                $"/containers/{outside}/json", cancellationToken);

            if (response.StatusCode is not (HttpStatusCode.Forbidden or HttpStatusCode.Unauthorized))
            {
                return new ScopeProbe
                {
                    Held = false,
                    ProbedAt = probedAt,
                    Diagnosis = $"the reach outside the pool prefix was ALLOWED: GET "
                              + $"/containers/{outside}/json answered {(int)response.StatusCode} "
                              + "instead of a refusal. The endpoint is not scoping.",
                };
            }

            // AND THE BUILD SCOPE (slice forty-one, rule 17). The proxy admits a
            // build tagged into the host's own registry and a push of an image in
            // it; so the probe asks for one tagged elsewhere and a push of a
            // foreign image, and requires both refused. An empty body builds
            // nothing even if a broken proxy lets it through.
            using (var build = await _httpClient.PostAsync(
                       $"/build?t={Uri.EscapeDataString(outside + "/outside:probe")}",
                       new ByteArrayContent([]), cancellationToken))
            {
                if (build.StatusCode is not (HttpStatusCode.Forbidden or HttpStatusCode.Unauthorized))
                {
                    return new ScopeProbe
                    {
                        Held = false,
                        ProbedAt = probedAt,
                        Diagnosis = "a build tagged outside the host's registry was ALLOWED: POST "
                                  + $"/build answered {(int)build.StatusCode} instead of a refusal. "
                                  + "The build scope is not bounded.",
                    };
                }
            }

            using (var push = await _httpClient.PostAsync(
                       $"/images/{outside}/outside/push?tag=probe", content: null, cancellationToken))
            {
                if (push.StatusCode is not (HttpStatusCode.Forbidden or HttpStatusCode.Unauthorized))
                {
                    return new ScopeProbe
                    {
                        Held = false,
                        ProbedAt = probedAt,
                        Diagnosis = "a push of an image outside the host's registry was ALLOWED: "
                                  + $"answered {(int)push.StatusCode} instead of a refusal. The "
                                  + "push scope is not bounded.",
                    };
                }
            }

            return new ScopeProbe { Held = true, ProbedAt = probedAt };
        }
        catch (HttpRequestException unreachable)
        {
            return new ScopeProbe
            {
                Held = false,
                ProbedAt = probedAt,
                Diagnosis = $"the scope probe could not reach the endpoint: {unreachable.Message}. "
                          + "Unknown is not false - an unreachable proxy proves nothing.",
            };
        }
    }

    /// <summary>
    /// Builds a recipe directory through the daemon, tagged into the pool's own
    /// registry and labelled with what it was made from (slice forty-one).
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>The context is a tar of the directory, symbolic links kept as links</b>
    /// - so a link inside a recipe that points at the host sends a link, never
    /// the file it points at.
    /// </para>
    /// <para>
    /// <b>Headers first, then the stream.</b> A build answers at once and then
    /// talks for as long as it takes, so the client's timeout bounds the answer
    /// and not the build.
    /// </para>
    /// </remarks>
    public async Task<ImageBuilt> BuildAsync(
        string context, string dockerfile, string tag, IReadOnlyDictionary<string, string> labels,
        CancellationToken cancellationToken = default)
    {
        await using var tar = new MemoryStream();
        await System.Formats.Tar.TarFile.CreateFromDirectoryAsync(
            context, tar, includeBaseDirectory: false, cancellationToken);
        tar.Position = 0;

        await using var labelJson = new MemoryStream();
        await using (var writer = new Utf8JsonWriter(labelJson))
        {
            writer.WriteStartObject();
            foreach (var (key, value) in labels)
            {
                writer.WriteString(key, value);
            }

            writer.WriteEndObject();
        }

        using var request = new HttpRequestMessage(
            HttpMethod.Post,
            $"/build?t={Uri.EscapeDataString(tag)}&dockerfile={Uri.EscapeDataString(dockerfile)}"
          + $"&labels={Uri.EscapeDataString(Encoding.UTF8.GetString(labelJson.ToArray()))}"
          + "&rm=1&forcerm=1")
        {
            Content = new StreamContent(tar),
        };
        request.Content.Headers.ContentType = new("application/x-tar");

        using var response = await _httpClient.SendAsync(
            request, HttpCompletionOption.ResponseHeadersRead, cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            return new ImageBuilt.Failed(
                $"the daemon refused the build (HTTP {(int)response.StatusCode}). A 403 is the pool "
              + "proxy: its configuration predates the build allowance, or the tag is outside the "
              + "host's registry.",
                null);
        }

        var (aux, error) = await StreamAsync(response, "ID", statusPrefix: null, cancellationToken);

        return error is not null
            ? new ImageBuilt.Failed(
                "the recipe did not build: the daemon reported a failed step. Its output is in this "
              + "pool host's maintainer log and is not sent, because it echoes the recipe.",
                error)
            : aux is { Length: > 0 } id
                ? new ImageBuilt.Built(id)
                : new ImageBuilt.Failed("the daemon finished the build without naming an image.", null);
    }

    /// <summary>
    /// Pushes a built image to its registry, and returns the digest the registry
    /// answered with - which is what a pin can name.
    /// </summary>
    public async Task<ImagePushed> PushImageAsync(
        string repository, string tag, CancellationToken cancellationToken = default)
    {
        using var request = new HttpRequestMessage(
            HttpMethod.Post, $"/images/{repository}/push?tag={Uri.EscapeDataString(tag)}");

        // ANONYMOUS: the pool's registry is the host's own. The header is required
        // by the daemon even when it carries nothing.
        request.Headers.Add("X-Registry-Auth", Convert.ToBase64String("{}"u8.ToArray()));

        using var response = await _httpClient.SendAsync(
            request, HttpCompletionOption.ResponseHeadersRead, cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            return new ImagePushed.Failed(
                $"the daemon refused the push (HTTP {(int)response.StatusCode}).", null);
        }

        // THE DIGEST IS WHERE THIS DAEMON SAYS IT, which is not always aux. Docker
        // 29 on the containerd image store sends no aux for a push and names the
        // digest only in its last status line - found on slice forty-one's walk,
        // where a push that landed attested failed. The tag is part of the line,
        // so a line about any other tag names nothing.
        var (digest, error) = await StreamAsync(
            response, "Digest", statusPrefix: $"{tag}: digest: ", cancellationToken);

        return error is not null
            ? new ImagePushed.Failed("the registry refused the push.", error)
            : digest is { Length: > 0 } pushed
                ? new ImagePushed.Pushed(pushed)
                : new ImagePushed.Failed("the registry accepted the push without naming a digest.", null);
    }

    /// <summary>
    /// Reads the daemon's line-per-object progress stream, returning one member
    /// of the final <c>aux</c> object and the first error, if any.
    /// </summary>
    /// <param name="statusPrefix">
    /// Where a digest may be named instead, when no <c>aux</c> carries it: a
    /// status line starting with this, followed by <c>sha256:</c> and the hex.
    /// Null where only <c>aux</c> answers.
    /// </param>
    private static async Task<(string? Aux, string? Error)> StreamAsync(
        HttpResponseMessage response, string auxMember, string? statusPrefix,
        CancellationToken cancellationToken)
    {
        string? aux = null;
        string? stated = null;
        string? error = null;

        using var reader = new StreamReader(await response.Content.ReadAsStreamAsync(cancellationToken));
        while (await reader.ReadLineAsync(cancellationToken) is { } line)
        {
            if (line.Length == 0)
            {
                continue;
            }

            try
            {
                using var progress = JsonDocument.Parse(line);
                var root = progress.RootElement;

                if (root.TryGetProperty("error", out var failed) && failed.ValueKind == JsonValueKind.String)
                {
                    error ??= failed.GetString();
                }

                if (root.TryGetProperty("aux", out var carried)
                    && carried.ValueKind == JsonValueKind.Object
                    && carried.TryGetProperty(auxMember, out var value)
                    && value.ValueKind == JsonValueKind.String)
                {
                    aux = value.GetString();
                }

                if (statusPrefix is not null
                    && root.TryGetProperty("status", out var status)
                    && status.ValueKind == JsonValueKind.String
                    && status.GetString() is { } said
                    && said.StartsWith(statusPrefix, StringComparison.Ordinal)
                    && said[statusPrefix.Length..].Split(' ')[0] is var named
                    && named.StartsWith("sha256:", StringComparison.Ordinal)
                    && named.Length == "sha256:".Length + 64
                    && named["sha256:".Length..].All(char.IsAsciiHexDigitLower))
                {
                    stated = named;
                }
            }
            catch (JsonException)
            {
                // A line that is not an object is progress text, and progress text
                // decides nothing.
            }
        }

        return (aux ?? stated, error);
    }

    private async Task<PoolObservation> CreateAndStartAsync(
        string pool, string member, MemberSpec spec, CancellationToken cancellationToken)
    {
        // The strategy's spec.Image, its own entrypoint, no binds, nothing
        // privileged: a pool member is the spec.Image, the label that says which
        // pool, and THE NAME OF THE IMAGE IT IS. Written with the writer rather
        // than the reflection serializer - this assembly is AOT-compiled, and
        // the shape is three fields.
        //
        // IT USED TO SAY "the spec.Image and nothing else", AND THE COMMENT MOVED
        // WITH THE CODE. That matters more than it looks: the sentence was a
        // containment claim, and a claim left standing over changed code is the
        // exact defect this slice is otherwise about.
        //
        // What was added is one non-secret variable naming the spec.Image the member
        // was started from. EnvironmentSurvey reads it on every fact ship and
        // reports ImageDigest from it - so a flight that ran in an environment
        // the platform MADE says which one, and a flight on a machine it merely
        // found reports null. The variable was declared and read since the fact
        // shipped, and set by nothing; this is its setter.
        //
        // BESIDE IT NOW: where to answer, and a single-use nonce to become
        // somebody with. Those two are what make a member a runner rather than
        // scenery - without them a container starts, finds the localhost
        // default, and registers with nobody.
        //
        // NO LABELS HERE, deliberately. A member receives what it may advertise
        // in the redeem response, from the strategy, decided control-plane-side
        // - so putting them in the environment as well would be a second source
        // of truth for the one thing this whole slice exists to stop taking on
        // a runner's word.
        //
        // A NONCE AND NEVER A CREDENTIAL. GET /containers/gg-pool-*/json is
        // reachable through the scope proxy, so everything written here is
        // readable by an inspect for the life of the container. The nonce is
        // worth nothing once redeemed; the credential is fetched by the member
        // over its own connection.
        //
        // STILL NO HostConfig, no binds, nothing privileged - and that is held
        // by a test rather than by this sentence, because the proxy filters
        // ?name= and never reads this body.
        // ARTICLE XI, BEFORE ANYTHING IS CREATED. A member with no nonce
        // cannot redeem, so it registers with nobody, advertises to nobody and
        // claims nothing - while the pool counts a container that exists. That
        // is the 196 wearing a better image, so it is refused here where the
        // diagnosis can name the cause, and nothing is made that would have to
        // be cleaned up.
        if (spec.Nonce is not { Length: > 0 })
        {
            return new PoolObservation
            {
                Outcome = PoolOutcomes.Failed,
                Diagnosis = $"no credential could be minted for '{member}', so it was not "
                          + "created. A member without one registers with nobody and claims "
                          + "nothing, and the pool would count it as warm.",
            };
        }

        await using var buffer = new MemoryStream();
        await using (var writer = new Utf8JsonWriter(buffer))
        {
            writer.WriteStartObject();
            writer.WriteString("Image", spec.Image);
            writer.WriteStartObject("Labels");
            writer.WriteString("gg.pool", pool);
            writer.WriteEndObject();
            writer.WriteStartArray("Env");
            writer.WriteStringValue(
                $"{Facts.EnvironmentSurvey.ImageDigestVariable}={spec.Image}");
            writer.WriteStringValue($"GG_CONTROL_PLANE={spec.ControlPlane}");
            writer.WriteStringValue($"{MemberBootstrap.NonceVariable}={spec.Nonce}");

            // AND WHERE TO ASK FOR ITS OWN ADDRESS, when the deployment named
            // somewhere. Without it a member offers the address it can see -
            // 172.17.x, inside this container - and a console that asked to be
            // introduced gets NoRouteBetweenUs after the channel is opened,
            // which reads as a machine refusing rather than as one that was
            // never told. Omitted rather than empty: StunConfiguration reads a
            // missing variable and an empty one the same way, and writing the
            // empty one claims a server nobody named.
            if (spec.StunServers is { Length: > 0 } stun)
            {
                writer.WriteStringValue($"{StunConfiguration.Variable}={stun}");
            }

            writer.WriteEndArray();
            writer.WriteEndObject();
        }

        var body = Encoding.UTF8.GetString(buffer.ToArray());

        using var created = await _httpClient.PostAsync(
            $"/containers/create?name={Uri.EscapeDataString(member)}",
            new StringContent(body, Encoding.UTF8, "application/json"),
            cancellationToken);
        if (created.StatusCode is not HttpStatusCode.Created)
        {
            return new PoolObservation
            {
                Outcome = PoolOutcomes.Failed,
                Diagnosis = $"creating '{member}' from '{spec.Image}' was refused: the daemon "
                          + $"answered {(int)created.StatusCode} "
                          + $"{await created.Content.ReadAsStringAsync(cancellationToken)}",
            };
        }

        using var started = await _httpClient.PostAsync(
            $"/containers/{member}/start", content: null, cancellationToken);
        if (started.StatusCode is not (HttpStatusCode.NoContent or HttpStatusCode.NotModified))
        {
            return new PoolObservation
            {
                Outcome = PoolOutcomes.Failed,
                Diagnosis = $"'{member}' was created and will not start: the daemon answered "
                          + $"{(int)started.StatusCode}.",
            };
        }

        var inspected = await InspectAsync(member, cancellationToken);
        return new PoolObservation
        {
            Outcome = PoolOutcomes.Verified,
            ImageDigest = inspected?.ImageDigest,
            Provenance = EnvironmentProvenance.Fresh,
        };
    }

    /// <summary>
    /// What the daemon says about one member: whether it runs, the spec.Image id it
    /// actually resolved to, and — separately — the reference it was CREATED
    /// FROM.
    /// </summary>
    /// <remarks>
    /// <b>Two different images, and the difference is the whole of convergence.</b>
    /// <c>Image</c> is the resolved id, which is what an attestation should
    /// carry: what the member actually runs. <c>Config.Image</c> is the
    /// reference it was made from, which is the only thing comparable to what a
    /// strategy PINS — and both sides of that comparison are digest-pinned by a
    /// shipped refusal, so it is exact. Comparing the id to a strategy's
    /// reference would compare two spellings of different things and reset
    /// forever.
    /// </remarks>
    private async Task<(bool Running, string Status, int ExitCode, string? ImageDigest, string? MadeFrom)?>
        InspectAsync(string member, CancellationToken cancellationToken)
    {
        using var response = await _httpClient.GetAsync(
            $"/containers/{member}/json", cancellationToken);
        if (response.StatusCode == HttpStatusCode.NotFound)
        {
            return null;
        }

        response.EnsureSuccessStatusCode();

        using var inspected = JsonDocument.Parse(
            await response.Content.ReadAsStringAsync(cancellationToken));
        var state = inspected.RootElement.GetProperty("State");

        return (state.GetProperty("Running").GetBoolean(),
                state.GetProperty("Status").GetString() ?? "unknown",

                // ABSENT MEANS UNKNOWN, AND UNKNOWN IS NOT "FINISHED". A
                // daemon that does not report an exit code must not have one
                // inferred: -1 is no exit code any process produces, so the
                // startable arm takes it, which is the arm that destroys
                // nothing.
                state.TryGetProperty("ExitCode", out var exit) ? exit.GetInt32() : -1,

                inspected.RootElement.GetProperty("Image").GetString(),
                inspected.RootElement.TryGetProperty("Config", out var config)
                    && config.TryGetProperty("Image", out var madeFrom)
                        ? madeFrom.GetString()
                        : null);
    }
}
