namespace Gg.Runner.Tests;

/// <summary>
/// The stub survives its own teardown, and takes a port without a window.
/// </summary>
/// <remarks>
/// <para>
/// <b>One cause behind the whole cluster, and it is teardown.</b> .NET's managed
/// <c>HttpListener</c> - the implementation on anything that is not Windows -
/// re-enters its endpoint manager on <c>Close</c>, and that path BINDS a socket
/// in order to look one up. When the entry has already gone it tries to bind a
/// port something else now holds and throws <c>Address already in use</c>, from
/// <c>Dispose</c>.
/// </para>
/// <para>
/// Every test here uses <c>await using</c>, so a throw during teardown fails
/// whichever test happened to be disposing - which is exactly why the failures
/// looked scattered across a file, named different behaviours, and never carried
/// an assertion message. Guarding <c>Close</c> took the measured rate from 1 in
/// 12 full-suite runs to 0 in 12.
/// </para>
/// <para>
/// <b>The port work below is a second, real defect found on the way.</b> Asking
/// the operating system for a free port and then giving it back before binding
/// it is a check whose answer expires, and two stub servers in two test
/// assemblies were both doing it. It was not what produced the observed
/// failures, and it is fixed because it would have.
/// </para>
/// <para>
/// That produced failures that looked unrelated and were not: a test asserting a
/// <c>409</c> fence saw a <c>200</c> because it was talking to a stub configured
/// by another test; a test counting observed requests saw none because its own
/// traffic went elsewhere; and a conformance test threw on dispose. Every one of
/// them is in the file that uses this stub.
/// </para>
/// <para>
/// <b>And the race is between PROCESSES.</b> Serialising construction inside one
/// assembly closed the window there and the failures continued - which is how the
/// second stub server, in the other test assembly, was found doing the same
/// thing. No lock spans that, so there is no probe at all now: a port is taken by
/// binding the real listener, and the operating system arbitrates.
/// </para>
/// <para>
/// <b>Deterministic, not probabilistic.</b> This does not sample a race - a test
/// that fires four times in five would rot into exactly the kind of flake being
/// fixed. It asserts the property that makes the race impossible.
/// </para>
/// </remarks>
public class StubSurfaceTests
{
    [Test]
    public async Task Many_stubs_built_at_once_each_get_their_own_port()
    {
        // Before the fix this collided; after it, it cannot, because the only
        // bind that happens is the real one.
        var stubs = await Task.WhenAll(
            Enumerable.Range(0, 16).Select(_ => Task.Run(() => new StubRunnerSurface())));

        try
        {
            var addresses = stubs.Select(s => s.BaseAddress).ToList();

            await Assert.That(addresses.Distinct(StringComparer.Ordinal).Count())
                .IsEqualTo(addresses.Count)
                .Because("two stubs sharing a port answer each other's clients, which is how a "
                       + "test asserting a 409 fence saw a 200.");
        }
        finally
        {
            foreach (var stub in stubs)
            {
                await stub.DisposeAsync();
            }
        }
    }

    [Test]
    public async Task Each_stub_answers_its_own_client_and_nobody_elses()
    {
        // The property the conformance tests actually depend on, asserted
        // directly rather than inferred from them passing. Every stub is given a
        // different renew status, and every client must see its own.
        var stubs = await Task.WhenAll(
            Enumerable.Range(0, 8).Select(_ => Task.Run(() => new StubRunnerSurface())));

        try
        {
            // Half fenced, half not, so a mix-up cannot pass by luck.
            for (var i = 0; i < stubs.Length; i++)
            {
                stubs[i].RenewStatus = i % 2 == 0
                    ? System.Net.HttpStatusCode.Conflict
                    : System.Net.HttpStatusCode.OK;
            }

            for (var i = 0; i < stubs.Length; i++)
            {
                using var http = new HttpClient { BaseAddress = new Uri(stubs[i].BaseAddress) };
                var result = await new RunnerProtocolClient(http, "t").RenewAsync("lease-9", 1);

                if (i % 2 == 0)
                {
                    await Assert.That(result).IsTypeOf<RenewResult.Fenced>()
                        .Because($"stub {i} was configured to fence and must be the one that answered.");
                }
                else
                {
                    await Assert.That(result).IsNotTypeOf<RenewResult.Fenced>();
                }

                await Assert.That(stubs[i].Observed).IsNotEmpty()
                    .Because("the request has to have reached the stub it was addressed to.");
            }
        }
        finally
        {
            foreach (var stub in stubs)
            {
                await stub.DisposeAsync();
            }
        }
    }

