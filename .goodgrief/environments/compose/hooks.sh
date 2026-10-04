#!/bin/sh
# One executable, invoked by gg as `hooks.sh <point>`.
#
# POSIX sh on purpose. `pwsh` is absent from the pool host AND from the member
# image, so a PowerShell hook would be five files nothing can run - slice 58
# rule 1. `bash` is not guaranteed either; everything here is sh.
#
# WHAT gg GUARANTEES, and what this script may therefore assume:
#
#   argv[1]           one of prepare | attach | sync | ready | detach
#   cwd               the root of the checkout (workspace.Trees[0].Path)
#   DOCKER_HOST       the GRANTED instance's socket. Never reassign it: a
#                     DOCKER_HOST arriving from the runner's own environment
#                     points at the pool host's daemon, which is the one thing
#                     the instance exists to keep this stack away from.
#   GG_PREVIEW_PORT   the port the preview connector forwards to, when the
#                     flight has an exposure. May be unset.
#   patience          10 minutes per point, after which gg kills it.
#
# WHAT gg READS BACK: an exit code from every point, and from `ready` also
# stdout - `ready=yes` or `ready=no`, plus any number of `key=value` lines.
# `url` is the one key gg interprets. A non-zero exit from `ready` is a THIRD
# outcome ("could not answer") and is never read as `ready=no`, so this script
# is careful to exit 0 whenever it has an answer at all.

set -eu

COMPOSE_FILE=".goodgrief/environments/compose/compose.yaml"
SERVICES="db web"

# ALL DIAGNOSTICS TO STDERR. `ready`'s stdout is a report that gg parses, and
# although its parser ignores lines it does not understand - compose announces
# its networks, docker reports pull progress - keeping the channel clean means a
# future reader of that output sees the answer and nothing else.
say() { printf '%s\n' "$*" >&2; }

compose() { docker compose -f "$COMPOSE_FILE" "$@"; }

port() { printf '%s' "${GG_PREVIEW_PORT:-18080}"; }

# The health of one service as the DAEMON judges it, which is the only place
# that can judge it. A hook runs host-side against a granted instance, so
# whether this process can reach a published port is a question about the
# instance's network namespace rather than about the stack being up - see the
# README. `docker inspect` asks the daemon, so it is right either way.
health() {
    id=$(compose ps -q "$1" 2>/dev/null || true)

    if [ -z "$id" ]; then
        printf 'absent'
        return 0
    fi

    # A CONTAINER WITHOUT A HEALTHCHECK has no .State.Health at all and the
    # template errors rather than printing empty, so both services declare one
    # and this still falls back to the run state instead of failing.
    docker inspect \
        --format '{{if .State.Health}}{{.State.Health.Status}}{{else}}{{.State.Status}}{{end}}' \
        "$id" 2>/dev/null || printf 'unknown'
}

case "${1:-}" in

prepare)
    # WARMTH IS THE IMAGE STORE AND NOTHING ELSE (ADR-0033's second amendment:
    # reclaim's volume prune is unfiltered, so no database state survives a
    # flight). So prepare's whole job is to have the images present before the
    # clock that matters starts - it may not seed data, because nothing it
    # seeded would still be there.
    say "prepare: pulling images into the granted instance"
    compose pull
    say "prepare: done"
    ;;

attach)
    # STARTS DETACHED, WAITS FOR HEALTH, EXITS 0 - slice 58 rule 3.
    #
    # `--wait` is exactly that contract: compose returns once every service
    # with a healthcheck reports healthy, and non-zero if one does not. No
    # process survives this hook; the instance's daemon owns what is running,
    # which is what keeps reclaim the single way an instance empties.
    say "attach: bringing the stack up on port $(port)"
    GG_PREVIEW_PORT="$(port)" compose up -d --wait --wait-timeout 300
    say "attach: up, and nothing is left running as a child of this hook"
    ;;

sync)
    # THIS ENVIRONMENT IS `shared`, so gg never invokes this point: it invokes a
    # sync hook only when the declared filesystem relationship is not `shared`.
    # Reaching here means the declaration and this script disagree, and that is
    # worth a refusal rather than a silent success - rule 5 asks a shared
    # environment to write no sync hook at all, and the next best thing is one
    # that says why it should not have been called.
    say "sync: refused - this environment declares 'filesystem: shared', so the"
    say "sync: worker and the stack see one tree and there is nothing to copy."
    say "sync: if that is no longer true, change the declaration first."
    exit 64
    ;;

ready)
    # ANSWERS, rather than merely exiting - rule 4. Both `yes` and `no` are
    # answers and both exit 0. A non-zero exit from here means this script could
    # not find out, which gg keeps as its own third outcome.
    waiting=""

    # EACH SERVICE IS ASKED ONCE and the answer is kept. Asking twice - once to
    # decide and once to report - lets the two reads disagree, and then the
    # report contradicts the verdict printed beside it.
    values=""

    for service in $SERVICES; do
        state=$(health "$service")
        values="$values$service=$state
"

        # THE FIRST ONE, not the last. `$SERVICES` is in dependency order, so
        # the first service that is not up is the one holding the rest back -
        # which is the one a person reading `waiting=` wants named.
        if [ "$state" != "healthy" ] && [ "$state" != "running" ] && [ -z "$waiting" ]; then
            waiting="$service"
        fi
    done

    say "ready: observed $(printf '%s' "$values" | tr '\n' ' ')"

    # THE NAMED VALUES. Anything beyond `url` is carried and shown to a person
    # and interpreted by nothing, which is what makes it safe to report the
    # per-service states: they cost nothing and they are what somebody reading a
    # not-ready answer actually wants.
    printf '%s' "$values"

    if [ -n "$waiting" ]; then
        printf 'ready=no\n'
        printf 'waiting=%s\n' "$waiting"
        exit 0
    fi

    printf 'ready=yes\n'

    # `url` IS THE ONE KEY gg INTERPRETS, and the address is the port gg handed
    # us rather than one this stack chose. gg does not forward to this string -
    # `preview.url` is gg's own public address out of its own inventory - so
    # this is the local address a person on the host can open, and the honest
    # thing for it to name is where we were told to bind.
    printf 'url=http://127.0.0.1:%s/\n' "$(port)"
    ;;

detach)
    # LEAVES NOTHING. Reclaim would destroy the instance anyway, but a detach
    # that tidies up is what lets this stack be brought up twice in one flight
    # without the second `attach` colliding with the first stack's containers.
    say "detach: taking the stack down"
    compose down --remove-orphans --volumes
    say "detach: done"
    ;;

*)
    # REFUSED BY NAME, naming what was expected - the disposition every closed
    # vocabulary in gg uses, because a caller told only that something was wrong
    # has to guess. `warm` and `verify` are listed because they are the two
    # names that were renamed and are the two somebody will type.
    say "unknown point '${1:-}' - expected one of: prepare attach sync ready detach"
    say "(warm was renamed prepare, and verify was renamed ready)"
    exit 64
    ;;

esac
