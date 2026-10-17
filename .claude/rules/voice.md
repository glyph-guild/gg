# Voice: every word a person reads

Applies to help text, error and refusal messages, console text, problem-details
returned to clients, and the docs. The full guide, with before-and-after
examples, is https://docs.goodgrief.dev/contributing/voice/.

- Neutral, formal register. No "you"/"your", no contractions, active voice.
  Plain imperatives for instructions: "Run gg login again."
- Say what happened, then what to do. Every refusal ends in a command, a
  setting or a person to ask; with no next step, point at `gg bundle`.
  Doctor's Detail/Fix split is the model.
- Reasons only when they change what the reader does, one clause at most.
  Rationale belongs in docs and comments. No aphorisms or sayings in output.
- Plain words first. Never in user output: principal (person), tenant
  (organisation), nonce, mint, redeem, hold. Introduce a Good Grief term in
  brackets when it is needed: "waiting for a person to approve it (a gate)".
- Say it directly. Lead with what it means for the reader, not an
  abstraction to decode: "Anyone who opens the link can join", not "The link
  names no one". When the reader must act, use an imperative; avoiding "you"
  never means describing the reader as "the person who created it". Leave out
  what does not happen unless the reader would expect it. One idea per
  sentence.
- Never show internals: no type or class names (`GetType().Name`), exception
  text, rule ids, Article numbers or ADR/slice numbers.
- State facts without judgement. Do not presume why the reader did something.
- Sentences: capital letter, full stop. Name the exact argument, file or
  setting that was wrong. Say what was not changed when a command fails.
- Keep what works: honesty about what did and did not happen, no blame, one
  help line per verb, never advertise a verb before it exists.
- New code comments use the same plain register: no sarcasm. Existing
  comments are not being rewritten.
