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

## What this deliberately does not do

**It does not provision itself.** Step 4 is a person, and that is the design
rather than a gap: `gg runner maintain` refuses without a session because
*registering a runner is a person's action*. A host is provisioned once by
somebody; it is not a thing that scales itself up.

Making it automatic means deciding whether a session token — a bearer with a
person's authority — may live at rest on a machine. **That decision has not
been taken, and this does not take it quietly.**

**It carries no credential.** The PAT a runner uses to reach a repository is
`local`: a file on the machine, which the control plane never sees. It is
placed out of band — on Azure, by the VM's own managed identity reading the
customer's Key Vault. Nothing here, and nothing in cloud-init, may carry one:
instance metadata is readable, and this file is committed.
