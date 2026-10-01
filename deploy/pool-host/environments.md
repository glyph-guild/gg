# Hosting environments on a pool host

A pool host runs members. This is how it also runs **environment instances** —
a customer's own stack, brought up by Aspire, so a `ui-preview` flight has
somewhere to point.

> **Status: this implements a PROPOSED decision.** ADR-0034 (control-plane repo)
> is not accepted. **The order was run start to finish on vmlinux001 for the
> first time on 2026-10-01**, and section 4 did not work as written — the
> corrections are in it now, each marked with what was measured. `gg-env-1`
> serves `ui` on that host and `gg` reaches its daemon.
>
> One deviation stands on that host and is the owner's call: **there is no data
> disk.** `/srv/env` is a plain directory on the OS disk, so section 1 was
> skipped. The blast radius section 1 exists to bound is therefore real — the
> host was reclaimed to 22 G free first, and a stack larger than that takes the
> pool's members down with it.

## Why it is shaped this way, in one paragraph each

**An instance owns a Docker daemon, and the daemon is the boundary.** The scope
proxy cannot bound Aspire: measured, Aspire addresses containers by the id
`create` returns, needs `PUT /archive` into the container root — the one endpoint
that proxy exists to refuse — and writes to the host-wide `bridge` network. A
name filter has nothing to match on. A daemon has nothing to filter.

**The daemon is rootless, so it costs no privilege.** A nested daemon needs
`--privileged` — measured, `docker:dind-rootless` in a container creates
containers and cannot start them, failing on `mounting "proc" to rootfs`. On the
host, with real subuid ranges, rootless simply works.

**An instance is a UNIX user.** One rootless daemon belongs to one user, so a
user is how two instances are kept apart — by the same kernel mechanism that
keeps them out of root. Measured: two daemons, two data roots, neither seeing the
other's containers.

**Its home is on a separate disk, and that is about blast radius before
capacity.** Every instance keeps its own image store. On the OS disk, an instance
pulling a stack can fill `/` and take the pool's own members down with it — they
share the filesystem. On its own disk it cannot.

## 1. The data disk

Separate, **not** a migration: nothing already on the host moves, so the pool
never stops.

```sh
# From a machine with az, against the VM's subscription.
az disk create -g <resource-group> -n <vm>-env01 --size-gb 128 --sku StandardSSD_LRS -l <region>
az vm disk attach -g <resource-group> --vm-name <vm> --name <vm>-env01
```

Then on the host. **Find the new disk by identity, never by guessing `sdc`** —
device names are assigned in attach order and change across reboots.

```sh
lsblk -o NAME,SIZE,FSTYPE,MOUNTPOINT          # the new disk is the one with no FSTYPE
DISK=/dev/disk/azure/scsi1/lun0               # Azure's stable path for the first data disk
sudo mkfs.ext4 -m 0 -L gg-env "$DISK"         # -m 0: no root reserve; nothing here is root's

sudo mkdir -p /srv/env
UUID=$(sudo blkid -s UUID -o value "$DISK")
echo "UUID=$UUID /srv/env ext4 defaults,noatime 0 2" | sudo tee -a /etc/fstab
sudo mount /srv/env
sudo chmod 755 /srv/env
```

**By UUID in `fstab`, and `0 2` in the last column.** A device path would mount
the wrong disk after an attach order changes; a fsck pass order of `0` would skip
it forever.

Verify before going on — a wrong mount here is silent until an instance fills
the OS disk anyway:

```sh
findmnt -no SOURCE,TARGET,FSTYPE /srv/env && df -h /srv/env
```

## 2. The host's rootless prerequisites, once

```sh
sudo apt-get install -y uidmap slirp4netns
```

**Two packages, and Docker's own check names only the first.**
`dockerd-rootless-setuptool.sh check` reports `uidmap` as missing and says
nothing about `slirp4netns`, because that one is a recommendation rather than a
requirement. Without it `dockerd-rootless.sh` silently falls back to
`gvisor-tap-vsock`, which `rootlesskit --help` marks *experimental*. Install both.

## 3. An instance

Repeat per slot. `gg-env-1`, `gg-env-2`, …

```sh
SLOT=gg-env-1
sudo useradd -m -b /srv/env -s /bin/bash "$SLOT"
```

**If the slot already exists with the wrong home**, which is what happens when
somebody made it before reading this, `usermod -d /srv/env/$SLOT -m $SLOT` moves
it — but **it refuses while any process belongs to the user, and
`enable-linger` guarantees one**: the `systemd --user` manager. The error names
a pid and reads like something is wedged. Turn linger off, stop the manager,
move, then put linger back:

```sh
sudo loginctl disable-linger "$SLOT"
sudo systemctl stop "user@$(id -u "$SLOT").service"
sudo usermod -d "/srv/env/$SLOT" -m "$SLOT"
sudo loginctl enable-linger "$SLOT"            # also recreates /run/user/<uid>
```

