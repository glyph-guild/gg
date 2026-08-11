using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Gg.Contracts;

namespace Gg.Client;

/// <summary>
/// What a verb produced. Every verb produces one of these and nothing else.
/// </summary>
/// <remarks>
/// <para>
/// No verb writes to a console. That is what makes "every verb has a console
/// equivalent and both render the same structured result" true by construction
/// rather than by discipline: there is no second path available to write, so
/// the console at step 4b and <c>--json</c> here cannot drift apart without
/// somebody deliberately adding one.
/// </para>
/// <para>
/// The wrapped values are the CONTRACT types - the same documents the control
/// plane sent. <c>--json</c> prints those unchanged rather than an envelope
/// invented here, so anything that knows the contract can read gg's output.
/// </para>
/// </remarks>
public abstract record VerbResult
{
    /// <summary>Names which shape this is, so a saved payload can be read back.</summary>
    public abstract string Kind { get; }

    public sealed record Flights(FlightList Value) : VerbResult
    {
        public override string Kind => VerbResultKinds.Flights;
    }

    public sealed record Flight(FlightSummary Value) : VerbResult
    {
        public override string Kind => VerbResultKinds.Flight;
    }

    public sealed record Launched(FlightLaunched Value) : VerbResult
    {
        public override string Kind => VerbResultKinds.Launched;
    }

    public sealed record Log(FlightLog Value) : VerbResult
    {
        public override string Kind => VerbResultKinds.Log;
    }

    public sealed record Runners(RunnerList Value) : VerbResult
    {
        public override string Kind => VerbResultKinds.Runners;
    }

    public sealed record Diagnosis(DoctorReport Value) : VerbResult
    {
        public override string Kind => VerbResultKinds.Diagnosis;
    }

    public sealed record Credentials(CredentialList Value) : VerbResult
    {
        public override string Kind => VerbResultKinds.Credentials;
    }

    public sealed record CredentialAdded(CredentialRegistered Value) : VerbResult
    {
        public override string Kind => VerbResultKinds.CredentialAdded;
    }

    public sealed record CredentialRemoved(Gg.Contracts.CredentialRemoved Value) : VerbResult
    {
        public override string Kind => VerbResultKinds.CredentialRemoved;
    }
}

/// <summary>The shapes a verb may produce.</summary>
public static class VerbResultKinds
{
    public const string Flights = "flights";
    public const string Flight = "flight";
    public const string Launched = "launched";
    public const string Log = "log";
    public const string Runners = "runners";
    public const string Diagnosis = "diagnosis";
    public const string Credentials = "credentials";
    public const string CredentialAdded = "credential-added";
    public const string CredentialRemoved = "credential-removed";
}

[JsonSourceGenerationOptions(
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    WriteIndented = true,
    DefaultIgnoreCondition = JsonIgnoreCondition.Never)]
[JsonSerializable(typeof(FlightList))]
[JsonSerializable(typeof(FlightSummary))]
[JsonSerializable(typeof(FlightLaunched))]
[JsonSerializable(typeof(FlightLog))]
[JsonSerializable(typeof(RunnerList))]
[JsonSerializable(typeof(DoctorReport))]
[JsonSerializable(typeof(CredentialList))]
[JsonSerializable(typeof(CredentialRegistered))]
[JsonSerializable(typeof(Gg.Contracts.CredentialRemoved))]
/// <summary>How verb results are written and read back.</summary>
/// <remarks>
/// <para>
/// Source-generated: this ships in a Native AOT binary, where reflection-based
/// serialization is not available at all.
/// </para>
/// <para>
/// The default encoder is kept, which escapes quotes and angle brackets as
/// \uXXXX. It reads a little worse - a JSON detail field comes out with its
/// quotes escaped - and every parser reads it identically.
/// The relaxed encoder is prettier and stops escaping the characters that
/// matter when somebody pipes this into a web page, which people do with
/// support output. Not worth trading for whitespace.
/// </para>
/// </remarks>
public sealed partial class VerbJsonContext : JsonSerializerContext;

