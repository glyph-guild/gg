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

**One command, on a laptop.** macOS or Linux:

```sh
v=0.49.0
curl -fsSL https://github.com/glyph-guild/gg/releases/download/v$v/install.sh \
  | sudo sh -s -- --version $v
```

Windows, in PowerShell:

```powershell
$v='0.49.0'
& ([scriptblock]::Create((irm https://github.com/glyph-guild/gg/releases/download/v$v/install.ps1))) -Version $v
```

It picks the build for the machine it is on, checks the bytes against the
build's own attestation before unpacking them, installs the native libraries
beside the binary, makes sure `/usr/local/bin` is on the PATH of the shell you
ran it from — adding it to that shell's startup file if it is not, and naming
the file — and **starts nothing**. It then says the two things left to do —
where your tenant is, and signing in:

```sh
gg config set control-plane <url>
gg login
```

**If another program called `gg` is already on your PATH** — there is one, a
git GUI — the installer stops and asks for a different name rather than
installing over it: answer the prompt, or pass `--as goodgrief` (`-Alias
goodgrief` on Windows) for a run with nobody at the keyboard. Every hint it
prints then uses the name you chose, and it is remembered, so an update needs
no flag. A runner is always `gg`: its service runs `/usr/local/bin/gg`.

**`--control-plane` is what makes a machine a runner**, and its absence is what
makes this a laptop install. Given one, the same script installs the service
that runs `gg runner up`, creates the user it runs as, and redeems an
enrollment token if you hand it one:

```sh
curl -fsSL .../install.sh | sudo sh -s -- --version $v \
  --control-plane <url> --agent-binary /usr/local/bin/claude --enroll
```

That is the fleet path — a machine somebody enrolled, which nobody signs in
on — rather than something that happens to you for leaving a flag off.

**On Windows gg is the command line.** The console's terminal UI wants
`SetConsoleMode` and ConPTY, which are not written, so gg there opens `$EDITOR`
and says so.

**By hand, if you would rather see every step** — and the second `install` line
is not optional:

```sh
v=0.49.0
curl -fsSL https://github.com/glyph-guild/gg/releases/download/v$v/gg-linux-x64.tar.gz | tar xz
sudo install -m 0755 gg /usr/local/bin/gg
sudo install -m 0644 libporta_pty.so libonigwrap.so /usr/local/bin/
gg --version
```

A release carries `linux-x64`, `linux-arm64`, `osx-arm64`, `osx-x64` and
`win-x64`; swap the asset for yours, and `.so` for `.dylib` on a Mac.

**A version, not `latest`.** `v` is the release this README was written
against, and the release that moves the version moves it. `releases/latest`
means whichever release GitHub last marked latest on this repository, and the
contract releases published here used to take it — a link to `latest` 404'd.

**Both `install` lines, and the second is not optional.** `gg` loads those two
libraries from its own directory — which is why they go in `bin` beside it
rather than somewhere tidier — and it loads them on first use rather than at
startup. A `gg` installed without them starts, runs, and prints its version
exactly as normal, then opens your editor without the gg bar. It will say which
file is missing when that happens, but it cannot say it before you have already
pressed the key.

**As a .NET tool**, if you already have the SDK, from nuget.org:

```sh
v=0.49.0
sudo dotnet tool install GlyphGuild.Gg.Cli --version $v --tool-path /usr/local/lib/gg
sudo ln -sf /usr/local/lib/gg/gg /usr/local/bin/gg
```

`--tool-path` rather than `-g`: a global install lands in the home of whoever
typed it, which on a host is the account the runner runs as — able to rewrite
its own executable. `--version` because a tool installed without one takes
whatever reached nuget.org last. `gg update` says the update command for
whichever shape it finds.

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
