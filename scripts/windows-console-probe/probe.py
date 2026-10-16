"""Open the gg console on Windows inside a ConPTY and record what it draws.

usage: probe.py <path to gg.exe> <output directory>

This is a probe, not a test. It answers one question - what does the console
do on Windows today - so it records every screen and every exit, and fails
only when the probe itself cannot run. Whatever gg does, the result is the
evidence, and it is written to the output directory and the job summary.

gg runs against fakeplane.py, a fake control plane serving invented example
data, with a throwaway home directory per scenario, so nothing here reads or
writes a real organisation's data.

Needs Python 3, pyte==0.8.2 and pywinpty.
"""
import json, os, queue, re, shutil, socket, subprocess, sys, tempfile, threading, time, traceback

import pyte
from winpty import PtyProcess

HERE = os.path.dirname(os.path.abspath(__file__))
ROWS, COLS = 34, 140

# THE QUESTIONS A TERMINAL IS ASKED, and what a real one answers. Terminal.Gui's
# ANSI driver paints nothing until these come back, and ConPTY asks for the
# cursor position itself when it starts. Answering them costs nothing if
# nobody asks.
QUERIES = [
    (re.compile(r"\x1b\[6n"), lambda r, c: "\x1b[1;1R"),
    (re.compile(r"\x1b\[0?c"), lambda r, c: "\x1b[?62;1;2;6;7;8;9;15;18;21;22c"),
    (re.compile(r"\x1b\[18t"), lambda r, c: f"\x1b[8;{r};{c}t"),
    (re.compile(r"\x1b\]10;\?(\x07|\x1b\\)"), lambda r, c: "\x1b]10;rgb:e6e6/e6e6/e6e6\x1b\\"),
    (re.compile(r"\x1b\]11;\?(\x07|\x1b\\)"), lambda r, c: "\x1b]11;rgb:1e1e/1e1e/2424\x1b\\"),
    (re.compile(r"\x1b\[\?u"), lambda r, c: "\x1b[?0u"),
]

SESSION = {
    "SessionToken": "example",
    "ExpiresAt": "2099-01-01T00:00:00+00:00",
    "TenantId": "00000000-0000-0000-0000-000000000001",
    "PrincipalDisplay": "Ada Lovelace",
}


def free_port():
    with socket.socket() as s:
        s.bind(("127.0.0.1", 0))
        return s.getsockname()[1]


def home(root, port, signed_in):
    """A throwaway home. Every XDG variable is honoured on Windows too."""
    for d in ("config/good-grief", "state", "cache", "profile"):
        os.makedirs(os.path.join(root, d), exist_ok=True)
    if signed_in:
        with open(os.path.join(root, "config", "good-grief", "session.json"), "w") as f:
            json.dump(SESSION, f)
    return {
        "XDG_CONFIG_HOME": os.path.join(root, "config"),
        "XDG_STATE_HOME": os.path.join(root, "state"),
        "XDG_CACHE_HOME": os.path.join(root, "cache"),
        "GG_CONTROL_PLANE": f"http://127.0.0.1:{port}",
    }


class Console:
    """One gg process in a ConPTY, its output replayed into a screen."""

    def __init__(self, gg, env, cwd, raw_path):
        self.screen = pyte.Screen(COLS, ROWS)
        self.stream = pyte.Stream(self.screen)
        self.raw = open(raw_path, "w", encoding="utf-8", newline="")
        self.chunks = queue.Queue()
        self.ended = threading.Event()
        full_env = dict(os.environ)
        full_env.update(env)
        full_env["TERM"] = "xterm-256color"
        full_env["COLORTERM"] = "truecolor"
        self.proc = PtyProcess.spawn([gg], cwd=cwd, env=full_env, dimensions=(ROWS, COLS))
        threading.Thread(target=self._read, daemon=True).start()

    def _read(self):
        # READ ON ITS OWN THREAD, because pywinpty's read blocks until there is
        # output, and a console that has drawn its screen and is waiting for a
        # key has no more output to give.
        try:
            while True:
                data = self.proc.read(65536)
                if data:
                    self.chunks.put(data)
        except (EOFError, OSError):
            pass
        finally:
            self.ended.set()

    def pump(self, seconds):
        end = time.time() + seconds
        while time.time() < end:
            try:
                data = self.chunks.get(timeout=0.05)
            except queue.Empty:
                if self.ended.is_set() and self.chunks.empty():
                    return False
                continue
            self.raw.write(data)
            for pattern, answer in QUERIES:
                for _ in pattern.finditer(data):
                    self.proc.write(answer(*self.size()))
            self.stream.feed(data)
        return True

    def size(self):
        return self.screen.lines, self.screen.columns

    def key(self, text):
        self.proc.write(text)

    def resize(self, rows, cols):
        self.proc.setwinsize(rows, cols)
        self.screen.resize(rows, cols)

    def text(self):
        return "\n".join(line.rstrip() for line in self.screen.display)

    def alive(self):
        return self.proc.isalive()

    def close(self):
        try:
            if self.proc.isalive():
                self.proc.write("\x03")
                self.pump(1)
            if self.proc.isalive():
                self.proc.terminate(force=True)
        except Exception:  # noqa: BLE001 - closing a console that may already be gone
            pass
        self.pump(0.5)
        self.raw.close()