/// <summary>
/// Writes a result as JSON, reads one back, and renders one for a person.
/// </summary>
/// <remarks>
/// <para>
/// <b>Reading back is not test scaffolding.</b> It is what lets gg re-render a
/// <c>--json</c> payload somebody sent us, which is the practical form of
/// "anything that can fail inside a customer's environment produces a
/// diagnosis they can send us" - we cannot look at their terminal, so the
/// document has to be enough.
/// </para>
/// <para>
/// It also makes the derivation assertable: rendering a result and rendering
/// it after a round trip must agree, and a renderer reaching for anything the
/// JSON does not carry breaks that.
/// </para>
/// </remarks>
public static class VerbOutput
{
    /// <summary>The result as JSON: the wire document, unwrapped.</summary>
    public static string ToJson(VerbResult result) => result switch
    {
        VerbResult.Flights r => JsonSerializer.Serialize(r.Value, VerbJsonContext.Default.FlightList),
        VerbResult.Flight r => JsonSerializer.Serialize(r.Value, VerbJsonContext.Default.FlightSummary),
        VerbResult.Launched r => JsonSerializer.Serialize(r.Value, VerbJsonContext.Default.FlightLaunched),
        VerbResult.Log r => JsonSerializer.Serialize(r.Value, VerbJsonContext.Default.FlightLog),
        VerbResult.Runners r => JsonSerializer.Serialize(r.Value, VerbJsonContext.Default.RunnerList),
        VerbResult.Diagnosis r => JsonSerializer.Serialize(r.Value, VerbJsonContext.Default.DoctorReport),
        VerbResult.Credentials r => JsonSerializer.Serialize(r.Value, VerbJsonContext.Default.CredentialList),
        VerbResult.CredentialAdded r =>
            JsonSerializer.Serialize(r.Value, VerbJsonContext.Default.CredentialRegistered),
        VerbResult.CredentialRemoved r =>
            JsonSerializer.Serialize(r.Value, VerbJsonContext.Default.CredentialRemoved),
        _ => throw Unknown(result?.Kind),
    };

    /// <summary>Reads a result back from a document gg wrote.</summary>
    public static VerbResult Parse(string kind, string json) => kind switch
    {
        VerbResultKinds.Flights => new VerbResult.Flights(Require(
            JsonSerializer.Deserialize(json, VerbJsonContext.Default.FlightList))),
        VerbResultKinds.Flight => new VerbResult.Flight(Require(
            JsonSerializer.Deserialize(json, VerbJsonContext.Default.FlightSummary))),
        VerbResultKinds.Launched => new VerbResult.Launched(Require(
            JsonSerializer.Deserialize(json, VerbJsonContext.Default.FlightLaunched))),
        VerbResultKinds.Log => new VerbResult.Log(Require(
            JsonSerializer.Deserialize(json, VerbJsonContext.Default.FlightLog))),
        VerbResultKinds.Runners => new VerbResult.Runners(Require(
            JsonSerializer.Deserialize(json, VerbJsonContext.Default.RunnerList))),
        VerbResultKinds.Diagnosis => new VerbResult.Diagnosis(Require(
            JsonSerializer.Deserialize(json, VerbJsonContext.Default.DoctorReport))),
        VerbResultKinds.Credentials => new VerbResult.Credentials(Require(
            JsonSerializer.Deserialize(json, VerbJsonContext.Default.CredentialList))),
        VerbResultKinds.CredentialAdded => new VerbResult.CredentialAdded(Require(
            JsonSerializer.Deserialize(json, VerbJsonContext.Default.CredentialRegistered))),
        VerbResultKinds.CredentialRemoved => new VerbResult.CredentialRemoved(Require(
            JsonSerializer.Deserialize(json, VerbJsonContext.Default.CredentialRemoved))),
        _ => throw Unknown(kind),
    };

