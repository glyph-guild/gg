namespace Gg.Contracts;

/// <summary>
/// How spent a window is, decided once for both sides of the protocol.
/// </summary>
/// <remarks>
/// <para>
/// <b>Here because it is the only place both repositories can see.</b> gg
/// renders this on four surfaces and the control plane decides two things with
/// it — whether a floor has been reached, and which machine is least spent.
/// The two cannot reference each other, so a rule written anywhere else is
/// written twice; four copies inside gg alone had already drifted once, and
/// across repositories the drift would show up as a pane and a refusal
/// describing different plans.
/// </para>
/// <para>
/// <b>Not a wire type, and it carries no state.</b> It sits beside
/// <see cref="AllowanceWindows"/> and <c>ProtocolSurface</c>, which are the
/// other two things in this package that describe the protocol rather than
/// travel on it.
/// </para>
/// </remarks>
public static class AllowanceShares
{
    /// <summary>
    /// Whether the meter's share is about a window that has already ended.
    /// </summary>
    /// <remarks>
    /// The meter keeps FIXED windows. Once one resets and nothing has asked
    /// again, its number is a true statement about a finished window — which
    /// is why it stops being an answer to "how spent is this allowance now".
    /// </remarks>
    public static bool RolledOver(DateTimeOffset? resetsAt, DateTimeOffset asOf) =>
        resetsAt is { } ended && ended <= asOf;

    /// <summary>The provider's own share, where it is about the window in force.</summary>
    /// <remarks>
    /// A share with no stated reset is still the provider's: a meter that
    /// reports a number and no window is still measuring the plan, and
    /// withholding it would prefer a typed ceiling on a technicality.
    /// </remarks>
    public static double? Live(double? reported, DateTimeOffset? resetsAt, DateTimeOffset asOf) =>
        reported is { } share && !RolledOver(resetsAt, asOf) ? share : null;

    /// <summary>The share of a ceiling somebody typed, where there is one.</summary>
    public static double? Typed(long tokens, long? limit) =>
        limit is { } ceiling and > 0 ? tokens / (double)ceiling : null;

    /// <summary>
    /// How spent this window is by the best measure available, or null when
    /// there is none.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>The provider's first.</b> A typed ceiling is a guess at a number the
    /// provider knows, and nothing on a machine can discover the real one.
    /// </para>
    /// <para>
    /// <b>Null rather than nought when neither answers.</b> Nought reads as a
    /// plan nobody has touched, which is the one answer a fleet deciding
    /// whether to spend somebody's subscription must never be handed by
    /// accident. A caller that cannot act on null has to say so itself — which
    /// is what the floor does, by refusing.
    /// </para>
    /// </remarks>
    public static double? Spent(
        double? reported, DateTimeOffset? resetsAt, long tokens, long? limit, DateTimeOffset asOf) =>
        Live(reported, resetsAt, asOf) ?? Typed(tokens, limit);

    /// <summary>The same rule, for a caller holding the wire type.</summary>
    /// <remarks>
    /// A convenience, and deliberately a one-line forward: the moment it
    /// computes anything of its own it is a second rule.
    /// </remarks>
    public static double? Spent(AllowanceWindow window, DateTimeOffset asOf)
    {
        ArgumentNullException.ThrowIfNull(window);

        return Spent(window.Reported, window.ResetsAt, window.Tokens, window.Limit, asOf);
    }
}
