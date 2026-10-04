# An environment that comes up through the five points

A deliberately ordinary compose stack — `nginx` in front of `postgres`, no
.NET, no Aspire — wired to gg through one executable, `hooks.sh`.

It is here to be **the thing a walk runs**. ADR-0033's second amendment made
five named points the abstraction and Aspire one implementation behind them,
and the honest test of that claim is a stack with nothing in common with an
AppHost coming up under the same five names.

## What gg does, in order

| point | what this stack does | what gg reads |
|---|---|---|
| `prepare` | `docker compose pull` | exit code |
| `attach` | `up -d --wait` — detached, waits for health, returns | exit code |
| `sync` | **never invoked**: this environment declares `filesystem: shared` | — |
| `ready` | asks the daemon for each service's health | **stdout**, plus exit code |
| `detach` | `down --remove-orphans --volumes` | exit code |

Before any of them, gg checks the declared file is present and inside the
checkout, and refuses the flight if it is not — a stack that cannot come up
should not consume a grant.

## How to declare it

On the environment's strategy in your airspace:

```yaml
hooks: .goodgrief/environments/compose/hooks.sh
filesystem: shared
```

`hooks:` is **one executable**, not a directory of five files — the point
arrives as its first argument. Two members would be two things to keep in
sync.

`filesystem:` is a declaration before it is a script: `shared` means the
worker and the stack see one tree, so there is nothing to copy and gg invokes
no sync hook. `push` and `pull` are in the vocabulary and not in the product;
a document declaring one is refused at apply, naming itself.

## What the script may assume

gg guarantees these, and `hooks.sh` is commented against each:

- **`argv[1]`** is one of `prepare` `attach` `sync` `ready` `detach`.
- **cwd** is the root of the checkout, so every path here is relative to it.
- **`DOCKER_HOST`** points at the *granted instance*. **Never reassign it.** A
  `DOCKER_HOST` arriving from the runner's own environment points at the pool
  host's daemon, which is the one thing the instance exists to keep your stack
  away from.
- **`GG_PREVIEW_PORT`** is the port the preview connector forwards to, when the
  flight has an exposure. It may be unset. Bind it rather than choosing your
  own: a stack that picked its own port would answer where nothing is looking.
- **Ten minutes** per point, after which gg kills it.

## How `ready` answers

`ready` **answers**; it does not merely exit.

```
ready=yes
url=http://127.0.0.1:18231/
db=healthy
web=healthy
```

- `ready=yes` or `ready=no`, then any number of `key=value` lines.
- `url` is the **only** key gg interprets. Everything else is carried and shown
  to a person, and interpreted by nothing — which is what makes it free to
  report the per-service states.
- **`ready=no` exits 0.** Not-ready is something the stack said. A non-zero exit
  is gg's *third* outcome — "the hook could not answer" — and is never read as
  `no`, because losing that difference means a broken hook reads as a stack that
  is merely still starting.
- Noise is ignored, so compose announcing its networks costs nothing. This
  script still sends every diagnostic to stderr and keeps stdout for the report.

## Why `ready` asks the daemon instead of fetching the URL

Health is judged **where the stack runs**. A hook runs host-side against a
granted instance, so whether *this process* can reach a published port is a
question about the instance's network namespace rather than about the stack
being up. `docker inspect` asks the daemon, which is right either way, and both
services declare a `HEALTHCHECK` so there is something to ask about.

The address in `url=` is therefore the port gg handed us — the local address a
person on that host can open. It is **not** what gg forwards to: `preview.url`
is gg's own public address out of its own inventory, and a reported
`http://127.0.0.1:…` is a local one nobody outside the host can open.

## What it does not do

**It does not seed data.** Warmth is the image store and nothing else: reclaim's
volume prune is unfiltered, so no database state survives a flight and anything
`prepare` seeded would be gone. That is why `db` has no named volume — one would
promise durability nothing can keep.

**It does not pin itself to a gg version**, so it is not a runbook and does not
move when a release does.
