using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Gg.Contracts;

namespace Gg.Runner.Execution;

/// <summary>
/// The one executor adapter: a frontier agent, headless.
/// </summary>
/// <remarks>
/// <para>
/// <b>No PTY, and that is the bet this slice was scoped on.</b> Slice one
/// removed the PTY layer and called it the largest schedule risk. The child is
/// started with redirected pipes, closed stdin and no shell, and it works: the
/// agent reads, edits and reports without a terminal anywhere. A structural
/// test asserts nothing here allocates one, because a pseudo-terminal added
/// quietly is that risk coming back without anybody deciding it should.
/// </para>
/// <para>
/// <b>Everything reported is measured.</b> Attempts and duration come from the
/// executor's own result record; moves come from the tool calls it actually
/// made. Nothing is taken from what the agent SAID about its work - that is
/// prose, it lives in the transcript, and the machine-checked obligation is
/// computed control-plane-side from facts the runner extracted from the tree.
/// An injected instruction has nothing to grab.
/// </para>
/// <para>
/// <b>The transcript is written outside the tree.</b> The tree is deleted when
/// the flight ends; a reference into it would name something that has already
/// gone by the time anybody follows it.
/// </para>
/// </remarks>
public sealed class ClaudeCodeExecutor(string binary = "claude") : IExecutorPort
{
    private readonly string _binary = binary;

    /// <summary>
    /// What this adapter can and cannot do, written from what failed.
    /// </summary>
    /// <remarks>
    /// Measured against the real binary. The two false values below are not
    /// omissions - each was tried, and each is why a rule elsewhere in this
    /// slice reads the way it does.
    /// </remarks>
    public static ExecutorCapabilities Capabilities { get; } = new()
    {
        Rung = ExecutorRungs.Frontier,
        ReportsAttempts = true,
        ReportsDuration = true,
        ReportsMovesUsed = true,
        ReportsTokens = true,
        EnforcesMoves = false,
        AttributesEditsToTools = false,
        Gaps =
        [
            new ExecutorGap
            {
                Name = "moves are observed, not bounded",
                Consequence =
                    "Passing the allowed tool set governs PERMISSION, not availability: the session "
                  + "still advertises every tool and the agent still reaches for ones the envelope "
                  + "did not name, and is refused. So what is recorded is what it attempted, which "
                  + "is the more useful signal and not a bound. Bounding them needs either a "
                  + "different invocation surface or a sandbox around the process.",
            },
            new ExecutorGap
            {
                Name = "no per-edit attribution",
                Consequence =
                    "The executor does not say which tool call produced which file change, so what "
                  + "a flight touched is read from the tree instead. That is the safer source and "
                  + "it means the manifest cannot be influenced by what the agent claims.",
            },
            new ExecutorGap
            {
                Name = "ambient machine configuration is visible to the agent",
                Consequence =
                    "Clearing setting sources removes plugins and external tool servers, and does "
                  + "not remove the machine's skills or memory directory. A runner therefore shares "
                  + "some of its operator's configuration with the agent, which is a fact a "
                  + "customer should know before pointing one at a private repository.",
            },
            new ExecutorGap
            {
                Name = "the transcript resolves only on this machine",
                Consequence =
                    "There is no storage port, so a transcript is written beside the runner's own "
                  + "state and the reference names a local path. A gate running anywhere else can "
                  + "verify the hash it was given but cannot fetch the bytes.",
            },
            new ExecutorGap
            {
                Name = "no partial result when the budget runs out",
                Consequence =
                    "Stopping the process ends the turn wherever it was. Edits already written to "
                  + "the tree survive and are measured; anything the agent was midway through "
                  + "reasoning about is lost, and it reports no attempts because the result record "
                  + "never arrived.",
            },
        ],
    };

    ExecutorCapabilities IExecutorPort.Capabilities => Capabilities;

