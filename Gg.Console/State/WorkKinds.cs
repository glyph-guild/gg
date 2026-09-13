namespace Gg.Console;

/// <summary>
/// The work kinds this tenant declared, as something to choose from.
/// </summary>
/// <remarks>
/// <para>
/// <b>A flight is FOR one thing, and only a work-kind layer supplies that.</b>
/// The topology carries every name a tenant has declared with the role it plays;
/// root is the floor that every kind narrows and is not a choice, and a
/// narrowing or a strategy is not one either. Filtering on the role is the same
/// reading <c>AirspaceViews</c> already does one file over.
/// </para>
/// <para>
/// <b>Absent must stay absent.</b> The control plane reads a missing work kind
/// as <c>implement</c> - the kind every flight before kinds existed was - so a
/// console that supplied that name locally would be declaring something nobody
/// chose. An empty list is "there is nothing to choose", never "choose this".
/// </para>
/// <para>
/// <b>Pure, so the question can be asked without a terminal.</b> What is on the
/// screen when somebody presses a key is the model's, and this is the one place
/// that turns a topology into the answer.
/// </para>
/// </remarks>
public static class WorkKinds
{
    /// <summary>One work kind a tenant declared, and what it says it is for.</summary>
    /// <param name="Name">The name a flight names. Empty on the row that names none.</param>
    /// <param name="Description">Its envelope's own sentence, or null where it gave one.</param>
    public sealed record WorkKind(string Name, string? Description)
    {
        /// <summary>What the row reads as when there is no description.</summary>
        /// <remarks>
        /// <b>Empty rather than invented.</b> A kind declared before
        /// descriptions existed has none, and writing something from its name
        /// would be this console describing somebody else's governance.
        /// </remarks>
        public string Said => Description ?? "";
    }

    /// <summary>
    /// Every row of the question, the answer that names no kind first.
    /// </summary>
    /// <remarks>
    /// <b>`No kind' is a row and it is the first one.</b> Inheriting the floor
    /// is an answer - it is what every flight before kinds existed was - so it
    /// is on the list rather than being what happens if you escape. Escaping
    /// opens nothing at all, which is a different act.
    /// </remarks>
    public static IReadOnlyList<WorkKind> Rows(AppState state) =>
        [new WorkKind("", "no kind - inherit the floor"), .. Declared(state)];

    /// <summary>
    /// The kind the question is sitting on, or null for "inherit the floor".
    /// </summary>
    /// <remarks>
    /// <b>Row zero is not a kind.</b> It is the answer every flight before
    /// kinds existed gave, and it has to travel as ABSENT rather than as a name:
    /// the control plane reads a missing kind as <c>implement</c>, so sending
    /// that word would be declaring something nobody chose.
    /// </remarks>
    public static string? Picked(AppState state)
    {
        var declared = Declared(state);
        var row = state.KindSelected - 1;

        return row >= 0 && row < declared.Count ? declared[row].Name : null;
    }

    /// <summary>Every work kind this tenant has declared, in the order it declared them.</summary>
    /// <remarks>
    /// <b>The description rides on the topology rather than being fetched.</b>
    /// It is written in the kind's envelope and projected onto the name when
    /// that envelope is applied, so a modal listing a tenant's kinds needs the
    /// one read it already does - not an envelope per row every time somebody
    /// opens the question.
    /// </remarks>
    public static IReadOnlyList<WorkKind> Declared(AppState state)
    {
        ArgumentNullException.ThrowIfNull(state);

        return state.Estate?.Names is not { } topology
            ? []
            :
            [
                .. topology.Names
                    .Where(name => string.Equals(
                        name.Role, Gg.Contracts.Roles.WorkKind, StringComparison.Ordinal))
                    .Select(name => new WorkKind(name.Name, name.Description)),
            ];
    }
}
