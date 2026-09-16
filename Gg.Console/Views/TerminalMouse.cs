using Terminal.Gui.App;

namespace Gg.Console.Views;

/// <summary>
/// Who owns the mouse: this console, or the terminal it is running in.
/// </summary>
/// <remarks>
/// <para>
/// <b>The only file that writes a terminal mode, and it writes exactly three.</b>
/// The same rule <c>LookStyles</c> keeps about colour and <c>KeyTranslator</c>
/// about keys: a sequence written from anywhere else is one nothing can find
/// when the terminal is left in a state nobody meant.
/// </para>
/// <para>
/// <b>Why it has to be given back rather than merely ignored.</b>
/// <c>IMouse.IsMouseDisabled</c> stops Terminal.Gui DISPATCHING a mouse event;
/// it does not stop the terminal SENDING one, and while <c>?1003h</c> is set a
/// terminal reports every movement to the application and will not draw a
/// selection of its own. Both are done here: the modes so the terminal takes
/// its selection back, and the flag so nothing arrives half way through.
/// </para>
/// <para>
/// <b>Any-motion is the one that matters.</b> gg asks for <c>?1003h</c>,
/// <c>?1015h</c> and <c>?1006h</c> at startup — motion reporting, the urxvt
/// encoding and SGR coordinates — and all three are reset together, so a
/// terminal that answered one of them is not left reporting through another.
/// </para>
/// <para>
/// <b>A terminal that ignores these is no worse off.</b> Resetting a mode that
/// was never set does nothing, so on a terminal with no selection of its own
/// the failure of this feature is that the screen simply stops — which is the
/// other half of what it is for.
/// </para>
/// </remarks>
public static class TerminalMouse
{
    /// <summary>Stop reporting: any-motion, urxvt encoding, SGR coordinates.</summary>
    private const string Released = "\u001b[?1003l\u001b[?1015l\u001b[?1006l";

    /// <summary>The three again, in the state Terminal.Gui sets up at startup.</summary>
    private const string Taken = "\u001b[?1003h\u001b[?1015h\u001b[?1006h";

    /// <summary>Hands the mouse to the terminal, so its own selection works.</summary>
    public static void ToTheTerminal(IApplication app)
    {
        ArgumentNullException.ThrowIfNull(app);

        app.Mouse.IsMouseDisabled = true;
        app.Driver?.WriteRaw(Released);
    }

    /// <summary>Takes it back.</summary>
    public static void ToTheConsole(IApplication app)
    {
        ArgumentNullException.ThrowIfNull(app);

        app.Driver?.WriteRaw(Taken);
        app.Mouse.IsMouseDisabled = false;
    }
}
