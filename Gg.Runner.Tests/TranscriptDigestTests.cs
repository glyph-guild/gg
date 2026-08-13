using System.Text.RegularExpressions;
using Gg.Contracts;
using Gg.Runner.Execution;

namespace Gg.Runner.Tests;

/// <summary>
/// What a person gets when the transcript is not available to them.
/// </summary>
/// <remarks>
/// <para>
/// ADR-0006 rejected reference-everything partly because <i>handoff dies</i>,
/// and its load-bearing claim is that <b>a digest carries enough to act on
/// while the bulk stays behind</b>. The transcript is a machine-local
/// reference: it does not cross, so whatever crosses has to be extracted from
/// the event stream before it is thrown away.
/// </para>
/// <para>
/// <b>Mechanically extracted, never model-generated</b>, and the reasons
/// compound. A model's summary is a claim rather than a fact, and the
/// vocabulary carries facts. It would be non-deterministic, so digests would
/// not be comparable across flights - and comparison across flights is the
/// whole of Article XIII's hardening. And it is an injection surface: the
/// transcript can contain text addressed to a model, so a summariser reading it
/// produces output that CROSSES, and an injected instruction would arrive at
/// the control plane inside the one artifact everyone was told is safe.
/// </para>
/// <para>
/// That last one is step 3's lesson one artifact further along. The manifest is
/// read from the tree rather than from the agent's account of its edits; the
/// digest is read from the event stream rather than from the agent's account of
/// its reasoning.
/// </para>
/// </remarks>
public class TranscriptDigestTests
{
    /// <summary>A transcript from a real agent run, kept as a fixture.</summary>
    /// <remarks>
    /// Real output rather than hand-written JSON, because every interesting
    /// thing here is a shape the agent chose: where a path lives on a
    /// <c>tool_use</c>, whether a failure is flagged on the result or only
    /// readable in its text, and what a search looks like. A fixture I invented
    /// would be a test of my invention.
    /// </remarks>
    private static string Fixture(string name)
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "Gg.sln")))
        {
            dir = dir.Parent;
        }

        var root = dir ?? throw new InvalidOperationException("Gg.sln not found");

        return File.ReadAllText(Path.Combine(
            root.FullName, "Gg.Runner.Tests", "Fixtures", name));
    }

    private static LoopDigest Considered() =>
        TranscriptDigest.Extract(
            Fixture("agent-considered.ndjson"), "implement", ["/work/tree"],
            LoopOutcomes.Completed, refused: []);

    private static LoopDigest Searched() =>
        TranscriptDigest.Extract(
            Fixture("agent-searched-and-failed.ndjson"), "implement", ["/work/tree"],
            LoopOutcomes.Completed, refused: []);

    // ---- the signal this step exists for ----

    [Test]
    public async Task Files_it_opened_and_left_alone_are_named()
    {
        // THE ONE TO GET RIGHT. A diff already tells you what changed; this is
        // what it looked at and did not change, which is the closest thing the
        // stream holds to "considered and ruled out". In this run the agent was
        // asked to match the project's style, read two files to find out, and
        // edited neither.
        var digest = Considered();

        await Assert.That(digest.FilesReadNotEdited).IsEquivalentTo(
            (string[])["src/util.py", "README.md"]);

        await Assert.That(digest.FilesReadNotEdited).DoesNotContain("src/greet.py")
            .Because("it was read AND edited, so it is the work rather than the thinking.");
    }

    [Test]
    public async Task What_it_changed_is_named_separately_from_what_it_only_read()
    {
        var digest = Considered();

        await Assert.That(digest.FilesEdited).IsEquivalentTo((string[])["src/greet.py"]);
    }

    [Test]
    public async Task Paths_are_relative_to_the_tree_rather_than_to_this_machine()
    {
        // Two reasons, and the second is the one that matters. An absolute path
        // is a machine detail crossing a boundary; and a digest carrying
        // /home/runner/... is not comparable with one carrying /work/..., which
        // would quietly end the cross-flight comparison this exists for.
        var digest = Considered();

        foreach (var path in digest.FilesReadNotEdited.Concat(digest.FilesEdited))
        {
            await Assert.That(path.StartsWith('/')).IsFalse().Because($"'{path}' is absolute.");
            await Assert.That(path).DoesNotContain("work/tree");
        }
    }

    // ---- what it went looking for ----

    [Test]
    public async Task Searches_are_carried_as_what_it_searched_for()
    {
        // What a person taking over would otherwise re-derive. The pattern is
        // the useful part - it says what it thought was relevant.
        var digest = Searched();

        await Assert.That(digest.Searches.Count).IsGreaterThan(0);
        await Assert.That(string.Join(" ", digest.Searches)).Contains("slugify");
    }

    // ---- what it hit ----

    [Test]
    public async Task Errors_are_carried_with_what_produced_them()
    {
        // The pytest run in this fixture fails because pytest is not installed.
        // A person taking over needs to know the command was tried and how it
        // went, or they will try it again as their first move.
        var digest = Searched();

        await Assert.That(digest.Errors.Count).IsGreaterThan(0);

        var error = digest.Errors[0];

        await Assert.That(error.Source).IsEqualTo("Bash");
        await Assert.That(error.Detail).Contains("pytest");
    }

    [Test]
    public async Task A_run_that_hit_nothing_carries_no_errors()
    {
        // Liveness on the negative: a detector that reported an error for every
        // run would satisfy the test above just as well.
        await Assert.That(Considered().Errors).IsEmpty();
    }

    // ---- what the envelope refused, and where it stopped ----

    [Test]
    public async Task Refused_moves_are_carried_because_they_say_where_the_envelope_fought_the_work()
    {
        var digest = TranscriptDigest.Extract(
            Fixture("agent-considered.ndjson"), "implement", ["/work/tree"],
            LoopOutcomes.Completed, refused: ["WebFetch", "Write"]);

        await Assert.That(digest.RefusedMoves).IsEquivalentTo((string[])["WebFetch", "Write"]);
    }

    [Test]
    public async Task Attempts_and_the_stop_reason_are_carried()
    {
        // So the digest stands alone. The outcome is on loop.outcome too, and
        // duplicating it is the point: a person reading only this must not have
        // to go and find the other fact.
        var digest = Considered();

        await Assert.That(digest.Attempts).IsGreaterThan(0);
        await Assert.That(digest.StopReason).IsEqualTo(LoopOutcomes.Completed);
    }

    // ---- deterministic, and free of the model ----

    [Test]
    public async Task The_same_stream_produces_the_same_digest()
    {
        // Comparison across flights is the whole of Article XIII's hardening,
        // and it needs the extraction to be a function rather than a summary.
        var first = Considered();
        var second = Considered();

        await Assert.That(first).IsEqualTo(second);
        await Assert.That(string.Join("|", first.FilesReadNotEdited))
            .IsEqualTo(string.Join("|", second.FilesReadNotEdited))
            .Because("ordering is part of the value: a set that reorders is not comparable.");
    }

    [Test]
    public async Task Nothing_in_the_digest_path_can_invoke_a_model()
    {
        // Structural, because the tempting implementation is one call away and
        // would look like an improvement. What it would actually do is turn the
        // one artifact that crosses into an injection surface.
        var source = File.ReadAllText(SourceOf("TranscriptDigest.cs"));

        var model = new Regex(
            @"HttpClient|Anthropic|OpenAI|claude|completion|Prompt|Summarise|Summarize|ExecuteAsync",
            RegexOptions.IgnoreCase | RegexOptions.Compiled);

        await Assert.That(model.IsMatch(Strip(source))).IsFalse()
            .Because("a digest produced by a model is a claim rather than a fact, it is not "
                   + "comparable across flights, and it carries whatever the transcript told it to.");

        // The scan can see one, so the absence above means something.
        await Assert.That(model.IsMatch("var reply = await http.PostAsync(anthropic, prompt);")).IsTrue();
    }

    [Test]
    public async Task The_digest_is_a_pure_function_of_text_it_is_handed()
    {
        // The other half of the structural claim: it reads no file, opens no
        // process, and reaches no network, so there is nowhere for a model to
        // be added later without changing the shape of the code.
        var source = Strip(File.ReadAllText(SourceOf("TranscriptDigest.cs")));

        // Word-bounded, because a PARAMETER called workingDirectory is not a
        // call to Directory - and a scan that cannot tell those apart gets
        // deleted by the next person rather than obeyed.
        foreach (var reaching in (string[])["File", "Process", "Directory", "Socket"])
        {
            var call = new Regex($@"\b{reaching}\.", RegexOptions.Compiled);

            await Assert.That(call.IsMatch(source)).IsFalse()
                .Because($"'{reaching}.' would make this something other than a function of its "
                       + "input, which is where a model would arrive.");
        }

        await Assert.That(new Regex(@"\bDirectory\.").IsMatch("var x = Directory.GetFiles(p);")).IsTrue()
            .Because("the scan has to be able to see one.");
        await Assert.That(new Regex(@"\bDirectory\.").IsMatch("workingDirectory.TrimEnd()")).IsFalse()
            .Because("and it has to be able to tell that apart from a parameter name.");
    }

    private static string SourceOf(string file)
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "Gg.sln")))
        {
            dir = dir.Parent;
        }

        var root = dir ?? throw new InvalidOperationException("Gg.sln not found");

        return Directory
            .EnumerateFiles(Path.Combine(root.FullName, "Gg.Runner"), file, SearchOption.AllDirectories)
            .Single();
    }

    /// <summary>Source with its comments removed, so a mention is not a match.</summary>
    private static string Strip(string source) =>
        string.Join('\n', source.Split('\n')
            .Where(l => !l.TrimStart().StartsWith("//", StringComparison.Ordinal)
                     && !l.TrimStart().StartsWith("*", StringComparison.Ordinal)
                     && !l.TrimStart().StartsWith("///", StringComparison.Ordinal)));

    // ---- the boundary rules that already applied to everything else ----

    [Test]
    public async Task Control_sequences_are_stripped_before_the_digest_is_built()
    {
        // The Roadmap's correction: stripping belongs in the RUNNER, before the
        // digest, or the stored bytes disagree with the hash that proves what
        // they were. A control plane holding an escape sequence is one that can
        // drive a terminal.
        var poisoned =
            "{\"type\":\"assistant\",\"message\":{\"content\":[{\"type\":\"tool_use\","
          // JSON-escaped in the payload, because a raw control byte inside a
          // JSON string is invalid JSON - the line would be skipped as
          // unparseable and the test would pass for the wrong reason.
          + "\"name\":\"Grep\",\"input\":{\"pattern\":\"\\u001b[2Jcleared\"}}]}}\n"
          + "{\"type\":\"result\",\"subtype\":\"success\",\"is_error\":false,"
          + "\"num_turns\":1,\"result\":\"done\"}";

        var digest = TranscriptDigest.Extract(
            poisoned, "implement", ["/work/tree"], LoopOutcomes.Completed, refused: []);

        await Assert.That(digest.Searches.Single()).DoesNotContain("\u001b");
        await Assert.That(digest.Searches.Single()).Contains("cleared")
            .Because("stripped rather than dropped: the search still happened.");
    }

    [Test]
    public async Task A_line_that_will_not_parse_does_not_stop_the_digest()
    {
        // Article XI, on a stream nobody controls. A half-written line at the
        // end of a file that is still being appended to is the ordinary case,
        // and a digest that threw would lose every signal before it.
        var ragged = Fixture("agent-considered.ndjson") + "\n{\"type\":\"assis";

        var digest = TranscriptDigest.Extract(
            ragged, "implement", ["/work/tree"], LoopOutcomes.Completed, refused: []);

        await Assert.That(digest.FilesReadNotEdited).IsNotEmpty();
    }
}
