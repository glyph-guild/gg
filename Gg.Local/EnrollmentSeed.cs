namespace Gg.Local;

/// <summary>
/// The enrollment token an install left beside this machine's
/// <c>config.json</c>, for <c>gg runner up</c> to redeem once.
/// </summary>
/// <remarks>
/// <para>
/// <b>Beside the configuration, owner-only, and spent on use</b> (slice
/// forty-three, rules 19 and 21). <c>gg service install --enroll</c> writes it
/// readable only by the service's user; the first start redeems it for a runner
/// credential of the machine's own and deletes it, so a token outlives nothing.
/// </para>
/// <para>
/// <b>The same name the installer writes</b> - <c>ServiceInstaller.EnrollmentFile</c>
/// in <c>gg service install</c>. Both say <c>enrollment</c>.
/// </para>
/// </remarks>
public static class EnrollmentSeed
{
    /// <summary>The file's name, beside <c>config.json</c>.</summary>
    public const string FileName = "enrollment";

    /// <summary>Where the seed is for a given configuration file.</summary>
    public static string PathBeside(string configurationPath) =>
        Path.Combine(
            Path.GetDirectoryName(configurationPath) ?? ".", FileName);

    /// <summary>The token, or null when there is none to redeem.</summary>
    public static string? Read(string configurationPath)
    {
        var path = PathBeside(configurationPath);

        try
        {
            return File.Exists(path) && File.ReadAllText(path).Trim() is { Length: > 0 } token
                ? token
                : null;
        }
        catch (Exception unreadable) when (unreadable is IOException or UnauthorizedAccessException)
        {
            // A seed this user cannot read is one it was not meant to redeem.
            return null;
        }
    }

    /// <summary>Removes the seed: a redeemed token is spent, and a spent one is kept nowhere.</summary>
    public static void Spend(string configurationPath)
    {
        var path = PathBeside(configurationPath);
        if (File.Exists(path))
        {
            File.Delete(path);
        }
    }
}