    /// <summary>
    /// The result, rendered for a person.
    /// </summary>
    /// <remarks>
    /// Every value printed here comes from the result. Nothing consults a
    /// status code, a header or the request that produced it - those are not in
    /// the JSON, and a renderer that used them would make the two surfaces
    /// different views rather than one view twice.
    /// </remarks>
    public static string ToText(VerbResult result) => result switch
    {
        VerbResult.Flights r => Flights(r.Value),
        VerbResult.Flight r => Flight(r.Value),
        VerbResult.Launched r => Launched(r.Value),
        VerbResult.Log r => Log(r.Value),
        VerbResult.Runners r => Runners(r.Value),
        VerbResult.Diagnosis r => Diagnosis(r.Value),
        VerbResult.Credentials r => Credentials(r.Value),
        VerbResult.CredentialAdded r => CredentialAdded(r.Value),
        VerbResult.CredentialRemoved r => CredentialRemoved(r.Value),
        _ => throw Unknown(result?.Kind),
    };

    /// <summary>
    /// The references, and nothing else - because there is nothing else.
    /// </summary>
    /// <remarks>
    /// The locator is printed on purpose: it is a place, and a person needs to
    /// know which file <c>gg doctor</c> is talking about. There is no value
    /// here to withhold, which is the whole point of the row.
    /// </remarks>
    private static string Credentials(CredentialList list)
    {
        if (list.Credentials.Count == 0)
        {
            return "No credentials registered. Run gg credential add --repo <slug> to register one.";
        }

        var text = new StringBuilder();
        foreach (var credential in list.Credentials)
        {
            text.AppendLine(
                $"{Clean(credential.Repo),-28}  {Clean(credential.Reference.Identity),-16}  "
              + $"{Clean(string.Join(',', credential.Reference.Scopes)),-8}  "
              + $"{Clean(credential.Reference.Locator)}");
            text.AppendLine($"  id  {Clean(credential.CredentialId)}   added {credential.AddedAt:u}");
        }
        return text.ToString().TrimEnd();
    }

    private static string CredentialAdded(CredentialRegistered registered) =>
        $"Registered {Clean(registered.Reference.Identity)} for "
      + $"{Clean(string.Join(',', registered.Reference.Scopes))}. "
      + $"The control plane holds {Clean(registered.Reference.Locator)}; the value stays here.";

    private static string CredentialRemoved(Gg.Contracts.CredentialRemoved removed) =>
        $"Removed {Clean(removed.CredentialId)}. "
      + $"The reference is gone and so is {Clean(removed.Reference.Locator)} on this machine.";

    private static string Flights(FlightList list)
    {
        // Nothing found and nothing printed look identical in a terminal, and
        // one of them is a bug somebody should be chasing.
        if (list.Flights.Count == 0)
        {
            return "No flights.";
        }

        var text = new StringBuilder();
        foreach (var flight in list.Flights)
        {
            text.AppendLine($"{Clean(flight.FlightNumber),-10}  {flight.CreatedAt:u}  {Clean(flight.Name)}");
        }
        return text.ToString().TrimEnd();
    }

    private static string Flight(FlightSummary flight)
    {
        var text = new StringBuilder();
        text.AppendLine($"  {Clean(flight.FlightNumber)}  {Clean(flight.Name)}");
        text.AppendLine($"  id          {Clean(flight.FlightId)}");
        text.AppendLine($"  opened      {flight.CreatedAt:u}");
        text.AppendLine($"  intent      {Intent(flight.Intent)}");
        // What governed the flight. Printed because a flight log that cannot
        // say which constitution was in force is one nobody can act on later.
        text.AppendLine($"  protocol    {flight.RunnerProtocolVersion}");
        text.AppendLine($"  vocabulary  {Clean(flight.FactVocabularyVersion)}");
        text.AppendLine($"  constitution {Clean(flight.ConstitutionVersion)}");
        text.AppendLine($"  envelope    {Clean(flight.EnvelopeVersion)}");
        text.AppendLine(Facts(flight.Facts));
        return text.ToString().TrimEnd();
    }

