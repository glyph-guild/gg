using System.Diagnostics;
using System.Text.Json;
using System.Text.Json.Serialization;
using Gg.Contracts;

namespace Gg.Client;

/// <summary>Where the seed ended up, so a person can be told.</summary>
public abstract record SeedPlacement
{
    /// <summary>On the clipboard, ready to paste.</summary>
    public sealed record Clipboard : SeedPlacement;

    /// <summary>
    /// In a file, because there was no clipboard.
    /// </summary>
    /// <remarks>
    /// Named, not implied. A fallback nobody is told about is a seed nobody
    /// finds, and the takeover proceeds either way - failing a takeover because
    /// a copy-paste helper was missing would be the tail wagging the dog.
    /// </remarks>
    public sealed record File(string Path, string Why) : SeedPlacement;
}

/// <summary>
/// Puts text where a person can paste it from.
/// </summary>
/// <remarks>
/// <para>
/// <b>Near the executor, never into it.</b> Sandcastle tried to pre-fill Claude
/// Code's composer, found no flag that fills without submitting, and settled on
/// clipboard plus a blank session. Ungainly and correct: the alternative is
/// screen-scraping another program's terminal interface, which breaks whenever
/// that program is improved.
/// </para>
/// <para>
/// A port, because "there is no clipboard" is an ordinary state - a headless
/// machine, a container, an ssh session - and the fallback has to be testable
/// without unplugging one.
/// </para>
/// </remarks>
public interface IClipboard
{
    /// <summary>Copies, or says why it could not.</summary>
    /// <returns>Null on success; the reason otherwise.</returns>
    string? Copy(string text);
}

/// <summary>The system clipboard, via whichever helper this machine has.</summary>
/// <remarks>
/// Three, because three platforms. Each is asked to exist before it is used, so
/// a missing helper is a diagnosis rather than a process that fails to start.
/// </remarks>
public sealed class SystemClipboard : IClipboard
{
    public string? Copy(string text)
    {
        ArgumentNullException.ThrowIfNull(text);

        var (command, arguments) = OperatingSystem.IsMacOS()
            ? ("pbcopy", "")
            : OperatingSystem.IsWindows()
                ? ("clip", "")
                : (System.Environment.GetEnvironmentVariable("WAYLAND_DISPLAY") is { Length: > 0 }
                    ? ("wl-copy", "")
                    : ("xclip", "-selection clipboard"));

        try
        {
            using var process = Process.Start(new ProcessStartInfo(command, arguments)
            {
                RedirectStandardInput = true,
                UseShellExecute = false,
            });

            if (process is null)
            {
                return $"{command} could not be started";
            }

            process.StandardInput.Write(text);
            process.StandardInput.Close();
            process.WaitForExit(5000);

            return process.ExitCode == 0 ? null : $"{command} exited {process.ExitCode}";
        }
        catch (Exception failure)
        {
            // A missing helper throws Win32Exception; a sandbox throws something
            // else. Either way the answer is the same and the takeover carries
            // on without it.
            return $"{command} is not available here ({failure.GetType().Name})";
        }
    }
}

/// <summary>
/// Places the seed where a person can get at it, whatever this machine has.
/// </summary>
public static class SeedPlacer
{
    /// <summary>Clipboard first, a named file otherwise.</summary>
    /// <remarks>
    /// <b>Never fails.</b> A takeover that refused to start because a clipboard
    /// helper was missing would be a feature defeated by its own convenience.
    /// </remarks>
    public static SeedPlacement Place(string seed, IClipboard clipboard, string directory)
    {
        ArgumentNullException.ThrowIfNull(seed);
        ArgumentNullException.ThrowIfNull(clipboard);

        if (clipboard.Copy(seed) is not { Length: > 0 } why)
        {
            return new SeedPlacement.Clipboard();
        }

        var path = System.IO.Path.Combine(directory, "gg-takeover-seed.txt");

        System.IO.Directory.CreateDirectory(directory);
        System.IO.File.WriteAllText(path, seed);

        return new SeedPlacement.File(path, why);
    }
}

/// <summary>
/// Reads the decision a person left behind, or nothing at all.
/// </summary>
/// <remarks>
/// <para>
/// <b>Defensive on purpose.</b> The console handed the terminal to somebody for
/// minutes and has no idea what happened in between. Optimism here produces a
/// client that silently applies a garbled decision, so every failure ends the
/// same way - null, a diagnosis, and a flight left exactly as it was for a person
/// to resolve.
/// </para>
/// <para>
/// <b>The third case is the one that parses.</b> A return file left over from a
/// previous takeover is well-formed and describes a different flight; applying it
/// would put one flight's decision onto another, which is worse than losing a
/// decision. The flight id is checked for that reason and no other.
/// </para>
/// </remarks>
public static class TakeoverReturnReader
{
    /// <summary>How much of a return file is worth reading.</summary>
    /// <remarks>
    /// A decision is a few hundred bytes. Anything enormous is a file that is not
    /// this, and reading it into memory to find that out is how a client is made
    /// to fall over by a file it did not write.
    /// </remarks>
    public const int MaxBytes = 64 * 1024;

    /// <summary>Where a person leaves their decision.</summary>
    public static string PathIn(string directory) =>
        System.IO.Path.Combine(directory, "gg-return.json");

    /// <summary>
    /// The decision, or null with a diagnosis.
    /// </summary>
    /// <param name="path">Where the file would be.</param>
    /// <param name="expectedFlightId">The flight that was actually taken.</param>
    public static (TakeoverReturn? Decision, string? Diagnosis) Read(
        string path, string expectedFlightId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(expectedFlightId);

        if (!System.IO.File.Exists(path))
        {
            // Not a failure. Somebody who took a flight and wrote nothing is the
            // ordinary end of an abandoned takeover, and it is recorded as
            // itself rather than as a broken file.
            return (null, null);
        }

        string text;
        try
        {
            var info = new FileInfo(path);
            if (info.Length > MaxBytes)
            {
                return (null,
                    $"The return file is {info.Length} bytes, and a decision is a few hundred. It "
                  + "was not read, and the flight is untouched.");
            }

            text = System.IO.File.ReadAllText(path);
        }
        catch (IOException failure)
        {
            return (null, $"The return file could not be read: {failure.Message}. The flight is "
                        + "untouched.");
        }

        TakeoverReturn? decision;
        try
        {
            decision = JsonSerializer.Deserialize(text, TakeoverJson.Default.TakeoverReturn);
        }
        catch (JsonException)
        {
            // Garbage, and truncation mid-write, arrive here identically - and
            // they lead to the same place, so they are not told apart.
            decision = null;
        }

        return TakeoverReturn.Validate(decision, expectedFlightId) is { } diagnosis
            ? (null, diagnosis)
            : (decision, null);
    }
}

/// <summary>Source-generated, because this ships in a Native AOT binary.</summary>
[JsonSourceGenerationOptions(
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    PropertyNameCaseInsensitive = true)]
[JsonSerializable(typeof(TakeoverReturn))]
[JsonSerializable(typeof(TakeoverRecord))]
public sealed partial class TakeoverJson : JsonSerializerContext;