    /// <summary>Runs the loop under its wall-clock budget.</summary>
    public async Task<ExecutorRun> ExecuteAsync(
        ExecutorRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var started = Stopwatch.StartNew();
        var moves = new List<string>();
        var transcript = new StringBuilder();

        using var budget = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        budget.CancelAfter(request.WallClock);

        using var process = new Process { StartInfo = StartInfo(request) };

        // ONLY the start is wrapped. It used to cover the read as well, and a
        // parsing bug inside it surfaced as "this runner could not start the
        // executor" - a diagnosis naming a cause that was not the cause, which
        // is the Article XI failure wearing a helpful sentence. A catch wide
        // enough to be convenient is wide enough to lie.
        try
        {
            process.Start();
        }
        catch (Exception failure) when (failure is System.ComponentModel.Win32Exception
                                            or InvalidOperationException
                                            or FileNotFoundException)
        {
            // A declared capability gap is answerable - this runner cannot
            // serve this flight - and a stalled flight is not.
            return ExecutorRun.Failed(
                request.LoopId,
                $"This runner could not start the '{Capabilities.Rung}' executor: {failure.Message}",
                attempts: 0, started.Elapsed, moves);
        }

        try
        {
            // Closed at once. A child that inherited an open stdin would wait
            // on it forever in a runner with nothing to type.
            process.StandardInput.Close();

            await ReadAsync(process, transcript, moves, budget.Token);
            await process.WaitForExitAsync(budget.Token);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            // The budget, not a shutdown. Ending the process ends the turn
            // wherever it was; whatever it had already written to the tree
            // survives and gets measured, which is what makes this a state
            // rather than a loss.
            Stop(process);
            return await WithTranscriptAsync(
                ExecutorRun.Exhausted(request.LoopId, request.WallClock, moves),
                request, transcript, cancellationToken);
        }

        var result = Result(transcript.ToString());

        return await WithTranscriptAsync(
            result.IsError
                ? ExecutorRun.Failed(
                    request.LoopId, result.Reason, result.Attempts, started.Elapsed, moves)
                : ExecutorRun.Completed(
                    request.LoopId, result.Reason, result.Attempts, started.Elapsed, moves),
            request, transcript, cancellationToken);
    }

    /// <summary>
    /// Headless, piped, no shell, and isolated from what it can be isolated
    /// from.
    /// </summary>
    /// <remarks>
    /// <c>--setting-sources</c> empty and strict tool-server configuration
    /// remove the operator's plugins and external servers. They do not remove
    /// the machine's skills or memory, which is why that is a declared gap
    /// rather than a solved problem.
    /// </remarks>
    private ProcessStartInfo StartInfo(ExecutorRequest request)
    {
        var info = new ProcessStartInfo
        {
            FileName = _binary,
            WorkingDirectory = request.WorkingDirectory,
            RedirectStandardInput = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            // No shell: a shell would hand the child whatever terminal this
            // process has, which is the whole thing this slice is scoped
            // around not needing.
            UseShellExecute = false,
        };

        foreach (var argument in (string[])
                 ["-p", Prompt(request),
                  "--output-format", "stream-json",
                  "--verbose",
                  "--setting-sources", "",
                  "--strict-mcp-config",
                  "--allowedTools", .. request.Moves.Select(Tool).Distinct(StringComparer.Ordinal)])
        {
            info.ArgumentList.Add(argument);
        }

        return info;
    }

    /// <summary>
    /// The work, as the agent is told it.
    /// </summary>
    /// <remarks>
    /// The URI, never a body somebody fetched and pasted. The agent resolves
    /// what it points at from inside the customer's environment, with the
    /// customer's own credential - which is also why the control plane needs
    /// no permission to read it.
    /// </remarks>
    private static string Prompt(ExecutorRequest request) =>
        $"Work the issue at {request.IntentUri} in this repository. Make the code changes it asks "
      + "for, in this working tree only. Do not create a branch, do not commit, and do not push "
      + "anything anywhere.";

    /// <summary>The envelope's move vocabulary, as this executor names tools.</summary>
    private static string Tool(string move) => move switch
    {
        LoopMoves.Read => "Read",
        LoopMoves.Edit => "Edit",
        LoopMoves.Search => "Grep",
        LoopMoves.RunTests => "Bash",
        _ => move,
    };

