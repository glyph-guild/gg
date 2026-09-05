using System.Text.Json;
using Gg.Contracts;
using Gg.Contracts.Description;
using Gg.Runner.Execution;
using Gg.Runner.Vcs;

namespace Gg.Runner.Tests;

/// <summary>
/// What the digest says the loop did, checked against what the stream says it
/// did.
/// </summary>
/// <remarks>
/// <para>
/// <b>Excluded from CI by name</b>, on <see cref="AgainstRealAgentTests"/>' terms:
/// it drives a real agent. What it produces is recorded by hand, which is this
/// repository's practice for facts that cost a real invocation.
/// </para>
/// <para>
/// <b>Two accounts of one run, compared.</b> The digest is the fact that crosses;
/// the transcript is the stream it was extracted from and stays on this machine.
/// Every claim in the first has to be findable in the second, and this is where a
/// claim that is not gets found - before it is rendered to somebody deciding.
/// </para>
/// </remarks>
[Category("RealAgent")]
public class DigestAccountingTests
{
    private static string Binary =>
        Environment.GetEnvironmentVariable("GG_EXECUTOR_BINARY")
        ?? throw new InvalidOperationException(
            "GG_EXECUTOR_BINARY is not set. This compares a digest against the stream it came "
          + "from; skipping it would leave both accounts unchecked.");

    /// <summary>A repository whose work needs a tool the envelope does not name.</summary>
    private sealed class BlockedWorkFixture : IDisposable
    {
        internal string Directory { get; }

        internal string BarePath { get; }

        internal BlockedWorkFixture()
        {
            Directory = Path.Combine(Path.GetTempPath(), "gg-digest-truth", Guid.NewGuid().ToString("n"));
            System.IO.Directory.CreateDirectory(Directory);

            BarePath = Path.Combine(Directory, "widgets.git");
            var work = Path.Combine(Directory, "work");

            GitFixture.Run(Directory, "init", "--bare", "--initial-branch=main", BarePath);
            GitFixture.Run(Directory, "clone", BarePath, work);

            Write(work, "ISSUE.md",
                "# Issue 1: orders need a discount\n\n"
              + "Orders should carry a discount. Add the column to the schema and read it where "
              + "orders are loaded.\n\n"
              + "Schema changes live in `migrations/`, one NEW numbered file per change - never "
              + "by editing an existing one. Then run `ls migrations/` and report what is there.\n");
            Write(work, "src/orders.py",
                "def load_order(row):\n    return {\"id\": row[0], \"total\": row[1]}\n");
            Write(work, "migrations/0001_init.sql",
                "create table orders (id integer primary key, total numeric);\n");

            GitFixture.Run(work, "add", ".");
            GitFixture.Run(work, "commit", "-m", "base");
            GitFixture.Run(work, "push", "origin", "main");
        }

        private static void Write(string root, string relative, string content)
        {
            var path = Path.Combine(root, relative.Replace('/', Path.DirectorySeparatorChar));
            System.IO.Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            File.WriteAllText(path, content);
        }

        public void Dispose()
        {
            if (System.IO.Directory.Exists(Directory))
            {
                System.IO.Directory.Delete(Directory, recursive: true);
            }
        }
    }

