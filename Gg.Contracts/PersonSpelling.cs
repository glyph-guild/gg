namespace Gg.Contracts;

/// <summary>
/// How a document names a person: <c>&lt;provider&gt;:&lt;subject&gt;</c>.
/// </summary>
/// <remarks>
/// <para>
/// <b>ADR-0024.</b> A watch's <c>for:</c> and an obligation's <c>approver:</c>
/// both name a person, and two spellings of one person drift. This is the one
/// parser both read.
/// </para>
/// <para>
/// <b>The provider subject, because it is the one thing a principal is unique
/// on</b>: a provider, and the subject that provider issued. A display changes
/// when somebody renames themselves and two people may share one; a principal
/// id means nothing to anybody writing a document.
/// </para>
/// <para>
/// <b>Syntax here, and nothing about any particular provider.</b> This binary
/// names no identity provider (<c>ProviderNeutralityTests</c>), so which
/// providers exist and what each one's subjects look like are the control
/// plane's to know. It resolves the pair to a principal of the tenant when the
/// document arrives, and a subject that names nobody there is refused there.
/// </para>
/// </remarks>
public static class PersonSpelling
{
    /// <summary>Whether this value claims to name a person rather than a role.</summary>
    public static bool IsPerson(string? value) => false;

    /// <summary>Why this value is not a well-formed person, or null when it is one.</summary>
    public static string? Diagnose(string value) => null;
}

/// <summary>Whose work a watch's flights are.</summary>
public static class LinesOfWork
{
    /// <summary>The tenant's, and what an absent <c>for:</c> means.</summary>
    public const string Tenant = "tenant";
}
