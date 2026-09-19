namespace Gg.Cli;

/// <summary>Which service manager a machine has, as far as installing gg goes.</summary>
public enum ServicePlatform
{
    /// <summary>Linux with systemd running as its init.</summary>
    Systemd,

    /// <summary>macOS, whose daemons are launchd's.</summary>
    Launchd,
}

/// <summary>What a person asked <c>gg service install</c> for.</summary>
/// <param name="ControlPlane">Where the runner reports; required for a first install.</param>
/// <param name="Enroll">Whether an enrollment token is to be read, prompted or piped - never an argument.</param>
/// <param name="User">The service user, or null for the platform's default.</param>
public sealed record ServiceRequest(string? ControlPlane, bool Enroll, string? User);

/// <summary>What happened, in sentences, and whether it was done or refused.</summary>
public sealed record ServiceOutcome(bool Done, IReadOnlyList<string> Said);

/// <summary>Raised by a host whose side effect did not happen.</summary>
public sealed class ServiceHostException(string message) : Exception(message);

/// <summary>
/// Every side effect <c>gg service install</c> has, and nothing else.
/// </summary>
public interface IServiceHost
{
    /// <summary>The service manager this machine has, or null for none this build installs on.</summary>
    ServicePlatform? Platform { get; }

    /// <summary>Whether this process may write root-owned files and create users.</summary>
    bool IsElevated { get; }

    /// <summary>Whether a file or directory is at this path.</summary>
    bool Exists(string path);

    /// <summary>A user's home directory, or null when there is no such user.</summary>
    string? HomeOf(string user);

    /// <summary>The groups a user is in.</summary>
    IReadOnlyList<string> GroupsOf(string user);

    /// <summary>Makes a system user with its own group and this home, and no login shell.</summary>
    void CreateUser(string user, string home);

    /// <summary>Makes a directory owned by <paramref name="owner"/> with this mode.</summary>
    void CreateDirectory(string path, string owner, UnixFileMode mode);

    /// <summary>Writes a file by rename, owned by <paramref name="owner"/>, with this mode from its first byte.</summary>
    void WriteFile(string path, string content, string owner, UnixFileMode mode);

    /// <summary>A file's text, or null when it is not there.</summary>
    string? ReadFile(string path);

    /// <summary>Removes a file, or a directory that is empty. Absent is not an error.</summary>
    void Delete(string path);

    /// <summary>Whether this is a directory with nothing in it.</summary>
    bool IsEmptyDirectory(string path);

    /// <summary>Enables and starts the service a definition describes.</summary>
    void Start(ServicePlatform platform, string name, string definition);

    /// <summary>Restarts a running service, so it runs the gg now installed.</summary>
    void Restart(ServicePlatform platform, string name);

    /// <summary>Stops and disables a service.</summary>
    void Stop(ServicePlatform platform, string name, string definition);

    /// <summary>Tells the service manager a definition is gone.</summary>
    void Forget(ServicePlatform platform, string name);
}

/// <summary><c>gg service install</c> and <c>gg service uninstall</c>.</summary>
/// <remarks>
/// <para>
/// <b>Slice forty-three, rule 21.</b> A runner became a service by a person
/// following a runbook - make a user, write a seed, copy a unit, sed two lines
/// of it, reload, enable - and nothing afterwards could say what had been
/// written, so nothing could take it away again. This writes the platform's own
/// definition from templates in this binary, records every path it made, and
/// removes exactly those.
/// </para>
/// <para>
/// <b>It never writes a byte of gg.</b> The service runs
/// <see cref="Binary"/>, a root-owned link install.sh makes by rename; a gg
/// that installed itself would be a gg that moves its own bytes, which this
/// program has never done.
/// </para>
/// <para>
/// <b>The enrollment token is written and not yet read.</b> Redeeming it is
/// step 4 of the slice, whose route does not exist yet. Until it does, the
/// service's first start still needs a person to sign in as its user, and the
/// install says so rather than implying otherwise.
/// </para>
/// </remarks>
public static partial class ServiceInstaller
{
    /// <summary>The gg a service runs: a root-owned link that install.sh makes.</summary>
    public const string Binary = "/usr/local/bin/gg";

