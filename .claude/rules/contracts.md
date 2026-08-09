---
paths:
  - "Gg.Contracts/**"
---

# Wire contract

- This is the public boundary: everything crossing between a customer's
  environment and the control plane, and the thing a customer audits.
- Every event and fact type carries a pinned id. A rename must not change the
  wire identity.
- Zero third-party package references, enforced by a test. It must not
  inherit anyone else's framework, and our wire identity must not be someone
  else's attribute.
- Changing this is a protocol change. Bump the package version and consider
  whether the protocol version floor moves.
