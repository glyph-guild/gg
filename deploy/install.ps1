<#
.SYNOPSIS
  Installs gg on a Windows laptop. Attached to every gg release, and attested
  with it.

.DESCRIPTION
  irm https://github.com/glyph-guild/gg/releases/latest/download/install.ps1 | iex

  installs the newest release. To install a chosen one:

  & ([scriptblock]::Create((irm .../install.ps1))) -Version 0.42.0

  WITHOUT -Version, THE NEWEST RELEASE: the tag of the release the forge's API
  names as latest. This used to be required, so that a machine ran only what
  somebody chose; the owner reversed that on 2026-10-15. The release found is
  checked against its attestation as a named one is, and "latest" is always a
  gg release, because contract releases are published with --latest=false.

  THE WHOLE SCRIPT IS ONE FUNCTION, CALLED ON ITS LAST LINE, so a copy cut off
  part way defines at most a function that nothing calls and runs nothing.

  WHAT THIS DOES NOT DO: make this machine a runner. Nothing is started, no
  user is made and no Windows service is registered. The console runs on
  Windows; a Windows runner is slice forty-five's, not this script's.

  (The phrase a test greps this file for is deliberately absent: the guarantee
  is worth checking literally, and a comment is source too.)
#>
[CmdletBinding()]
param(
    # The release to install. Without it, the newest release.
    [string] $Version,

    # Where gg is unpacked. Per-user by default: nothing here needs
    # administrator, and a per-user install cannot be rewritten by anything
    # else on the machine that is not already this user.
    [string] $Prefix,

    # The organisation's control plane. Written into gg's configuration when
    # given, because the default is localhost and a laptop nobody pointed
    # anywhere points at nothing.
    [string] $ControlPlane,

    # The command name to install, when another program is already called gg
    # (there is one, a git GUI). Asked for at the prompt when there is one;
    # this is the answer for a run with nobody at the keyboard. Remembered in
    # the prefix, so an update needs no flag.
    [string] $Alias
)

