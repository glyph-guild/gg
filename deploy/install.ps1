<#
.SYNOPSIS
  Installs gg on a Windows laptop. Attached to every gg release, and attested
  with it.

.DESCRIPTION
  irm https://github.com/glyph-guild/gg/releases/download/v<version>/install.ps1 | iex

  or, to choose the version rather than take this script's own:

  & ([scriptblock]::Create((irm .../install.ps1))) -Version 0.42.0

  WHAT THIS DOES NOT DO: make this machine a runner. Nothing is started, no
  user is made and no Windows service is registered - the terminal UI wants
  SetConsoleMode and ConPTY, which are not written, so gg on Windows is the
  command line and it says so when it opens $EDITOR instead of a console.
  A Windows runner is slice forty-five's, not this script's.

  (The phrase a test greps this file for is deliberately absent: the guarantee
  is worth checking literally, and a comment is source too.)
#>
[CmdletBinding()]
param(
    # The release to install. Required, always: this never resolves a newest
    # one, so a machine runs what somebody chose.
    [Parameter(Mandatory = $true)]
    [string] $Version,

    # Where gg is unpacked. Per-user by default: nothing here needs
    # administrator, and a per-user install cannot be rewritten by anything
    # else on the machine that is not already you.
    [string] $Prefix = "$env:LOCALAPPDATA\Programs\gg",

    # The tenant's control plane. Written into gg's configuration when given,
    # because the default is localhost and a laptop nobody pointed anywhere
    # points at nothing.
    [string] $ControlPlane
)

$ErrorActionPreference = 'Stop'
$repo = 'glyph-guild/gg'
$Version = $Version.TrimStart('v')

if ($Version -notmatch '^[0-9A-Za-z.+-]+$') { throw "'$Version' is not a version." }

# ONE ARCHITECTURE, AND IT SAYS SO. A release carries win-x64; an arm64 Windows
# machine running the x64 build under emulation would work and would also be a
# thing nobody chose, so it is refused by name instead.
$arch = (Get-CimInstance Win32_Processor | Select-Object -First 1).Architecture
if ($arch -ne 9) {
    # --tool-path, NOT -g, in the suggestion as well as in the runbooks: a
    # global install lands in the home of whoever typed it, which is an
    # executable its own user can rewrite. The ratchet over every install line
    # in this repository caught this sentence, and it was right to.
    throw "gg is released for win-x64, and this machine reports architecture $arch. The .NET tool works anywhere the SDK does: dotnet tool install GlyphGuild.Gg.Cli --version $Version --tool-path $Prefix\tool"
}

$asset = 'gg-win-x64.tar.gz'
$url = "https://github.com/$repo/releases/download/v$Version/$asset"
$work = New-Item -ItemType Directory -Path (Join-Path $env:TEMP "gg-$Version-$PID")

try {
    Write-Host "install.ps1: downloading $asset"
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
            throw "the attestation for $asset does not vouch for these bytes. Nothing was installed."
        }
        Write-Host "install.ps1: attestation verified by gh"
    }
    else {
        Write-Host "install.ps1: sha256 $digest (install gh to verify its attestation too)"
    }

    # BESIDE THE LAST ONE, AND THE LINK MOVES, which is how install.sh updates:
    # a version goes in its own directory and `gg.cmd` points at it, so an
    # update never leaves the binary half-replaced and a rollback is one edit.
    $target = Join-Path $Prefix $Version
    if (Test-Path $target) {
        Write-Host "install.ps1: v$Version was already at $target, and is left as it is"
    }
    else {
        $staging = Join-Path $work 'out'
        New-Item -ItemType Directory -Path $staging | Out-Null
        tar -xzf $archive -C $staging
        if (-not (Test-Path (Join-Path $staging 'gg.exe'))) {
            throw "$asset holds no gg.exe. Nothing was installed."
        }
        New-Item -ItemType Directory -Path $Prefix -Force | Out-Null
        Move-Item $staging $target
        Write-Host "install.ps1: v$Version installed at $target"
    }

    # A SHIM RATHER THAN A COPY, because gg loads its native libraries from its
    # own directory - a gg.exe copied out of that directory starts, prints its
    # version, and fails on the first keypress that reaches one of them.
    $shim = Join-Path $Prefix 'gg.cmd'
    Set-Content -Path $shim -Encoding ASCII -Value @(
        '@echo off',
        "`"%~dp0$Version\gg.exe`" %*"
    )

    $userPath = [Environment]::GetEnvironmentVariable('Path', 'User')
    if (($userPath -split ';') -notcontains $Prefix) {
        [Environment]::SetEnvironmentVariable('Path', "$userPath;$Prefix", 'User')
        Write-Host "install.ps1: added $Prefix to your PATH - open a new terminal for it"
    }
    $env:Path = "$env:Path;$Prefix"

    & $shim --version

    if ($ControlPlane) {
        & $shim config set control-plane $ControlPlane
    }

    Write-Host ''
    Write-Host 'install.ps1: this machine is not a runner - nothing was started and no service was made.'
    if (-not $ControlPlane) {
        Write-Host '  gg config set control-plane <url>   # where your tenant is'
    }
    Write-Host '  gg login                            # then sign in'
    Write-Host 'gg on Windows is the command line: the console UI is not written for it yet, so it opens your editor and says so.'
}
finally {
    Remove-Item -Recurse -Force $work -ErrorAction SilentlyContinue
}