    /// <summary>What an install wrote, so an uninstall removes exactly that.</summary>
    public const string Manifest = "/etc/gg/service.manifest";

    /// <summary>The systemd unit's name.</summary>
    public const string SystemdName = "gg-runner-up";

    /// <summary>The launchd daemon's label, and so its plist's name.</summary>
    /// <remarks>
    /// <b>With <c>gg-</c> in it</b>, as the systemd unit has: the doctor
    /// recognizes a gg service by that prefix to say which checks describe the
    /// shell it was run from rather than the service.
    /// </remarks>
    public const string LaunchdLabel = "dev.glyphguild.gg-runner-up";

    /// <summary>Beside config.json: the token the runner will redeem.</summary>
    public const string EnrollmentFile = "enrollment";

    private const string Root = "root";

    private static readonly UnixFileMode RootReadable =
        UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.GroupRead | UnixFileMode.OtherRead;

    private static readonly UnixFileMode RootDirectory =
        RootReadable | UnixFileMode.UserExecute | UnixFileMode.GroupExecute | UnixFileMode.OtherExecute;

    private static readonly UnixFileMode OwnerOnly = UnixFileMode.UserRead | UnixFileMode.UserWrite;

    private static readonly UnixFileMode OwnerOnlyDirectory = OwnerOnly | UnixFileMode.UserExecute;

    /// <summary>The user a platform's service runs as when nobody names one.</summary>
    /// <remarks>An underscore on macOS, which is how that platform marks a daemon's user.</remarks>
    public static string DefaultUser(ServicePlatform platform) =>
        platform == ServicePlatform.Launchd ? "_gg" : "gg";

    /// <summary>Where a user this creates lives.</summary>
    public static string DefaultHome(ServicePlatform platform, string user) =>
        platform == ServicePlatform.Launchd
            ? $"/private/var/{user.TrimStart('_')}"
            : $"/var/lib/{user}";

    /// <summary>Where a platform's service definition goes.</summary>
    public static string DefinitionPath(ServicePlatform platform) =>
        platform == ServicePlatform.Launchd
            ? $"/Library/LaunchDaemons/{LaunchdLabel}.plist"
            : $"/etc/systemd/system/{SystemdName}.service";

    private static string ServiceName(ServicePlatform platform) =>
        platform == ServicePlatform.Launchd ? LaunchdLabel : SystemdName;