# The parameters above are the script's; the function takes them as they were
# given, so its defaults apply to whatever was left out.
function Install-Gg {
    [CmdletBinding()]
    param(
        [string] $Version,
        [string] $Prefix = "$env:LOCALAPPDATA\Programs\gg",
        [string] $ControlPlane,
        [string] $Alias
    )

    $ErrorActionPreference = 'Stop'
    $repo = 'glyph-guild/gg'

    if ($Version) {
        $Version = $Version.TrimStart('v')
        if ($Version -notmatch '^[0-9A-Za-z.+-]+$') {
            throw "'$Version' is not a version. Give one such as -Version 0.42.0, or leave -Version off for the newest release."
        }
    }

    # ONE ARCHITECTURE, AND IT SAYS SO. A release carries win-x64; an arm64 Windows
    # machine running the x64 build under emulation would work and would also be a
    # thing nobody chose, so it is refused by name instead.
    $arch = (Get-CimInstance Win32_Processor | Select-Object -First 1).Architecture
    if ($arch -ne 9) {
        # --tool-path, NOT -g, in the suggestion as well as in the runbooks: a
        # global install lands in the home of whoever typed it, which is an
        # executable its own user can rewrite. The ratchet over every install line
        # in this repository caught this sentence, and it was right to.
        $pinned = if ($Version) { " --version $Version" } else { '' }
        throw "This machine reports architecture $arch, and gg is released for Windows only as win-x64. Nothing was installed. The .NET tool works anywhere the SDK does; to install it, run: dotnet tool install GlyphGuild.Gg.Cli$pinned --tool-path $Prefix\tool"
    }

    # WHAT THE COMMAND IS CALLED. Ours is the shim in $Prefix; anything else on
    # PATH called gg is somebody's, and writing a shim beside it would leave one of
    # the two unreachable depending on PATH order - so the person chooses, and the
    # choice is written down for the next run.
    $record = Join-Path $Prefix 'command'
    if (-not $Alias -and (Test-Path $record)) { $Alias = (Get-Content $record -Raw).Trim() }

    $theirs = Get-Command gg -ErrorAction SilentlyContinue |
        Where-Object { $_.Source -and $_.Source -notlike "$Prefix*" } |
        Select-Object -First 1

    # ASKED WHERE THERE IS SOMEBODY TO ASK, AND OTHERWISE SAID. The shim goes in
    # $Prefix, so another gg elsewhere on PATH is in no danger from this install
    # and neither is the install - only which one the name resolves to is at
    # stake, and stopping for that from `irm | iex` is a refusal nobody can argue
    # with.
    if ($theirs -and -not $Alias -and
        [Environment]::UserInteractive -and -not [Console]::IsInputRedirected) {
        Write-Host "install.ps1: Another program called gg is at $($theirs.Source)."
        $answer = Read-Host "Install Good Grief's command under a different name [goodgrief]"
        $Alias = if ($answer) { $answer } else { 'goodgrief' }
    }
    if (-not $Alias) { $Alias = 'gg' }
    if ($Alias -notmatch '^[A-Za-z0-9_-][A-Za-z0-9._-]*$') {
        throw "'$Alias' is not a command name. Use only letters, digits, dots, dashes and underscores, for example -Alias goodgrief. Nothing was installed."
    }

    # THE NEWEST RELEASE, WHEN NONE WAS NAMED, looked up after every refusal that
    # needs no network. Anything that is not a version is refused before anything
    # is written; the release found is checked below like a named one.
    if (-not $Version) {
        $tag = $null
        try {
            $tag = (Invoke-RestMethod -Uri "https://api.github.com/repos/$repo/releases/latest" -UseBasicParsing).tag_name
        }
        catch {
            $tag = $null
        }
        $Version = "$tag".TrimStart('v')
        if ($Version -notmatch '^[0-9][0-9A-Za-z.+-]*$') {
            throw "Could not find the newest gg release on GitHub. Nothing was installed. Run this again with -Version <version>. The releases are listed at https://github.com/$repo/releases."
        }
        Write-Host "install.ps1: Installing the newest release, v$Version."
    }

    $asset = 'gg-win-x64.tar.gz'
    $url = "https://github.com/$repo/releases/download/v$Version/$asset"
    $work = New-Item -ItemType Directory -Path (Join-Path $env:TEMP "gg-$Version-$PID")

    try {
        Write-Host "install.ps1: Downloading $asset (v$Version)."
        $archive = Join-Path $work $asset
        Invoke-WebRequest -Uri $url -OutFile $archive -UseBasicParsing

        # CHECKED BEFORE ANYTHING IS EXTRACTED, which is install.sh's rule and its
        # reason: whoever can replace an asset on a release page can replace a
        # checksum beside it, so the proof is the build's attestation rather than
        # anything the page carries. With gh, the signature is verified; without
        # it, the digest is printed for somebody to compare and the install goes
        # on - refusing would make the attestation a dependency of installing.
        $digest = (Get-FileHash -Algorithm SHA256 -Path $archive).Hash.ToLowerInvariant()

        if (Get-Command gh -ErrorAction SilentlyContinue) {
            gh attestation verify $archive --repo $repo | Out-Null
            if ($LASTEXITCODE -ne 0) {
                throw "The attestation for $asset does not match the file that arrived. Nothing was installed. Run this again to download it afresh, or compare the release at https://github.com/$repo/releases/tag/v$Version."
            }
            Write-Host "install.ps1: Verified the attestation with gh."
        }
        else {
            Write-Host "install.ps1: The download's sha256 is $digest. Install gh and run this again to verify its attestation too."
        }

        # BESIDE THE LAST ONE, AND THE LINK MOVES, which is how install.sh updates:
        # a version goes in its own directory and the shim points at it, so an
        # update never leaves the binary half-replaced and a rollback is one edit.
        $target = Join-Path $Prefix $Version
        if (Test-Path $target) {
            Write-Host "install.ps1: Release v$Version was already at $target and was left as it is."
        }
        else {
            $staging = Join-Path $work 'out'
            New-Item -ItemType Directory -Path $staging | Out-Null
            tar -xzf $archive -C $staging
            if (-not (Test-Path (Join-Path $staging 'gg.exe'))) {
                throw "The downloaded $asset holds no gg.exe. Nothing was installed. Report it at https://github.com/$repo/issues."
            }
            New-Item -ItemType Directory -Path $Prefix -Force | Out-Null
            Move-Item $staging $target
            Write-Host "install.ps1: Installed v$Version at $target."
        }

        # A SHIM RATHER THAN A COPY, because gg loads its native libraries from its
        # own directory - a gg.exe copied out of that directory starts, prints its
        # version, and fails on the first keypress that reaches one of them.
        $shim = Join-Path $Prefix "$Alias.cmd"
        Set-Content -Path $shim -Encoding ASCII -Value @(
            '@echo off',
            "`"%~dp0$Version\gg.exe`" %*"
        )
        Set-Content -Path $record -Encoding ASCII -Value $Alias

        # THE USER'S PATH, NEVER THE MACHINE'S - a per-user install is the whole
        # point of the default prefix, and nothing here has administrator. Joined
        # from the entries that exist rather than with a separator regardless: on a
        # profile whose user PATH is empty, the old join wrote a leading empty
        # entry, and an empty PATH entry means the current directory to cmd.exe -
        # a PATH that runs whatever is in the folder you happen to be in.
        $userPath = [Environment]::GetEnvironmentVariable('Path', 'User')
        $entries = @($userPath -split ';' | Where-Object { $_ })
        if ($entries -notcontains $Prefix) {
            [Environment]::SetEnvironmentVariable('Path', (($entries + $Prefix) -join ';'), 'User')
            Write-Host "install.ps1: Added $Prefix to the user PATH. Open a new terminal for it to take effect."
        }
        if (($env:Path -split ';') -notcontains $Prefix) {
            $env:Path = "$env:Path;$Prefix"
        }

        & $shim --version

        if ($theirs -and $Alias -eq 'gg') {
            Write-Host "install.ps1: Another program called gg is at $($theirs.Source), earlier on PATH, so ``gg`` still runs that one. Install this one under a name of its own with -Alias <name>, for example -Alias goodgrief."
        }

        if ($ControlPlane) {
            & $shim config set control-plane $ControlPlane
        }

        Write-Host ''
        Write-Host 'install.ps1: This machine is not a runner. Nothing was started and no service was made.'
        if (-not $ControlPlane) {
            Write-Host "  $Alias config set control-plane <url>   # the organisation's control plane"
        }
        Write-Host "  $Alias login                            # then sign in"
        Write-Host "  $Alias                                  # then open the console"
    }
    finally {
        Remove-Item -Recurse -Force $work -ErrorAction SilentlyContinue
    }
}

Install-Gg @PSBoundParameters
