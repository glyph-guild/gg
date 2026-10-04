using System.Security.Cryptography;

namespace Gg.Client;

/// <summary>
/// A P-256 private key kept in a restricted file, made on first use.
/// </summary>
/// <remarks>
/// <para>
/// <b>One derivation, two namings.</b> A runner's identity key and a machine's
/// store key are different things — one per runner name, one per machine — but
/// they rest identically: PKCS#8, base64, a file nobody else may read. Writing
/// that twice is how the two come to disagree about padding, about encoding, or
/// about whether the directory is created first, and the one that is wrong is
/// found on a machine nobody is watching.
/// </para>
/// <para>
/// <b>The curve is named here and nowhere else.</b> Everything that seals in
/// this product is P-256 because <c>RunnerSeal</c> is, and a second curve
/// arriving by default rather than by decision would produce keys that agree
/// with nothing.
/// </para>
/// </remarks>
internal static class KeyAtRest
{
    /// <summary>The key at that path, or a new one written there.</summary>
    public static ECDiffieHellman LoadOrCreate(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);

        if (File.Exists(path))
        {
            var key = ECDiffieHellman.Create();
            key.ImportPkcs8PrivateKey(Convert.FromBase64String(File.ReadAllText(path).Trim()), out _);
            return key;
        }

        var made = ECDiffieHellman.Create(ECCurve.NamedCurves.nistP256);

        Directory.CreateDirectory(Path.GetDirectoryName(path)!);

        // LOCKED DOWN BEFORE THE KEY GOES IN, which is FileCredentialStore's
        // discipline applied to the thing that opens it: there must be no
        // instant in which a readable file holds a private key.
        using (File.Create(path)) { }
        Restrict(path);

        File.WriteAllText(path, Convert.ToBase64String(made.ExportPkcs8PrivateKey()));
        Restrict(path);

        return made;
    }

    /// <summary>
    /// Nobody but this account, where the platform can say so.
    /// </summary>
    /// <remarks>
    /// <b>Best effort, and it does not fail the caller</b>, which is
    /// <see cref="RunnerIdentityKey"/>'s judgement carried over: refusing to
    /// start over a permission bit would take a fleet down for a property the
    /// surrounding directory already provides, and on Windows there is no chmod
    /// to make.
    /// </remarks>
    private static void Restrict(string path)
    {
        if (OperatingSystem.IsWindows())
        {
            return;
        }

        try
        {
            File.SetUnixFileMode(path, UnixFileMode.UserRead | UnixFileMode.UserWrite);
        }
        catch (Exception failure) when (failure is IOException or UnauthorizedAccessException)
        {
            // The directory's own mode is the backstop.
        }
    }
}
