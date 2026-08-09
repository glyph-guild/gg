# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## What this is

Developer-side half of Good Grief: one Native AOT binary, `gg` (built from
`Gg.Cli`). No arguments launches the Terminal.Gui console; `gg runner up`
spawns the runner role as a **separate child process** (`gg runner serve`,
same binary re-exec). That process boundary is deliberate and load-bearing —
the runner is treated as hostile, and the OS keeps it apart from the console.
`Gg.Contracts` is the wire protocol and the artifact a customer audits. The
control plane (separate, private repo) never sees customer code or
credentials.

## Commands

```sh
dotnet build && dotnet test              # TUnit — see gotcha below
dotnet test -- --treenode-filter '/*/*/KeymapTests/*'    # single class

dotnet publish Gg.Cli -c Release -r osx-arm64 -o artifacts/aot
./artifacts/aot/gg --version             # CI does the same on linux-x64
```

## Non-negotiables

- **Terminal release is the architecture.** `TerminalGuiSession` is one
  complete Init/Run/dispose lifetime; `ConsoleLoop` tears the UI down, hands
  the terminal to `$EDITOR` (a separate process), and rebuilds views FROM the
  surviving `AppState`. Views are never the source of truth; `AppState` stays
  plain JSON-serializable data (source-generated, `AppStateJsonContext`).
- **`Keymap.Resolve` is pure** — no Terminal.Gui types; only
  `Views/KeyTranslator` touches `Key`. Status hints come from
  `Keymap.Hints(context)`, the same context dispatch uses.
- **Gg.Contracts: zero third-party package references**, every wire type
  carries `[PinnedId]` and appears in `Vocabulary` — all three enforced by
  tests in `Gg.Contracts.Tests`. Register new types or the build fails.
- **Everything must stay AOT-publishable** (CI publishes `Gg.Cli`): no
  reflection-driven serialization, source generators only. Tests are TUnit +
  Rocks + Bogus, never xUnit/NUnit/Moq — they break AOT.
- **Gg.Runner takes no Whizbang dependency.**
- One version: `VersionPrefix` in `Directory.Build.props`; CI stamps
  `0.1.0-alpha.N` (N = commit count on main).

## Gotchas

- `dotnet test` only works because of the `"test"` section in `global.json`
  (Microsoft.Testing.Platform mode). Removing it breaks TUnit with a cryptic
  VSTest error.
- Terminal.Gui 2.4.17: the static `Application` facade, `Toplevel`, and
  `TextView` are obsolete — obsolete warnings are errors here. Use
  `Application.Create()` → `IApplication`, `Window`/`Runnable`, and `Label`
  (or tui-cs/Editor if real editing is ever needed). ListView selection is
  the `ValueChanged` event and `SelectedItem` is `int?`.
- Rocks is pinned to 10.2.0 in `Directory.Packages.props`: 10.3.0's analyzer
  requires a newer Roslyn than SDK 10.0.102 ships (CS9057 if bumped).
- Package versions are centrally managed (`Directory.Packages.props`); csproj
  `PackageReference`s have no `Version` attribute.
- NuGet publish of `GlyphGuild.Gg.Contracts` is skipped until the
  `NUGET_API_KEY` repo secret exists (workflow logs it, still packs).

## CI

`ci.yml` jobs: `dotnet`, `aot` (publish `gg`, run `--version`), summarized by
a single required check named **CI** (branch protection points at it — keep
the job name stable).

## Practices

- Strict TDD. YOU MUST write the failing test and COMMIT IT, then write the
  code and commit separately. Two commits minimum. The ordering is the
  practice and it is invisible in the final diff.
- No `Task.Delay` or sleeps in tests. Inject time.
- Some constraints here have reasons not stated in this repository. If one
  looks arbitrary or wrong, stop and ask. Do not infer the reason, and do not
  work around it.

## How work reaches `main`

`main` is protected on both repos and **administrators are included** — that
means you, and it means an agent using an owner's token. There is no push to
`main`. Not for a one-line fix, not to unblock yourself.

    git switch -c <branch>        # branch first, always
    …red commit…                  # test only, failing
    …green commit…                # implementation
    gh pr create
    # wait for the CI check
    gh pr merge --rebase --delete-branch

**Rebase merge only.** Squash and merge commits are disabled at the repo level,
deliberately — see below.

### CI is red on purpose, half the time

Strict TDD here means a red commit followed by a green one, and the red one is
pushed. **A PR whose CI is failing on the red commit is working correctly.**

Do not "fix" it, do not squash it away, do not reorder the commits so the branch
is green throughout. The ordering is the deliverable. Push the red commit, then
push the green one to the same PR, and merge when the final state is green.

If you are ever tempted to combine the test and the implementation into one
commit because the branch looks broken: that is the exact thing this forbids.

### Why squash merge is disabled, so nobody re-enables it

Squashing collapses `red` and `green` into a single commit showing a test
arriving alongside the code that satisfies it. Commit history is the **only**
place the TDD ordering is visible — it is invisible in the final diff — so
squashing erases the evidence that the practice was followed.

Force-push is also blocked, so that erasure would be permanent.

### Commit messages

Label the pair so the ordering is legible without reading diffs:

    Add pinned-id manifest tests (red: ProtocolHello unpinned, unregistered)
    Pin ProtocolHello and register it in the vocabulary (manifest tests green)

State what is red and *why* it is red. A reviewer — human or agent — should be
able to tell from the log alone that the assertion was wired to something before
the implementation existed.

### The rules in force

Force pushes blocked · deletions blocked · PR required · `CI` required green ·
administrators included · zero reviews required · rebase merge only · branches
auto-delete on merge.

Zero reviews is intentional: self-approval is theatre. The gate is CI and the
history rules, not a rubber stamp.

This repository is public. Write every commit message as if a customer will
read it, because they can.
