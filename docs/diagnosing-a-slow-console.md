# Diagnosing a slow console

The console can be asked where its time went. Set `GG_TIMING` to a path and it
appends one line per phase — what it was, how long it took, and how many calls
it made where that is what explains the duration.

```sh
GG_TIMING=/tmp/gg-timing.txt gg
```

Reproduce whatever felt slow, quit, and read the file. It is written **as it
goes** rather than on exit, so a console you had to kill still says where it
was.

Off unless asked: with the variable unset nothing is measured, formatted or
written. It is listed on `gg config show` beside `GG_STATE_DUMP`.

## What the lines say

```
06:12:41.118  boot.round-one                 1426.4ms  reads=11
06:12:43.151  boot.logs                      2033.1ms  reads=11
06:12:43.152  read.log                        110.2ms
06:12:46.690  boot                           7049.7ms
06:12:47.028  http GET /v1/board              383.1ms
06:12:47.140  refresh.Board                   275.6ms
06:12:47.142  render.Board                      1.3ms
06:12:48.180  paint.period                   1038.0ms
06:12:48.181  pool.workers-free                 32766
```

| prefix | what it measures |
| --- | --- |
| `boot`, `boot.*` | the first load, and each round of reads inside it |
| `refresh.<tab>`, `refresh.lists`, `refresh.logs` | a tab's reads, thirty seconds apart |
| `http <METHOD> <path>` | every request, through one handler — the path, never the query |
| `read.log` | one flight's log, so a straggler can be told from a slow batch |
| `render.<tab>`, `paint.*`, `board.*` | building and assigning what a tab shows |
| `paint.period` | **start of one paint to the start of the next** |
| `input.<Command>`, `input.row-pointed` | a key or a click, to the console having acted |
| `pool.*`, `mem.mb`, `gc.gen2` | the runtime at that paint |

## Reading it

**Compare a phase against its `reads`.** Three hundred milliseconds for three
calls and three hundred for twenty are different findings with different fixes.

**Look at `paint.period` when everything else looks fast.** Every other number
is the duration of something the console chose to wrap; `paint.period` is the
gap between paints, so whatever is holding the loop up lands there even though
nothing measures it — Terminal.Gui's own drawing, which happens *after* the
console has finished deciding what to show, or a runtime with nothing free. A
period of seconds beside a two-millisecond `render` means the work is not the
problem.

**A phase's sum should match its parts.** When it does not, the time went
somewhere nothing measures yet, and that gap is the finding.

**One slow request can set a whole phase.** Every batch here is awaited
together, so ten requests at 110ms and one at 2s is a two-second phase. The
`read.log` lines are there to tell that apart from everything being slow.

## What it found the first time

`paint.flight-assign` at **1,973ms** against `paint.flight-build` at **32ms** —
handing the text to the pane cost sixty times what producing it did. The flight
pane is a `Label` filling its frame, and a Label does not scroll, so every line
past the bottom was already invisible and was being laid out anyway.

Three fixes came out of one rule — *do no work for something that is not on
screen* — and `render.Board` went from 42ms to 1.3ms.

Worth knowing before you theorise: on the same evidence, request count, tail
latency, HTTP version, IPv6 connect stalls and thread-pool starvation were all
proposed and all measured false. The file is quicker than the argument.
