# gg

Good Grief developer tooling. Runs in **your** environment: a TUI console for
driving AI-agent work, and a runner that leases work, holds your credentials,
clones repos, extracts facts, and reports them. The control plane it talks to
never sees your code or credentials.

One language, one AOT binary:

- `gg` — the console (Terminal.Gui v2)
- `gg runner up` — spawns the runner role as a **separate child process**
  (`gg runner serve`). Deliberate: the console acts as the developer, the
  runner is treated as hostile by the rest of the design, and the OS is what
  keeps them apart.

`Gg.Contracts` is the wire protocol, published as `GlyphGuild.Gg.Contracts` on
each [release](https://github.com/glyph-guild/gg/releases). It declares its own
version, which moves only when the wire surface does — so a consumer seeing the
number change learns something. It carries one third-party package reference and
no more (it is the artifact a customer audits), and every wire type carries a
`[PinnedId]` and appears in the `Vocabulary` manifest. All three rules are
enforced by tests.

## Install

**No .NET required** — a pool host, a CI runner, a laptop:

```sh
curl -fsSL https://github.com/glyph-guild/gg/releases/latest/download/gg-linux-x64.tar.gz | tar xz
sudo install -m 0755 gg /usr/local/bin/gg
gg --version
```

Swap `linux-x64` for `osx-arm64` on an Apple-silicon Mac.

**As a .NET tool**, if you already have the SDK. `--add-source` takes a
directory rather than a URL, so the package is downloaded first:

```sh
curl -fsSL -O https://github.com/glyph-guild/gg/releases/latest/download/GlyphGuild.Gg.Cli.0.1.0.nupkg
dotnet tool install -g --add-source . GlyphGuild.Gg.Cli
```

Either way the command is `gg`. The package id is not `gg` because that one is
taken on nuget.org; the command is unaffected.

## Build

```sh
dotnet build
dotnet test                                                # TUnit
dotnet publish Gg.Cli -c Release -r osx-arm64 -o artifacts/aot
./artifacts/aot/gg --version
```

## License

MIT — see [LICENSE](LICENSE).
