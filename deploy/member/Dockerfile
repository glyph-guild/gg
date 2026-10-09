# A pool member for the dev pool: gg, git, node and the agent, and no browser.
#
# IT LIVES HERE SO ITS VERSION CANNOT GO STALE. This recipe was in
# gg-airspace, where nothing held `ARG GG_VERSION` to anything - and it sat on
# gg 0.37.0 while every host ran 0.67.0, thirty releases back, on a runner
# that would have silently collapsed a plan's legs (gg#768). Moved to 0.67.0
# by hand, and three releases later it was stale again.
#
# TheHostRunbooksNameWhatExistsTests.A_pin_in_a_runbook_is_the_version_being_
# released now reads this file and fails this repository's build if the
# version below is not the one being cut. That is the whole point of the
# move: the release moves it, and a ratchet catches whoever forgets.
#
# The dev strategy names it as `gg:deploy/member`, which needs `gg` in the
# tenant registry and a `github` host on the maintainer that builds it.
# gg-member: the dev pool's member image, built by the pool's own maintainer.
#
# The dev strategy names this directory as its recipe (build: in
# airspace/strategies/dev.yaml), so a change here builds nothing until
# somebody asks - `gg strategy build dev` - or the strategy's build: moves.
# What a build pushed comes back as the strategy's next version, with
# built-from naming this repository, this path and the commit it was built at.
#
# This directory is the whole build context, and nothing else in the
# repository reaches the build. It was /opt/member-image on vmlinux001 until
# slice forty-one.
FROM ubuntu:24.04
# git: the runner shells out to it. libicu: gg segfaults at startup without it.
# xz-utils: the Node tarball below.
RUN apt-get update && apt-get install -y --no-install-recommends git ca-certificates libicu74 curl xz-utils \
    && rm -rf /var/lib/apt/lists/*
# gg from the release the fleet runs, whole - the tarball carries native
# side-cars beside the binary, and copying gg alone drops them. Same pattern as
# gg/deploy/member-browser/Dockerfile.
ARG GG_VERSION=0.88.10
RUN curl -fsSL "https://github.com/glyph-guild/gg/releases/download/v${GG_VERSION}/gg-linux-x64.tar.gz" -o /tmp/gg.tar.gz \
 && tar xz --no-same-owner -C /usr/local/bin -f /tmp/gg.tar.gz \
 && rm /tmp/gg.tar.gz && chmod +x /usr/local/bin/gg && /usr/local/bin/gg --version
# THE CONNECTOR A PREVIEW IS SERVED THROUGH, baked for the same reason gg is.
# CloudflaredConnector runs this binary by name and reports its absence rather
# than throwing, so a member without it provisions perfectly, flies perfectly,
# and publishes no preview - with a diagnosis nobody reads until somebody asks
# where the URL went.
#
# GG-268 downloaded it instead, inside the flight, and read a trycloudflare.com
# URL out of its own output. That is an address no identity provider was ever
# told about, and the preview showed a login page that could not log anybody in.
# Pinned, like everything else here: a connector that moved under a deploy is a
# preview that stops working for a reason nothing in this repository changed.
ARG CLOUDFLARED_VERSION=2026.9.1
RUN curl -fsSL "https://github.com/cloudflare/cloudflared/releases/download/${CLOUDFLARED_VERSION}/cloudflared-linux-amd64" -o /usr/local/bin/cloudflared \
 && chmod +x /usr/local/bin/cloudflared && cloudflared --version
# Node, for the agent: the version the ui members run (their base image ships
# it), from nodejs.org with its published checksum checked.
ARG NODE_VERSION=22.23.2
RUN cd /tmp \
 && curl -fsSLO "https://nodejs.org/dist/v${NODE_VERSION}/node-v${NODE_VERSION}-linux-x64.tar.xz" \
 && curl -fsSLO "https://nodejs.org/dist/v${NODE_VERSION}/SHASUMS256.txt" \
 && grep " node-v${NODE_VERSION}-linux-x64.tar.xz\$" SHASUMS256.txt | sha256sum -c - \
 && tar -xJf "node-v${NODE_VERSION}-linux-x64.tar.xz" -C /usr/local --strip-components=1 --no-same-owner \
 && rm -f "node-v${NODE_VERSION}-linux-x64.tar.xz" SHASUMS256.txt \
 && node --version && npm --version
# The executor, installed as gg/deploy/member-browser/Dockerfile installs it -
# with no symlink of our own, which that file found replaces the good one npm
# writes with a dangling one. GG_EXECUTOR_BINARY is read once, from the
# environment, when the runner starts, so the image is the only place to say it.
RUN npm install -g @anthropic-ai/claude-code && claude --version
ENV GG_EXECUTOR_BINARY=/usr/local/bin/claude
ENV XDG_CONFIG_HOME=/root/.config
ENTRYPOINT ["/usr/local/bin/gg","runner","up"]
