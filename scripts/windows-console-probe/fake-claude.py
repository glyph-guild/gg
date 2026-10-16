"""A stand-in for Claude Code, so mux screenshots show no real session.

Sets a window title and reports its activity through `gg mux mark`, as the
real agent's hooks would, then shows a short example transcript.
"""
import json, os, subprocess, sys, time

GG = os.environ.get("GG_SCREENSHOT_GG", "gg")
COUNT = os.environ.get("GG_SCREENSHOT_COUNT", os.path.join(os.environ.get("TEMP", "."), "gg-screenshot-agents"))

AGENTS = [
    ("✳ Add retry to the webhook sender", "waiting", None, [
        "> Add retry with backoff to the webhook sender.",
        "",
        "  Read src/webhooks/sender.ts",
        "  Edited src/webhooks/sender.ts (+18 -4)",
        "  Ran npm test: 42 passed",
        "",
        "  Failed deliveries now retry after 1s, 2s and 4s, then give up and",
        "  record the failure.",
        "",
        "> ",
    ]),
    ("◐ Find why the nightly export fails", "working", None, [
        "> Why does the nightly export fail on Mondays?",
        "",
        "  Read jobs/export.ts",
        "  Searched for \"weekStart\"",
        "  Reading jobs/schedule.ts ...",
    ]),
    ("✳ Apply the session-table migration", "notification", {"notification_type": "permission_prompt"}, [
        "> Apply the session-table migration to the test database.",
        "",
        "  Wants to run: npm run migrate -- --env test",
        "  Allow? (y/n)",
    ]),
]

try:
    n = int(open(COUNT).read())
except (OSError, ValueError):
    n = 0
open(COUNT, "w").write(str(n + 1))
title, mark, payload, lines = AGENTS[n % len(AGENTS)]

sys.stdout.write(f"\x1b]0;{title}\x07\x1b[2J\x1b[H")
sys.stdout.write("\r\n".join(lines))
sys.stdout.flush()
subprocess.run([GG, "mux", "mark", mark],
               input=json.dumps(payload) if payload else "", text=True,
               stdout=subprocess.DEVNULL, stderr=subprocess.DEVNULL)
while True:
    time.sleep(60)
