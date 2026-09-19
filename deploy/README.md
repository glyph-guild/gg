# Adding a host

A machine joins the fleet in one of two roles, and each has its own runbook.

| role | what it runs | runbook |
|---|---|---|
| **resident runner** | `gg runner up` — takes flights itself | [`resident-runner/`](resident-runner/README.md) |
| **pool host** | `gg runner maintain` — keeps a pool of member containers ready, behind a scope proxy, with its own registry | [`pool-host/`](pool-host/README.md) |

One machine can be both — vmlinux001 is — by following both runbooks. Each
installs the same pinned `gg`, and each ends at the same place: **a person signs
in on the machine.** Registering a runner is a person's action, and nothing
provisioned may do it for them.

## Stand a new host up; never clone one

Copying a working host's disk looks like the fast way to a second one. Every
piece of what makes it work is either local to it or an identity only one
machine may hold:

- **A pool's name is the fleet's, not the machine's.** Members are minted as
  `{pool}-{slot}`, and a host picks the slot from the members *it* can see.
  Two hosts maintaining the same pool each mint `…-1`, and the control plane
  sees one member where there are two machines. A second host is given a pool
  of its own.
- **Its images live in its own registry**, at `127.0.0.1:5000`. A clone would
  carry a copy of them and a volume nobody pushes to; a host stood up fresh
  builds and pushes its own, and a strategy pins them by digest either way.
- **Its credential is one runner.** The store under the `gg` user's
  `~/.config/good-grief` is the credential of the runner registered there, and
  a copy is the same runner on two machines — each heartbeating and claiming
  as the other.

So a new host starts from the runbook, with a pool name of its own, and a
person signs in on it. That is the path today. ADR-0025 (proposed) and slice
forty-three replace the sign-in with a person-minted enrollment token and one
install verb; until they land, these two runbooks are the way in, and
`TheHostRunbooksNameWhatExistsTests` holds what they install to what is
published.
