using System.Text;
using Gg.Client;
using Gg.Contracts;
using Gg.Local;

namespace Gg.Console.Tests;

/// <summary>
/// A plan session run against a scripted host: it records the child's arguments, and plays keys
/// into the bar and reads what the panel draws, through the same two callbacks the real pty host
/// calls. Slice sixty-six.
/// </summary>
internal sealed class PlanSessionFixture : IDisposable
{
    internal string Root { get; } = Directory.CreateTempSubdirectory("gg-plan-session-").FullName;

    internal ItineraryDrafts Drafts => new(Root);

    internal IReadOnlyList<string> Arguments { get; private set; } = [];

    /// <summary>What the panel drew after each played key, in order.</summary>
    internal List<string> Frames { get; } = [];

    /// <summary>Whether the bar took each played key.</summary>
    internal List<bool> Taken { get; } = [];

    internal static SelfInvocation Ourselves() => new("/usr/local/bin/gg", ["runner", "tools"]);

    internal static byte[] Key(char key) => Encoding.ASCII.GetBytes([key]);

    internal static readonly byte[] Prefix = [HostedBar.Prefix];

    internal static readonly byte[] Escape = [0x1b];

    /// <summary>A draft of three legs, the last after the first, and a result the server left.</summary>
    internal void ThreeLegs(string result = "Legs (3):\n     opens: A 'implement' flight would open.")
    {
        _ = Drafts.Change("console", d => d with
        {
            Intent = FlightIntent.Of("three findings in one bug"),
            Legs =
            [
                new FlightNomination { Subject = "the icon", WorkKind = "implement", Reason = "named first" },
                new FlightNomination { Subject = "the padding", WorkKind = "implement", Reason = "shared" },
                new FlightNomination { Subject = "the walk", WorkKind = "implement", Reason = "last", After = "the icon" },
            ],
        });
        Drafts.KeepResult("console", result);
    }

    /// <summary>Runs the session, playing <paramref name="keys"/> into the bar one at a time.</summary>
    internal string Run(params byte[][] keys) => new PtyPlanSession(
            "claude",
            () => new HostedTerminal { Columns = 100, Rows = 40 },
            self: Ourselves(),
            host: (_, _, args, _, panel, took, _) =>
            {
                Arguments = args;
                foreach (var key in keys)
                {
                    Taken.Add(took(HostedGesture.Typed, key));
                    Frames.Add(string.Join('\n', panel(30, 100).Top));
                }

                return Task.FromResult(0);
            },
            drafts: Drafts)
        .Run();

    public void Dispose()
    {
        try
        {
            Directory.Delete(Root, recursive: true);
        }
        catch (IOException)
        {
        }
    }
}
