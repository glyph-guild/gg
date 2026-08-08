# gg

Good Grief developer tooling. Runs in **your** environment: a TUI console for
driving AI-agent work, and a runner that leases work, holds your credentials,
clones repos, extracts facts, and reports them. The control plane it talks to
never sees your code or credentials.

One distribution, two executables, one version:

- `gg` — the console (TypeScript, Ink)
- `gg-runner` — the runner (.NET, Native AOT)

`Gg.Contracts` is the wire protocol between them and the control plane,
published to NuGet as `GlyphGuild.Gg.Contracts` (prerelease per commit,
`0.1.0-alpha.N`). `console/src/generated` is generated from it — never edit
by hand:

```sh
dotnet run --project tools/Gg.ContractsGen
```

## Build

```sh
dotnet build          # contracts, runner, generator, tests
dotnet test           # TUnit
cd console
npm ci
npm run build         # emits dist/; `node dist/cli.js --version`
npm test              # vitest
npx tsc --noEmit      # typecheck incl. tests
```

## License

MIT — see [LICENSE](LICENSE).
