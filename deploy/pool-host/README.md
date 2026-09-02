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

## The `gg` user must NOT be in the `docker` group

**This README used to ask for one, in step one, and it was the whole control
undone.** Nothing in this repository ever opens the Docker socket: no source
file names that path, sets `DOCKER_HOST`, or runs the `docker` binary. The
runner reaches it as HTTP to `GG_POOL_ENDPOINT` and by no other route.

(The path is not spelled out here on purpose — `PoolHostTests` allows exactly
one artefact in this directory to name it, and prose explaining the rule would
otherwise break it.)

So the membership grants the runner nothing it uses, and returns everything the
proxy exists to withhold — and it is invisible in every test about the control.
The socket stays mounted in exactly one place; `PoolHostTests` keeps passing.
The bypass is not in any artefact's contents, it is in who may open a file none
of them mention. `PoolProvisioningTests` now holds `cloud-init.yaml` to it.

Docker is brought up by root during provisioning. That is why `gg` needs no
access to it at all.

## Standing one up

**With cloud-init**, which is steps 1–3 already done: hand
[`cloud-init.yaml`](cloud-init.yaml) to the machine at first boot. It installs
Docker, clones this repository, builds the binary, starts the proxy, and links
the unit — then stops, because what remains is a person. Its `final_message`
says so on the console.

**By hand:**

1. Docker, as root. Not a `gg` user that may talk to it — see above.
2. `docker compose up -d` in this directory.
3. Put `gg` at `/usr/local/bin/gg`. There is no published release asset to
   download: `dotnet publish Gg.Cli -c Release -r linux-x64` is what CI proves
   runs, and it is what cloud-init does on the machine.
4. **A person signs in on the machine:** `gg login`. This is a device flow —
   the person approves in their own browser, and the session lands here.
5. Set `GG_POOL` **and `GG_CONTROL_PLANE`** in the unit file, then `systemctl
   enable --now gg-runner-maintain`.

Unset, `GG_CONTROL_PLANE` falls back to `http://localhost:5199`. That is the
right default on a laptop and on a pool host it means the machine answers to
itself: nothing is unset, nothing is refused, and it looks configured. Both the
unit file and cloud-init's drop-in name it for that reason.

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
