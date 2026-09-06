using System.Text;
using Gg.Contracts;

namespace Gg.Console.Tests;

/// <summary>
/// The log is readable while the runner is running, and a runner this console
/// did not start still reports itself.
/// </summary>
/// <remarks>
/// <para>
/// <b>The log was empty after ninety minutes of a runner running.</b> The child
/// was drained into <c>StreamWriter.BaseStream</c> - past the writer's own
/// buffer and into a <c>FileStream</c> whose buffer nothing flushed until the
/// process exited. <c>RunnerHost</c> prints "nothing ready" every time it looks,
/// so there was plenty to see; the modal said "It has said nothing yet" for the
/// whole of it, and killing the runner is what would finally have written the
/// file. A log a person cannot read until the thing it is about has stopped is
/// not a log.
/// </para>
/// <para>
/// <b>And a runner started anywhere else was reported as no runner at all.</b>
/// The modal knew only about a child this console spawned, which is one way a
/// runner comes to be running on a machine and not the usual one. The fleet
/// already answers the question - state, what it holds, when it was last heard
/// from - and this machine's row is the one with the arrow on it.
/// </para>
/// </remarks>
public class TheRunnerLogIsReadableWhileItRunsTests
{
    private static readonly DateTimeOffset T0 = new(2026, 9, 6, 12, 0, 0, TimeSpan.Zero);

    private const string Mine = "01a078bb-0000-0000-0000-000000000001";

    [Test]
    public async Task What_the_runner_writes_is_on_disk_before_it_stops()
    {
        var path = Path.Combine(Path.GetTempPath(), $"gg-runner-{Guid.NewGuid():N}.log");
        var source = new Pipe();

        try
        {
            var draining = RunnerLog.CaptureAsync(source, path, CancellationToken.None);

            await source.WriteAsync("nothing ready\nnothing ready\n");

            // NOT A SLEEP, AND NOT A CLOSE. The pipe says when the capture has
            // consumed everything and come back for more, so the file is read
            // while the source is still open - which is the whole claim, since
            // closing it would flush a buffer that had never been flushed.
            await source.Drained;

            await Assert.That(new RunnerLog(path).Read())
                .IsEquivalentTo(new[] { "nothing ready", "nothing ready" })
                .Because("the runner is still running and the file already has what it said. "
                       + "Draining into an unflushed FileStream is what made this empty.");

            source.Finish();
            await draining;
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Test]
    public async Task A_runner_this_console_did_not_start_still_reports_its_status()
    {
        var state = new AppState
        {
            Mode = UiMode.Runner,
            LocalRunnerId = Mine,
            Here = new RunnerHere { LogPath = "/tmp/gg/runner.log" },
            Runners = new RunnerList
            {
                Runners =
                [
                    new RunnerSummary
                    {
                        RunnerId = Mine,
                        Label = "Kevins-MBP",
                        State = RunnerStates.Busy,
                        CurrentFlightNumber = "GG-54",
                        LastHeartbeatAt = T0,
                    },
                ],
            },
        };

        var modal = PaneText.Modal(state);

        await Assert.That(modal).DoesNotContain("has not started a runner")
            .Because("one is running on this machine, and which terminal started it is not "
                   + $"what somebody opened this to ask. Modal:\n{modal}");
        await Assert.That(modal).Contains("GG-54")
            .Because("what it is working on is the status.");
        await Assert.That(modal).Contains(RunnerStates.Busy);
    }

    [Test]
    public async Task And_nothing_running_anywhere_says_that_plainly()
    {
        var modal = PaneText.Modal(new AppState { Mode = UiMode.Runner });

        await Assert.That(modal).Contains("no runner")
            .Because("the anchor: with nothing registered and no child, the modal has to say "
                   + $"so rather than describe a runner it invented. Modal:\n{modal}");
    }

    /// <summary>
    /// A stream a test writes into, which says when the reader has caught up.
    /// </summary>
    /// <remarks>
    /// <c>Drained</c> completes when a read finds nothing left and is about to
    /// wait, so the assertion happens at a known point rather than after a
    /// pause somebody tuned on their own machine.
    /// </remarks>
    private sealed class Pipe : Stream
    {
        private readonly Lock _gate = new();
        private readonly SemaphoreSlim _more = new(0);
        private readonly MemoryStream _buffer = new();
        private TaskCompletionSource _drained = new();
        private bool _finished;
        private long _read;

        internal Task Drained => _drained.Task;

        internal async Task WriteAsync(string text)
        {
            lock (_gate)
            {
                _drained = new TaskCompletionSource();
                var at = _buffer.Position;
                _buffer.Position = _buffer.Length;
                _buffer.Write(Encoding.UTF8.GetBytes(text));
                _buffer.Position = at;
            }

            _more.Release();
            await Task.Yield();
        }

        internal void Finish()
        {
            _finished = true;
            _more.Release();
        }

        public override async ValueTask<int> ReadAsync(
            Memory<byte> destination, CancellationToken cancellationToken = default)
        {
            while (true)
            {
                lock (_gate)
                {
                    if (_read < _buffer.Length)
                    {
                        _buffer.Position = _read;
                        var got = _buffer.Read(destination.Span);
                        _read += got;
                        return got;
                    }

                    _drained.TrySetResult();
                }

                if (_finished)
                {
                    return 0;
                }

                await _more.WaitAsync(cancellationToken);
            }
        }

        public override bool CanRead => true;

        public override bool CanSeek => false;

        public override bool CanWrite => false;

        public override long Length => _buffer.Length;

        public override long Position { get => _read; set => throw new NotSupportedException(); }

        public override void Flush() { }

        public override int Read(byte[] buffer, int offset, int count) =>
            ReadAsync(buffer.AsMemory(offset, count)).AsTask().GetAwaiter().GetResult();

        public override long Seek(long offset, SeekOrigin origin) =>
            throw new NotSupportedException();

        public override void SetLength(long value) => throw new NotSupportedException();

        public override void Write(byte[] buffer, int offset, int count) =>
            throw new NotSupportedException();
    }
}
