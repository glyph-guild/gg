using System.Globalization;
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

    public sealed record Invited(InvitationIssued Value) : VerbResult
    {
        public override string Kind => VerbResultKinds.Invited;
    }

    /// <summary>The checklist: what a flight would need, and who could satisfy it.</summary>
    /// <summary>The topology, root first - what gg airspace show renders.</summary>
    public sealed record AirspaceTopology(EnvelopeTopology Value) : VerbResult
    {
        public override string Kind => VerbResultKinds.AirspaceTopology;
    }

    /// <summary>What this tenant has registered, and can therefore fly against.</summary>
    public sealed record AirspaceRepositories(RegisteredRepositories Value) : VerbResult
    {
        public override string Kind => VerbResultKinds.AirspaceRepositories;
    }

    /// <summary>What a pull did to the working copy.</summary>
    public sealed record AirspacePulled(TreeWritten Value) : VerbResult
    {
        public override string Kind => VerbResultKinds.AirspacePulled;
    }

    /// <summary>What applying the working copy came to.</summary>
    public sealed record AirspaceApplied(EstateApplied Value) : VerbResult
    {
        public override string Kind => VerbResultKinds.AirspaceApplied;
    }

    /// <summary>What the working copy would change.</summary>
    public sealed record AirspaceDiffed(EstateDiff Value) : VerbResult
    {
        public override string Kind => VerbResultKinds.AirspaceDiffed;
    }

    public sealed record Plan(Checklist Value) : VerbResult
    {
        public override string Kind => VerbResultKinds.Plan;
    }

    /// <summary>
    /// The fleet's advertised labels, each with its disposition. The same wire
    /// document as <see cref="Runners"/>, rendered per label - one document,
    /// two views, the FlightSummary-facts precedent.
    /// </summary>
    public sealed record RunnerLabels(RunnerList Value) : VerbResult
    {
        public override string Kind => VerbResultKinds.RunnerLabels;
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

    public sealed record Bundle(DiagnosticsBundle Value) : VerbResult
    {
        public override string Kind => VerbResultKinds.Bundle;
    }

    public sealed record EnvelopeShown(EnvelopeState Value) : VerbResult
    {
        public override string Kind => VerbResultKinds.Envelope;
    }

    /// <summary>
    /// What applying produced, and what applying discarded.
    /// </summary>
    /// <remarks>
    /// The notes ride alongside the wire document rather than in it: what the
    /// round trip dropped is a fact about THIS machine's text, and the control
    /// plane never saw the text.
    /// </remarks>
    public sealed record EnvelopeApplied(
        Gg.Contracts.EnvelopeApplied Value, IReadOnlyList<string> Notes) : VerbResult
    {
        public override string Kind => VerbResultKinds.EnvelopeApplied;
    }

    public sealed record EnvelopeValidated(EnvelopeValidation Value) : VerbResult
    {
        public override string Kind => VerbResultKinds.EnvelopeValidated;
    }

    /// <summary>
    /// Why each obligation applied to a flight, or did not.
    /// </summary>
    /// <remarks>
    /// <b>Rendered, never computed.</b> The value arrives from the control plane
    /// already decided. A client that worked out for itself why an obligation
    /// attached could explain a verdict it did not produce, and the two would
    /// drift - which is Article IX wearing the costume of a rendering concern.
    /// </remarks>
    public sealed record Why(FlightAttribution Value) : VerbResult
    {
        public override string Kind => VerbResultKinds.Why;
    }

    /// <summary>
    /// What is waiting on a person.
    /// </summary>
    /// <remarks>
    /// <b>A list, and there is nothing beside it that answers one.</b> Nothing an
    /// agent can call may unstick a flight, and the cheapest guarantee of that is
    /// for the verb that shows gates to have no companion that resolves them.
    /// </remarks>
    public sealed record Gates(GateList Value) : VerbResult
    {
        public override string Kind => VerbResultKinds.Gates;
    }

    /// <summary>
    /// What the control plane did with a decision.
    /// </summary>
    /// <remarks>
    /// <b>Rendered, never computed.</b> Whether the work may now land arrived in the
    /// response; a client that worked it out from the decision it just posted would be
    /// deciding admission, which is not its job.
    /// </remarks>
    /// <remarks>
    /// <b>A report rather than the record, since ADR-0012 step 1.</b> What happened is
    /// OBSERVED on the read surface; the synchronous answer rides along beside it while
    /// there still is one. Step 2 empties that field and this shape does not move.
    /// </remarks>
    public sealed record Decided(DecisionReport Value) : VerbResult
    {
        public override string Kind => VerbResultKinds.Decided;
    }

    /// <summary>
    /// A flight taken over: what it tried and ruled out, plus this hold's terms.
    /// </summary>
    /// <remarks>
    /// <b>The notes are not in the document, deliberately, and it is the same
    /// arrangement <see cref="EnvelopeApplied"/> uses.</b> The seed is a fact about
    /// the FLIGHT; when this invocation's hold lapses and how soon to renew are
    /// facts about this invocation. A result read back from JSON has no notes,
    /// because inventing them would make a re-rendered payload claim a hold the
    /// process re-rendering it never took.
    /// </remarks>
    public sealed record Taken(TakeSeed Value, IReadOnlyList<string> Notes) : VerbResult
    {
        public override string Kind => VerbResultKinds.Taken;
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
    public const string Invited = "invited";
    public const string Diagnosis = "diagnosis";
    public const string Credentials = "credentials";
    public const string CredentialAdded = "credential-added";
    public const string CredentialRemoved = "credential-removed";
    public const string Bundle = "bundle";
    public const string Envelope = "envelope";
    public const string EnvelopeApplied = "envelope-applied";
    public const string EnvelopeValidated = "envelope-validated";

    public const string Plan = "plan";
    public const string AirspaceTopology = "airspace-topology";

    /// <summary>What this tenant has registered.</summary>
    public const string AirspaceRepositories = "airspace-repositories";

    public const string AirspacePulled = "airspace-pulled";

    public const string AirspaceApplied = "airspace-applied";

    public const string AirspaceDiffed = "airspace-diffed";
    public const string RunnerLabels = "runner-labels";

    public const string Why = "why";
    public const string Gates = "gates";
    public const string Decided = "decided";
    public const string Taken = "taken";
}

[JsonSourceGenerationOptions(
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    WriteIndented = true,
    DefaultIgnoreCondition = JsonIgnoreCondition.Never)]
[JsonSerializable(typeof(InvitationIssued))]
[JsonSerializable(typeof(FlightList))]
[JsonSerializable(typeof(FlightSummary))]
[JsonSerializable(typeof(FlightLaunched))]
[JsonSerializable(typeof(FlightLog))]
[JsonSerializable(typeof(RunnerList))]
[JsonSerializable(typeof(DoctorReport))]
[JsonSerializable(typeof(CredentialList))]
[JsonSerializable(typeof(CredentialRegistered))]
[JsonSerializable(typeof(Gg.Contracts.CredentialRemoved))]
[JsonSerializable(typeof(DiagnosticsBundle))]
[JsonSerializable(typeof(EnvelopeState))]
[JsonSerializable(typeof(FlightAttribution))]
[JsonSerializable(typeof(GateList))]
[JsonSerializable(typeof(DecisionRecorded))]
[JsonSerializable(typeof(DecisionReport))]
[JsonSerializable(typeof(Gg.Contracts.EnvelopeApplied))]
[JsonSerializable(typeof(EnvelopeValidation))]
[JsonSerializable(typeof(TakeSeed))]
[JsonSerializable(typeof(Checklist))]
[JsonSerializable(typeof(EnvelopeTopology))]
[JsonSerializable(typeof(RegisteredRepositories))]
[JsonSerializable(typeof(TreeWritten))]
[JsonSerializable(typeof(EstateApplied))]
[JsonSerializable(typeof(EstateDiff))]
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
        VerbResult.Invited r => JsonSerializer.Serialize(r.Value, VerbJsonContext.Default.InvitationIssued),
        VerbResult.Diagnosis r => JsonSerializer.Serialize(r.Value, VerbJsonContext.Default.DoctorReport),
        VerbResult.Credentials r => JsonSerializer.Serialize(r.Value, VerbJsonContext.Default.CredentialList),
        VerbResult.CredentialAdded r =>
            JsonSerializer.Serialize(r.Value, VerbJsonContext.Default.CredentialRegistered),
        VerbResult.CredentialRemoved r =>
            JsonSerializer.Serialize(r.Value, VerbJsonContext.Default.CredentialRemoved),
        VerbResult.Bundle r => JsonSerializer.Serialize(r.Value, VerbJsonContext.Default.DiagnosticsBundle),
        VerbResult.EnvelopeShown r => JsonSerializer.Serialize(r.Value, VerbJsonContext.Default.EnvelopeState),
        VerbResult.Why r => JsonSerializer.Serialize(r.Value, VerbJsonContext.Default.FlightAttribution),
        VerbResult.Gates r => JsonSerializer.Serialize(r.Value, VerbJsonContext.Default.GateList),
        VerbResult.Decided r => JsonSerializer.Serialize(r.Value, VerbJsonContext.Default.DecisionReport),
        VerbResult.Taken r => JsonSerializer.Serialize(r.Value, VerbJsonContext.Default.TakeSeed),
        VerbResult.EnvelopeApplied r =>
            JsonSerializer.Serialize(r.Value, VerbJsonContext.Default.EnvelopeApplied),
        VerbResult.EnvelopeValidated r =>
            JsonSerializer.Serialize(r.Value, VerbJsonContext.Default.EnvelopeValidation),
        VerbResult.Plan r => JsonSerializer.Serialize(r.Value, VerbJsonContext.Default.Checklist),
        VerbResult.AirspaceTopology r =>
            JsonSerializer.Serialize(r.Value, VerbJsonContext.Default.EnvelopeTopology),
        VerbResult.AirspaceRepositories r =>
            JsonSerializer.Serialize(r.Value, VerbJsonContext.Default.RegisteredRepositories),
        VerbResult.AirspacePulled r =>
            JsonSerializer.Serialize(r.Value, VerbJsonContext.Default.TreeWritten),
        VerbResult.AirspaceApplied r =>
            JsonSerializer.Serialize(r.Value, VerbJsonContext.Default.EstateApplied),
        VerbResult.AirspaceDiffed r =>
            JsonSerializer.Serialize(r.Value, VerbJsonContext.Default.EstateDiff),
        VerbResult.RunnerLabels r =>
            JsonSerializer.Serialize(r.Value, VerbJsonContext.Default.RunnerList),
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
        VerbResultKinds.Invited => new VerbResult.Invited(Require(
            JsonSerializer.Deserialize(json, VerbJsonContext.Default.InvitationIssued))),
        VerbResultKinds.Diagnosis => new VerbResult.Diagnosis(Require(
            JsonSerializer.Deserialize(json, VerbJsonContext.Default.DoctorReport))),
        VerbResultKinds.Credentials => new VerbResult.Credentials(Require(
            JsonSerializer.Deserialize(json, VerbJsonContext.Default.CredentialList))),
        VerbResultKinds.CredentialAdded => new VerbResult.CredentialAdded(Require(
            JsonSerializer.Deserialize(json, VerbJsonContext.Default.CredentialRegistered))),
        VerbResultKinds.CredentialRemoved => new VerbResult.CredentialRemoved(Require(
            JsonSerializer.Deserialize(json, VerbJsonContext.Default.CredentialRemoved))),
        VerbResultKinds.Bundle => new VerbResult.Bundle(Require(
            JsonSerializer.Deserialize(json, VerbJsonContext.Default.DiagnosticsBundle))),
        VerbResultKinds.Envelope => new VerbResult.EnvelopeShown(Require(
            JsonSerializer.Deserialize(json, VerbJsonContext.Default.EnvelopeState))),
        // The notes are not in the document, so a result read back has none.
        // That is honest rather than lossy: they were about text this process
        // never saw.
        VerbResultKinds.EnvelopeApplied => new VerbResult.EnvelopeApplied(Require(
            JsonSerializer.Deserialize(json, VerbJsonContext.Default.EnvelopeApplied)), []),
        VerbResultKinds.EnvelopeValidated => new VerbResult.EnvelopeValidated(Require(
            JsonSerializer.Deserialize(json, VerbJsonContext.Default.EnvelopeValidation))),
        // No notes, for the reason written on the record: they described this
        // invocation's hold, and a payload re-rendered somewhere else holds nothing.
        VerbResultKinds.Plan => new VerbResult.Plan(Require(
            JsonSerializer.Deserialize(json, VerbJsonContext.Default.Checklist))),
        VerbResultKinds.AirspacePulled => new VerbResult.AirspacePulled(Require(
            JsonSerializer.Deserialize(json, VerbJsonContext.Default.TreeWritten))),
        VerbResultKinds.AirspaceApplied => new VerbResult.AirspaceApplied(Require(
            JsonSerializer.Deserialize(json, VerbJsonContext.Default.EstateApplied))),
        VerbResultKinds.AirspaceDiffed => new VerbResult.AirspaceDiffed(Require(
            JsonSerializer.Deserialize(json, VerbJsonContext.Default.EstateDiff))),
        VerbResultKinds.AirspaceTopology => new VerbResult.AirspaceTopology(Require(
            JsonSerializer.Deserialize(json, VerbJsonContext.Default.EnvelopeTopology))),
        VerbResultKinds.AirspaceRepositories => new VerbResult.AirspaceRepositories(Require(
            JsonSerializer.Deserialize(json, VerbJsonContext.Default.RegisteredRepositories))),
        VerbResultKinds.RunnerLabels => new VerbResult.RunnerLabels(Require(
            JsonSerializer.Deserialize(json, VerbJsonContext.Default.RunnerList))),
        VerbResultKinds.Taken => new VerbResult.Taken(Require(
            JsonSerializer.Deserialize(json, VerbJsonContext.Default.TakeSeed)), []),
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
        VerbResult.Invited r => Invited(r.Value),
        VerbResult.Diagnosis r => Diagnosis(r.Value),
        VerbResult.Credentials r => Credentials(r.Value),
        VerbResult.CredentialAdded r => CredentialAdded(r.Value),
        VerbResult.CredentialRemoved r => CredentialRemoved(r.Value),
        VerbResult.Bundle r => Bundle(r.Value),
        VerbResult.EnvelopeShown r => Envelope(r.Value),
        VerbResult.Why r => WhyText(r.Value),
        VerbResult.Gates r => GatesText(r.Value),
        VerbResult.Decided r => DecidedText(r.Value),
        VerbResult.EnvelopeApplied r => EnvelopeApplied(r.Value, r.Notes),
        VerbResult.EnvelopeValidated r => EnvelopeValidated(r.Value),
        VerbResult.Taken r => TakenText(r.Value, r.Notes),
        VerbResult.Plan r => PlanText(r.Value),
        VerbResult.AirspaceTopology r => AirspaceText(r.Value),
        VerbResult.AirspaceRepositories r => RepositoriesText(r.Value),
        VerbResult.AirspacePulled r => PulledText(r.Value),
        VerbResult.AirspaceApplied r => AppliedText(r.Value),
        VerbResult.AirspaceDiffed r => DiffText(r.Value),
        VerbResult.RunnerLabels r => RunnerLabelsText(r.Value),
        _ => throw Unknown(result?.Kind),
    };

    /// <summary>
    /// The seed as its own composer renders it, with this hold's terms after.
    /// </summary>
    /// <remarks>
    /// <b>Rendered by the CONTRACT's composer rather than here.</b> The same text
    /// reaches a person at a terminal and an agent reading declared context, and
    /// two renderings of one document would drift on the first change to either.
    /// </remarks>
    private static string TakenText(TakeSeed seed, IReadOnlyList<string> notes)
    {
        var text = new StringBuilder(TakeSeedComposer.Render(seed));

        if (notes.Count == 0)
        {
            return text.ToString();
        }

        text.AppendLine();
        text.AppendLine();

        foreach (var note in notes)
        {
            text.AppendLine(note);
        }

        return text.ToString().TrimEnd();
    }

    /// <summary>
    /// The envelope, as its canonical text, with the version above it.
    /// </summary>
    /// <remarks>
    /// The rendering IS the canonical form rather than a prettier summary of
    /// it: what a person reads here has to be what they can edit and apply
    /// back, or `show` and `apply` are two different documents.
    /// </remarks>
    private static string Envelope(EnvelopeState state)
    {
        var text = new StringBuilder();
        text.AppendLine($"# version   {Clean(state.Version)}");
        text.AppendLine($"# updated   {state.UpdatedAt:u} by {Clean(state.UpdatedBy)}");
        text.AppendLine();
        text.Append(EnvelopeText.Render(state.Envelope));
        return text.ToString().TrimEnd();
    }

    private static string EnvelopeApplied(
        Gg.Contracts.EnvelopeApplied applied, IReadOnlyList<string> notes)
    {
        var text = new StringBuilder();

        // "Nothing changed" is an answer, not a non-event. A version minted per
        // apply would make "which rules governed this change" differ for two
        // flights that ran under the same rules.
        text.AppendLine(applied.Changed
            ? $"Applied. This envelope is now {Clean(applied.Version)}."
            : $"Nothing changed. The envelope is still {Clean(applied.Version)}.");

        foreach (var note in notes)
        {
            text.AppendLine($"  note: {Clean(note)}");
        }

        return text.ToString().TrimEnd();
    }

    private static string EnvelopeValidated(EnvelopeValidation validation)
    {
        var text = new StringBuilder();

        if (!validation.Valid)
        {
            text.AppendLine("This is not an envelope.");
            text.AppendLine($"  {Clean(validation.Diagnosis)}");
            return text.ToString().TrimEnd();
        }

        text.AppendLine("This is a valid envelope.");
        foreach (var note in validation.Notes)
        {
            text.AppendLine($"  note: {Clean(note)}");
        }

        if (validation.Canonical is { Length: > 0 } canonical)
        {
            text.AppendLine();
            text.AppendLine("It will be stored and rendered back as:");
            text.AppendLine();
            text.Append(canonical);
        }

        return text.ToString().TrimEnd();
    }

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
            text.AppendLine(
                $"{Clean(flight.FlightNumber),-10}  {flight.CreatedAt:u}  "
              + $"{Rendered(flight.State),-9}  {Clean(flight.Name)}");

            // ONLY when it is waiting. A waiting column on every healthy
            // flight is noise somebody learns to skip, and null means not
            // waiting - the LeaseClaimStatus.Lease absence rule.
            if (flight.Waiting is { } waiting)
            {
                // Derived from the kind - one grammar, contract-side. An
                // unknown kind THROWS in Sentence: a render that shrugged
                // would file a governed refusal under healthy.
                text.AppendLine($"            {Clean(Reason.Sentence(waiting.Kind, waiting.Params))}");
            }
        }
        return text.ToString().TrimEnd();
    }

    /// <summary>How a flight's state prints, and what happens when it cannot.</summary>
    /// <remarks>
    /// <para>
    /// <b>Article XI, and RunnerSummary.State's throw one noun over.</b> A state
    /// nothing can render halts rather than being shown as one that can. The
    /// alternative is worse than it looks: the plausible default here is
    /// <c>open</c>, and printing a flight that ended in a way this build does
    /// not understand as one somebody is still working on is precisely the
    /// confusion the whole vocabulary exists to remove.
    /// </para>
    /// <para>
    /// <b><c>unknown</c> renders, deliberately.</b> It is a real state - a
    /// flight nobody can account for - and it is exactly what somebody should
    /// see. Only a value outside the vocabulary halts.
    /// </para>
    /// </remarks>
    private static string Rendered(string state) =>
        FlightStates.All.Contains(state, StringComparer.Ordinal)
            ? state
            : throw new InvalidOperationException(
                $"Flight state '{state}' has no published name. A state nothing can render "
              + "must not be shown as one that can - and the plausible guess here is "
              + "'open', which would show a finished flight as one somebody is still "
              + "working on.");

    /// <summary>
    /// Prose under a label, laid out the way the label promised.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>A field's value that arrives with line breaks in it is still one
    /// value</b>, and a block rendering has exactly one way to say so: put the
    /// continuation under the column the label opened. Pasted in raw, the
    /// second line lands at column zero, where the surrounding conventions make
    /// it a heading - a gate list renders one three-line question as three
    /// gates, and the field below it reads as the sentence's last line.
    /// </para>
    /// <para>
    /// <b>The line breaks are the agent's own and they stay.</b> Flattening
    /// them would be the easier fix and the wrong one: a question laid out over
    /// three lines is one somebody wrote to be read, and the field a person
    /// reads while deciding something is the last one to reformat.
    /// </para>
    /// </remarks>
    private static string Prose(string? value, int column) =>
        Clean(value, lines: true).ReplaceLineEndings("\n" + new string(' ', column));

    /// <summary>
    /// How a loop ended, in words rather than in a vocabulary value.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Two of the four are not failures and the surface has to carry that on
    /// its own.</b> <c>exhausted</c> ran out of budget and <c>blocked</c> asked
    /// for a decision it is not allowed to make; both are waiting on somebody,
    /// and a reader who files either beside a crash pages the wrong person.
    /// This feature is worth nothing if the first thing an estate learns is
    /// that <i>blocked</i> means <i>broken</i>.
    /// </para>
    /// <para>
    /// <b>The word stays, and the sentence explains it.</b> The vocabulary
    /// value is what a script reads and what somebody types into a search; a
    /// rendering that replaced it would leave the two surfaces with no shared
    /// noun.
    /// </para>
    /// <para>
    /// <b>An unknown value throws</b>, on <see cref="Rendered"/>'s argument one
    /// noun over. The plausible default here is <c>failed</c>, and showing a
    /// state this build does not understand as a failure is exactly the
    /// confusion the vocabulary exists to remove.
    /// </para>
    /// </remarks>
    private static string Outcome(string outcome) => outcome switch
    {
        LoopOutcomes.Completed => $"{outcome} - the loop finished on its own terms",
        LoopOutcomes.Failed => $"{outcome} - something went wrong and it said what",
        LoopOutcomes.Exhausted =>
            $"{outcome} - it ran out of budget and is waiting on whoever the envelope's "
          + "on-exhaustion names. Waiting, not broken",
        LoopOutcomes.Blocked =>
            $"{outcome} - it asked for a decision it is not allowed to make and stopped. "
          + "Waiting on a person, not broken and not finished",
        _ => throw new InvalidOperationException(
            $"Loop outcome '{outcome}' has no published name. An outcome nothing can render "
          + "must not be shown as one that can - and the plausible guess here is 'failed', "
          + "which would page somebody about a loop that is merely waiting. Expected one of: "
          + string.Join(", ", LoopOutcomes.All) + "."),
    };

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

        // The reason it cannot start, by name, only when there is one. The
        // sentence derives from the kind; an unknown kind throws in Sentence.
        if (flight.Waiting is { } waiting)
        {
            text.AppendLine($"  {Clean(Reason.Sentence(waiting.Kind, waiting.Params))}");
        }

        // HOW MANY TIMES THIS HAS BEEN ROUND. Nothing enforces a ceiling - budget.attempts
        // does not exist yet - so a person deciding is the only thing that stops a
        // reject-and-run cycle, and they can only be that if they can see the count.
        //
        // None rather than 0, because a flight nobody has run is not a flight that ran
        // once, and a zero beside a label reads like a counter that failed to increment.
        text.AppendLine(
            $"  attempts    {(flight.Attempts == 0 ? "none" : flight.Attempts.ToString(CultureInfo.InvariantCulture))}");
        text.AppendLine(Facts(flight.Facts, flight.Intent));
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
    private static string Facts(IReadOnlyList<FactEnvelope> facts, FlightIntent intent)
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

            if (fact.Loop is { } loop)
            {
                text.AppendLine($"    loop        {Clean(loop.LoopId)}");
                text.AppendLine($"    outcome     {Outcome(loop.Outcome)}");
                text.AppendLine($"    reason      {Prose(loop.Reason, 16)}");
                // MEASURED, BESIDE THE PROSE. The reason is what the executor
                // said about its run; these are what the run WAS, and a reason
                // with no run behind it is a sentence somebody has to take on
                // trust.
                text.AppendLine(
                    $"    ran         {loop.Attempts} turn(s) over "
                  + $"{loop.DurationMs / 1000d:0.#}s on {Clean(loop.Executor)}");
                text.AppendLine(
                    $"    moves       {Clean(string.Join(", ", loop.MovesUsed))}");
            }

            if (fact.Question is { } question)
            {
                // THE ONE FIELD, AND IT IS THE POINT OF THE FACT. A question
                // nobody can read without fetching an artifact is a question
                // nobody answers, which is why it is inline - and a question
                // nobody can find on the flight is the same thing with an extra
                // step. Rule 3 means a recorded question need not have opened a
                // gate, so this surface is the only place some of them appear.
                text.AppendLine($"    asked       {Prose(question.Question, 16)}");
            }

            if (fact.Nomination is { } nomination)
            {
                // WHAT WAS DECIDED, not that a decision happened. A classify
                // flight's whole product is this one value and the sentence
                // behind it, and a rendering that showed the fact's kind and
                // stopped would report a decision without reporting it.
                text.AppendLine($"    nominated   {Clean(nomination.WorkKind)}");
                text.AppendLine($"    because     {Clean(nomination.Reason, lines: true)}");

                // A POINTER FOR A PERSON, WHICH IS A QUERY AND NOT A
                // REFERENCE. Neither flight holds a field naming the other -
                // the control plane asserts that as an absence over the command
                // that opens one - so this cannot print a flight number and
                // must not grow a field to make it able to. Correlation is the
                // work item, so what a person needs is the command that groups
                // them, built from this flight's OWN intent.
                if (Correlatable(intent) is { Length: > 0 } token)
                {
                    text.AppendLine($"    opened      gg flights --intent {token}");
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

    /// <summary>
    /// This flight's intent as <c>gg flights --intent</c> takes it, or empty
    /// when it is not something that can be correlated.
    /// </summary>
    /// <remarks>
    /// <b>Empty for a text intent, and that is the honest answer.</b> "Fix the
    /// login bug" identifies no line of work anybody else could ask about, so
    /// there is no query to print - and printing one that answered about every
    /// text flight would be worse than printing nothing.
    /// </remarks>
    private static string Correlatable(FlightIntent intent) =>
        intent.Kind switch
        {
            FlightIntentKinds.Ticket when intent.Provider is { Length: > 0 }
                                       && intent.Id is { Length: > 0 } =>
                $"{Clean(intent.Provider)}#{Clean(intent.Id)}",
            FlightIntentKinds.Uri when intent.Uri is { Length: > 0 } => Clean(intent.Uri),
            _ => "",
        };

    private static string Intent(FlightIntent intent) =>
        intent.Kind switch
        {
            FlightIntentKinds.Uri => Clean(intent.Uri, lines: true),
            FlightIntentKinds.Ticket => $"{Clean(intent.Provider)}#{Clean(intent.Id)}",
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
            // The labels are on the row because this listing is where somebody
            // looks when a flight is waiting, and the label is what they are
            // looking for. The dispositions stay with gg runner labels.
            var labels = runner.Labels.Count > 0
                ? $"  [{string.Join(", ", runner.Labels.Select(l => Clean(l.Name)))}]"
                : "";
            text.AppendLine($"{Clean(runner.State),-8}  {Clean(runner.Label),-16}{beat}{on}{labels}");
        }
        return text.ToString().TrimEnd();
    }

    /// <summary>
    /// The checklist, one row per requirement.
    /// </summary>
    /// <remarks>
    /// The satisfier column renders exactly two sentences in this slice -
    /// already true via matching, and nobody: declared capability gap -
    /// because strategies do not exist yet, and rendering a placeholder for
    /// machinery that does not exist would be the checklist containing a
    /// promise. The wire carries the closed vocabulary; the sentence is this
    /// rendering's.
    /// </remarks>
    /// <summary>
    /// The topology as a table: name, role, parent, and who declared it.
    /// </summary>
    /// <remarks>
    /// In the server's order - root first, then declaration order - because
    /// the reading order is the authority order: everything below the floor
    /// narrows it.
    /// </remarks>
    /// <summary>
    /// What this tenant can fly against, one repository a line.
    /// </summary>
    /// <remarks>
    /// <b>An empty registry is said out loud.</b> A tenant with nothing
    /// registered and a tenant whose read failed look identical as a blank
    /// answer, and only one of them is a person's next action.
    /// </remarks>
    private static string RepositoriesText(RegisteredRepositories registered)
    {
        if (registered.Repositories.Count == 0)
        {
            return "No repositories are registered. Nothing can be flown against a repository "
                 + "until one is - see gg airspace register.";
        }

        var text = new System.Text.StringBuilder();

        foreach (var repository in registered.Repositories)
        {
            text.Append(repository.Name)
                .Append("  ")
                .Append(repository.Provider)
                .Append('/')
                .Append(repository.Path);

            if (repository.Ref is { Length: > 0 } pinned)
            {
                text.Append("  @").Append(pinned);
            }

            text.AppendLine();
        }

        return text.ToString().TrimEnd();
    }

    private static string AirspaceText(EnvelopeTopology topology)
    {
        var text = new System.Text.StringBuilder();

        foreach (var name in topology.Names)
        {
            text.Append(name.Name).Append("  role=").Append(name.Role);
            if (name.Parent is { Length: > 0 } parent)
            {
                text.Append("  under=").Append(parent);
            }

            if (name.SubjectBinding is { Length: > 0 } binding)
            {
                text.Append("  subject=").Append(binding);
            }

            text.Append("  declared by ").Append(name.DeclaredBy);
            text.AppendLine();
        }

        return text.ToString();
    }

    private static string PlanText(Checklist plan)
    {
        var text = new StringBuilder();
        var about = plan.FlightNumber is { Length: > 0 } flight
            ? $"{Clean(flight)} (envelope {Clean(plan.EnvelopeVersion)})"
            : $"envelope {Clean(plan.EnvelopeVersion)}";
        text.AppendLine($"  checklist for {about}");

        if (plan.Environment is { Length: > 0 } environment)
        {
            text.AppendLine($"  environment {Clean(environment)}");
        }

        if (plan.Repository is { Length: > 0 } repository)
        {
            text.AppendLine($"  repository  {Clean(repository)}");
        }

        if (plan.Items.Count == 0)
        {
            // Said out loud: an empty checklist and one that failed to render
            // look identical otherwise, and only one means "start any time".
            text.AppendLine("  nothing must hold first - this envelope selects no environment");
            return text.ToString().TrimEnd();
        }

        foreach (var item in plan.Items)
        {
            var satisfier = item.Satisfier switch
            {
                ChecklistSatisfiers.MatchingRunner => "already true via matching",
                ChecklistSatisfiers.Nobody => "nobody: declared capability gap",
                ChecklistSatisfiers.DeclinedByBound => "declined by bound",
                // The capacity is there and a declaration is holding it: the
                // row says so, and the reason underneath says which of the
                // three ways and who to go to. "nobody" here would send a
                // person to bring up a machine they already own.
                ChecklistSatisfiers.Withheld => "withheld: capacity exists, held back",
                var other => Clean(other ?? ""),
            };
            text.AppendLine($"  {Clean(item.Requirement),-36}  {satisfier}  ({Clean(item.Disposition)})");

            if (item.WhenUnmet is { } unmet)
            {
                text.AppendLine($"    {Clean(Reason.Sentence(unmet.Kind, unmet.Params))}");
            }
        }

        return text.ToString().TrimEnd();
    }

    /// <summary>Every advertised label, with the word that says what the claim is worth.</summary>
    private static string RunnerLabelsText(RunnerList list)
    {
        if (list.Runners.Count == 0)
        {
            return "No runners registered. Run gg runner up on a machine that should take work.";
        }

        var text = new StringBuilder();
        foreach (var runner in list.Runners)
        {
            text.AppendLine($"{Clean(runner.State),-8}  {Clean(runner.Label)}");

            if (runner.Labels.Count == 0)
            {
                // A fact somebody diagnosing a waiting flight needs, not an
                // absence to hide.
                text.AppendLine("          (advertises nothing)");
                continue;
            }

            foreach (var label in runner.Labels)
            {
                text.AppendLine($"          {Clean(label.Name),-36}  {Clean(label.Disposition)}");
            }
        }

        return text.ToString().TrimEnd();
    }

    /// <summary>
    /// The invitation, for the person who has to pass it on.
    /// </summary>
    /// <remarks>
    /// <b>The link is on a line of its own and nothing else is.</b> Whoever ran
    /// this is about to copy it into a message, and a URL wrapped in prose is a
    /// URL that arrives truncated. The expiry is said in the sentence after,
    /// because it changes what they write, not what they copy.
    /// </remarks>
    private static string Invited(InvitationIssued invitation)
    {
        var text = new StringBuilder();
        text.AppendLine(Clean(invitation.InvitationUrl));
        text.AppendLine();
        text.AppendLine($"Send that to the person you are inviting. It works once, and stops");
        text.AppendLine($"working at {invitation.ExpiresAt:u}.");
        text.AppendLine();
        // SAID PLAINLY, because it is the one thing about this link somebody
        // would not guess: it is not addressed to anybody. Whoever opens it
        // becomes a principal in this tenant.
        text.Append("Anybody who opens it joins this tenant, so send it the way you would send a password.");
        return text.ToString();
    }

    /// <summary>
    /// The bundle, for a person to read before they send it.
    /// </summary>
    /// <remarks>
    /// Completeness first, because it changes how everything below it should
    /// be read. Every value comes out of the document; nothing here consults
    /// the machine it is running on, or the two surfaces would be different
    /// views rather than one view twice.
    /// </remarks>
    private static string Bundle(DiagnosticsBundle bundle)
    {
        var text = new StringBuilder();
        text.AppendLine($"bundle            {Clean(bundle.Completeness)}");
        text.AppendLine($"                  {Clean(bundle.CompletenessDetail)}");
        text.AppendLine($"taken             {bundle.TakenAt:u}");
        text.AppendLine($"binary            {Clean(bundle.Binary)}");
        text.AppendLine($"protocol          {bundle.Protocol}");
        text.AppendLine($"fact vocabulary   {Clean(bundle.FactVocabulary)}");
        text.AppendLine($"environment       {Clean(bundle.Environment.HostFingerprint)} "
                      + $"({Clean(bundle.Environment.Provenance)})");

        foreach (var tool in bundle.Environment.Tools)
        {
            text.AppendLine($"  tool            {Clean(tool.Name)} {Clean(tool.Version)}");
        }

        text.AppendLine();
        text.AppendLine(Diagnosis(new DoctorReport { Checks = bundle.Checks }));

        // Repeated deliberately. The checks above are the whole picture and
        // people skim them; this is the list somebody should act on, and it is
        // empty when there is nothing to act on.
        text.AppendLine();
        text.AppendLine(bundle.Degradations.Count == 0
            ? "degradations      none"
            : "degradations");
        foreach (var degradation in bundle.Degradations)
        {
            var mark = degradation.Blocking ? "STOP" : "warn";
            text.AppendLine($"  {mark}  {Clean(degradation.Name),-16}  {Clean(degradation.Detail)}");
            if (degradation.Remedy is { Length: > 0 } remedy)
            {
                text.AppendLine($"        fix: {Clean(remedy)}");
            }
        }

        text.AppendLine();
        text.AppendLine(bundle.FlightLog is { } log
            ? Log(log)
            : "flight log        not in this bundle - see the completeness line above.");

        return text.ToString().TrimEnd();
    }

    private static string Diagnosis(DoctorReport report)
    {
        var text = new StringBuilder();

        // CHECKS FIRST, DISCLOSURES AFTER. A standing note among the checks reads as an
        // item that is not passing however it is marked - and keeping them apart is what
        // lets this list be genuinely all-green, which is what makes an all-green run
        // worth anything to the person reading it.
        foreach (var check in report.Checks.Where(c => c.Outcome != DoctorOutcome.Disclosure))
        {
            // Blocking and fixable are printed separately because they are
            // answered separately. Collapsing them into one severity loses the
            // two cases that matter: a blocking problem the person cannot fix,
            // and a non-blocking one they can.
            // THREE MARKS, from the three states. Reading Passed alone is what made a
            // permanent disclosure look identical to a non-blocking failure.
            var mark = check.Outcome switch
            {
                DoctorOutcome.Pass => "ok  ",
                DoctorOutcome.Disclosure => "note",
                _ => check.Blocking ? "STOP" : "warn",
            };
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

        var disclosures = report.Checks
            .Where(c => c.Outcome == DoctorOutcome.Disclosure)
            .ToList();

        if (disclosures.Count > 0)
        {
            // NOT A HEADING THAT SOUNDS LIKE A PROBLEM. These are reported every run and
            // will never stop being reported, so the words have to say that is normal.
            text.AppendLine();
            text.AppendLine("also true, and not a fault:");

            foreach (var disclosure in disclosures)
            {
                text.AppendLine(
                    $"note  {Clean(disclosure.Name),-16}  {Clean(disclosure.Detail)}");
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
    /// <summary>
    /// Why each obligation applied, as a person reads it.
    /// </summary>
    /// <remarks>
    /// <b>Every obligation appears, including the ones that did not attach.</b> A
    /// list of only the ones that applied would make non-attachment invisible,
    /// which is the failure this verb exists to prevent - and the three states are
    /// spelled differently on purpose, because that is the whole point.
    /// </remarks>
    private static string WhyText(FlightAttribution attribution)
    {
        var text = new StringBuilder();

        text.AppendLine($"{Clean(attribution.FlightNumber)} — governed by envelope "
                      + $"{Clean(attribution.EnvelopeVersion)}");

        if (attribution.Halt is { Length: > 0 } halt)
        {
            text.AppendLine();
            text.AppendLine($"HALTED: {Clean(halt, lines: true)}");
        }

        if (attribution.Obligations.Count == 0)
        {
            text.AppendLine();
            text.AppendLine("This envelope declares no obligation, so nothing governed this flight.");
            return text.ToString().TrimEnd();
        }

        foreach (var obligation in attribution.Obligations)
        {
            text.AppendLine();
            text.AppendLine($"{Clean(obligation.ObligationId)}: {Clean(obligation.Attachment)}");

            // COULD NEVER APPLY, said before the condition line, because it is
            // the answer to the question somebody opened this verb with. A rule
            // marked here is one the work kind can never answer - not one that
            // was measured and did not fire - and the two must not read alike:
            // the second may fire tomorrow.
            if (obligation.Inapplicable is { Length: > 0 } family)
            {
                // THE SHARED SENTENCE, never a local one. The plan and this verb
                // are two renderings of one claim, and the only thing a reader
                // can compare across them is the words.
                text.AppendLine($"  never:    {Clean(Inapplicability.Because(family))}");
            }

            if (obligation.Condition is { Length: > 0 } condition)
            {
                text.AppendLine($"  when:     {Clean(condition)}");
            }
            else if (obligation.Attachment == Attachments.Attached)
            {
                // Said, rather than left blank. A missing 'when' line and a
                // condition that could not be read must not look the same.
                text.AppendLine("  when:     always (this obligation declares no condition)");
            }

            if (obligation.Transitions.Count > 0)
            {
                // The answer's own history: when it attached, when it detached,
                // when it came back - a gate that appeared and vanished is
                // exactly what a reviewer needs to see, and only the times make
                // the middle readable.
                text.AppendLine("  history:  " + string.Join(" -> ", obligation.Transitions
                    .Select(t => $"{Clean(t.To)} {t.At.UtcDateTime:HH':'mm}")));
            }

            if (obligation.Because is { Length: > 0 } because)
            {
                text.AppendLine($"  because:  {Clean(because, lines: true)}");
            }

            if (obligation.Outcome is { Length: > 0 } outcome)
            {
                text.AppendLine($"  verdict:  {Clean(outcome)}");
            }

            if (obligation.Diagnosis is { Length: > 0 } diagnosis)
            {
                text.AppendLine($"  detail:   {Clean(diagnosis, lines: true)}");
            }
        }

        return text.ToString().TrimEnd();
    }

    /// <summary>
    /// The gate list, oldest first.
    /// </summary>
    /// <remarks>
    /// <b>The reason is a line rather than a column</b>, because it is a sentence and
    /// a table would truncate it - and the sentence is why somebody looks at all.
    /// The commit is abbreviated for reading and carried whole in the json, which is
    /// what a script would read.
    /// </remarks>
    private static string GatesText(GateList gates)
    {
        if (gates.Gates.Count == 0)
        {
            // An answer, not an empty table. A header over nothing reads as a query
            // that failed.
            return "Nothing is waiting on a decision.";
        }

        var text = new StringBuilder();

        text.AppendLine($"{gates.Gates.Count} decision(s) waiting.");

        foreach (var gate in gates.Gates)
        {
            text.AppendLine();
            text.AppendLine($"{Clean(gate.FlightNumber)} - {Clean(gate.ObligationId)}");
            text.AppendLine($"  approver: {Clean(gate.Approver)}");
            // A DECISION THAT IS NOT ABOUT A REPOSITORY SAYS SO, rather than
            // printing an empty commit on an empty branch. The two absences render
            // as one sentence because they are one fact: there is no code here.
            text.AppendLine(gate.Commit is { Length: > 0 } commit
                ? $"  commit:   {Clean(Short(commit))} on {Clean(gate.Branch ?? "an unnamed branch")}"
                : "  commit:   none - this decision is not about a repository");

            if (gate.Condition is { Length: > 0 } condition)
            {
                text.AppendLine($"  when:     {Clean(condition)}");
            }
            else
            {
                // The same rule as `gg why`: "declares no condition" and "the
                // condition could not be read" must not render alike.
                text.AppendLine("  when:     always (this obligation declares no condition)");
            }

            // Column 12, which is where "  because:  " ends. The value of this
            // field is the whole decision when the condition is `loop asked for
            // a decision` - the Engine composes the sentence from the fact, so
            // the agent's own question is the tail of it.
            text.AppendLine($"  because:  {Prose(gate.Because, 12)}");
            text.AppendLine($"  since:    {gate.AwaitingSince:u}");
        }

        return text.ToString().TrimEnd();
    }

    /// <summary>
    /// What was recorded, and what it changed.
    /// </summary>
    /// <remarks>
    /// The admission line is the point: a decision that satisfied the last outstanding
    /// obligation lets the work land, and one that did not says so. Both come from the
    /// response.
    /// </remarks>
    private static string DecidedText(DecisionReport report)
    {
        var text = new StringBuilder();
        var seen = report.Observation;

        // WHAT WAS OBSERVED, FIRST AND ALWAYS. The line a person reads comes from
        // the read surface rather than from the answer to the submission, because
        // that is the line that still exists once the write is a command.
        text.AppendLine(seen.State switch
        {
            ObservationStates.Decided =>
                $"decided: the obligation is now {Clean(seen.Outcome)}",
            ObservationStates.Refused =>
                $"refused: {Clean(seen.Because, lines: true)}",
            // NOT A FAILURE, and the wording has to carry that on its own -
            // somebody reading this at 2am decides whether to submit again.
            _ => $"not yet visible: {Clean(seen.Because, lines: true)}",
        });

        text.AppendLine(
            $"  looked:   {seen.Polls} time(s) over {seen.WaitedSeconds:0.#}s "
          + $"of a {seen.BoundSeconds:0.#}s bound");

        if (report.Decision is not { } recorded)
        {
            return text.ToString().TrimEnd();
        }

        // WHAT WAS DECIDED, beside what the obligation became. The observation
        // says the obligation is satisfied; only this says somebody approved it,
        // and a person reading their own decision back needs their own word for
        // it. It disappears with the synchronous answer, which is why it sits in
        // the block that does.
        text.AppendLine($"  decided:  {Clean(recorded.Outcome)}");
        text.AppendLine($"  by:       {Clean(recorded.DecidedBy)}");
        text.AppendLine($"  at:       {recorded.DecidedAt:u}");

        text.AppendLine(recorded.Admission is { } admission
            ? $"  landing:  {Clean(admission.DestinationId)} - {Clean(admission.Reason, lines: true)}"
            // Said rather than omitted. A decision that changed nothing about landing is
            // a normal outcome, and a blank line would read as one that did.
            : "  landing:  not yet - something else this destination requires is outstanding");

        return text.ToString().TrimEnd();
    }

    /// <summary>The first seven characters, which is what a person reads.</summary>
    private static string Short(string commit) => commit.Length > 7 ? commit[..7] : commit;

    private static string Clean(string? value, bool lines = false) => ControlText.Strip(value, lines);

    private static InvalidOperationException Unknown(string? kind) =>
        new($"'{kind}' is not a result gg knows how to render. "
          + $"Expected one of: {VerbResultKinds.Flights}, {VerbResultKinds.Flight}, "
          + $"{VerbResultKinds.Launched}, {VerbResultKinds.Log}, {VerbResultKinds.Runners}, "
          + $"{VerbResultKinds.Diagnosis}, {VerbResultKinds.Credentials}, "
          + $"{VerbResultKinds.CredentialAdded}, {VerbResultKinds.CredentialRemoved}.");

    private static T Require<T>(T? value) where T : class =>
        value ?? throw new InvalidOperationException("The result document was empty.");

    /// <summary>
    /// What a pull did, as a person reads it.
    /// </summary>
    /// <remarks>
    /// <b>Nothing to do is said out loud.</b> A pull that changed no file is the
    /// common case once an estate settles, and silence would read as a failure -
    /// the empty-list lesson the plan renderer already learned.
    /// </remarks>
    private static string PulledText(TreeWritten pulled)
    {
        var text = new StringBuilder();

        if (pulled.Written.Count == 0 && pulled.Removed.Count == 0)
        {
            text.Append("the working copy already matches the estate\n");
        }
        else
        {
            text.Append($"{pulled.Written.Count} document(s) rendered\n");
            foreach (var path in pulled.Written)
            {
                text.Append($"  {path}\n");
            }

            foreach (var path in pulled.Removed)
            {
                text.Append($"  removed {path}\n");
            }
        }

        // NAMED, NEVER DROPPED. A document whose name no path can carry is one
        // this estate predates the name rule for, and a file that cannot be
        // written back is worse than a name a person can go and fix.
        foreach (var name in pulled.Unrepresentable)
        {
            text.Append(
                $"  NOT WRITTEN: '{name}' is a name no file path can hold, so the tree "
              + "cannot carry it. Retire it and declare a name a path can hold.\n");
        }

        return text.ToString();
    }

    /// <summary>What an apply did, as a person reads it.</summary>
    private static string AppliedText(EstateApplied applied)
    {
        var text = new StringBuilder();

        if (applied.Applied.Count == 0)
        {
            text.Append("nothing to apply: the working copy matches the estate\n");
        }

        foreach (var document in applied.Applied)
        {
            if (document.Flight is { Length: > 0 } flight)
            {
                text.Append(
                    $"{document.Name}: widens {document.Widens} - flight {flight} awaits "
                  + $"{document.Awaiting}\n");
            }
            else if (document.Changed)
            {
                text.Append($"{document.Name}: applied as {document.Version}\n");
            }
            else
            {
                text.Append($"{document.Name}: nothing changed, still {document.Version}\n");
            }
        }

        // AN INTENT, NOT AN ACT. There is no delete verb: retiring is a terminal
        // version of the name, gated and attributed like any other change.
        foreach (var name in applied.Retiring)
        {
            text.Append(
                $"{name}: the tree no longer holds this document. Retiring a name is "
              + "applying a terminal version of it, which is its own gated change.\n");
        }

        return text.ToString();
    }

    /// <summary>What a diff found, as a person reads it.</summary>
    private static string DiffText(EstateDiff diff)
    {
        var text = new StringBuilder();

        if (diff.Changes.Count == 0 && diff.Retiring.Count == 0 && diff.Unreadable.Count == 0)
        {
            text.Append("no changes: the working copy matches the estate\n");
        }

        foreach (var change in diff.Changes)
        {
            text.Append($"{change.Path}: {change.Direction}");
            if (change.Field is { Length: > 0 } field)
            {
                // The field is what decides whether this gates, so it leads.
                text.Append($" - {field} cannot be shown to tighten");
            }

            text.Append('\n');
        }

        foreach (var name in diff.Retiring)
        {
            text.Append($"{name}: missing from the tree - an intent to retire\n");
        }

        foreach (var path in diff.Unreadable)
        {
            text.Append($"{path}: does not read as a document\n");
        }

        return text.ToString();
    }
}
