#!/bin/sh
# Installs gg on a Linux or macOS machine, and makes this machine's runner a
# service. Attached to every gg release, and attested with it.
#
#   curl -fsSL https://github.com/glyph-guild/gg/releases/download/v<version>/install.sh \
#     | sudo sh -s -- --version <version> --control-plane <url> --enroll
#
# Run it again with a newer --version and that is the update: the new release
# goes in beside the last, the link moves to it, and the service restarts onto
# it. gg never replaces its own binary; this script is what moves the bytes.
#
# Options:
#   --version <v>        the release to install. Required, always: this never
#                        resolves a newest one, so a machine runs what somebody chose.
#   --control-plane <u>  where the runner reports - and what makes this machine a
#                        runner at all. Without it (and without --enroll) this
#                        installs the binary and starts nothing: a laptop.
#   --enroll             read an enrollment token at a prompt on this terminal.
#   --enroll-file <path> read it from a file instead, for a machine with no terminal.
#   --user <name>        the service user, when not the platform's default.
#   --agent-binary <p>   where the agent this machine runs is, as an absolute path.
#                        gg declares it; it never installs it - this script does not
#                        install git either, and a profile names an agent by name
#                        because the binary is the machine's to have.
#   --as <name>          the command name to install, when another program is
#                        already called gg. Remembered, so an update needs no flag.
#                        A runner is always gg: its service runs /usr/local/bin/gg.
#   --root <dir>         install as if <dir> were /. For staging and for tests; the
#                        service always runs /usr/local/bin/gg.
#
# THE TOKEN IS NEVER AN ARGUMENT, here or to gg. It is handed to
# `gg service install --enroll` on stdin, because an argument sits in shell
# history and in ps, where anybody on the machine can read it.

set -eu

repo="glyph-guild/gg"
version=""
control_plane=""
user=""
agent_binary=""
enroll=""
enroll_file=""
root=""
command_name=""

refuse() {
  printf 'install.sh: %s\n' "$*" >&2
  exit 1
}

value() {
  # $1 is the option, $2 the count of what is left, $3 what follows it.
  [ "$2" -ge 2 ] || refuse "$1 needs a value after it."
  case "$3" in --*) refuse "$1 needs a value after it, not another option ($3)." ;; esac
}

while [ $# -gt 0 ]; do
  case "$1" in
    --version) value "$1" $# "${2-}"; version="$2"; shift 2 ;;
    --control-plane) value "$1" $# "${2-}"; control_plane="$2"; shift 2 ;;
    --user) value "$1" $# "${2-}"; user="$2"; shift 2 ;;
    --agent-binary) value "$1" $# "${2-}"; agent_binary="$2"; shift 2 ;;
    --enroll) enroll=1; shift ;;
    --enroll-file) value "$1" $# "${2-}"; enroll_file="$2"; shift 2 ;;
    --root) value "$1" $# "${2-}"; root="$2"; shift 2 ;;
    --as) value "$1" $# "${2-}"; command_name="$2"; shift 2 ;;
    *) refuse "'$1' is not an option. It takes --version, --control-plane, --user, --agent-binary, --enroll or --enroll-file, --as, and --root." ;;
  esac
done

# EVERYTHING THAT CAN BE REFUSED WITHOUT A DOWNLOAD IS REFUSED BEFORE ONE.

[ -n "$version" ] || refuse "--version is required, e.g. --version 0.38.0. A machine installs the release somebody chose; this never resolves a newest one."
version="${version#v}"
case "$version" in
  *[!0-9A-Za-z.+-]*) refuse "'$version' is not a version." ;;
esac

# Where `gg service install` records what it wrote: its presence is how this
# tells a first install from an update.
manifest="$root/etc/gg/service.manifest"

# WHETHER THIS MACHINE BECOMES A RUNNER, which is the one question this script
# asks and used to answer for you. A control plane or an enrollment token means
# a runner: `gg service install` makes the user, writes its configuration and
# starts the service. Neither means a laptop - the binary, and nothing running.
#
# IT USED TO REFUSE HERE without --control-plane, which made every scripted
# install a runner install and left a person who wanted the command line with
# the README's four manual steps.
runner=""
if [ -n "$control_plane" ] || [ -n "$enroll" ] || [ -n "$enroll_file" ] || [ -e "$manifest" ]; then
  runner=1
