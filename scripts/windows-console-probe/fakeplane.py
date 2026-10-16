"""A fake Good Grief control plane with invented example data, for console screenshots.

usage: fakeplane.py <port>
Every name, flight and repository here is made up.
"""
import json, sys, uuid
from http.server import BaseHTTPRequestHandler, ThreadingHTTPServer
from urllib.parse import urlparse, parse_qs

import datetime as _dt

_NOW = _dt.datetime.now(_dt.timezone.utc).replace(second=0, microsecond=0)


class _Clock:
    """Times are written as hh:mm on a notional morning; 09:40 maps to two minutes ago."""

    @staticmethod
    def format(at):
        h, m = map(int, at.split(":"))
        moment = _NOW + _dt.timedelta(minutes=(h * 60 + m) - (9 * 60 + 40) - 2)
        return moment.strftime("%Y-%m-%dT%H:%M:%SZ")


T = _Clock()
ME = "Ada Lovelace"


def fid(n):
    return str(uuid.UUID(int=0x6F1C2A10_0000_0000_0000_000000000000 + n))


def flight(n, name, state, stage, kind, intent, at, attempts=1, labels=None):
    f = {
        "flightId": fid(n), "flightNumber": f"GG-{n}", "name": name, "intent": intent,
        "createdAt": T.format(at), "runnerProtocolVersion": 1, "factVocabularyVersion": "0.44.0",
        "constitutionVersion": "1.0.0", "envelopeVersion": "v9", "attempts": attempts, "facts": [],
        "workKind": kind, "state": state, "stage": stage,
    }
    if labels:
        f["requiredLabels"] = labels
    return f


def text(t):
    return {"kind": "text", "text": t}


def ticket(i):
    return {"kind": "ticket", "provider": "ado", "id": str(i)}


FLIGHTS = [
    flight(48, "Add retry with backoff to the webhook sender", "open", "evaluated", "implement",
           ticket(1287), "09:12", labels=["environment=dev"]),
    flight(47, "Why does login sign people out after an hour?", "open", "leased", "investigate",
           text("Why does login sign people out after an hour?"), "09:05"),
    flight(46, "Review: move session settings into config", "open", "ready", "review",
           {"kind": "uri", "uri": "https://git.example.com/example-org/web-app/pull/311"}, "08:58"),
    flight(45, "Score backlog items for readiness", "landed", "ended", "score",
           ticket(1279), "08:30"),
    flight(44, "Explain the nightly export failure", "landed", "ended", "investigate",
           ticket(1266), "07:41"),
    flight(43, "Update the README install steps", "landed", "ended", "implement",
           text("Update the README install steps"), "07:02"),
    flight(42, "Upgrade the date library", "failed", "ended", "implement", ticket(1251), "06:20", attempts=2),
    flight(41, "Rename the billing module", "grounded", "ended", "implement", ticket(1240), "05:55"),
]
BY_NUMBER = {f["flightNumber"]: f for f in FLIGHTS}

GATES = [{
    "flightNumber": "GG-48", "obligationId": "code-review", "approver": "reviewers",
    "manifestHash": "sha256:4be1c0", "because": "the change touches src/webhooks/**",
    "awaitingSince": T.format("09:31"), "attempt": 1,
    "branch": "gg/GG-48", "commit": "3f2c1e9", "condition": "change.manifest touches src/webhooks/**",
}]

STORIES = {
    "GG-48": [
        ("09:12", "created", [ME], {"kind": "person", "name": ME}),
        ("09:12", "lease-granted", ["dev-1"], None),
        ("09:13", "lease-renewed", [], None),
        ("09:29", "loop-ran", ["completed", "implement"], None),
        ("09:30", "evaluated", ["3", "2"], None),
        ("09:31", "decision-asked", ["code-review", "reviewers"], None),
        ("09:31", "lease-released", ["outstanding"], None),
    ],
    "GG-47": [
        ("09:05", "created", [ME], {"kind": "person", "name": ME}),
        ("09:05", "lease-granted", ["dev-2"], None),
        ("09:06", "lease-renewed", [], None),
    ],
    "GG-44": [
        ("07:41", "created", [ME], {"kind": "person", "name": ME}),
        ("07:41", "lease-granted", ["dev-1"], None),
        ("07:52", "loop-ran", ["completed", "investigate"], None),
        ("07:52", "lease-released", ["completed"], None),
        ("07:52", "ended", ["landed"], None),
    ],
}