    /// <summary>
    /// What the runner observed, rendered.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The first thing this surface shows that no part of the control plane
    /// could have known. It comes through the same verb path as everything
    /// else - the summary carries it - so the JSON and this rendering are two
    /// views of one document.
    /// </para>
    /// <para>
    /// Paths, counts, hashes and a commit. There is nothing here that could be
    /// a line of somebody's source code, which is the property the control
    /// plane's own absence scan proves and this rendering inherits.
    /// </para>
    /// </remarks>
    private static string Facts(IReadOnlyList<FactEnvelope> facts)
    {
        var text = new StringBuilder();
        text.AppendLine();

        if (facts.Count == 0)
        {
            // Said out loud. A flight with no facts and a flight whose facts we
            // failed to render look identical otherwise, and only one of them
            // means the runner never got there.
            text.AppendLine("  facts       (none yet - the runner reports them as it works)");
            return text.ToString().TrimEnd();
        }

        foreach (var fact in facts)
        {
            text.AppendLine($"  {Clean(fact.Kind),-20}  {fact.ObservedAt:u}");

            if (fact.Environment is { } environment)
            {
                text.AppendLine($"    host        {Clean(environment.HostFingerprint)[..16]}…  "
                              + $"({Clean(environment.Provenance)})");
                var image = Clean(environment.ImageDigest);
                text.AppendLine(
                    $"    image       {(image.Length > 0 ? image : "(not running in an image)")}");
                foreach (var tool in environment.Tools)
                {
                    text.AppendLine($"    {Clean(tool.Name),-11} {Clean(tool.Version)}");
                }
                foreach (var held in environment.Locks)
                {
                    text.AppendLine($"    lock        {Clean(held.Path)}  {Clean(held.Sha256)[..16]}…");
                }
            }

            if (fact.Change is { } change)
            {
                text.AppendLine($"    range       {Clean(change.BaseCommit)[..8]}…{Clean(change.HeadCommit)[..8]}");
                text.AppendLine(
                    $"    change      {change.FilesChanged} file(s), "
                  + $"+{change.LinesAdded} -{change.LinesRemoved}");

                // Said out loud, both of them. A consumer must never have to
                // guess whether it is looking at everything - a rollup is a
                // true statement at lower resolution, and a withheld count is
                // what keeps a filtered list from reading as a smaller change.
                if (change.Resolution == ChangeResolution.Directories)
                {
                    text.AppendLine(
                        $"    resolution  by directory - {change.FilesChanged} file(s) summarised "
                      + "because the full list would not fit the evidence budget");
                    foreach (var directory in change.Directories)
                    {
                        text.AppendLine(
                            $"      {Clean(directory.Directory),-28}  {directory.Files} file(s), "
                          + $"+{directory.LinesAdded} -{directory.LinesRemoved}");
                    }
                }
                else
                {
                    foreach (var path in change.Paths)
                    {
                        text.AppendLine(
                            $"      {Clean(path.Change),-8} {Clean(path.Path),-40}  "
                          + $"+{path.LinesAdded} -{path.LinesRemoved}  {Clean(path.Classification)}");
                    }
                }

                if (change.PathsWithheld > 0)
                {
                    text.AppendLine(
                        $"      ({change.PathsWithheld} path(s) withheld: above this tenant's "
                      + "classification ceiling)");
                }

                foreach (var language in change.Languages)
                {
                    text.AppendLine(
                        $"    {Clean(language.Language),-11} {language.Files} file(s), "
                      + $"+{language.LinesAdded} -{language.LinesRemoved}");
                }
            }

            if (fact.Source is { } source)
            {
                text.AppendLine($"    repo        {Clean(source.Slug)}");
                text.AppendLine($"    commit      {Clean(source.HeadCommit)}");
                text.AppendLine($"    ref         {Clean(source.RequestedRef)} → {Clean(source.ResolvedRef)}");
                // Named rather than implied. A run that examined a fork and did
                // not say so is a false fact, which this design treats as
                // unrecoverable - so the rendering says it either way.
                var head = source.HeadIsFork
                    ? $"a fork, {Clean(source.ForkSlug)}"
                    : "the base repository";
                text.AppendLine($"    head        {head}");
                text.AppendLine($"    size        {source.FileCount} file(s), {source.Bytes:N0} bytes");
            }
        }

        return text.ToString().TrimEnd();
    }