    /// <summary>Installs the service, or restarts the one already installed.</summary>
    /// <param name="host">Every side effect.</param>
    /// <param name="request">What the person asked for.</param>
    /// <param name="readToken">
    /// Reads the enrollment token, called only once everything that could
    /// refuse has had its say - a token nobody will use is not asked for.
    /// </param>
    public static ServiceOutcome Install(IServiceHost host, ServiceRequest request, Func<string> readToken)
    {
        ArgumentNullException.ThrowIfNull(host);
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(readToken);

        if (host.Platform is not { } platform)
        {
            return Refused(
                "gg service install writes a systemd unit or a launchd daemon, and this machine "
              + "has neither running. Windows is slice forty-five's; a Linux without systemd has "
              + "no installed path yet. Nothing was written.");
        }

        if (!host.IsElevated)
        {
            return Refused(
                "gg service install writes root-owned files and creates a user, so it needs "
              + "root: run it again with sudo. Nothing was written.");
        }

        var name = ServiceName(platform);
        var definition = DefinitionPath(platform);

        // INSTALLED ALREADY, which is the update: install.sh has moved the
        // bytes and the link and asks again. The seed is the machine's own file
        // by now - the runner writes what it is offered into it - so nothing is
        // written, and the service is restarted onto the gg now at the link.
        if (host.Exists(Manifest))
        {
            if (request.ControlPlane is not null || request.Enroll || request.User is not null)
            {
                return Refused(
                    $"this machine's service is already installed ({Manifest}), and a second "
                  + "install only restarts it onto the gg now installed - it would not use "
                  + "--control-plane, --enroll or --user, so it will not take them. To install it "
                  + "afresh, run `gg service uninstall` first.");
            }

            try
            {
                host.Restart(platform, name);
            }
            catch (ServiceHostException failed)
            {
                return Refused($"{name} is installed and did not restart: {failed.Message}");
            }

            return Done($"{name} restarted, running {Binary}. Nothing was written.");
        }

        if (request.ControlPlane is not { Length: > 0 } controlPlane)
        {
            return Refused(
                "gg service install needs --control-plane <url> for a first install: nothing "
              + "else can tell this machine where its control plane is. Nothing was written.");
        }

        if (!Uri.TryCreate(controlPlane, UriKind.Absolute, out var address)
            || (address.Scheme != Uri.UriSchemeHttps && address.Scheme != Uri.UriSchemeHttp))
        {
            return Refused(
                $"'{controlPlane}' is not an http or https address, so it cannot be a control "
              + "plane. Nothing was written.");
        }

        var user = request.User ?? DefaultUser(platform);

        if (!ServiceUser().IsMatch(user) || user == Root)
        {
            return Refused(
                $"'{user}' cannot be the service user: it must be a plain user name - lower "
              + "case letters, digits, '_' and '-' - and never root. Nothing was written.");
        }

        if (!host.Exists(Binary))
        {
            return Refused(
                $"the service runs {Binary}, and nothing is there. gg service install never "
              + "writes a byte of gg: install.sh puts it there, by rename, and links it. "
              + "Nothing was written.");
        }

        if (host.Exists(definition))
        {
            return Refused(
                $"{definition} is already there, and gg service install did not write it - a "
              + "runner set up by hand from the runbook, most likely. Two runners on one "
              + "machine is not something this makes: remove that one first, or leave this "
              + "machine as it is. Nothing was written.");
        }

        var home = host.HomeOf(user);
        var creating = home is null;

        // THE DOCKER GROUP IS ROOT ON THE MACHINE: a member of it can mount /
        // into a container and write anything. A runner never holds it - the
        // pool host's proxy exists to take exactly that away from its
        // maintainer - so an existing user in it is refused rather than used.
        if (!creating && host.GroupsOf(user).Contains("docker", StringComparer.Ordinal))
        {
            return Refused(
                $"the user '{user}' is in the docker group, which is root on this machine by "
              + "another name. A runner never runs with it: name another --user, or take "
              + $"'{user}' out of the group. Nothing was written.");
        }

        home ??= DefaultHome(platform, user);

        var dotConfig = $"{home}/.config";
        var seedDirectory = $"{dotConfig}/good-grief";
        var seed = $"{seedDirectory}/config.json";
        var enrollment = $"{seedDirectory}/{EnrollmentFile}";

        if (host.Exists(seed))
        {
            return Refused(
                $"'{user}' already has {seed}: that is a machine's own configuration, and a "
              + "first install writes a fresh one. It will not overwrite yours. Nothing was "
              + "written.");
        }

        string? token = null;

        if (request.Enroll)
        {
            token = readToken().Trim();

            if (token.Length == 0)
            {
                return Refused(
                    "--enroll read no token. Paste it at the prompt, or pipe it in on stdin. "
                  + "Nothing was written.");
            }
        }

        // EVERY PATH THIS MAKES, RECORDED BEFORE ANY OF IT IS MADE, so an
        // install that stops halfway leaves a manifest uninstall can finish
        // from. A directory is listed only when this makes it: one that was
        // already there is somebody else's, and so is everything under it.
        var directories = new List<(string Path, string Owner, UnixFileMode Mode)>();

        if (!host.Exists("/etc/gg"))
        {
            directories.Add(("/etc/gg", Root, RootDirectory));
        }

        var owned = new List<(string Path, string Owner, UnixFileMode Mode)>();

        if (creating || !host.Exists(dotConfig))
        {
            owned.Add((dotConfig, user, OwnerOnlyDirectory));
        }

        if (creating || !host.Exists(seedDirectory))
        {
            owned.Add((seedDirectory, user, OwnerOnlyDirectory));
        }

        var files = new List<string> { seed };

        if (token is not null)
        {
            files.Add(enrollment);
        }

        files.Add(definition);

        var manifest = new ServiceManifest(
            platform, name, definition, user, creating, home,
            [.. directories.Select(d => d.Path), .. owned.Select(d => d.Path)],
            files);

        var said = new List<string>();

        try
        {
            foreach (var (path, owner, mode) in directories)
            {
                host.CreateDirectory(path, owner, mode);
            }

            host.WriteFile(Manifest, manifest.Render(), Root, RootReadable);

            if (creating)
            {
                host.CreateUser(user, home);
                said.Add($"created the user '{user}', home {home}, in no group but its own.");
            }

            foreach (var (path, owner, mode) in owned)
            {
                host.CreateDirectory(path, owner, mode);
            }

            host.WriteFile(
                seed,
                Gg.Local.ConfigurationFile.Render(new Gg.Local.Configuration
                {
                    ControlPlane = controlPlane,

                    // THE RUNBOOK'S SEED, and for its reason: accept-offered has
                    // no variable, deliberately, so a machine that takes what
                    // its control plane offers is one whose file says so.
                    AcceptOffered = true,
                }),
                user,
                OwnerOnly);
            said.Add($"wrote {seed} ({user}, 0600): control plane {controlPlane}, taking what it offers.");

            if (token is not null)
            {
                host.WriteFile(enrollment, token + "\n", user, OwnerOnly);
                said.Add(
                    $"wrote {enrollment} ({user}, 0600): the enrollment token. `gg runner up` "
                  + "does not redeem it yet - that is slice forty-three's step 4 - so until "
                  + $"then the runner still needs a person to sign in as '{user}' once.");
            }
            else
            {
                said.Add(
                    $"no enrollment token, so the runner needs a person to sign in as '{user}' "
                  + "once before it registers.");
            }

            host.WriteFile(
                definition,
                platform == ServicePlatform.Launchd
                    ? ServiceTemplates.Launchd(user, home)
                    : ServiceTemplates.Systemd(user),
                Root,
                RootReadable);
            said.Add($"wrote {definition} (root, 0644): runs `{Binary} runner up` as '{user}'.");

            host.Start(platform, name, definition);
            said.Add($"started {name}. `gg service uninstall` removes exactly what this wrote, "
                   + $"as {Manifest} lists it.");
        }
        catch (ServiceHostException failed)
        {
            said.Add(
                $"stopped part way: {failed.Message}. What was written is listed in {Manifest}, "
              + "and `gg service uninstall` removes it.");
            return new ServiceOutcome(false, said);
        }

        return new ServiceOutcome(true, said);
    }

