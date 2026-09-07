#!/usr/bin/env bash
#
# Fetches the patched SIPSorcery build this console's WebRTC path needs.
#
# WHY A FORK AT ALL. Upstream SIPSorcery does not work under Native AOT: the
# SCTP state cookie is carried as reflection-serialised JSON, the trimmer
# removes the members, the COOKIE ECHO arrives empty, and the association stalls
# in CookieEchoed - so data channels never open. gg publishes Native AOT, so
# that is the difference between having WebRTC and not.
#
#   fix:   https://github.com/sipsorcery-org/sipsorcery/pull/1817
#   issue: https://github.com/sipsorcery-org/sipsorcery/issues/1816
#
# THIS EXISTS TO BE DELETED. When a released SIPSorcery contains the fix, pin
# that version, delete this script and its nuget.config source, and the
# PackageReference stops being special. TheForkIsTemporaryTests is what keeps
# that from being forgotten.
#
# The version is a PRERELEASE on purpose. SemVer sorts 10.0.16-gg.1 BELOW
# 10.0.16, so this build can never win a version range against a real release -
# the day upstream ships the fix, an unpinned resolve prefers theirs.
#
# A release asset rather than a package feed, for the reason fetch-contracts.sh
# gives in good-grief: a public repo's release assets download with no
# authentication, so nothing here needs a credential.
#
set -euo pipefail

root="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"

# Directory.Packages.props is the single source of truth for the version, read
# rather than duplicated so the pin and the download cannot drift.
version="$(sed -n 's/.*"SIPSorcery" Version="\([^"]*\)".*/\1/p' \
  "$root/Directory.Packages.props")"

if [ -z "$version" ]; then
  echo "error: no SIPSorcery version pinned in Directory.Packages.props" >&2
  exit 1
fi

# AN UPSTREAM PIN NEEDS NOTHING FETCHING, which is what makes deleting the fork
# a one-line change rather than a script edit.
case "$version" in
  *-gg.*) ;;
  *)
    echo "SIPSorcery $version is an upstream release; nothing to fetch"
    exit 0
    ;;
esac

package="SIPSorcery.$version.nupkg"
destination="$root/packages"

if [ -f "$destination/$package" ]; then
  echo "$package already present"
else
  mkdir -p "$destination"

  url="https://github.com/kdeenanauth/sipsorcery/releases/download/v$version/$package"
  echo "fetching $url"

  # A temporary name and a move, so an interrupted download cannot leave a
  # truncated .nupkg that fails restore with a corrupt-archive error.
  curl --fail --silent --show-error --location --retry 3 \
    --output "$destination/$package.partial" "$url"
  mv "$destination/$package.partial" "$destination/$package"

  echo "fetched $package"
fi

# WHAT RESTORE READS IS THE CACHE, NOT THIS DIRECTORY. good-grief learned this
# the expensive way: a locally packed branch occupied the pinned version in
# ~/.nuget/packages, NuGet preferred it over the downloaded asset, and the build
# compiled against an assembly nobody had published. Every version number
# agreed; the assemblies differed by 512 bytes.
#
# The same hazard applies here and is arguably likelier: anyone who runs
# `dotnet pack` on the fork produces this exact version.
sha256() {
  if command -v shasum >/dev/null 2>&1; then
    shasum -a 256 | cut -d' ' -f1
  else
    sha256sum | cut -d' ' -f1
  fi
}

# NuGet lowercases both the id and the version in the cache path.
lowered="$(printf '%s' "$version" | tr '[:upper:]' '[:lower:]')"
cached="${NUGET_PACKAGES:-$HOME/.nuget/packages}/sipsorcery/$lowered"

if [ -d "$cached" ]; then
  # net10.0 specifically: SIPSorcery ships nine target frameworks and gg builds
  # against one, so comparing the whole package would compare assemblies this
  # repo never binds to.
  entry="lib/net10.0/SIPSorcery.dll"

  # LISTED ONCE INTO A VARIABLE, not piped into `grep -q`. That pattern is
  # racy under `set -o pipefail`: grep exits on the first match, unzip takes
  # SIGPIPE, and the pipeline reports a failure that depends on which process
  # got there first. It passed in isolation and failed in the script.
  listing="$(unzip -Z1 "$destination/$package")"

  if ! printf '%s\n' "$listing" | grep -qx "$entry"; then
    echo "error: $package has no $entry - is it the package we think?" >&2
    exit 1
  fi

  released="$(unzip -p "$destination/$package" "$entry" | sha256)"
  local_dll="$cached/lib/net10.0/SIPSorcery.dll"

  if [ -f "$local_dll" ] && [ "$(sha256 < "$local_dll")" != "$released" ]; then
    echo "error: the cached SIPSorcery $version is not the one the fork published." >&2
    echo "  cached:   $local_dll" >&2
    echo "  released: $destination/$package ($entry)" >&2
    echo "" >&2
    echo "NuGet prefers the cache over ./packages, so a build here would use that" >&2
    echo "assembly and not the release. It is usually a local pack of the fork." >&2
    echo "Remove it and run this again:" >&2
    echo "" >&2
    echo "  rm -rf '$cached'" >&2
    exit 1
  fi

  echo "the cached $version is the one the fork published"
fi