And check the move **with sudo**. The home is `drwxr-x---` and owned by the
slot, so a bare `test -d /srv/env/$SLOT/.local/share/docker` is false for a
perfectly good move, and reports a failure that did not happen.

**`-b /srv/env` is the whole trick.** The home lands on the data disk, so the
rootless daemon's default store — `~/.local/share/docker` — is on that disk with
nothing to configure and nothing to remember.

Check the kernel range it was given, and that it overlaps nobody:

```sh
grep "$SLOT" /etc/subuid /etc/subgid
cat /etc/subuid                                # ranges must not overlap
```

`useradd` allocates this automatically where `/etc/login.defs` sets
`SUB_UID_COUNT`; if it did not, the slot has no isolation and the rest of this
document is theatre. Stop and fix it before continuing.

```sh
sudo loginctl enable-linger "$SLOT"            # this is what creates /run/user/<uid>
U=$(id -u "$SLOT")
sudo -u "$SLOT" env XDG_RUNTIME_DIR=/run/user/$U dockerd-rootless-setuptool.sh install
```

`enable-linger` is not optional and is easy to skip: without it `/run/user/<uid>`
does not exist, the user manager is not running, and the setuptool fails in a way
that reads like a permissions problem.

## 3b. Say what the slot is for

```sh
echo ui | sudo -u "$SLOT" tee "/srv/env/$SLOT/environment" >/dev/null
```

**Nothing else on this host knows.** The slot is called `gg-env-1`, and that
name says nothing about `ui` or `api` — so the runner reads this file, and a
slot without one is not reported at all. That is deliberate: guessing the
purpose from the name would be the runner deciding what you meant.

The name must be an environment this tenant has **charted** (`gg airspace show`).
An uncharted name is refused where every uncharted name is — at the document, by
the control plane — and the slot simply never becomes grantable.

One host may serve two environments: give its slots different files. Nothing
here assumes a host is single-purpose.

