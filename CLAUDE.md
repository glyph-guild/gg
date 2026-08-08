# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## What this is

Developer-side half of Good Grief: one distribution, two executables, one
version. `gg` (TypeScript/Ink TUI, `console/`) and `gg-runner` (.NET Native
AOT, `Gg.Runner/`). `Gg.Contracts` is the wire protocol and the source of
truth for `console/src/generated`. Runs in the customer's environment and
holds their credentials; the control plane (separate, private repo) never
sees code or credentials.

## Commands

```sh
dotnet build && dotnet test              # TUnit — see gotcha below
dotnet test -- --treenode-filter '/*/*/ProtocolHelloTests/*'   # single class

cd console
npm test                                 # vitest
npx vitest run src/keymap.test.ts        # single file
npx tsc --noEmit                         # typecheck incl. tests (CI runs this)
npm run build && node dist/cli.js --version

dotnet run --project tools/Gg.ContractsGen   # regenerate console/src/generated
```

## Non-negotiables

- **`console/src/generated` is generated** from `Gg.Contracts` by
  `tools/Gg.ContractsGen`. Never edit by hand; regenerate and commit after any
  contracts change — CI (`generated-sync`) diffs it. When adding a property
  type the generator doesn't know, extend `ZodFor` in its `Program.cs`
  (unknown types throw deliberately).
- **Tests are TUnit + Rocks + Bogus, never xUnit/NUnit/Moq** — those break
  Native AOT. `Gg.Runner` must stay AOT-publishable (`PublishAot` is set;
  builds run the AOT analyzers).
- **`src/keymap.ts` stays pure** — no Ink imports; it takes a structural
  `KeyInfo`, returns `Command | null`. **State (`src/state/`) stays
  JSON-serializable** — plain data only, no functions/Dates/classes.
- **One version everywhere**: `VersionPrefix` in `Directory.Build.props` and
  `console/package.json` must be bumped together to the same value. CI stamps
  prereleases `0.1.0-alpha.N` (N = commit count on main).

## Gotchas

- `dotnet test` only works because of the `"test"` section in `global.json`
  (Microsoft.Testing.Platform mode). Removing it breaks TUnit with a cryptic
  VSTest error.
- Rocks is pinned to 10.2.0 in `Directory.Packages.props`: 10.3.0's analyzer
  requires a newer Roslyn than SDK 10.0.102 ships (CS9057 if bumped).
- Package versions are centrally managed (`Directory.Packages.props`); csproj
  `PackageReference`s have no `Version` attribute.
- `console/` is ESM (`"type": "module"`, NodeNext): relative imports need a
  `.js` extension even in `.ts`/`.tsx` source.
- Two tsconfigs: `tsconfig.json` typechecks everything (noEmit);
  `tsconfig.build.json` emits `dist/` and excludes tests.
- NuGet publish of `GlyphGuild.Gg.Contracts` is skipped until the
  `NUGET_API_KEY` repo secret exists (workflow logs it, still packs).

## CI

`ci.yml` jobs: `dotnet`, `console`, `generated-sync`, summarized by a single
required check named **CI** (branch protection points at it — keep the job
name stable).

## Practices

- Strict TDD. YOU MUST write the failing test and COMMIT IT, then write the
  code and commit separately. Two commits minimum. The ordering is the
  practice and it is invisible in the final diff.
- No `Task.Delay` or sleeps in tests. Inject time.
- Some constraints here have reasons not stated in this repository. If one
  looks arbitrary or wrong, stop and ask. Do not infer the reason, and do not
  work around it.