    private static string Intent(FlightIntent intent) =>
        intent.Kind switch
        {
            FlightIntentKinds.Uri => Clean(intent.Uri, lines: true),
            _ => Clean(intent.Text, lines: true),
        };

    private static string Launched(FlightLaunched launched) =>
        // Null is not missing: at 202 the number has not been minted. Saying so
        // beats printing an empty column and letting somebody guess.
        launched.FlightNumber is { Length: > 0 } number
            ? $"Opened {Clean(number)}."
            : $"Opened {Clean(launched.FlightId)}. The flight number is assigned as it starts.";

    private static string Log(FlightLog log)
    {
        var text = new StringBuilder();
        text.AppendLine($"  {Clean(log.FlightNumber)}  {Clean(log.FlightId)}");

        if (log.Entries.Count == 0)
        {
            text.AppendLine("  (nothing has happened yet)");
            return text.ToString().TrimEnd();
        }

        foreach (var entry in log.Entries)
        {
            text.AppendLine($"  {entry.At:u}  {Clean(entry.Kind),-16}  {Clean(entry.Detail)}");
        }
        return text.ToString().TrimEnd();
    }

    private static string Runners(RunnerList list)
    {
        if (list.Runners.Count == 0)
        {
            return "No runners registered. Run gg runner up on a machine that should take work.";
        }

        var text = new StringBuilder();
        foreach (var runner in list.Runners)
        {
            var on = runner.CurrentFlightNumber is { Length: > 0 } number ? $"  on {Clean(number)}" : "";
            var beat = runner.LastHeartbeatAt is { } at ? $"  last seen {at:u}" : "  never seen";
            text.AppendLine($"{Clean(runner.State),-8}  {Clean(runner.Label),-16}{beat}{on}");
        }
        return text.ToString().TrimEnd();
    }

    private static string Diagnosis(DoctorReport report)
    {
        var text = new StringBuilder();
        foreach (var check in report.Checks)
        {
            // Blocking and fixable are printed separately because they are
            // answered separately. Collapsing them into one severity loses the
            // two cases that matter: a blocking problem the person cannot fix,
            // and a non-blocking one they can.
            var mark = check.Passed ? "ok  " : check.Blocking ? "STOP" : "warn";
            text.AppendLine($"{mark}  {Clean(check.Name),-16}  {Clean(check.Detail)}");

            if (!check.Passed && check.Fixable && check.Fix is { Length: > 0 } fix)
            {
                text.AppendLine($"      fix: {Clean(fix)}");
            }
            else if (!check.Passed && !check.Fixable)
            {
                text.AppendLine("      not something this machine can fix.");
            }
        }
        return text.ToString().TrimEnd();
    }

    /// <summary>
    /// Last line of defence against a control sequence reaching a terminal.
    /// </summary>
    /// <remarks>
    /// Text is stored clean, so in a healthy system this removes nothing. It is
    /// here because gg is the process that actually writes to the terminal: a
    /// control plane that was compromised, or an older one that stored a name
    /// before stripping existed, must not be able to drive it through us.
    /// </remarks>
    private static string Clean(string? value, bool lines = false) => ControlText.Strip(value, lines);

    private static InvalidOperationException Unknown(string? kind) =>
        new($"'{kind}' is not a result gg knows how to render. "
          + $"Expected one of: {VerbResultKinds.Flights}, {VerbResultKinds.Flight}, "
          + $"{VerbResultKinds.Launched}, {VerbResultKinds.Log}, {VerbResultKinds.Runners}, "
          + $"{VerbResultKinds.Diagnosis}, {VerbResultKinds.Credentials}, "
          + $"{VerbResultKinds.CredentialAdded}, {VerbResultKinds.CredentialRemoved}.");

    private static T Require<T>(T? value) where T : class =>
        value ?? throw new InvalidOperationException("The result document was empty.");
}
