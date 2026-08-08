---
paths:
  - "console/**"
---

# Console

- The keymap is a PURE function: (input, key, context) -> Command | null.
  One module, exhaustively tested. Bindings live nowhere else.
- Status hints are generated from the same context the keymap dispatches on,
  so advertised keys cannot drift from live ones.
- State is fully serializable, with every non-serializable handle on a
  controller outside the store. This is what makes terminal release possible
  and it is an architectural constraint, not a style choice.
- Modals own the keyboard while open, with exactly one escape hatch so the
  terminal can never be locked up.
- Externally-sourced text — PR titles, branch names, author handles, paths —
  is stripped of terminal control sequences at INGRESS, before storage. Not
  at render time.
- The live view pane is OFF by default. It is a trust artifact meant to decay.
- `console/src/generated/` is generated. Never hand-edit it.