def run(name, scenario, gg, port, out, work, log):
    root = os.path.join(work, name)
    env = home(root, port, scenario.get("signedIn", False))
    env.update({k: v.replace("{here}", HERE).replace("{gg}", gg).replace("{root}", root)
                for k, v in scenario.get("env", {}).items()})
    cwd = os.path.join(root, "profile")
    console = Console(gg, env, cwd, os.path.join(out, f"{name}.raw.txt"))
    record = {"scenario": name, "screens": [], "exited": None}
    try:
        for step in scenario["steps"]:
            kind, arg = step[0], step[1]
            if kind == "wait":
                if not console.pump(float(arg)):
                    break
            elif kind == "key":
                console.key(arg)
                if not console.pump(float(step[2]) if len(step) > 2 else 1.0):
                    break
            elif kind == "resize":
                console.resize(int(arg), int(step[2]))
                if not console.pump(float(step[3]) if len(step) > 3 else 2.0):
                    break
            elif kind == "shot":
                text = console.text()
                with open(os.path.join(out, f"{name}.{arg}.txt"), "w", encoding="utf-8") as f:
                    f.write(text)
                record["screens"].append({"name": arg, "text": text})
                log(f"{name}: saved {arg}")
    finally:
        if not console.alive():
            # WHAT IT SAID AS IT DIED. An unhandled exception prints after the
            # console leaves the alternate screen, so the last screen holds it.
            record["exited"] = console.proc.exitstatus
            record["screens"].append({"name": "after-exit", "text": console.text()})
            log(f"{name}: gg exited with {console.proc.exitstatus}")
        console.close()
    return record


def summary(records, path):
    lines = ["# gg console on Windows", ""]
    for r in records:
        state = "still running at the end" if r["exited"] is None else f"exited with {r['exited']}"
        lines.append(f"## {r['scenario']} ({state})")
        if r.get("error"):
            lines += ["", "Probe error:", "", "```", r["error"], "```"]
        for s in r["screens"]:
            lines += ["", f"### {s['name']}", "", "```", s["text"] or "(blank)", "```"]
        lines.append("")
    with open(path, "a", encoding="utf-8") as f:
        f.write("\n".join(lines) + "\n")


def main():
    gg = os.path.abspath(sys.argv[1])
    out = os.path.abspath(sys.argv[2])
    os.makedirs(out, exist_ok=True)
    scenarios = json.load(open(os.path.join(HERE, "scenarios.json"), encoding="utf-8"))
    work = tempfile.mkdtemp(prefix="gg-probe-")
    port = free_port()
    plane_log = open(os.path.join(out, "fakeplane.log"), "w")
    plane = subprocess.Popen([sys.executable, os.path.join(HERE, "fakeplane.py"), str(port)],
                             stderr=plane_log)
    time.sleep(1.5)

    def log(message):
        print(message, flush=True)

    records = []
    try:
        for name, scenario in scenarios.items():
            log(f"--- {name}")
            try:
                records.append(run(name, scenario, gg, port, out, work, log))
            except Exception:  # noqa: BLE001 - one broken scenario must not hide the rest
                records.append({"scenario": name, "screens": [], "exited": None,
                                "error": traceback.format_exc()})
                log(traceback.format_exc())
    finally:
        plane.kill()
        shutil.rmtree(work, ignore_errors=True)

    with open(os.path.join(out, "probe.json"), "w", encoding="utf-8") as f:
        json.dump(records, f, indent=2)
    summary(records, os.path.join(out, "summary.md"))
    if os.environ.get("GITHUB_STEP_SUMMARY"):
        summary(records, os.environ["GITHUB_STEP_SUMMARY"])

    # THE PROBE FAILS ONLY WHEN IT COULD NOT LOOK. A console that crashed is a
    # finding, recorded above; a scenario the probe could not run is not.
    if any(r.get("error") for r in records):
        sys.exit(1)


if __name__ == "__main__":
    main()