fi

if [ -n "$enroll" ] && [ -n "$enroll_file" ]; then
  refuse "--enroll reads the token at a prompt and --enroll-file reads it from a file; give one of them."
fi

if [ -n "$enroll_file" ] && [ ! -r "$enroll_file" ]; then
  refuse "cannot read $enroll_file."
fi

case "$(uname -s)" in
  Linux) os=linux ;;
  Darwin) os=osx ;;
  # WINDOWS IS RELEASED AND THIS IS NOT ITS INSTALLER. Somebody here is in
  # git-bash or WSL's borrowed shell, and being told their platform does not
  # exist would send them to a release page to do it by hand.
  MINGW* | MSYS* | CYGWIN* | Windows_NT)
    refuse "gg is released for Windows, and this script installs the Unix builds. In PowerShell: irm https://github.com/$repo/releases/download/v$version/install.ps1 | iex" ;;
  *) refuse "gg is released for Linux, macOS and Windows, and this is $(uname -s)." ;;
esac

case "$(uname -m)" in
  x86_64 | amd64) arch=x64 ;;
  arm64 | aarch64) arch=arm64 ;;
  *) refuse "this machine is $(uname -m), and gg is not released for it." ;;
esac

rid="$os-$arch"

# THE FOUR UNIX BUILDS A RELEASE CARRIES. win-x64 is the fifth and install.ps1
# is where it lands; AOT cannot cross-compile, so each of these is a runner of
# that platform in the publish workflow rather than a flag.
case "$rid" in
  linux-x64 | linux-arm64 | osx-arm64 | osx-x64) ;;
  *) refuse "gg is released for linux-x64, linux-arm64, osx-arm64 and osx-x64, and this machine is $rid." ;;
esac

asset="gg-$rid.tar.gz"
lib="$root/usr/local/lib/gg"
bin="$root/usr/local/bin"
service_binary="/usr/local/bin/gg"

# WHAT THE COMMAND IS CALLED. gg, unless another program already is - there
# is one, a git GUI - in which case the person chooses, and the choice is
# written down so the update path (running this again) needs no flag and
# cannot quietly grow a second link called gg beside the program it avoided.
record="$lib/command"
if [ -z "$command_name" ] && [ -r "$record" ]; then
  command_name="$(cat "$record")"
fi

command_ok() {
  case "$1" in
    "" | .* | *[!A-Za-z0-9._-]*) return 1 ;;
  esac
}

if [ -n "$command_name" ]; then
  command_ok "$command_name" \
    || refuse "'$command_name' is not a command name: letters, digits, dot, dash and underscore, e.g. --as goodgrief."
  if [ -n "$runner" ] && [ "$command_name" != gg ]; then
    refuse "--as renames the command a person types, and a runner's service runs $service_binary, so a runner is always installed as gg. Leave --as off here, or leave --control-plane off to make this a laptop."
  fi
fi
[ -n "$command_name" ] || command_name=gg

