# A pool host

A machine that runs the resident runner, so a pool of environments exists
before anything asks for one.

## What it is built to satisfy

**The runner reaches Docker only through the proxy.** The socket is host root —
anything that reaches it can start a privileged container and own the machine.
`compose.yaml` gives the socket to the proxy and to nothing else, and
`PoolHostTests` holds that exactly one artefact here may name it.

**The proxy listens on loopback only.** It refuses out-of-scope reaches but it
does not authenticate: anything that can reach the port can do everything the
pool is allowed to do. Loopback is what keeps *allowed to act on the pool* and
*reachable from the network* apart.

**Every pool carries `gg-pool-`.** The proxy allows creating a member only
under that prefix, and the runner refuses a pool that could not pass — so a 403
from the proxy means one thing: something reached outside the scope.

## Standing one up

1. Docker, and a `gg` user that may talk to it.
2. `docker compose up -d` in this directory.
3. Put `gg` at `/usr/local/bin/gg`.
4. **A person signs in on the machine:** `gg login`. This is a device flow —
   the person approves in their own browser, and the session lands here.
5. Set `GG_POOL` in the unit file, then `systemctl enable --now
   gg-runner-maintain`.

## A person is needed once a month, and only then

**Step 4 is a person, and after it the host restarts on its own.** The runner
keeps its own credential at `~/.config/good-grief/runner.json`, owner-only, and
reuses it — so a reboot needs nobody.

**The credential at rest is the RUNNER's, not the person's**, and the reason is
arithmetic rather than taste: a session lasts twelve hours and a runner token
thirty days, while `gg runner maintain` registers on every start. A host
holding a session would fail to restart after half a day, with nobody at it.
`RunnerRegistry` designed that separation — *"the runner's lifetime is its
own"* — and keeping the runner's token preserves it where keeping a session
would discard it and hold the wider authority besides.

**At thirty days a person signs in again.** Nothing renews a runner token: the
protocol's renew is for a *lease*. That is a cadence rather than a bug, and the
refusal names it so it does not read like a broken machine. Making it fully
unattended would need a renewal path — a contract change and a different
security posture, not a thing to decide inside provisioning.

**It carries no credential.** The PAT a runner uses to reach a repository is
`local`: a file on the machine, which the control plane never sees. It is
placed out of band — on Azure, by the VM's own managed identity reading the
customer's Key Vault. Nothing here, and nothing in cloud-init, may carry one:
instance metadata is readable, and this file is committed.
