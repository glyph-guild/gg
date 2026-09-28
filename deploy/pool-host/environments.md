# Hosting environments on a pool host

A pool host runs members. This is how it also runs **environment instances** —
a customer's own stack, brought up by Aspire, so a `ui-preview` flight has
somewhere to point.

> **Status: this implements a PROPOSED decision.** ADR-0034 (control-plane repo)
> is not accepted, and nothing here is deployed on vmlinux001 beyond the
> measurements that produced it. Every step below was run on that host at least
> once; the order has not been run start to finish.

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

## 4. Verify, in a way that cannot lie

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

## 5. Removing a slot

```sh
SLOT=gg-env-1; U=$(id -u "$SLOT")
sudo -u "$SLOT" env XDG_RUNTIME_DIR=/run/user/$U dockerd-rootless-setuptool.sh uninstall
sudo loginctl disable-linger "$SLOT"
sudo userdel -r "$SLOT"                        # -r takes the home, and the image store with it
```

`userdel -r` is the teardown proof this design was chosen for: everything the
instance ever made lived under one home, inside one daemon, and goes with it.

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
