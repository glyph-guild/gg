# packaging

One install, one executable, one version.

## What a release ships

**A self-contained Native AOT binary per platform**, `gg-<rid>.tar.gz`, for
`linux-x64` and `osx-arm64`. These need nothing on the machine — no runtime, no
SDK, no compiler — which is why they are what a pool host installs. The console
and the runner role live in the same binary; `gg runner up` spawns
`gg runner serve` as a separate OS process.

**A .NET tool package**, `GlyphGuild.Gg.Cli`, for anyone who already has the
SDK. The command it installs is `gg`; the package id differs only because `gg`
belongs to somebody else on nuget.org.

Both are built and attached by `.github/workflows/publish-cli.yml`.

## Where it is published, and what that costs

**Release assets, not nuget.org.** No account exists for this org, and the
release is the same channel `GlyphGuild.Gg.Contracts` already uses.

The consequence is worth stating rather than leaving to be discovered:
`dotnet tool install --add-source` wants a **feed**, not a file URL, so
installing the tool means downloading the `.nupkg` first and pointing
`--add-source` at the directory holding it. The native binary is one `curl`
and an `install`. **The binaries are the easy path.**

## The version

The single source of truth is `VersionPrefix` in `Directory.Build.props`, and
every project inherits it — including `Gg.Client`, which is the assembly
`gg --version` actually reads.

**Bumping that number is what causes a release.** The workflow skips when the
tag already exists, because a published version is immutable and re-uploading
its assets would churn the timestamp on files people have pinned. The failure
mode that creates: forgetting to bump means a release that silently does not
happen.

`GlyphGuild.Gg.Contracts` is deliberately **not** on this scheme — it declares
its own `<Version>` and moves only when the wire surface does, so that a
consumer seeing the number change learns something. See the comment at the top
of `Gg.Contracts/Gg.Contracts.csproj`.