    /// <summary>Removes exactly what an install wrote, and says what it left.</summary>
    public static ServiceOutcome Uninstall(IServiceHost host)
    {
        ArgumentNullException.ThrowIfNull(host);

        if (host.Platform is null)
        {
            return Refused(
                "gg service uninstall removes a systemd unit or a launchd daemon, and this "
              + "machine has neither running.");
        }

        if (!host.IsElevated)
        {
            return Refused(
                "gg service uninstall removes root-owned files and stops a service, so it needs "
              + "root: run it again with sudo. Nothing was removed.");
        }

        if (host.ReadFile(Manifest) is not { } text)
        {
            return Done(
                $"there is no {Manifest}, so gg service install wrote nothing here that is "
              + "still installed. Nothing was removed.");
        }

        if (ServiceManifest.Parse(text) is not { } manifest)
        {
            return Refused(
                $"{Manifest} is not a manifest this gg can read, so it cannot say what to "
              + "remove. Nothing was removed.");
        }

        var said = new List<string>();

        try
        {
            host.Stop(manifest.Platform, manifest.Name, manifest.Definition);
            said.Add($"stopped {manifest.Name}.");
        }
        catch (ServiceHostException failed)
        {
            // SAID AND CARRIED ON: a service that is not running is the ordinary
            // case for a machine being taken apart, and the files still go.
            said.Add($"{manifest.Name} did not stop cleanly ({failed.Message}); removing it anyway.");
        }

        try
        {
            foreach (var file in manifest.Files.Reverse())
            {
                host.Delete(file);
                said.Add($"removed {file}.");
            }

            host.Forget(manifest.Platform, manifest.Name);

            // DEEPEST FIRST, and only when empty: the runner keeps its own
            // identity beside the seed, and that is not this verb's to remove.
            foreach (var directory in manifest.Directories
                         .Where(d => d != "/etc/gg")
                         .OrderByDescending(d => d.Length))
            {
                if (host.IsEmptyDirectory(directory))
                {
                    host.Delete(directory);
                    said.Add($"removed {directory}.");
                }
                else if (host.Exists(directory))
                {
                    said.Add($"left {directory}: something other than this install is in it.");
                }
            }

            host.Delete(Manifest);

            if (manifest.Directories.Contains("/etc/gg") && host.IsEmptyDirectory("/etc/gg"))
            {
                host.Delete("/etc/gg");
            }

            said.Add($"removed {Manifest}.");
        }
        catch (ServiceHostException failed)
        {
            said.Add($"stopped part way: {failed.Message}. {Manifest} still lists what is left.");
            return new ServiceOutcome(false, said);
        }

        said.Add(
            manifest.CreatedUser
                ? $"left the user '{manifest.User}' and its home {manifest.Home}: they hold this "
                + "runner's identity and credentials, which gg service install did not write. "
                + "Remove the user yourself when this machine is done being a runner."
                : $"left the user '{manifest.User}', which was there before the install.");

        return new ServiceOutcome(true, said);
    }

