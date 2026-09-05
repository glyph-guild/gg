using System.Diagnostics;
using Gg.Local;

namespace Gg.Console;

/// <summary>
/// One reader, as a child process this console starts and stops.
/// </summary>
/// <remarks>
/// <para>
/// <b>Owned by the loop, never by a session.</b> <c>LiveTails</c> states the
/// rule and this obeys it: the object outlives any UI lifetime, is owned by
/// whoever composed the console, and a session merely calls it. A reader is
/// that plus a process handle, which is a much better reason to be certain it
/// stops.
/// </para>
/// <para>
/// <b>Started lazily and once.</b> A console that spawned a reader at startup
/// would pay for a browse nobody asked for, on every run, including the runs
/// where a person only wanted the queue.
/// </para>
/// <para>
/// <b>Every failure is an outcome and none is an exception.</b> An operator's
/// typo in the declaration arrives here as a <c>Win32Exception</c>, and a
/// console that let it out would die on a configuration mistake. See
/// <see cref="BrowseOutcome"/>.
/// </para>
/// <para>
/// <b>No credential passes through this type.</b> The command line came from
/// <see cref="IntentConfiguration"/> and carries a locator at most - a name the
/// child resolves for itself. That is what keeps a console that browses out of
/// the business of holding secrets.
/// </para>
/// </remarks>
public sealed class SpawnedReader(IntentReader reader, TimeSpan patience) : IAsyncDisposable
{
    private readonly IntentReader _reader = reader;
    private readonly TimeSpan _patience = patience;
    private readonly SemaphoreSlim _oneAtATime = new(1, 1);

    private Process? _child;
    private ReaderConversation? _asking;
    private BrowseOutcome? _neverStarted;

    /// <summary>The child's pid, or null where none is running.</summary>
    /// <remarks>Exposed so a test can go and look; nothing in the console needs it.</remarks>
    public int? ProcessId
    {
        get
        {
            var child = _child;
            return child is null || child.HasExited ? null : child.Id;
        }
    }

    /// <summary>Start the child if it is not already running.</summary>
    /// <returns>The reason it cannot be started, or null.</returns>
    public Task<BrowseOutcome?> StartAsync()
    {
        if (_asking is not null)
        {
            return Task.FromResult<BrowseOutcome?>(null);
        }

        if (_neverStarted is not null)
        {
            return Task.FromResult<BrowseOutcome?>(_neverStarted);
        }

        var start = new ProcessStartInfo(_reader.Command)
        {
            RedirectStandardInput = true,
            RedirectStandardOutput = true,
            // SWALLOWED, NOT MERGED. A server's diagnostics belong on its own
            // channel; folded into stdout they would be the stray line that
            // ends the conversation.
            RedirectStandardError = true,
            UseShellExecute = false,
        };

        foreach (var argument in _reader.Arguments)
        {
            start.ArgumentList.Add(argument);
        }

        try
        {
            _child = Process.Start(start);
        }
        catch (Exception problem) when (problem is not OperationCanceledException)
        {
            // AN OPERATOR'S TYPO, AND IT MUST NOT KILL A CONSOLE. The command
            // is what needs fixing, so the command is what the sentence names.
            return Task.FromResult<BrowseOutcome?>(_neverStarted = new BrowseOutcome.Silent(
                $"The reader for '{_reader.Key}' could not be started: '{_reader.Command}' "
              + $"({problem.Message}). Check the command in the declaration."));
        }

        if (_child is null)
        {
            return Task.FromResult<BrowseOutcome?>(_neverStarted = new BrowseOutcome.Silent(
                $"The reader for '{_reader.Key}' started no process for '{_reader.Command}'."));
        }

        _asking = new ReaderConversation(
            _child.StandardOutput, _child.StandardInput, _reader.Key);

        return Task.FromResult<BrowseOutcome?>(null);
    }

    /// <summary>A page of work from this reader, or why there is not one.</summary>
    public async Task<BrowseOutcome> BrowseAsync(
        string? cursor, int limit, CancellationToken cancellationToken = default)
    {
        // ONE CONVERSATION AT A TIME. The protocol is request-then-reply over
        // one pipe, so two overlapping browses would read each other's answers
        // - a pane showing the wrong page with nothing saying so.
        await _oneAtATime.WaitAsync(cancellationToken);

        try
        {
            if (await StartAsync() is { } refused)
            {
                return refused;
            }

            using var deadline = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            deadline.CancelAfter(_patience);

            try
            {
                return await _asking!.BrowseAsync(cursor, limit, deadline.Token);
            }
            catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
            {
                // A BROWSE THAT NEVER RETURNS IS A CONSOLE THAT NEVER REDRAWS,
                // so the wait is bounded and the child is dropped rather than
                // left holding a pipe nobody will read again. Its stdout may
                // now contain a half-written reply, and resuming a conversation
                // mid-answer would show somebody else's page.
                Stop();

                return new BrowseOutcome.Silent(
                    $"The reader for '{_reader.Key}' did not answer within "
                  + $"{_patience.TotalMilliseconds:0}ms, so it was stopped.");
            }
        }
        finally
        {
            _oneAtATime.Release();
        }
    }

    public ValueTask DisposeAsync()
    {
        Stop();
        _oneAtATime.Dispose();
        return ValueTask.CompletedTask;
    }

    /// <summary>
    /// End the child, whatever state it is in.
    /// </summary>
    /// <remarks>
    /// <b>Killed rather than asked.</b> There is no shutdown in this protocol -
    /// closing stdin is the only hint available and a server blocked on a
    /// tracker will not see it before the console is gone. Reaped with
    /// <c>WaitForExit</c> so the pid is actually released rather than left as a
    /// zombie for whatever started us.
    /// </remarks>
    private void Stop()
    {
        var child = Interlocked.Exchange(ref _child, null);
        _asking = null;

        if (child is null)
        {
            return;
        }

        try
        {
            if (!child.HasExited)
            {
                child.Kill(entireProcessTree: true);
                child.WaitForExit(2000);
            }
        }
        catch (Exception problem) when (problem is InvalidOperationException
                                             or System.ComponentModel.Win32Exception
                                             or NotSupportedException)
        {
            // ALREADY GONE, OR NEVER OURS TO KILL. Disposal must not throw: it
            // runs while a console is shutting down, and an exception here
            // would replace whatever the person was actually told.
        }
        finally
        {
            child.Dispose();
        }
    }
}
