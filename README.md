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
says so. That covers CI and anything behind a pipe. On Windows gg hosts the
editor on the console itself, through ConPTY.

## Install

**One command, on a laptop.** macOS or Linux:

```sh
curl -fsSL https://github.com/glyph-guild/gg/releases/latest/download/install.sh | sudo sh
```

Windows, in PowerShell:

```powershell
irm https://github.com/glyph-guild/gg/releases/latest/download/install.ps1 | iex
```

Once goodgrief.dev serves the installers, the short form is the recommended
one, and runs the same scripts:

```sh
curl -fsSL https://goodgrief.dev/install.sh | sudo sh
```

```powershell
irm https://goodgrief.dev/install.ps1 | iex
```

**Verify the script first**, if piping it into a shell is not acceptable: the
script is attested with the release, like the binaries it installs.

```sh
curl -fsSLO https://goodgrief.dev/install.sh && gh attestation verify install.sh --repo glyph-guild/gg && sudo sh install.sh
```

The script runs only once all of it has arrived: its whole body is one
function, called on its last line, so a download cut off part way runs nothing.

It picks the build for the machine it is on, checks the bytes against the
build's own attestation before unpacking them, installs the native libraries
beside the binary, makes sure `/usr/local/bin` is on the PATH of the shell you
ran it from — adding it to that shell's startup file if it is not, and naming
the file — and **starts nothing**. It then says the two things left to do —
the organisation's control plane, and signing in:

```sh
gg config set control-plane <url>
gg login
```

**If another program called `gg` is already where this puts its link** — there is one, a
git GUI — the installer stops rather than deleting it, and asks for a
different name: answer the prompt, or pass `--as goodgrief` (`-Alias
goodgrief` on Windows) for a run with nobody at the keyboard. One merely
*earlier on your PATH* is not in danger and does not stop the install; you are
told it will keep the name, and the same flag gives this one its own. Every hint it
prints then uses the name you chose, and it is remembered, so an update needs
no flag. A runner is always `gg`: its service runs `/usr/local/bin/gg`.

**`--control-plane` is what makes a machine a runner**, and its absence is what
makes this a laptop install. Given one, the same script installs the service
that runs `gg runner up`, creates the user it runs as, and redeems an
enrollment token if you hand it one:

```sh
curl -fsSL .../install.sh | sudo sh -s -- --version <version> \
  --control-plane <url> --agent-binary /usr/local/bin/claude --enroll
```

That is the fleet path — a machine somebody enrolled, which nobody signs in
on — rather than something that happens to you for leaving a flag off.

**On Windows the console runs in Windows Terminal or the Windows console**,
with the agent column and the editor hosted beside it, as on macOS and Linux.

**By hand, if you would rather see every step** — and the second `install` line
is not optional:

```sh
curl -fsSL https://github.com/glyph-guild/gg/releases/latest/download/gg-linux-x64.tar.gz | tar xz
sudo install -m 0755 gg /usr/local/bin/gg
sudo install -m 0644 libporta_pty.so libonigwrap.so /usr/local/bin/
gg --version
```

A release carries `linux-x64`, `linux-arm64`, `osx-arm64`, `osx-x64` and
`win-x64`; swap the asset for yours, and `.so` for `.dylib` on a Mac.

**The newest release, unless a version is named.** The installers take the
newest release by default and say which one they chose; `--version <version>`
(`-Version` on Windows) installs a chosen one, which is what a fleet does — its
machines run the release somebody picked, and cloud-init and the member images
pin it. `releases/latest` is reliable here because the contract releases this
repository also publishes are never marked latest, so it always resolves to a
gg release. Either way the installer checks the bytes against the release's
attestation before unpacking them.

**Both `install` lines, and the second is not optional.** `gg` loads those two
libraries from its own directory — which is why they go in `bin` beside it
rather than somewhere tidier — and it loads them on first use rather than at
startup. A `gg` installed without them starts, runs, and prints its version
exactly as normal, then opens your editor without the gg bar. It will say which
file is missing when that happens, but it cannot say it before you have already
pressed the key.

**As a .NET tool**, if you already have the SDK, from nuget.org:

```sh
sudo dotnet tool install GlyphGuild.Gg.Cli --tool-path /usr/local/lib/gg
sudo ln -sf /usr/local/lib/gg/gg /usr/local/bin/gg
```

`--tool-path` rather than `-g`: a global install lands in the home of whoever
typed it, which on a host is the account the runner runs as — able to rewrite
its own executable. Without `--version` it installs the newest release on
nuget.org; add `--version <version>` to pin one. `gg update` says the update
command for whichever shape it finds.

Either way the command is `gg`. The package id is not `gg` because that one is
taken on nuget.org; the command is unaffected.

## When it feels slow

The console can say where its time went — set `GG_TIMING` to a path and it
writes a line per phase. See
[docs/diagnosing-a-slow-console.md](docs/diagnosing-a-slow-console.md).

## Build

```sh
dotnet build
dotnet test                                                # TUnit
dotnet publish Gg.Cli -c Release -r osx-arm64 -o artifacts/aot
./artifacts/aot/gg --version
```

## License

MIT — see [LICENSE](LICENSE).