# WHETHER A gg ALREADY THERE IS OURS, in every shape this project tells people
# to install: a link into a `lib/gg` directory covers both the versioned one
# this script writes and the `dotnet tool` shim at /usr/local/lib/gg/gg, and
# the by-hand install from the README has no link at all - it is the binary
# itself with its two native libraries beside it, and that pairing is what
# says it is ours.
#
# A LINK IS READ WHETHER OR NOT IT RESOLVES. A dangling one at this path is
# still ours to move, and `[ -e ]` alone is false for it.
ours() {
  case "$(readlink "$1" 2>/dev/null || true)" in
    */lib/gg/*) return 0 ;;
  esac

  if [ ! -L "$1" ] && [ -f "$1" ]; then
    for beside in "$(dirname "$1")"/libporta_pty.*; do
      [ -e "$beside" ] && return 0
    done
  fi

  return 1
}

# ANOTHER gg EARLIER ON PATH IS SAID, NOT REFUSED - it is in no danger from
# this install and neither is the install; what is at stake is only which one
# the name resolves to. Held until after the install, where a person reads it
# beside the line that says where ours went.
shadowing=""

if [ "$command_name" = gg ]; then
  # AT THE LINK PATH, THOUGH, INSTALLING WOULD REPLACE IT. That is the
  # destructive one, and the only one worth stopping for.
  if { [ -e "$bin/gg" ] || [ -L "$bin/gg" ]; } && ! ours "$bin/gg"; then
    if [ -n "$runner" ]; then
      refuse "another program called gg is at $bin/gg, and a runner's service runs $service_binary. Remove or rename it first; nothing was installed."
    elif (: < /dev/tty) 2>/dev/null; then
      printf 'install.sh: another program called gg is at %s.\n' "$bin/gg" > /dev/tty
      printf "Install Good Grief's command under a different name [goodgrief]: " > /dev/tty
      IFS= read -r answer < /dev/tty || answer=""
      command_name="${answer:-goodgrief}"
      command_ok "$command_name" \
        || refuse "'$command_name' is not a command name: letters, digits, dot, dash and underscore."
    else
      refuse "another program called gg is at $bin/gg, and installing over it would delete it. Run this again with --as <name>, e.g. --as goodgrief, to install Good Grief's command under another name. Nothing was installed."
    fi
  fi

  if [ "$command_name" = gg ]; then
    found="$(command -v gg 2>/dev/null || true)"
    if [ -n "$found" ] && [ "$found" != "$service_binary" ] && [ "$found" != "$bin/gg" ] \
       && ! ours "$found"; then
      shadowing="$found"
    fi
  fi
fi

work="$(mktemp -d)"
trap 'rm -rf "$work"' EXIT

curl -fsSL -o "$work/$asset" "https://github.com/$repo/releases/download/v$version/$asset" \
  || refuse "could not download $asset for v$version. Is v$version a release of $repo?"

sha256() {
  if command -v sha256sum >/dev/null 2>&1; then
    sha256sum "$1" | cut -d ' ' -f 1
  else
    shasum -a 256 "$1" | cut -d ' ' -f 1
  fi
}

# CHECKED BEFORE ANYTHING IS EXTRACTED. Whoever can replace an asset on a
# release page can replace a checksum beside it, so the proof is the build's
# attestation - signed against the workflow run that built these bytes, and
# held where the release page cannot rewrite it.
#
# WITH gh, the attestation's signature is verified. WITHOUT it - the fresh
# machine this is for - the attestation is looked up by the digest of the bytes
# that arrived: bytes swapped on the release page are named by no attestation
# of this repository, and are refused. That second check trusts the forge's API
# over TLS rather than checking a signature itself; install gh for the stronger
# one.
digest="$(sha256 "$work/$asset")"

if command -v gh >/dev/null 2>&1 && gh auth status >/dev/null 2>&1; then
  gh attestation verify "$work/$asset" --repo "$repo" >/dev/null 2>&1 \
    || refuse "$asset (sha256:$digest) does not verify against $repo's build attestation. Nothing was installed."
  checked="its attestation's signature verified by gh"
else
  curl -fsSL -o "$work/attestations.json" \
    "https://api.github.com/repos/$repo/attestations/sha256:$digest" \
    || refuse "no attestation of $repo names sha256:$digest, the digest of the $asset that arrived. Nothing was installed."
  grep -q '"bundle"' "$work/attestations.json" \
    || refuse "the attestation lookup for sha256:$digest named no bundle. Nothing was installed."
  checked="its digest named by an attestation of $repo (install gh to check the signature too)"
fi

# BY RENAME, NEVER BY COPY. Extracted beside where it goes, then renamed into
# place, so nothing ever runs half-written bytes - and macOS, which kills a
# binary whose bytes change under a signature it already checked, never sees a
# change at all.
#
# --no-same-owner because, run as root, tar would otherwise give the files the
# owner the archive recorded: the build machine's user id, which here may be
# anybody, including the service user. A gg its runner can write is a runner
# that can replace what the OS starts.
target="$lib/$version"
mkdir -p "$lib" "$bin"

if [ -d "$target" ]; then
  installed="v$version was already at $target, and is left as it is"
else
  staging="$lib/.incoming-$version-$$"
  rm -rf "$staging"
  mkdir "$staging"
  tar -xzf "$work/$asset" --no-same-owner -C "$staging"
  chmod -R go-w "$staging"

  if [ ! -x "$staging/gg" ]; then
    rm -rf "$staging"
    refuse "$asset holds no gg. Nothing was installed."
  fi

  mv "$staging" "$target"
  installed="v$version installed at $target"
fi

# THE LINK MOVES BY RENAME TOO: a new link beside the old, renamed over it, so
# /usr/local/bin/gg is never absent for a moment. It points at where the
# release is on the machine, not at wherever --root staged it.
link="$bin/.gg.incoming.$$"
rm -f "$link"
ln -s "/usr/local/lib/gg/$version/gg" "$link"
mv -f "$link" "$bin/$command_name"
printf '%s\n' "$command_name" > "$record"

printf 'install.sh: %s, %s; /usr/local/bin/%s points at it.\n' "$installed" "$checked" "$command_name"

if [ -n "$shadowing" ]; then
  printf 'install.sh: another program called gg is at %s, earlier on your PATH, so `gg` still runs that one. Install this one under a name of its own with --as <name>, e.g. --as goodgrief, or put /usr/local/bin first.\n' "$shadowing"
fi

# AND WHETHER A SHELL WILL FIND IT. "gg: command not found" after a successful
# install is what a person meets on a machine whose PATH lacks /usr/local/bin -
# a stripped container, an rc file that rewrites PATH, a distro with no
# /etc/environment. A Mac's path_helper and most distros put it there already,
# so usually this says so and writes nothing; when it is missing, the line goes
# in the startup file of the shell the PERSON is in.
#
# THE PERSON'S SHELL, NOT THIS ONE. This script is /bin/sh, and under sudo $HOME
# and $SHELL are root's, so the invoking user's are read from the passwd
# database - and a file this creates in their home is made theirs, not root's.
who="${SUDO_USER-}"
home="${HOME-}"
shell="${SHELL-}"
if [ -n "$who" ]; then
  entry="$(getent passwd "$who" 2>/dev/null || true)"
  if [ -n "$entry" ]; then
    home="$(printf '%s' "$entry" | cut -d: -f6)"
    shell="$(printf '%s' "$entry" | cut -d: -f7)"
  elif command -v dscl >/dev/null 2>&1; then
    home="$(dscl . -read "/Users/$who" NFSHomeDirectory 2>/dev/null | cut -d' ' -f2-)"
    shell="$(dscl . -read "/Users/$who" UserShell 2>/dev/null | cut -d' ' -f2-)"
  fi
fi

path_line='export PATH="/usr/local/bin:$PATH"'
marker="# added by gg's installer, so the shell finds the command"

case ":$PATH:" in
  *":/usr/local/bin:"*)
    printf 'install.sh: /usr/local/bin is on PATH.\n' ;;
  *)
    # ONE FILE PER SHELL, THE ONE IT READS FOR A NEW TERMINAL. bash differs by
    # platform: Terminal on a Mac opens login shells, which read .bash_profile
    # and not .bashrc. fish does not read POSIX `export` at all - a line that is
    # a syntax error in its startup file breaks every new shell, worse than the
    # missing PATH - so it gets its own spelling in the directory fish reads
    # for exactly this, and fish_add_path is idempotent on its own.
    startup=""
    case "${shell##*/}" in
      zsh) startup="$home/.zshrc" ;;
      bash) if [ "$os" = osx ]; then startup="$home/.bash_profile"; else startup="$home/.bashrc"; fi ;;
      fish) startup="$home/.config/fish/conf.d/gg.fish" ;;
      sh | dash | ash | ksh) startup="$home/.profile" ;;
    esac

    if [ -z "$home" ] || [ -z "$startup" ]; then
      # GUESSING A FILE IS HOW SOMEBODY'S STARTUP GETS A LINE FOR A SHELL THEY DO
      # NOT USE. The line itself is the one thing this can still hand over.
      printf 'install.sh: /usr/local/bin is not on PATH, and this shell is not one this script knows. Add this to your shell'\''s startup file:\n  %s\n' "$path_line"
    elif [ -f "$startup" ] && grep -qF -- "$marker" "$startup" 2>/dev/null; then
      printf 'install.sh: /usr/local/bin was already added to %s.\n' "$startup"
    else
      mkdir -p "$(dirname "$startup")"
      created=""
      [ -e "$startup" ] || created=1
      if [ "${shell##*/}" = fish ]; then
        printf '%s\nfish_add_path -g /usr/local/bin\n' "$marker" >> "$startup"
      else
        printf '\n%s\n%s\n' "$marker" "$path_line" >> "$startup"
      fi
      [ -z "$who" ] || [ -z "$created" ] || chown "$who" "$startup" "$(dirname "$startup")" 2>/dev/null || true
      printf 'install.sh: /usr/local/bin was not on PATH; added it to %s for %s. Open a new terminal, or run: %s\n' "$startup" "${shell##*/}" "$path_line"
    fi ;;