    [Test]
    public async Task Every_stub_server_guards_the_close_that_throws_during_teardown()
    {
        // THE STRUCTURAL TEST OF THE CAUSE. The behaviour cannot be asserted -
        // it is a fault in the framework's own bookkeeping that appears under
        // load - and a test that sampled it would be the flake it is fixing.
        // What can be asserted forever is that neither stub lets its teardown
        // throw into a test that has already finished.
        await Assert.That(Stubs().Count).IsEqualTo(2)
            .Because("both stub servers are covered, or the one that is not is the next flake.");

        foreach (var file in Stubs())
        {
            var code = File.ReadAllText(file);
            var dispose = code[code.IndexOf("DisposeAsync()", StringComparison.Ordinal)..];
            var closeAt = dispose.IndexOf("_listener.Close()", StringComparison.Ordinal);
            var guardAt = dispose.IndexOf("try", StringComparison.Ordinal);
            var catchAt = dispose.IndexOf("catch (HttpListenerException)", StringComparison.Ordinal);

            await Assert.That(closeAt).IsGreaterThan(0);
            await Assert.That(guardAt).IsGreaterThanOrEqualTo(0)
                .Because($"{Path.GetFileName(file)} closes its listener unguarded.");
            await Assert.That(guardAt).IsLessThan(closeAt)
                .Because($"{Path.GetFileName(file)}'s guard has to be around the close.");
            await Assert.That(catchAt).IsGreaterThan(closeAt)
                .Because($"{Path.GetFileName(file)} has to catch the one that is actually thrown.");
        }
    }

    [Test]
    public async Task A_retry_never_reuses_the_listener_it_just_failed_on()
    {
        // A FAULT THE FIX INTRODUCED, and the measurement loop found. A failed
        // Start disposes the listener it failed on, so reusing it for the next
        // candidate throws ObjectDisposedException from Prefixes - out of a
        // constructor, in whichever test was unlucky. Which is the same shape as
        // the flake being fixed, arriving from the other end.
        //
        // The property: the listener is built inside the retry, so each attempt
        // gets its own.
        foreach (var file in Stubs())
        {
            var code = Code(file);
            var bind = code[code.IndexOf("BindLoopback", StringComparison.Ordinal)..];
            var loopAt = bind.IndexOf("for (", StringComparison.Ordinal);
            var newAt = bind.IndexOf("new HttpListener()", StringComparison.Ordinal);

            await Assert.That(newAt).IsGreaterThan(0)
                .Because($"{Path.GetFileName(file)} has to construct its own listener to bind.");
            await Assert.That(loopAt).IsLessThan(newAt)
                .Because($"{Path.GetFileName(file)} builds the listener OUTSIDE the retry, so the "
                       + "second attempt uses the one the first attempt's failure disposed.");
        }
    }

    [Test]
    public async Task Every_stub_awaits_its_serve_loop_before_closing_the_listener()
    {
        // The other ordering, and it only became visible once the close stopped
        // throwing: closing while the serve loop still holds the listener hands
        // it a disposed object.
        foreach (var file in Stubs())
        {
            var code = Code(file);
            var dispose = code[code.IndexOf("DisposeAsync()", StringComparison.Ordinal)..];

            await Assert.That(dispose.IndexOf("await _loop", StringComparison.Ordinal))
                .IsLessThan(dispose.IndexOf("_listener.Close()", StringComparison.Ordinal))
                .Because($"{Path.GetFileName(file)} closes the listener while its loop may still be "
                       + "using it.");
        }
    }

    [Test]
    public async Task No_stub_server_asks_whether_a_port_is_free_before_binding_it()
    {
        // THE STRUCTURAL HALF, and it covers both stubs rather than the one this
        // file is about - because the race is between PROCESSES. A lock inside
        // one assembly closed the window there and the failures continued, which
        // is how the second stub in the other test assembly was found.
        //
        // The property is the absence of the probe: a port is taken by binding
        // the real listener, and the operating system arbitrates the only bind
        // that happens.
        var offenders = Sources()
            .Where(f => Path.GetFileName(f) != "StubSurfaceTests.cs")
            .Where(f => Code(f).Contains("TcpListener", StringComparison.Ordinal))
            .Select(f => Path.GetFileName(f)!)
            .ToList();

        await Assert.That(offenders).IsEmpty()
            .Because("a port that was probed and released is a port somebody else can take before "
                   + "it is bound, and two test assemblies were both doing it. Found: "
                   + string.Join(", ", offenders));
    }

    /// <summary>Both stub servers, wherever they live.</summary>
    private static List<string> Stubs() =>
        [.. Sources().Where(f => Path.GetFileName(f) is "StubRunnerSurface.cs" or "StubControlPlane.cs")];

    /// <summary>Source with comments removed, so a mention is not a match.</summary>
    private static string Code(string file) =>
        string.Join('\n', File.ReadAllLines(file)
            .Where(l => !l.TrimStart().StartsWith("//", StringComparison.Ordinal)
                     && !l.TrimStart().StartsWith("///", StringComparison.Ordinal)
                     && !l.TrimStart().StartsWith("*", StringComparison.Ordinal)));

    private static IEnumerable<string> Sources() =>
        Directory.EnumerateFiles(Root(), "*.cs", SearchOption.AllDirectories)
            .Where(f => !f.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}",
                            StringComparison.Ordinal)
                     && !f.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}",
                            StringComparison.Ordinal));

    private static string Root()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "Gg.sln")))
        {
            dir = dir.Parent;
        }

        return (dir ?? throw new InvalidOperationException("Gg.sln not found")).FullName;
    }

}