    private static ServiceOutcome Done(string sentence) => new(true, [sentence]);

    private static ServiceOutcome Refused(string sentence) => new(false, [sentence]);

    [System.Text.RegularExpressions.GeneratedRegex(@"^[a-z_][a-z0-9_-]{0,31}\z")]
    private static partial System.Text.RegularExpressions.Regex ServiceUser();
}

/// <summary>What one install wrote, as a file uninstall can read back.</summary>
/// <remarks>
/// <b>Lines, not JSON</b>: a person reading <c>/etc/gg/service.manifest</c>
/// with <c>cat</c> should see what is on their machine, and a format with a
/// serializer behind it is one more context this binary must generate for.
/// </remarks>
internal sealed record ServiceManifest(
    ServicePlatform Platform,
    string Name,
    string Definition,
    string User,
    bool CreatedUser,
    string Home,
    IReadOnlyList<string> Directories,
    IReadOnlyList<string> Files)
{
    private const string Header =
        "# gg service install wrote these, and gg service uninstall removes exactly them.";

    public string Render()
    {
        var lines = new List<string>
        {
            Header,
            $"platform {Platform.ToString().ToLowerInvariant()}",
            $"service {Name}",
            $"definition {Definition}",
            $"user {User} {(CreatedUser ? "created" : "existing")}",
            $"home {Home}",
        };

        lines.AddRange(Directories.Select(d => $"dir {d}"));
        lines.AddRange(Files.Select(f => $"file {f}"));

        return string.Join('\n', lines) + "\n";
    }

    public static ServiceManifest? Parse(string text)
    {
        ServicePlatform? platform = null;
        string? name = null, definition = null, user = null, home = null;
        var created = false;
        var directories = new List<string>();
        var files = new List<string>();

        foreach (var line in text.Split('\n', StringSplitOptions.RemoveEmptyEntries))
        {
            if (line.StartsWith('#'))
            {
                continue;
            }

            var space = line.IndexOf(' ', StringComparison.Ordinal);

            if (space <= 0)
            {
                return null;
            }

            var (key, value) = (line[..space], line[(space + 1)..]);

            switch (key)
            {
                case "platform":
                    platform = value switch
                    {
                        "systemd" => ServicePlatform.Systemd,
                        "launchd" => ServicePlatform.Launchd,
                        _ => null,
                    };
                    break;
                case "service": name = value; break;
                case "definition": definition = value; break;
                case "home": home = value; break;
                case "user":
                    var parts = value.Split(' ');
                    user = parts[0];
                    created = parts.Length > 1 && parts[1] == "created";
                    break;
                case "dir": directories.Add(value); break;
                case "file": files.Add(value); break;
                default: return null;
            }
        }

        return platform is { } known && name is not null && definition is not null
               && user is not null && home is not null
            ? new ServiceManifest(known, name, definition, user, created, home, directories, files)
            : null;
    }
}