esac

# WHERE AN UPDATE COMES FROM, WRITTEN DOWN BY THE THING THAT KNOWS. `gg update`
# refuses to pick an installer for a machine - "unset means gg update says so
# rather than choosing one" - because running an installer from a URL gg chose
# is not the machine's choice. This IS the machine's choice: somebody ran this
# script, so the script records where it came from and the refusal never fires
# on a machine installed the ordinary way.
#
# THE PERSON'S CONFIG, NOT ROOT'S, for the PATH block's reason above: the file
# lives under the invoking user's home and gg reads the one belonging to
# whoever runs it. Under sudo that is not $HOME. A machine with no invoking
# user - cloud-init, a container - gets nothing here and says so, which is the
# honest outcome: there is nobody whose config to write.
installer_url="https://github.com/$repo/releases/latest/download/install.sh"

if [ -n "$home" ] && [ -d "$home" ]; then
  config_dir="${root}$home/.config/good-grief"
  mkdir -p "$config_dir"
  "$target/gg" config set GG_INSTALLER "$installer_url" >/dev/null 2>&1 \
    && installer_written=1 || installer_written=""

  if [ -n "$installer_written" ]; then
    [ -z "$who" ] || chown -R "$who" "$config_dir" 2>/dev/null || true
    printf 'install.sh: `%s update` will fetch its installer from the latest release.\n' \
      "$command_name"
  fi
