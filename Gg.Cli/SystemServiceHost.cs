using System.Diagnostics;
using System.Runtime.Versioning;

namespace Gg.Cli;

/// <summary>
/// The real machine: its files, its users, and its service manager.
/// </summary>
/// <remarks>
/// <para>
/// <b>Every call is a program this machine already has</b> - <c>useradd</c>,
/// <c>dscl</c>, <c>chown</c>, <c>systemctl</c>, <c>launchctl</c> - run with an
/// argument list, never a shell string, so no value this is handed is ever
/// parsed by a shell.
/// </para>
/// <para>
/// <b>Not walked on a Mac yet.</b> The launchd half creates its user with
/// <c>dscl</c>, which is how a daemon's user is made there and is the half no
/// test here can reach. The slice's walk is on Linux.
/// </para>
/// </remarks>
[UnsupportedOSPlatform("windows")]
internal sealed class SystemServiceHost : IServiceHost
{
    public ServicePlatform? Platform { get; } =
        OperatingSystem.IsLinux() && Directory.Exists("/run/systemd/system") ? ServicePlatform.Systemd
        : OperatingSystem.IsMacOS() ? ServicePlatform.Launchd
        : null;

    public bool IsElevated => Environment.IsPrivilegedProcess;

    public bool Exists(string path) => File.Exists(path) || Directory.Exists(path);

    public string? HomeOf(string user)
    {
        if (OperatingSystem.IsMacOS())
        {
            var (code, output, _) = Run("dscl", ".", "-read", $"/Users/{user}", "NFSHomeDirectory");
            return code == 0 && output.Split(':', 2) is [_, var home] ? home.Trim() : null;
        }

        var (found, line, _) = Run("getent", "passwd", user);
        return found == 0 && line.Trim().Split(':') is { Length: >= 6 } fields ? fields[5] : null;
    }

    public IReadOnlyList<string> GroupsOf(string user)
    {
        var (code, output, error) = Run("id", "-Gn", user);

        return code == 0
            ? output.Split((char[])[' ', '\n'], StringSplitOptions.RemoveEmptyEntries)
            : throw new ServiceHostException($"could not read the groups of '{user}': {error.Trim()}");
    }

    public void CreateUser(string user, string home)
    {
        if (OperatingSystem.IsMacOS())
        {
            CreateDaemonUser(user, home);
            return;
        }

        // ITS OWN GROUP AND NO OTHER, no shell, and a home of its own: the
        // runner keeps its identity and credentials there.
        Must("useradd", "--system", "--user-group", "--create-home", "--home-dir", home,
            "--shell", "/usr/sbin/nologin", user);
    }

    public void CreateDirectory(string path, string owner, UnixFileMode mode)
    {
        Directory.CreateDirectory(path);
        File.SetUnixFileMode(path, mode);
        Must("chown", owner, path);
    }

    public void WriteFile(string path, string content, string owner, UnixFileMode mode)
    {
        // THE MODE FROM THE FIRST BYTE, and the file in place by rename: a seed
        // holding a token is never readable by anybody else, not even for the
        // moment between writing it and restricting it.
        var incoming = path + ".incoming";

        using (var stream = new FileStream(incoming, new FileStreamOptions
               {
                   Mode = FileMode.Create,
                   Access = FileAccess.Write,
                   UnixCreateMode = mode,
               }))
        using (var writer = new StreamWriter(stream))
        {
            writer.Write(content);
        }

        File.SetUnixFileMode(incoming, mode);
        Must("chown", owner, incoming);
        File.Move(incoming, path, overwrite: true);
    }

    public string? ReadFile(string path) => File.Exists(path) ? File.ReadAllText(path) : null;

    public void Delete(string path)
    {
        try
        {
            if (Directory.Exists(path))
            {
                Directory.Delete(path, recursive: false);
            }
            else
            {
                File.Delete(path);
            }
        }
        catch (IOException failed)
        {
            throw new ServiceHostException($"could not remove {path}: {failed.Message}");
        }
        catch (UnauthorizedAccessException failed)
        {
            throw new ServiceHostException($"could not remove {path}: {failed.Message}");
        }
    }

    public bool IsEmptyDirectory(string path) =>
        Directory.Exists(path) && !Directory.EnumerateFileSystemEntries(path).Any();

    public void Start(ServicePlatform platform, string name, string definition)
    {
        if (platform == ServicePlatform.Launchd)
        {
            Must("launchctl", "bootstrap", "system", definition);
            return;
        }

        Must("systemctl", "daemon-reload");
        Must("systemctl", "enable", "--now", $"{name}.service");
    }

    public void Restart(ServicePlatform platform, string name)
    {
        if (platform == ServicePlatform.Launchd)
        {
            Must("launchctl", "kickstart", "-k", $"system/{name}");
            return;
        }

        Must("systemctl", "restart", $"{name}.service");
    }

    public void Stop(ServicePlatform platform, string name, string definition)
    {
        if (platform == ServicePlatform.Launchd)
        {
            Must("launchctl", "bootout", $"system/{name}");
            return;
        }

        Must("systemctl", "disable", "--now", $"{name}.service");
    }

