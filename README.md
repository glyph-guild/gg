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

`Gg.Contracts` is the wire protocol, published to NuGet as
`GlyphGuild.Gg.Contracts` (prerelease per commit, `0.1.0-alpha.N`). It has
zero third-party package references — it is the artifact a customer audits —
and every wire type carries a `[PinnedId]` and appears in the `Vocabulary`
manifest. Both rules are enforced by tests.

## Build

```sh
dotnet build
dotnet test                                                # TUnit
dotnet publish Gg.Cli -c Release -r osx-arm64 -o artifacts/aot
./artifacts/aot/gg --version
```

## License

MIT — see [LICENSE](LICENSE).