else
  printf 'install.sh: no invoking user, so nothing recorded where updates come from - set\n'
  printf '  GG_INSTALLER to an installer this machine chose before `%s update` will run.\n' \
    "$command_name"
fi

# AND WHAT A LAPTOP IS: the binary, and nothing running. Said rather than
# silent, because "did that make me a runner?" is the question somebody has
# after typing one line - and because the default control plane is localhost,
# so a laptop nobody pointed anywhere points at nothing.
if [ -z "$runner" ]; then
  printf 'install.sh: this machine is not a runner - nothing was started and no user was made.\n'
  printf '  %s config set control-plane <url>   # where your tenant is\n' "$command_name"
  printf '  %s login                            # then sign in\n' "$command_name"
  printf 'To make it a runner instead, run this again with --control-plane <url>.\n'
  exit 0
fi

# AND THE SERVICE, by the gg just installed - not through the link, which under
# --root points somewhere this run did not write.
gg="$target/gg"
set -- service install
[ -z "$control_plane" ] || set -- "$@" --control-plane "$control_plane"
[ -z "$user" ] || set -- "$@" --user "$user"
[ -z "$agent_binary" ] || set -- "$@" --agent-binary "$agent_binary"

if [ -n "$enroll_file" ]; then
  "$gg" "$@" --enroll < "$enroll_file"
elif [ -n "$enroll" ]; then
  (: < /dev/tty) 2>/dev/null \
    || refuse "--enroll asks for the token on this terminal, and there is none. Use --enroll-file <path>."
  printf 'Enrollment token (not echoed): ' > /dev/tty
  stty -echo < /dev/tty
  IFS= read -r token < /dev/tty || token=""
  stty echo < /dev/tty
  printf '\n' > /dev/tty
  printf '%s\n' "$token" | "$gg" "$@" --enroll
else
  "$gg" "$@" < /dev/null
fi
