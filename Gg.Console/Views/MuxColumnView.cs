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
    private static readonly Color Shade = new(56, 79, 92);
    private static readonly Color Light = new(230, 237, 243);

    private readonly Action<MuxTab> _chosen;
    private readonly Action _fold;

    /// <param name="chosen">A row was clicked: switch to its tab.</param>
    /// <param name="fold">The background was clicked: fold the column, or unfold it.</param>
    public MuxColumnView(Action<MuxTab> chosen, Action fold)
    {
        _chosen = chosen;
        _fold = fold;
        CanFocus = false;
    }

    /// <summary>The agents and whether ctrl-g is armed, as the model has them.</summary>
    public IReadOnlyList<MuxRow> Agents { get; set; } = [];

    public bool Armed { get; set; }

    /// <summary>Whether it is folded to one cell, as the mux holds it.</summary>
    public bool Folded { get; set; }

    protected override bool OnDrawingContent(DrawContext? context)
    {
        var lines = MuxColumn.Lines(Agents, MuxTab.Gg, Math.Max(Viewport.Height, 3), Armed, Folded);
        for (var row = 0; row < lines.Count && row < Viewport.Height; row++)
        {
            var line = lines[row];
            SetAttribute(line.Active
                ? new Terminal.Gui.Drawing.Attribute(Ink, Blue, TextStyle.Bold)
                : new Terminal.Gui.Drawing.Attribute(Light, Shade));
            AddStr(0, row, line.Text);
        }

        return true;
    }

    protected override bool OnMouseEvent(Mouse mouse)
    {
        ArgumentNullException.ThrowIfNull(mouse);

        if (mouse.Position is not { } at)
        {
            return false;
        }

        // THE CLICK, NOT THE PRESS, FOLDS: folded on the press, the release landed on gg's content.
        var tab = MuxColumn.At(Agents, at.Y, Math.Max(Viewport.Height, 3));
        switch (MuxColumn.Mouse(mouse.IsPressed, mouse.IsSingleDoubleOrTripleClicked, tab))
        {
            case MuxColumnAct.Switch:
                _chosen(tab!.Value);
                break;
            case MuxColumnAct.Fold:
                _fold();
                break;
        }

        mouse.Handled = true;
        return true;
    }
}
