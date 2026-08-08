---
paths:
  - "Gg.Runner/**"
---

# Runner

- The order is load-bearing: lease → resolve credentials → materialize →
  extract facts → compute DIGEST → apply FILTER → emit. The digest is computed
  BEFORE the filter; the filter runs BEFORE anything leaves the machine.
- The resolved secret never leaves this machine. It must not appear in any
  outbound request body, log line, trace or span.
- Native AOT: no reflection, no dynamic serialization. Source-generated only.
- Repo access is read-only. There is no write path, and adding one is a scope
  change rather than an implementation detail.
- A fact the pinned vocabulary does not contain is rejected loudly. Never
  accepted-and-ignored — silently absent is indistinguishable from satisfied.
