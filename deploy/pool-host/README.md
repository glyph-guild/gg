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
Docker and the .NET SDK, installs a **pinned** `gg` from nuget.org, unpacks the
**pinned** pool-host bundle, starts the proxy, and links the unit — then stops,
because what remains is a person. Its `final_message` says so on the console.

**Nothing is built on the host, and nothing is cloned onto it.** It used to do
both: `git clone --depth 1` of the default branch, then `dotnet publish`. That
meant a pool host ran whatever was on `main` the minute it provisioned, with no
tag and no commit to name, so two hosts an hour apart ran different code and
reported the same version. Both artefacts are now pinned to one version, and
`PoolProvisioningTests` holds the two pins to each other.

**Nothing on the host is owned by the `gg` user.** The old file finished with
`chown -R gg:gg /opt/gg`, over the very directory the systemd unit was linked
out of — so the account the runner runs as could rewrite its own `ExecStart`
and `User=`, and wait for a reboot. Root ownership of the binary never covered
that: it is a second route to the same place, through the file that decides
what the binary is.

**By hand:**

1. Docker, as root. Not a `gg` user that may talk to it — see above.
2. `docker compose up -d` in this directory.
3. Put `gg` on the machine. **Two shapes, and they update differently:**

   *With the SDK* — what cloud-init does, and what a host that will ever be
   updated should use:

   ```sh
   sudo dotnet tool install GlyphGuild.Gg.Cli --version <v> --tool-path /usr/local/lib/gg
   sudo ln -sf /usr/local/lib/gg/gg /usr/local/bin/gg
   ```

   `--tool-path` rather than `-g` is the security-relevant half: a global
   install lands in `$HOME/.dotnet/tools` of whoever ran it, and this unit runs
   as `User=gg`. The runner must not be able to rewrite its own executable.

   *Without the SDK* — a laptop, a machine that will be re-provisioned rather
   than updated:

   ```sh
   curl -fsSL https://github.com/glyph-guild/gg/releases/download/v<v>/gg-linux-x64.tar.gz | tar xz
   sudo install -m 0755 gg /usr/local/bin/gg
   ```

   This shape has no update path at all, by design — see below.
4. **A person signs in on the machine:** `gg login`. This is a device flow —
   the person approves in their own browser, and the session lands here.
5. Set `GG_POOL` **and `GG_CONTROL_PLANE`** in the unit file, then `systemctl
   enable --now gg-runner-maintain`.

## Moving a host to a new version

**`gg` never replaces its own binary** — not by download, not by rename, not by
shelling out. `dotnet` moves the bytes, or a person does. That is why there is
no updater to audit here, and no signed manifest, no compiled-in public key, no
per-platform atomic-replace, and no recovery story for a host bricked by the
tool that was meant to repair it.

With no lease held on the host:

```sh
sudo systemctl stop gg-runner-maintain
sudo dotnet tool update GlyphGuild.Gg.Cli --version <new> --tool-path /usr/local/lib/gg
curl -fsSL https://github.com/glyph-guild/gg/releases/download/v<new>/gg-pool-host.tar.gz \
  | sudo tar xz --no-same-owner -C /opt/gg-pool-host
sudo systemctl daemon-reload && sudo systemctl start gg-runner-maintain
```

**Take both, or neither.** The binary and the bundle ship as one version, and a
host running one against the other comes up looking entirely healthy.

**`--no-same-owner` is not optional.** `tar` run by root restores the numeric
uid recorded in the archive, and the archive is built on a CI runner where
everything belongs to uid 1001 — which on a cloud image is `gg`. Without the
flag the unit and the proxy's allowlist end up owned by the account the runner
runs as, which is the one thing this host is built to prevent.

**`--version` is not optional in practice.** `dotnet tool update` with none
takes whatever was pushed last, and what was pushed last is whatever reached
nuget.org — an API key is the trust root there, and a stolen one produces a
package nuget.org signs and every client accepts. On a fleet updating to
*latest* on a timer, that is the whole attack.

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
