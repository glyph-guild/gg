# A pool member that can drive a browser.
#
# THE IMAGE IS THE ENVIRONMENT. A member is created from this and nothing else:
# DockerPoolAdapter writes an image, a label and three environment variables,
# and no HostConfig at all. So everything this member needs to be - gg, git, the
# agent binary, node, Playwright, Chromium, and every setting - has to be here.
#
# AND IT MUST BE A GG MEMBER FIRST. A bare browser image would start, advertise
# nothing, register with nobody, and become another of the dead members
# PoolPlanner's remarks are scarred by. The entrypoint is what makes it a
# member; the browser is what makes it this one.
ARG PLAYWRIGHT_BASE=apify/actor-node-playwright-chrome:22
FROM ${PLAYWRIGHT_BASE}

USER root

# GIT IS NOT OPTIONAL. Gg.Runner/Vcs/GitInvocation.cs and EnvironmentSurvey both
# shell out to it, so a member without it provisions perfectly and fails on the
# first flight - which is the pool host's README saying the same thing about the
# host.
# AND NEITHER IS ICU, which is the one nobody had written down. `gg` runs
# Terminal.Gui's module initializer at startup - in EVERY invocation, including
# `gg runner up` and even `gg --version`, because a module initializer runs
# before Main and does not care which verb was asked for. It builds a
# CultureInfo, and without ICU that is a segfault at startup: exit 139, before
# a single line of gg's own code.
#
# Measured: the first build of this file died exactly there.
#   at System.Globalization.CultureData.GetCultureData(String, Boolean)
#   at Terminal.Gui.Configuration.TuiConfigurationBuilder..cctor()
#   at Terminal.Gui.ModuleInitializers.InitializeConfiguration()
RUN apt-get update \
 && apt-get install -y --no-install-recommends git ca-certificates curl libicu-dev \
 && rm -rf /var/lib/apt/lists/*

# gg itself, from the release the fleet runs. Native AOT, linux-x64.
ARG GG_VERSION=0.56.0
RUN curl -fsSL \
      "https://github.com/glyph-guild/gg/releases/download/v${GG_VERSION}/gg-linux-x64.tar.gz" \
      -o /tmp/gg.tar.gz \
 && tar xz --no-same-owner -C /usr/local/bin -f /tmp/gg.tar.gz \
 && rm /tmp/gg.tar.gz \
 && chmod +x /usr/local/bin/gg \
 && /usr/local/bin/gg --version

# THE CONNECTOR A PREVIEW IS SERVED THROUGH. CloudflaredConnector runs this
# binary by name and reports its absence rather than throwing - so a member
# without it provisions perfectly, flies perfectly, and publishes no preview
# with a diagnosis nobody reads until somebody asks where the URL went.
#
# BAKED, BECAUSE THE FIRST ONE DOWNLOADED IT. GG-268's work kind told the agent
# to fetch cloudflared itself and read a trycloudflare.com URL out of its own
# output; that is an address no identity provider was ever told about, and the
# preview showed a login page that could not log anybody in. A binary the image
# carries is what lets the runner dial a name the tenant registered.
#
# Pinned like gg beside it. A connector that moved under a deploy is a preview
# that stops working for a reason nothing in this repository changed.
ARG CLOUDFLARED_VERSION=2026.9.1
RUN curl -fsSL \
      "https://github.com/cloudflare/cloudflared/releases/download/${CLOUDFLARED_VERSION}/cloudflared-linux-amd64" \
      -o /usr/local/bin/cloudflared \
 && chmod +x /usr/local/bin/cloudflared \
 && cloudflared --version

# The executor. GG_EXECUTOR_BINARY IS NOT OFFERABLE - "a control plane naming a
# program a machine executes is not configuration" - and MemberSpec writes only
# three variables, none of them this one. So the image is the only place it can
# be said.
#
# NO SYMLINK OF OUR OWN. The first version of this file added one, and it
# replaced the good symlink npm had already written with a dangling one - so
# `find` showed /usr/local/bin/claude existing while `sh` said "not found".
# npm's global install puts the binary on the PATH; there is nothing to help it
# with.
RUN npm install -g @anthropic-ai/claude-code \
 && claude --version

ENV GG_EXECUTOR_BINARY=/usr/local/bin/claude
ENV XDG_CONFIG_HOME=/root/.config

# SO AN AGENT CAN WRITE ORDINARY PLAYWRIGHT CODE. The base image installs
# Playwright under its own user (/home/myuser/node_modules) and gg runs as root,
# so `require("playwright")` resolves from nowhere the agent would work in.
# NODE_PATH is what joins them, and with it a naive
#   const { chromium } = require("playwright"); await chromium.launch();
# works with no arguments - measured, in this image. Without it the agent has to
# know this base image's layout, which is a thing no prompt should have to say.
ENV NODE_PATH=/home/myuser/node_modules

# CHROMIUM IN A CONTAINER, and the good news is written down rather than
# assumed. --shm-size and --ipc=host live in HostConfig, which
# DockerPoolAdapter deliberately writes nothing into (asserted byte-for-byte by
# MemberIsARunnerTests) - so if the browser needed them, a member could not have
# them. Measured: it does not. Chromium launches and screenshots in this image
# with no HostConfig and no launch arguments at all, because the base image
# already ships a browser built for a container (PLAYWRIGHT_BROWSERS_PATH
# /pw-browsers, and a Chrome at /usr/bin/google-chrome).
#
# Kept as the base image's own convention, which its tooling reads and plain
# Playwright ignores.
ENV PLAYWRIGHT_CHROMIUM_ARGS="--disable-dev-shm-usage --no-sandbox"

# THE ENTRYPOINT IS WHAT MAKES IT A MEMBER. `gg runner up` finds
# GG_MEMBER_NONCE in its environment, redeems it for a runner identity whose
# labels came from the strategy, and starts claiming.
ENTRYPOINT ["/usr/local/bin/gg", "runner", "up"]
