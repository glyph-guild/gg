using System.Diagnostics;
using Gg.Client;
using Gg.Contracts;

namespace Gg.Console;

/// <summary>What a takeover asked for.</summary>
public sealed record TakeRequest
{
    public required string FlightId { get; init; }

    public required string FlightNumber { get; init; }

    /// <summary>The held tree. The child starts here and nowhere else.</summary>
    public required string TreePath { get; init; }

    /// <summary>What the person reads before they start.</summary>
    public required TakeSeed Seed { get; init; }
}

/// <summary>What came back, whether or not anybody wrote anything.</summary>
public sealed record TakeResult
{
    public required TimeSpan Held { get; init; }

    /// <summary>Where the seed went, so the person could be told.</summary>
    public required SeedPlacement Placement { get; init; }

    /// <summary>The decision, when there was a usable one.</summary>
    public TakeoverReturn? Decision { get; init; }

    /// <summary>Why there is no decision, when a file existed and could not be used.</summary>
    public string? Diagnosis { get; init; }
}

/// <summary>
/// Hands the terminal to a person working in a flight's tree.
/// </summary>
/// <remarks>
/// Only ever called while no UI session is running. <c>ConsoleLoop</c> spawns
/// children between sessions, which is what makes the terminal provably free.
/// </remarks>
public interface ITakeSession
{
    TakeResult Take(TakeRequest request);
}

/// <summary>
/// A blank agent session in the flight's tree, with the seed on the clipboard.
/// </summary>
/// <remarks>
/// <para>
/// <b>Nothing types into the child.</b> Sandcastle tried to pre-fill Claude
/// Code's composer, found no flag that fills without submitting, and settled on
/// clipboard plus a blank session. Ungainly and correct: the alternative is
/// screen-scraping another program's terminal interface, which breaks whenever
/// that program is improved.
/// </para>
/// <para>
/// <b>And the takeover proceeds without a clipboard.</b> Headless machines exist,
/// and a takeover that refused to start because a copy-paste helper was missing
/// would be a feature defeated by its own convenience. The seed goes to a file,
/// and the person is told the path.
/// </para>
/// <para>
/// <b>The terminal is inherited, not redirected.</b> This is the opposite of the
/// executor, which redirects everything precisely so no terminal is involved.
/// Here a person is at the keyboard, and the child owns the screen until it
/// exits.
/// </para>
/// </remarks>
public sealed class TakeSession(
    string? command = null,
    IClipboard? clipboard = null,
    TimeProvider? time = null,
    Func<string, TakeoverClaim>? claim = null) : ITakeSession
{
    /// <summary>
    /// Takes the hold before the terminal goes, or says who has it.
    /// </summary>
    /// <remarks>
    /// <b>Null means no claim is made, and that is only for the UI tests.</b> A
    /// console that spawned a session without claiming would reintroduce the exact
    /// failure slice seven exists to remove - two people on two machines both
    /// working one flight, each believing they hold it - so the product passes one
    /// and <c>ConsoleTakeWiringTests</c> asserts it does.
    /// </remarks>
    private readonly Func<string, TakeoverClaim>? _claim = claim;

    private readonly string _command = command
        ?? Environment.GetEnvironmentVariable("GG_TAKE_COMMAND")
        ?? "claude";

    private readonly IClipboard _clipboard = clipboard ?? new SystemClipboard();
    private readonly TimeProvider _time = time ?? TimeProvider.System;

    /// <summary>The reason not to hand the terminal over, or null.</summary>
    private static string? Refusal(TakeoverClaim claim) => claim switch
    {
        TakeoverClaim.Granted => null,

        TakeoverClaim.Refused { Holder: { } holder } =>
            $"{holder.By} has held this since {holder.Since:HH:mm} and until "
          + $"{holder.HeldUntil:HH:mm}. Nothing was started: exactly one person holds a flight at "
          + "a time, which is what stops two people editing the same work.",

        TakeoverClaim.Refused =>
            "Somebody holds this flight and the control plane would not say who. Nothing was "
          + "started - a hold nobody can be attributed to is still a hold.",

        _ => "The control plane has no such flight, so there is nothing to take over.",
    };

    public TakeResult Take(TakeRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        // THE HOLD FIRST, and nothing is spawned without it. Exactly one person
        // holds a flight at a time, and the refusal names who - which is the whole
        // difference between a portable handoff and two people editing divergent
        // copies of the same work.
        if (_claim is not null && Refusal(_claim(request.FlightNumber)) is { } refused)
        {
            return new TakeResult
            {
                Held = TimeSpan.Zero,
                Placement = new SeedPlacement.File("", "no claim was made"),
                Diagnosis = refused,
            };
        }

        var placement = SeedPlacer.Place(
            TakeSeedComposer.Render(request.Seed), _clipboard, request.TreePath);

        // Said before the child starts, because once it starts the screen is
        // its own and nothing of ours will be read again until it exits.
        System.Console.WriteLine();
        System.Console.WriteLine($"Taking over {request.FlightNumber} in {request.TreePath}");
        System.Console.WriteLine(placement switch
        {
            SeedPlacement.Clipboard => "The handoff summary is on your clipboard. Paste it in.",
            SeedPlacement.File(var path, var why) =>
                $"No clipboard here ({why}). The handoff summary is at {path}.",
            _ => "",
        });
        System.Console.WriteLine(
            $"When you are done, write your decision to {TakeoverReturnReader.PathIn(request.TreePath)}");
        System.Console.WriteLine();

        var started = _time.GetTimestamp();

        try
        {
            var parts = _command.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            var start = new ProcessStartInfo(parts[0])
            {
                // The tree, so whatever they run is already looking at the work.
                WorkingDirectory = request.TreePath,
                UseShellExecute = false,
            };

            foreach (var argument in parts.Skip(1))
            {
                start.ArgumentList.Add(argument);
            }

            using var child = Process.Start(start);
            child?.WaitForExit();
        }
        catch (Exception failure)
        {
            // The person still has the seed and the tree. Saying which command
            // could not start beats a takeover that looks like it happened.
            System.Console.WriteLine($"'{_command}' could not be started: {failure.Message}");
        }

        var held = _time.GetElapsedTime(started);

        var (decision, diagnosis) = TakeoverReturnReader.Read(
            TakeoverReturnReader.PathIn(request.TreePath), request.FlightId);

        return new TakeResult
        {
            Held = held,
            Placement = placement,
            Decision = decision,
            Diagnosis = diagnosis,
        };
    }
}
