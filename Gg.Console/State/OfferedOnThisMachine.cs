namespace Gg.Console;

/// <summary>
/// What a control plane offers this machine, as much of it as a page needs.
/// </summary>
/// <remarks>
/// <para>
/// <b>A summary rather than the offer.</b> The wire type carries every setting
/// and its value; this carries what the Environment page draws and what the
/// take needs — the version, how much is in it, and the two facts that decide
/// what the page should say. Putting the whole document on the model would
/// mean the values a control plane proposed were serialized into
/// <c>GG_STATE_DUMP</c> and the diagnostics bundle, which is a copy of somebody
/// else's configuration in a file a person mails us.
/// </para>
/// <para>
/// <b>The version is here because taking it needs it.</b>
/// <c>gg config accept</c> refuses anything but what is offered NOW, so the
/// console has to hand back the version the person actually read — the same
/// guard, reached by a key instead of by typing.
/// </para>
/// </remarks>
public sealed record OfferedOnThisMachine
{
    /// <summary>Names this exact offer.</summary>
    public required string Version { get; init; }

    /// <summary>How many settings it names.</summary>
    public required int Settings { get; init; }

    /// <summary>Whether it repoints something, and so needs a person.</summary>
    public bool NeedsAPerson { get; init; }

    /// <summary>Whether this machine has already taken this exact offer.</summary>
    public bool AlreadyAccepted { get; init; }
}