RUNNERS = [
    {"runnerId": "r-dev-1", "label": "dev-1", "state": "idle", "lastHeartbeatAt": T.format("09:40"),
     "registeredBy": ME, "machine": "dev-1", "labels": [{"name": "environment=dev", "disposition": "stated"}],
     "ownership": "tenant"},
    {"runnerId": "r-dev-2", "label": "dev-2", "state": "busy", "currentFlightId": fid(47),
     "currentFlightNumber": "GG-47", "lastHeartbeatAt": T.format("09:40"), "registeredBy": ME,
     "machine": "dev-2", "labels": [{"name": "environment=dev", "disposition": "stated"}], "ownership": "tenant"},
    {"runnerId": "r-ui-1", "label": "ui-1", "state": "idle", "lastHeartbeatAt": T.format("09:39"),
     "registeredBy": "Grace Hopper", "machine": "ui-1",
     "labels": [{"name": "environment=ui", "disposition": "stated"}], "ownership": "tenant"},
    {"runnerId": "r-ada", "label": "ada-laptop", "state": "offline", "lastHeartbeatAt": T.format("06:02"),
     "registeredBy": ME, "machine": "ada-laptop", "ownership": "claimed", "owner": ME},
]


def nomination(n, subject, kind, mode, state, at, reason, **extra):
    d = {"nominationId": {1: "5d0e7b21-8c4a-4f2e-9b1d-0a6c3e5f7a90", 2: "9c41f3a8-2b6d-4e71-a0f5-7d3e1b8c6a24", 3: "e27a9d05-6f3b-48c2-b1e4-c95a0d7f3b16"}[n], "nominator": "triage",
         "subject": subject, "version": "1", "workKind": kind, "mode": mode, "state": state,
         "madeAt": T.format(at), "reason": reason}
    d.update(extra)
    return d


NOMINATIONS = [
    nomination(3, "ado#1291", "investigate", "gated", "standing", "09:20",
               "New bug report with a stack trace from the export job."),
    nomination(2, "ado#1290", "score", "gated", "standing", "09:20",
               "New backlog item has no readiness score yet."),
]
ENDED = [
    nomination(1, "ado#1266", "investigate", "gated", "ended", "07:30",
               "Nightly export failed twice this week.", ending="opened", because="Worth a look before Friday.",
               flightId=fid(44), flightNumber="GG-44", endedAt=T.format("07:41")),
]

TOPOLOGY = {"names": [
    {"name": "root", "role": "root", "declaredBy": ME, "declaredAt": T.format("01:00")},
    {"name": "implement", "role": "work-kind", "parent": "root", "declaredBy": ME, "declaredAt": T.format("01:00")},
    {"name": "investigate", "role": "work-kind", "parent": "root", "declaredBy": ME, "declaredAt": T.format("01:00")},
    {"name": "review", "role": "work-kind", "parent": "root", "declaredBy": ME, "declaredAt": T.format("01:00")},
    {"name": "score", "role": "work-kind", "parent": "root", "declaredBy": ME, "declaredAt": T.format("01:00")},
    {"name": "triage", "role": "watch", "parent": "root", "declaredBy": ME, "declaredAt": T.format("01:00")},
]}

REPOSITORIES = {"repositories": [
    {"name": "web-app", "provider": "github", "id": "example-org/web-app", "path": "example-org/web-app",
     "credential": "required", "registeredBy": ME, "registeredAt": T.format("01:00")},
    {"name": "api", "provider": "github", "id": "example-org/api", "path": "example-org/api",
     "credential": "required", "registeredBy": ME, "registeredAt": T.format("01:00")},
]}


def story(number):
    f = BY_NUMBER.get(number) or FLIGHTS[0]
    entries = []
    for at, kind, params, actor in STORIES.get(number, [("07:00", "created", [ME], {"kind": "person", "name": ME})]):
        e = {"at": T.format(at), "kind": kind, "params": params}
        if actor:
            e["actor"] = actor
        entries.append(e)
    return {"flightId": f["flightId"], "flightNumber": f["flightNumber"], "stage": f["stage"],
            "state": f["state"], "workKind": f["workKind"], "forDisplay": ME, "entries": entries}


def why(number):
    obligations = [
        {"obligationId": "in-scope", "attachment": "attached", "outcome": "satisfied",
         "because": "the flight changed only files the envelope permits"},
        {"obligationId": "loop-finished", "attachment": "attached", "outcome": "satisfied",
         "because": "the agent finished within its budget"},
    ]
    if number == "GG-48":
        obligations.append({"obligationId": "code-review", "attachment": "attached",
                            "condition": "change.manifest touches src/webhooks/**",
                            "because": "the change touches src/webhooks/sender.ts",
                            "diagnosis": "This obligation is decided by a person. reviewers may decide it."})
    return {"flightNumber": number, "envelopeVersion": "v9", "obligations": obligations}


