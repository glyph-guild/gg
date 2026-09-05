namespace Gg.Client;

/// <summary>
/// The runner a person flies with, on their own machine.
/// </summary>
/// <remarks>
/// <para>
/// <b>A third runner on a host that already has two, and that is why it needs a
/// name.</b> A pool host runs <c>gg runner up</c> as itself and
/// <c>gg runner maintain</c> as <c>&lt;machine&gt;:maintain</c>; a single
/// credential file meant whichever registered last owned the only one, which is
/// the defect <see cref="FileRunnerStore.PathFor"/> exists to have fixed. A
/// hand-flight sharing either slot would be that defect arriving a third time —
/// and the harder one to see, because the two runners fighting would be on the
/// machine of whoever was flying.
/// </para>
/// <para>
/// <b>Named the way maintain is</b> — the machine, then what it is doing here —
/// because the reason is the same and a second scheme is a second thing to
/// remember. It is also what a person reads in <c>gg runners</c>, where a row
/// called <c>laptop-7</c> beside one called <c>laptop-7:hand</c> says which is
/// which without anybody having to ask.
/// </para>
/// </remarks>
public static class AttendedRunner
{
    /// <summary>What this machine's hand-flying runner registers under.</summary>
    public static string NameFor(string machine) => machine + ":hand";
}
