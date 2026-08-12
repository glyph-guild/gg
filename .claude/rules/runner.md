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
- Repo access is read-only UNLESS two independent controls both hold: the
  envelope declares a destination (the tenant, in the control plane, granting
  permission) and the credential carries write scope (the developer, in their
  own store, granting the ability). Neither alone is sufficient.
- **The envelope cannot widen a credential.** A write destination against a
  read-only credential fails at the credential, with a diagnosis naming the
  reference. Nothing in this repo composes a scope set; it reads what the
  developer registered.
- Writing lives behind `IDestinationAdapter` and nowhere else. `IVcsAdapter`
  stays read-only, and a write path in any other file fails the build.
- The runner pushes; the control plane does not, and its app holds no
  permission to. The landing decision arrives on the facts response as a
  `DestinationAdmission`, and **absent means no** — the runner never derives it
  from a verdict it can see.
- Nothing is ever force-pushed. An existing branch is refused by name.
- A fact the pinned vocabulary does not contain is rejected loudly. Never
  accepted-and-ignored — silently absent is indistinguishable from satisfied.