def body(path, query):
    q = {k: v[0] for k, v in parse_qs(query).items()}
    parts = path.split("/")
    if path.startswith("/v1/flights/") and len(parts) == 5:
        ref, leaf = parts[3], parts[4]
        number = ref if ref.startswith("GG-") else next(
            (f["flightNumber"] for f in FLIGHTS if f["flightId"] == ref), "GG-48")
        if leaf == "log":
            return {"flightId": BY_NUMBER[number]["flightId"], "flightNumber": number, "entries": [
                {"at": T.format(at), "kind": kind, "detail": " ".join(params)}
                for at, kind, params, _ in STORIES.get(number, [])]}
        if leaf == "story":
            return story(number)
        if leaf == "why":
            return why(number)
        if leaf == "seed":
            return {"revision": 1, "flightNumber": number, "flightId": BY_NUMBER[number]["flightId"],
                    "measurements": {"filesEdited": ["src/webhooks/sender.ts", "src/webhooks/sender.test.ts"],
                                     "filesReadNotEdited": ["src/webhooks/queue.ts"], "searches": ["retry"],
                                     "errors": [], "undeclaredMovesUsed": [], "attempts": 1,
                                     "stopReason": "completed"},
                    "accountState": "none", "transcriptState": "none"}
        if leaf == "facts":
            return None
    if path.startswith("/v1/runners/") and path.endswith("/flights"):
        return {"flights": [{"flightId": fid(44), "number": 44, "kind": "investigate", "state": "landed",
                             "claimedAt": T.format("07:41"), "endedAt": T.format("07:52")}]}
    routes = {
        "/v1/flights": lambda: {"flights": FLIGHTS if q.get("all") == "true" else
                                [f for f in FLIGHTS if f["state"] == "open"], "next": None},
        "/v1/runners": lambda: {"runners": RUNNERS},
        "/v1/gates": lambda: {"gates": GATES},
        "/v1/credentials": lambda: {"credentials": []},
        "/v1/auth/whoami": lambda: {"principalId": "p-ada", "principalDisplay": ME, "tenantId": "t-example",
                                    "tenantDisplay": "Example Org", "expiresAt": "2099-01-01T00:00:00Z",
                                    "notices": [], "isAdmin": True, "subject": "github:ada"},
        "/v1/allowances": lambda: {"allowances": []},
        "/v1/board": lambda: {"nominations": NOMINATIONS + (ENDED if q.get("ended") == "true" else []),
                              "includedEnded": q.get("ended") == "true", "next": None},
        "/v1/airspace/watch-standings": lambda: {"standings": [
            {"name": "triage", "version": "3", "window": "24h", "lastHeardAt": T.format("09:20"),
             "outcome": "swept", "nominated": 2, "opened": 1, "executor": "resident-runner"}], "next": None},
        "/v1/environments": lambda: {"environments": []},
        "/v1/airspace/strategies": lambda: {"strategies": []},
        "/v1/pools": lambda: {"pools": []},
        "/v1/airspace/topology": lambda: TOPOLOGY,
        "/v1/airspace/repositories": lambda: REPOSITORIES,
        "/v1/airspace/envelopes": lambda: {"documents": []},
        "/v1/airspace/watches": lambda: {"watches": []},
        "/v1/airspace/fleet": lambda: {"profiles": []},
        "/v1/airspace/exposures": lambda: {"exposures": []},
        "/v1/auth/keys": lambda: {"keys": []},
    }
    return routes[path]() if path in routes else None


class Handler(BaseHTTPRequestHandler):
    protocol_version = "HTTP/1.1"
    def log_message(self, *args):
        sys.stderr.write("%s %s\n" % (self.command, self.path))

    def _send(self, status, payload=None):
        data = b"" if payload is None else json.dumps(payload).encode()
        self.send_response(status)
        if payload is not None:
            self.send_header("Content-Type", "application/json")
        self.send_header("Content-Length", str(len(data)))
        self.end_headers()
        self.wfile.write(data)

    def do_GET(self):
        url = urlparse(self.path)
        if url.path == "/v1/configuration/offered":
            return self._send(204)
        try:
            payload = body(url.path, url.query)
        except Exception as e:  # noqa: BLE001 - a fake should say what broke
            sys.stderr.write(f"error on {url.path}: {e}\n")
            return self._send(500)
        if payload is None:
            return self._send(404)
        self._send(200, payload)

    def do_POST(self):
        self._send(404)


if __name__ == "__main__":
    class Server(ThreadingHTTPServer):
        request_queue_size = 256
        daemon_threads = True

    Server(("127.0.0.1", int(sys.argv[1])), Handler).serve_forever()
