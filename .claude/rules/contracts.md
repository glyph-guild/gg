---
paths:
  - "Gg.Contracts/**"
---

# Wire contract

- This is the public boundary: everything crossing between a customer's
  environment and the control plane, and the thing a customer audits.
- Every event and fact type carries a pinned id. A rename must not change the
  wire identity.
- Changing this is a protocol change. Bump the package version, regenerate the
  TypeScript client and Zod schemas, and consider whether the protocol version
  floor moves.
- TypeScript is GENERATED from here, never the reverse.