    [Test]
    public async Task What_the_digest_claims_is_checked_against_the_stream_it_came_from()
    {
        using var fixture = new BlockedWorkFixture();
        using var trees = new ScratchTreeRoot();

        var tree = await new Materializer(new LocalVcsAdapter(fixture.Directory), trees.Root)
            .MaterializeAsync("flight-1", new RepoTarget
            {
                Provider = LocalVcsAdapter.ProviderKey,
                Slug = fixture.BarePath,
                PinnedRef = "refs/heads/main",
            }, secret: null);

        var run = await new ClaudeCodeExecutor(Binary).ExecuteAsync(
            new ExecutorRequest
            {
                WorkingDirectory = tree.Path,
                LoopId = "implement",
                IntentUri = "https://forge.example/acme/widgets/issues/1",
                // The envelope names read and edit. Creating a file needs Write and
                // listing a directory needs Bash, and it names neither - which is
                // the shape every real flight has today.
                Moves = [LoopMoves.Read, LoopMoves.Edit],
                WallClock = TimeSpan.FromMinutes(10),
                TranscriptPath = Path.Combine(fixture.Directory, "transcripts", "implement.ndjson"),
            },
            CancellationToken.None);

        var digest = run.Digest!;
        var stream = File.ReadAllText(run.Transcript!.Locator);

        // GROUND TRUTH, from the stream rather than from the digest: which tools
        // were called, and which of those calls came back an error.
        var (called, failed) = ToolOutcomes(stream);

        var record = new System.Text.StringBuilder()
            .AppendLine("=== the digest, verbatim ===")
            .AppendLine(JsonSerializer.Serialize(digest, new JsonSerializerOptions
            {
                WriteIndented = true,
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            }))
            .AppendLine("=== the stream's own account ===")
            .AppendLine("  tools called:  " + string.Join(", ", called.Order(StringComparer.Ordinal)))
            .AppendLine("  calls that errored: " + string.Join(", ", failed.Order(StringComparer.Ordinal)))
            .AppendLine("  tools called AND never errored: "
                + string.Join(", ", called.Except(failed).Order(StringComparer.Ordinal)))
            .AppendLine("=== the tree ===")
            .AppendLine(GitFixture.Run(tree.Path, "status", "--porcelain"))
            .ToString();

        Console.WriteLine(record);

        if (Environment.GetEnvironmentVariable("GG_DIGEST_RECORD") is { Length: > 0 } into)
        {
            File.WriteAllText(into, record);
            File.WriteAllText(into + ".stream.ndjson", stream);
        }

        await Assert.That(called).IsNotEmpty()
            .Because("a run that called nothing would make this comparison meaningless.");
    }

    /// <summary>
    /// Which tools the stream shows being called, and which calls came back an
    /// error.
    /// </summary>
    /// <remarks>
    /// The result is joined to the call by id, because a failure arrives on a
    /// separate event from the call that caused it - the same join the digest's
    /// own extractor makes.
    /// </remarks>
    private static (HashSet<string> Called, HashSet<string> Failed) ToolOutcomes(string stream)
    {
        var called = new HashSet<string>(StringComparer.Ordinal);
        var failed = new HashSet<string>(StringComparer.Ordinal);
        var toolById = new Dictionary<string, string>(StringComparer.Ordinal);

        foreach (var line in stream.Split('\n'))
        {
            if (line.Length == 0)
            {
                continue;
            }

            JsonDocument document;
            try
            {
                document = JsonDocument.Parse(line);
            }
            catch (JsonException)
            {
                continue;
            }

            using (document)
            {
                if (document.RootElement.ValueKind != JsonValueKind.Object
                    || !document.RootElement.TryGetProperty("message", out var message)
                    || message.ValueKind != JsonValueKind.Object
                    || !message.TryGetProperty("content", out var content)
                    || content.ValueKind != JsonValueKind.Array)
                {
                    continue;
                }

                foreach (var block in content.EnumerateArray())
                {
                    if (block.ValueKind != JsonValueKind.Object
                        || !block.TryGetProperty("type", out var type))
                    {
                        continue;
                    }

                    switch (type.GetString())
                    {
                        case "tool_use"
                            when block.TryGetProperty("name", out var name)
                              && name.GetString() is { Length: > 0 } tool:
                            called.Add(tool);
                            if (block.TryGetProperty("id", out var id)
                                && id.GetString() is { Length: > 0 } callId)
                            {
                                toolById[callId] = tool;
                            }
                            break;

                        case "tool_result"
                            when block.TryGetProperty("is_error", out var error)
                              && error.ValueKind == JsonValueKind.True
                              && block.TryGetProperty("tool_use_id", out var forId)
                              && forId.GetString() is { Length: > 0 } key
                              && toolById.TryGetValue(key, out var which):
                            failed.Add(which);
                            break;
                    }
                }
            }
        }

        return (called, failed);
    }
}
