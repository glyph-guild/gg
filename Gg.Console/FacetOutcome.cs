using Gg.Local;

namespace Gg.Console;

/// <summary>
/// How asking a reader what there is to filter by ended.
/// </summary>
/// <remarks>
/// <para>
/// <b>Two endings, on <see cref="ItemOutcome"/>'s terms.</b> A pane offering
/// choices has one thing to do when there are none to offer - say why - so the
/// sentence is carried whole rather than sorted into a shape nobody branches
/// on. <see cref="BrowseOutcome"/> has five because each one is a different
/// thing for a person to go and do; this has one.
/// </para>
/// <para>
/// <b>Empty is not the same as unavailable.</b> A tracker with no iterations
/// answers <see cref="Offered"/> with an empty list, and a reader that cannot
/// be asked answers <see cref="Nothing"/> - because the first means nobody has
/// made a sprint and the second means go and look at the reader.
/// </para>
/// <para>
/// <b>It never throws, for <see cref="BrowseOutcome"/>'s reason.</b> The caller
/// is a redraw, and a redraw that has to catch is a console that dies because a
/// tracker did.
/// </para>
/// </remarks>
public abstract record FacetOutcome
{
    /// <summary>The tracker said what it has.</summary>
    public sealed record Offered(WorkItemFacets Facets) : FacetOutcome;

    /// <summary>It did not, and this is why.</summary>
    public sealed record Nothing(string Why) : FacetOutcome;
}
