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
    /// <summary>Every work kind this tenant has declared, in the order it declared them.</summary>
    public static IReadOnlyList<string> Declared(AppState state)
    {
        ArgumentNullException.ThrowIfNull(state);

        return state.Estate?.Names is not { } topology
            ? []
            :
            [
                .. topology.Names
                    .Where(name => string.Equals(
                        name.Role, Gg.Contracts.Roles.WorkKind, StringComparison.Ordinal))
                    .Select(name => name.Name),
            ];
    }
}