**This is what makes the slot appear.** `gg` reports what it finds under
`/srv/env` on a cadence, and the control plane's record of which instances exist
is built from those reports and nothing else (good-grief#617) — no document
lists them, and no person keeps them in sync. Remove the slot and the next
report retires it.

## 4. Let the runner reach it, without letting it become anybody

The runner is `gg`. It has no `sudo`, and it does not need any: the slot's daemon
is already running, started at boot by `enable-linger`. What it needs is to
*talk* to one.

**The default socket cannot be that one.** Measured: `/run/user/<uid>` is
`drwx------` owned by the slot, on a tmpfs mounted `mode=700` and recreated every
boot — so `gg` is refused, and an ACL placed there would not survive a restart.

Give the daemon a second listener on a durable path instead.
`dockerd-rootless.sh` ends in `exec "$dockerd" "$@"`, so its arguments reach
`dockerd`:

```sh
SLOT=gg-env-1; U=$(id -u "$SLOT")
sudo groupadd -f gg-env && sudo usermod -aG gg-env gg

sudo -u "$SLOT" mkdir -p "/srv/env/$SLOT/run"
sudo chgrp gg-env "/srv/env/$SLOT" "/srv/env/$SLOT/run"
sudo chmod 750 "/srv/env/$SLOT" "/srv/env/$SLOT/run"

sudo -u "$SLOT" mkdir -p "/srv/env/$SLOT/.config/systemd/user/docker.service.d"
sudo -u "$SLOT" tee "/srv/env/$SLOT/.config/systemd/user/docker.service.d/reachable.conf" >/dev/null <<EOF
[Service]
ExecStart=
ExecStart=/usr/bin/dockerd-rootless.sh -H unix:///run/user/$U/docker.sock -H unix:///srv/env/$SLOT/run/docker.sock
EOF

sudo -u "$SLOT" env XDG_RUNTIME_DIR=/run/user/$U systemctl --user daemon-reload
sudo -u "$SLOT" env XDG_RUNTIME_DIR=/run/user/$U systemctl --user restart docker
```

**The empty `ExecStart=` before the real one is required**, not tidiness: without
it systemd appends a second command rather than replacing the first, and the unit
fails to start. **Both `-H` flags are required too** — naming one replaces the
default rather than adding to it, and the slot's own tooling still expects the
runtime-dir socket.

**The socket cannot be given to `gg-env`, and trying breaks the daemon.**
Measured 2026-10-01, which is the first time this section was run start to
finish. Rootless `dockerd` chowns its own socket after binding it, and it can
only chown to a gid inside its subuid/subgid mapping — `gg-env` is a host group
far outside that range. Left alone the socket lands as `srw-rw---- 1 <slot>
232057`, a mapped subgid no host user is in, so `gg` is refused. And putting
setgid on the directory so the socket inherits `gg-env` makes it **worse**: the
daemon then fails to start at all, with

    failed to load listeners: can't create unix socket
    /srv/env/$SLOT/run/docker.sock: chown …: operation not permitted

**So the DIRECTORY is the gate and the socket is open inside it.** `0750`
`root:gg-env` on `run/` means only `gg` and the slot may enter; a `0666` socket
behind a door you cannot open is not access. The mode is reapplied on every
start because the socket is recreated on every start, and it is polled because
`ExecStartPost` runs as soon as `ExecStart` has been *forked* — before `dockerd`
has bound anything:

```sh
ExecStartPost=/bin/sh -c "for i in $(seq 1 100); do [ -S /srv/env/$SLOT/run/docker.sock ] && exec chmod 0666 /srv/env/$SLOT/run/docker.sock; sleep 0.1; done; exit 1"
```

Add that line to the same drop-in, under the two `ExecStart=` lines.

> If you reached here from a `chown … operation not permitted`, check for a
> stray setgid bit first: `chmod 0750` does **not** clear it on a directory that
> already has it on this kernel — `chmod g-s` does, and `ls -lnd` showing
> `drwxr-s---` is the tell.

`gg` must re-login for its new group to take effect; `sg gg-env -c …` or a
restart of the runner service is the short way. Then:

```sh
sudo -u gg env DOCKER_HOST=unix:///srv/env/gg-env-1/run/docker.sock docker version --format '{{.Server.Version}}'
```

**This grants the runner that slot's daemon and nothing else.** It is a file
permission, not a privilege: `gg` still cannot become the slot, cannot reach the
host's daemon, and cannot touch another slot it has not been added to.
`TheRunnerReachesAnInstanceWithoutBecomingAnybodyTests` holds the other half —
that nothing in `Gg.Runner` ever tries.

## 5. Verify, in a way that cannot lie

```sh
U=$(id -u gg-env-1); for u in "gg-env-1:$U"; do n=${u%%:*}; i=${u##*:}; printf "%-9s root=%s\n" "$n" "$(sudo -u $n env XDG_RUNTIME_DIR=/run/user/$i DOCKER_HOST=unix:///run/user/$i/docker.sock docker info --format '{{.DockerRootDir}}')"; done
```

The root must be **under `/srv/env`**. If it reads `/home/…`, the user was made
without `-b` and its images are on the OS disk — which is the failure this whole
document exists to prevent, and it is invisible until the disk fills.

Two slots, side by side, must not see each other:

```sh
sudo -u gg-env-1 env XDG_RUNTIME_DIR=/run/user/$(id -u gg-env-1) DOCKER_HOST=unix:///run/user/$(id -u gg-env-1)/docker.sock docker ps -a --format '{{.Names}}'
sudo -u gg-env-2 env XDG_RUNTIME_DIR=/run/user/$(id -u gg-env-2) DOCKER_HOST=unix:///run/user/$(id -u gg-env-2)/docker.sock docker ps -a --format '{{.Names}}'
```

**Read the data roots, not the container lists.** A name collision between slots
reads exactly like shared state and is usually a re-run; identical
`DockerRootDir` values are the thing that actually means they are one daemon.

Ports are ordinary — measured, `-p 127.0.0.1:8099:80` publishes onto the host's
loopback and the numbers may differ. There is no range to reserve.

## 6. Removing a slot

```sh
SLOT=gg-env-1; U=$(id -u "$SLOT")
sudo -u "$SLOT" env XDG_RUNTIME_DIR=/run/user/$U dockerd-rootless-setuptool.sh uninstall
sudo loginctl disable-linger "$SLOT"
sudo userdel -r "$SLOT"                        # -r takes the home, and the image store with it
```

`userdel -r` is the teardown proof this design was chosen for: everything the
instance ever made lived under one home, inside one daemon, and goes with it.

**Nothing else is needed to retire it.** The runner's next report does not find
the slot, the control plane reconciles against what it is sent, and any grant
the slot was holding is released with it. There is no command to run against the
control plane and no document to edit — which is the whole point of the host
being the one that attests.

## What this does NOT do, stated where somebody looks

**Nothing here bounds a slot's disk usage.** A slot can fill `/srv/env` and
starve its neighbours. It cannot reach the OS disk, so the pool's members survive
— which is the property being bought — but per-slot quotas are not configured.
`ext4` with `usrquota` and `setquota` is the obvious next step and is not taken.

**The runner cannot create or start a slot.** The resident runner runs as `gg`,
which has no `sudo` and cannot `useradd`, `enable-linger`, or reach another
user's `systemd --user`. So slots here are made by hand, and how a runner is
allowed to bring one up — a narrow `sudoers` entry, a polkit rule, a setuid
helper — is an open seam and belongs in the slice that needs it.

**Nothing here starts a customer's stack.** That is an AppHost, pointed at the
slot's daemon with `DOCKER_HOST=unix:///run/user/<uid>/docker.sock`. Whether
JDNext's thirteen containers, five volumes and three traefik bind mounts survive
a rootless daemon is not measured; a bind mount reaching the host's filesystem is
the one to doubt first.

**The `gg` user may have a rootless daemon from the measurements.** It is not
used by anything and is not part of this design — instances are `gg-env-N`.
Remove it with the same `uninstall` line if it is in the way.
