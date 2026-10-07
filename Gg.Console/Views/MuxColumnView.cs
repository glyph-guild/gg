using Terminal.Gui.Drawing;
using Terminal.Gui.Input;
using Terminal.Gui.ViewBase;

namespace Gg.Console.Views;

/// <summary>
/// The mux's navigation column, drawn by Terminal.Gui while gg is the tab on screen (slice
/// sixty-nine): the same <see cref="MuxColumn.Lines"/> the mux paints beside an agent.
/// </summary>
/// <remarks>
/// <b>Labels, not a child.</b> This draws the column's text and nothing an agent wrote: no child
/// is ever rendered inside a view. Its rows come from the model, folded in from the mux on the
/// screen's tick.
/// </remarks>
internal sealed class MuxColumnView : View
{
    private static readonly Color Blue = new(0x7C, 0xB0, 0xCC);
    private static readonly Color Ink = new(16, 24, 32);

    private readonly Action<MuxTab> _chosen;

    public MuxColumnView(Action<MuxTab> chosen)
    {
        _chosen = chosen;
        CanFocus = false;
    }

    /// <summary>The agents and whether ctrl-g is armed, as the model has them.</summary>
    public IReadOnlyList<MuxRow> Agents { get; set; } = [];

    public bool Armed { get; set; }

    protected override bool OnDrawingContent(DrawContext? context)
    {
        var lines = MuxColumn.Lines(Agents, MuxTab.Gg, Math.Max(Viewport.Height, 3), Armed);
        for (var row = 0; row < lines.Count && row < Viewport.Height; row++)
        {
            var line = lines[row];
            SetAttribute(line.Active
                ? new Terminal.Gui.Drawing.Attribute(Blue, Ink, TextStyle.Bold)
                : new Terminal.Gui.Drawing.Attribute(Ink, Blue));
            AddStr(0, row, line.Text);
        }

        return true;
    }

    protected override bool OnMouseEvent(Mouse mouse)
    {
        ArgumentNullException.ThrowIfNull(mouse);

        if (!mouse.IsPressed || mouse.Position is not { } at)
        {
            return false;
        }

        if (MuxColumn.At(Agents, at.Y, Math.Max(Viewport.Height, 3)) is { } tab)
        {
            _chosen(tab);
        }

        mouse.Handled = true;
        return true;
    }
}
