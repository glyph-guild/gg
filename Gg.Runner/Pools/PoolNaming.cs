namespace Gg.Runner.Pools;

/// <summary>
/// What a pool may be called, because the proxy and this runner must agree.
/// </summary>
/// <remarks>
/// <para>
/// <b>A reserved prefix, held on both sides.</b> The scope proxy allows
/// <c>/containers/create</c> only for names carrying it, and
/// <see cref="MaintainLoop"/> mints members as <c>{pool}-{slot}</c>. Those two
/// facts are one contract, and it used to be written down in only one of them:
/// the proxy named the WALK's pool, so every other pool was refused at create.
/// </para>
/// <para>
/// <b>The reason to refuse early is that the late refusal lies.</b> A 403 from
/// that proxy is what a correct refusal looks like — it is what
/// <c>ProbeScopeAsync</c> asks for and treats as proof the bound holds. A
/// misnamed pool produced the same answer as a genuine out-of-scope reach, so
/// the bug wore the safety property's face. Refusing here, before anything is
/// asked of the proxy, is what makes a 403 mean one thing again.
/// </para>
/// <para>
/// <b>It is a prefix rather than a list.</b> A deployment names its own pools
/// and this binary learns none of them; what it holds is the one shape the
/// proxy was configured to allow, which is deployment knowledge expressed once.
/// </para>
/// </remarks>
public static class PoolNaming
{
    /// <summary>The prefix every pool - and so every member - must carry.</summary>
    public const string ReservedPrefix = "gg-pool-";

    /// <summary>
    /// The pool name, or a refusal naming both it and the prefix.
    /// </summary>
    /// <remarks>
    /// Article XI: the diagnosis rather than a bare throw. Somebody whose pool
    /// is refused needs the convention, not the news that something was wrong
    /// with a name they chose.
    /// </remarks>
    public static string Require(string pool)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(pool);

        return pool.StartsWith(ReservedPrefix, StringComparison.Ordinal)
            ? pool
            : throw new ArgumentException(
                $"The pool '{pool}' cannot be maintained: every pool this runner acts on must be "
              + $"named '{ReservedPrefix}…', because the scope proxy allows creating a container "
              + "only under that prefix and a member is named after its pool. Refused here rather "
              + "than at the proxy, because the proxy's refusal is a 403 - which is also what a "
              + "correct out-of-scope refusal looks like, and the two must not be confused.",
                nameof(pool));
    }
}