/// <summary>The service definitions, compiled in so a machine needs nothing beside gg.</summary>
internal static class ServiceTemplates
{
    /// <summary>A systemd unit for <c>gg runner up</c>.</summary>
    /// <remarks>
    /// The runbook's unit, less its <c>Environment=</c> line: the control
    /// plane is in the seed, and an <c>Environment=</c> line would beat the
    /// file, putting a fleet's configuration back on this one host.
    /// </remarks>
    public static string Systemd(string user) => $"""
        # Written by `gg service install`; `gg service uninstall` removes it.
        # What this machine is configured with is in the service user's own
        # config.json, not here: an Environment= line would beat that file.
        [Unit]
        Description=Good Grief runner
        After=network-online.target
        Wants=network-online.target

        [Service]
        User={user}
        ExecStart={ServiceInstaller.Binary} runner up

        # ALWAYS, NOT on-failure: the runner exits cleanly, while idle, to take a
        # changed offer on its next start. Under on-failure it would simply stop.
        Restart=always
        RestartSec=10

        # Nothing it starts may gain a privilege this user does not have.
        NoNewPrivileges=yes

        [Install]
        WantedBy=multi-user.target

        """;

    /// <summary>A launchd daemon for <c>gg runner up</c>.</summary>
    /// <remarks>
    /// <b>HOME and PATH are set</b>, because launchd hands a daemon neither the
    /// user's home to find its config.json in nor a PATH with
    /// <c>/usr/local/bin</c> on it, where git and an agent usually are.
    /// </remarks>
    public static string Launchd(string user, string home) => $"""
        <?xml version="1.0" encoding="UTF-8"?>
        <!DOCTYPE plist PUBLIC "-//Apple//DTD PLIST 1.0//EN" "http://www.apple.com/DTDs/PropertyList-1.0.dtd">
        <!-- Written by `gg service install`; `gg service uninstall` removes it. -->
        <plist version="1.0">
        <dict>
          <key>Label</key>
          <string>{ServiceInstaller.LaunchdLabel}</string>
          <key>UserName</key>
          <string>{Xml(user)}</string>
          <key>ProgramArguments</key>
          <array>
            <string>{ServiceInstaller.Binary}</string>
            <string>runner</string>
            <string>up</string>
          </array>
          <key>EnvironmentVariables</key>
          <dict>
            <key>HOME</key>
            <string>{Xml(home)}</string>
            <key>PATH</key>
            <string>/usr/local/bin:/opt/homebrew/bin:/usr/bin:/bin:/usr/sbin:/sbin</string>
          </dict>
          <key>RunAtLoad</key>
          <true/>
          <key>KeepAlive</key>
          <true/>
          <key>ThrottleInterval</key>
          <integer>10</integer>
          <key>StandardOutPath</key>
          <string>/var/log/{ServiceInstaller.SystemdName}.log</string>
          <key>StandardErrorPath</key>
          <string>/var/log/{ServiceInstaller.SystemdName}.log</string>
        </dict>
        </plist>

        """;

    private static string Xml(string value) => System.Security.SecurityElement.Escape(value);
}
