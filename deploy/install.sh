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
#   --control-plane <u>  where the runner reports. A first install needs it.
#   --enroll             read an enrollment token at a prompt on this terminal.
#   --enroll-file <path> read it from a file instead, for a machine with no terminal.
#   --user <name>        the service user, when not the platform's default.
#   --agent-binary <p>   where the agent this machine runs is, as an absolute path.
#                        gg declares it; it never installs it - this script does not
#                        install git either, and a profile names an agent by name
#                        because the binary is the machine's to have.
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
    *) refuse "'$1' is not an option. It takes --version, --control-plane, --user, --agent-binary, --enroll or --enroll-file, and --root." ;;
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

if [ ! -e "$manifest" ] && [ -z "$control_plane" ]; then
  refuse "a first install needs --control-plane <url>: nothing else can tell this machine where its control plane is."
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
  *) refuse "gg is released for Linux and macOS, and this is $(uname -s)." ;;
esac

case "$(uname -m)" in
  x86_64 | amd64) arch=x64 ;;
  arm64 | aarch64) arch=arm64 ;;
  *) refuse "this machine is $(uname -m), and gg is not released for it." ;;
esac

rid="$os-$arch"

case "$rid" in
  linux-x64 | osx-arm64) ;;
  *) refuse "gg is released for linux-x64 and osx-arm64, and this machine is $rid." ;;
esac

asset="gg-$rid.tar.gz"
lib="$root/usr/local/lib/gg"
bin="$root/usr/local/bin"
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
mv -f "$link" "$bin/gg"

printf 'install.sh: %s, %s; /usr/local/bin/gg points at it.\n' "$installed" "$checked"

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
