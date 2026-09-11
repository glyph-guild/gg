using System.Diagnostics;
using System.Runtime.InteropServices;

namespace Gg.Console;

/// <summary>
/// The verification link, opened in a browser or put on the clipboard.
/// </summary>
/// <remarks>
/// <para>
/// <b>Both spawn a child, which is why neither is a session's.</b> gg owns the
/// terminal the link is drawn in, so it cannot be clicked and cannot be
/// selected without fighting the alternate screen - and the alternative is a
/// person reading a long URL across to a browser by hand.
/// </para>
/// <para>
/// <b>What happened is always said.</b> A browser that opened behind this
/// window and a copy that silently failed look identical from where a person
/// is sitting, so the outcome is a sentence either way rather than a silence
/// that means success.
/// </para>
/// <para>
/// <b>The command is chosen by platform and nothing is shell-interpreted.</b>
/// The URI is passed as a single argument or written to standard input, never
/// through a shell, so a link cannot become a second command.
/// </para>
/// <para>
/// <b>The clipboard takes it on standard input and the browser cannot.</b> A
/// URI on a command line is visible to every process on the machine, and this
/// one carries a single-use code - so where there is a choice it is not put
/// there. Opening a browser has no such choice, which is worth knowing rather
/// than pretending otherwise.
/// </para>
/// </remarks>
public static class ConsoleLink
{
    /// <summary>How long to wait for a child that should return at once.</summary>
    private const int Moment = 5000;

    /// <param name="start">
    /// Runs the child, writing the second argument to its standard input when
    /// there is one, and answers its exit code.
    /// </param>
    public static AppState Open(
        AppState state, string uri, Func<ProcessStartInfo, string?, int> start)
    {
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(start);

        if (Opener() is not { } opener)
        {
            return state with
            {
                LastSignIn =
                    $"This platform has no browser command gg knows. Copy the link instead: {uri}",
            };
        }

        var info = new ProcessStartInfo(opener);
        info.ArgumentList.Add(uri);

        return state with
        {
            LastSignIn = Ran(info, null, start)
                ? "Opened the link in a browser. Approve it there, then press a."
                : "The browser would not open. Copy the link instead, with c.",
        };
    }

    public static AppState Copy(
        AppState state, string uri, Func<ProcessStartInfo, string?, int> start)
    {
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(start);

        if (Copier() is not { } copier)
        {
            return state with
            {
                LastSignIn = $"This platform has no clipboard command gg knows: {uri}",
            };
        }

        var info = new ProcessStartInfo(copier.Command)
        {
            RedirectStandardInput = true,
        };

        foreach (var argument in copier.Arguments)
        {
            info.ArgumentList.Add(argument);
        }

        // THROUGH STANDARD INPUT, not an argument: a URI on a command line is
        // visible to every process on the machine, and this one carries a
        // single-use code.
        return state with
        {
            LastSignIn = Ran(info, uri, start)
                ? "The link is on the clipboard."
                : "The clipboard would not take it.",
        };
    }

    /// <summary>
    /// Puts text on the clipboard, or answers why it could not.
    /// </summary>
    /// <remarks>
    /// <b>Extracted so there is ONE platform table.</b>
    /// <see cref="ConsoleClipboard"/> copies a modal's text and this copies a
    /// sign-in link; a second pbcopy/xclip/clip switch would be a copy of this
    /// one to keep in agreement. What differs between the two callers is which
    /// slot the sentence lands in, which is theirs to say.
    /// </remarks>
    /// <returns>Null when it went on; the reason otherwise.</returns>
    public static string? Copied(string text, Func<ProcessStartInfo, string?, int> start)
    {
        ArgumentNullException.ThrowIfNull(text);
        ArgumentNullException.ThrowIfNull(start);

        if (Copier() is not { } copier)
        {
            return "this platform has no clipboard command gg knows";
        }

        var info = new ProcessStartInfo(copier.Command)
        {
            RedirectStandardInput = true,
        };

        foreach (var argument in copier.Arguments)
        {
            info.ArgumentList.Add(argument);
        }

        // THROUGH STANDARD INPUT, not an argument, for the reason the sign-in
        // link gives: a command line is visible to every process on the
        // machine. A refusal is not a secret, but the habit is worth keeping
        // where one method serves both.
        return Ran(info, text, start) ? null : "the clipboard command refused it";
    }

    private static bool Ran(
        ProcessStartInfo info, string? input, Func<ProcessStartInfo, string?, int> start)
    {
        try
        {
            return start(info, input) == 0;
        }
        catch (Exception failure) when (failure is System.ComponentModel.Win32Exception
                                            or InvalidOperationException
                                            or PlatformNotSupportedException)
        {
            return false;
        }
    }

    /// <summary>The command that opens a URL, or null where gg knows none.</summary>
    private static string? Opener() =>
        RuntimeInformation.IsOSPlatform(OSPlatform.OSX) ? "open"
        : RuntimeInformation.IsOSPlatform(OSPlatform.Linux) ? "xdg-open"
        : RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? "explorer"
        : null;

    /// <summary>The command that takes text on stdin and puts it on the clipboard.</summary>
    private static (string Command, string[] Arguments)? Copier() =>
        RuntimeInformation.IsOSPlatform(OSPlatform.OSX) ? ("pbcopy", [])
        : RuntimeInformation.IsOSPlatform(OSPlatform.Linux) ? ("xclip", ["-selection", "clipboard"])
        : RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? ("clip", [])
        : null;

    /// <summary>How long a child that should return at once is given.</summary>
    public static int Grace => Moment;
}