    public void Forget(ServicePlatform platform, string name)
    {
        if (platform == ServicePlatform.Systemd)
        {
            Must("systemctl", "daemon-reload");
        }
    }

    /// <summary>A hidden daemon user with a group of its own, the way macOS makes one.</summary>
    /// <remarks>
    /// The first id free for both a user and a group below 500, where macOS
    /// keeps the accounts that nobody logs in as.
    /// </remarks>
    private static void CreateDaemonUser(string user, string home)
    {
        var taken = Ids("/Users", "UniqueID").Concat(Ids("/Groups", "PrimaryGroupID")).ToHashSet();
        var id = Enumerable.Range(300, 200).FirstOrDefault(candidate => !taken.Contains(candidate));

        if (id == 0)
        {
            throw new ServiceHostException("no user id between 300 and 499 is free for the service user.");
        }

        var number = id.ToString(System.Globalization.CultureInfo.InvariantCulture);

        Must("dscl", ".", "-create", $"/Groups/{user}");
        Must("dscl", ".", "-create", $"/Groups/{user}", "PrimaryGroupID", number);
        Must("dscl", ".", "-create", $"/Users/{user}");
        Must("dscl", ".", "-create", $"/Users/{user}", "UniqueID", number);
        Must("dscl", ".", "-create", $"/Users/{user}", "PrimaryGroupID", number);
        Must("dscl", ".", "-create", $"/Users/{user}", "UserShell", "/usr/bin/false");
        Must("dscl", ".", "-create", $"/Users/{user}", "NFSHomeDirectory", home);
        Must("dscl", ".", "-create", $"/Users/{user}", "RealName", "gg runner");
        Must("dscl", ".", "-create", $"/Users/{user}", "IsHidden", "1");

        Directory.CreateDirectory(home);
        File.SetUnixFileMode(home, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
        Must("chown", $"{user}:{user}", home);
    }

    private static IEnumerable<int> Ids(string table, string attribute)
    {
        var (_, output, _) = Run("dscl", ".", "-list", table, attribute);

        foreach (var line in output.Split('\n', StringSplitOptions.RemoveEmptyEntries))
        {
            if (line.Split(' ', StringSplitOptions.RemoveEmptyEntries) is [_, var value]
                && int.TryParse(value, System.Globalization.CultureInfo.InvariantCulture, out var id))
            {
                yield return id;
            }
        }
    }

    private static void Must(string program, params string[] arguments)
    {
        var (code, _, error) = Run(program, arguments);

        if (code != 0)
        {
            throw new ServiceHostException(
                $"`{program} {string.Join(' ', arguments)}` exited {code}: {error.Trim()}");
        }
    }

    private static (int Code, string Output, string Error) Run(string program, params string[] arguments)
    {
        var start = new ProcessStartInfo(program)
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
        };

        foreach (var argument in arguments)
        {
            start.ArgumentList.Add(argument);
        }

        try
        {
            using var process = Process.Start(start)
                ?? throw new ServiceHostException($"{program} did not start.");
            var error = process.StandardError.ReadToEndAsync();
            var output = process.StandardOutput.ReadToEnd();
            process.WaitForExit();
            return (process.ExitCode, output, error.GetAwaiter().GetResult());
        }
        catch (System.ComponentModel.Win32Exception missing)
        {
            throw new ServiceHostException($"{program} is not on this machine: {missing.Message}");
        }
    }
}

/// <summary>
/// A machine with no service manager this build installs on: Windows, until
/// slice forty-five gives it one.
/// </summary>
/// <remarks>
/// It answers only the question the installer asks first, and that answer
/// refuses before any side effect is reached - so every other member saying so
/// is a defect report, not a path.
/// </remarks>
internal sealed class NoServiceHost : IServiceHost
{
    public ServicePlatform? Platform => null;

    public bool IsElevated => false;

    public bool Exists(string path) => throw Unreachable();

    public string? HomeOf(string user) => throw Unreachable();

    public IReadOnlyList<string> GroupsOf(string user) => throw Unreachable();

    public void CreateUser(string user, string home) => throw Unreachable();

    public void CreateDirectory(string path, string owner, UnixFileMode mode) => throw Unreachable();

    public void WriteFile(string path, string content, string owner, UnixFileMode mode) =>
        throw Unreachable();

    public string? ReadFile(string path) => throw Unreachable();

    public void Delete(string path) => throw Unreachable();

    public bool IsEmptyDirectory(string path) => throw Unreachable();

    public void Start(ServicePlatform platform, string name, string definition) => throw Unreachable();

    public void Restart(ServicePlatform platform, string name) => throw Unreachable();

    public void Stop(ServicePlatform platform, string name, string definition) => throw Unreachable();

    public void Forget(ServicePlatform platform, string name) => throw Unreachable();

    private static ServiceHostException Unreachable() =>
        new("this machine has no service manager gg installs on, and nothing should have asked it to act.");
}
