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

## When gg hands over the terminal

Pressing `e` opens `$EDITOR`. gg runs it inside a pseudo-terminal it owns rather
than handing the terminal away, which is what lets it keep the top row:

```
 gg · editing — save and quit to come back
```

**This changes what gg can see, and that is worth stating plainly.** When a child
inherits the terminal, the operating system keeps gg out of it: gg sees nothing
of what is typed or drawn. Hosting puts gg in the middle, so every byte in and
out passes through the gg process.

Where that boundary sits:

- **Nothing is stored.** The host holds no state between calls — it has no
  fields, and a test asserts it has none, because a field would hand the next
  session the last one's screen.
- **Nothing is written to disk.** A test rejects file writes in the host and the
  renderer by name. An earlier debugging build traced every byte to a temp file;
  it is not in this one and cannot come back quietly.
- **Nothing reaches a diagnostics bundle or a state dump.** Asserted by planting
  a distinctive string through the real path — a real child prints it, a real
  pseudo-terminal carries it — and looking for it in the artifacts.
- **Nothing leaves the machine.** The host makes no network call and resolves no
  credential; it is spawn, read, paint, restore.

What gg does with the terminal itself is put it back exactly as it found it —
line discipline, echo, auto-wrap and alternate screen — whether the child exits
cleanly, exits non-zero, or is killed.

**On a machine that cannot host**, gg opens `$EDITOR` the way it always did and
says so. That covers CI, anything behind a pipe, and Windows, where
`SetConsoleMode` and ConPTY are not written yet.

## Install

**No .NET required** — a pool host, a CI runner, a laptop:

```sh
curl -fsSL https://github.com/glyph-guild/gg/releases/latest/download/gg-linux-x64.tar.gz | tar xz
sudo install -m 0755 gg /usr/local/bin/gg
sudo install -m 0644 libporta_pty.so libonigwrap.so /usr/local/bin/
gg --version
```

Swap `linux-x64` for `osx-arm64` on an Apple-silicon Mac, and `.so` for
`.dylib`.

**Both `install` lines, and the second is not optional.** `gg` loads those two
libraries from its own directory — which is why they go in `bin` beside it
rather than somewhere tidier — and it loads them on first use rather than at
startup. A `gg` installed without them starts, runs, and prints its version
exactly as normal, then opens your editor without the gg bar. It will say which
file is missing when that happens, but it cannot say it before you have already
pressed the key.

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
