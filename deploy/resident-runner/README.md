# Bringing up a resident runner

Two files, and between them they are the whole of a host's own configuration.
Everything else this machine runs on comes from what its control plane offers.

## Why there are two

`accept-offered` has **no environment variable**, deliberately. The recorded
reason is that a variable is one a container image or a systemd unit could
carry without anybody reading it — and letting something else configure your
machine should cost somebody opening a file. So it cannot live in the unit, and
a host that accepts offers is one where a person wrote `config.json`.

`control-plane` could live in either. It is in the unit because a unit is what
an operator edits when a host moves between environments, and because nothing
can tell you where the control plane is except the control plane.

That leaves the seed at two lines, and everything a fleet shares — the relays,
the labels — arrives from the control plane at startup.

## Install

```sh
# 1. gg itself, root-owned and outside the runner user's home.
sudo dotnet tool install --tool-path /usr/local/lib/gg GlyphGuild.Gg
sudo ln -sf /usr/local/lib/gg/gg /usr/local/bin/gg

# 2. The seed, owned by the user the service runs as.
sudo -u gg mkdir -p /home/gg/.config/good-grief
sudo -u gg install -m 0644 config.json /home/gg/.config/good-grief/config.json
sudo -u gg sed -i 's|https://CHANGEME.example|https://your-control-plane|' \
    /home/gg/.config/good-grief/config.json

# 3. The unit, with the same control plane.
sudo install -m 0644 gg-runner-up.service /etc/systemd/system/
sudo sed -i 's|https://CHANGEME.example|https://your-control-plane|' \
    /etc/systemd/system/gg-runner-up.service

# 4. A person signs in ON THIS MACHINE. `gg runner up` refuses without a
#    session: registering a runner is a person's action, and the service will
#    not start until one has happened.
sudo -u gg gg login

sudo systemctl daemon-reload
sudo systemctl enable --now gg-runner-up
```

## What happens on the first start

The runner registers, makes one heartbeat, and asks what that answer carried.
If the control plane is offering relays or labels, it writes them into
`config.json` and then composes its loop from what it just wrote — so a fresh
machine is configured before it claims any work.

If the control plane cannot be reached, it says so and starts on what is
already in force. Offers are an enhancement to bring-up, never a dependency of
it: a runner that would not start without one would turn a single control-plane
outage into a fleet that does not come back.

## What happens when the offer changes

The runner notices on an idle beat, says so, and exits. `Restart=always` brings
it back, and the next start takes the new values. Nothing is applied to a
running loop — labels and relays are read once when it is built — and it never
exits this way while holding a flight.

It only stops for an offer it would actually take. A key that repoints
something needs a person, so the runner logs the `gg config accept` line and
carries on rather than restarting for ever over a document it may not apply.

## Checking it

```sh
sudo -u gg gg config show      # every value, and which source answered
sudo -u gg gg config offered   # what the control plane is offering, if anything
journalctl -u gg-runner-up -f  # what it did about it
```