    /// <summary>
    /// Reads the stream, keeping the transcript and the moves.
    /// </summary>
    /// <remarks>
    /// Line-delimited JSON. A line that will not parse is KEPT in the
    /// transcript and skipped for moves: the transcript is the record of what
    /// happened, and silently dropping part of it would make the hash describe
    /// something nobody can reconstruct.
    /// </remarks>
    private static async Task ReadAsync(
        Process process, StringBuilder transcript, List<string> moves, CancellationToken cancellationToken)
    {
        while (await process.StandardOutput.ReadLineAsync(cancellationToken) is { } line)
        {
            transcript.Append(line).Append('\n');

            try
            {
                using var document = JsonDocument.Parse(line);

                // ValueKind checked at every step. A `message` is sometimes a
                // string rather than an object, and asking a string for a
                // property throws - which is how a stream this adapter reads
                // perfectly well came back as a start failure.
                if (document.RootElement.ValueKind == JsonValueKind.Object
                    && document.RootElement.TryGetProperty("message", out var message)
                    && message.ValueKind == JsonValueKind.Object
                    && message.TryGetProperty("content", out var content)
                    && content.ValueKind == JsonValueKind.Array)
                {
                    foreach (var block in content.EnumerateArray())
                    {
                        if (block.ValueKind == JsonValueKind.Object
                            && block.TryGetProperty("type", out var type)
                            && type.GetString() == "tool_use"
                            && block.TryGetProperty("name", out var name)
                            && name.GetString() is { Length: > 0 } tool)
                        {
                            moves.Add(tool);
                        }
                    }
                }
            }
            catch (JsonException)
            {
                // Kept above, counted nowhere. See the remark.
            }
        }
    }

    /// <summary>What the executor's own result record said.</summary>
    private static (bool IsError, string Reason, int Attempts) Result(string transcript)
    {
        foreach (var line in transcript.Split('\n', StringSplitOptions.RemoveEmptyEntries).Reverse())
        {
            try
            {
                using var document = JsonDocument.Parse(line);
                var root = document.RootElement;

                if (root.ValueKind != JsonValueKind.Object
                    || !root.TryGetProperty("type", out var type) || type.GetString() != "result")
                {
                    continue;
                }

                var isError = root.TryGetProperty("is_error", out var error) && error.GetBoolean();
                var attempts = root.TryGetProperty("num_turns", out var turns) ? turns.GetInt32() : 0;
                var reason = root.TryGetProperty("result", out var text)
                    ? text.GetString() ?? ""
                    : "";

                return (isError,
                        reason.Length > 0 ? reason : "The loop ended without saying anything.",
                        attempts);
            }
            catch (JsonException)
            {
                continue;
            }
        }

        // Article XI. No result record is not a success, and it is not a
        // failure of the work either - it is this adapter not being able to
        // tell, which is a different thing and should read as one.
        return (true, "The executor produced no result record, so what it did cannot be reported.", 0);
    }

    /// <summary>
    /// Writes the transcript where it will still be, and references it.
    /// </summary>
    /// <remarks>
    /// The hash is over the bytes as written. That is what makes this a
    /// reference rather than a rumour: the locator only resolves on this
    /// machine, and the hash proves what was there regardless of who can reach
    /// it.
    /// </remarks>
    private static async Task<ExecutorRun> WithTranscriptAsync(
        ExecutorRun run, ExecutorRequest request, StringBuilder transcript, CancellationToken cancellationToken)
    {
        var bytes = Encoding.UTF8.GetBytes(transcript.ToString());

        Directory.CreateDirectory(Path.GetDirectoryName(request.TranscriptPath)!);
        await File.WriteAllBytesAsync(request.TranscriptPath, bytes, cancellationToken);

        return run with
        {
            Transcript = new ArtifactReference
            {
                Locator = request.TranscriptPath,
                Sha256 = Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant(),
                Bytes = bytes.LongLength,
                MediaType = "application/x-ndjson",
                // The declared gap, on the artifact itself, so a gate that
                // cannot follow it finds out from the reference rather than
                // from an empty fetch.
                Scope = ArtifactScopes.RunnerLocal,
            },
        };
    }

    private static void Stop(Process process)
    {
        try
        {
            if (!process.HasExited)
            {
                process.Kill(entireProcessTree: true);
            }
        }
        catch (InvalidOperationException)
        {
            // It exited between the check and the kill. Nothing to do.
        }
    }
}
