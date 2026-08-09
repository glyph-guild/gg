---
type: meta
status: ratified
version: 1.0.0
amended: 2026-08-04
applies-to: our repos — platform, docs, marketing
canonical-at: ".goodgrief/constitution.md in each repo, version-pinned"
---
> Vendored copy. Amendments are made in the source of truth and re-vendored;
> editing this file changes nothing.

# Constitution

**The rules every agent working on *our* codebase obeys.** Ratified 2026-08-04.

> [!important] This is ours, not a product artifact
> Split from the product concept on 2026-08-04. Customers get their own — see Tenant Constitution. That distinction resolved the Article V and VII phase-inconsistency: they were only contradictory when read as claims about the product. As internal delivery rules they are ordinary and enforceable.
>
> This document is `Practice/` in spirit even though it lives in `Charter/`. It stays here because it also constrains architecture, not just process.

Rewritten at 1.0.0 against Vision, Platform Architecture, and ADR-0004 — Execution and identity model. The 0.x articles were seeded from a one-line description before the platform existed; these are chosen to be **violable in specific, detectable ways** — because an article an agent cannot concretely break is decoration.

## Article I — The architect owns intent; agents own execution

An agent may choose *how*. It may not choose *what* or *whether*. Any change that alters intent stops and asks. Intent lives in Specs; no spec, no mandate.

## Article II — Contracts are law

No service modifies another service's contract. Breaking changes route through gate-contract-change regardless of who proposed them or how obvious the fix looks. Additive changes are free.

## Article III — *retired*

Demoted to Convention 1 — Changes trace to a spec on 2026-08-02 because nothing enforced it. The number is retired rather than reused, so references to "Article III" land here and are redirected.

Article numbers are permanent identifiers. Later articles are never renumbered.

## Article IV — Gates present evidence, not diffs

A gate that shows an approver a raw diff has offloaded the work rather than the decision. Every gate defines the evidence payload that makes its decision answerable in under two minutes. See HITL Gates.

## Article V — The envelope is the only path to production

No out-of-band deploys — not by humans, not for hotfixes. If the envelope is too slow for an incident, that is an envelope defect to fix, not a rule to suspend.

## Article VI — Reversibility over speed

Prefer the change that can be undone. One-way changes — schema migration, public API, data deletion — are gated decisions by definition.

## Article VII — Documentation is an artifact of the change

Docs and marketing claims describing shipped behaviour are updated in the change that ships it, across repo-gg-docs and repo-gg-marketing. Unenforceable until cross-repo flights ship; kept as an article because the mechanism is committed rather than hypothetical.

---

The articles below were added at 1.0.0. They exist because they are the ways **this specific platform** goes wrong, and an agent can violate each of them in a single commit.

## Article VIII — The control plane holds nothing it does not need

No customer source code. No credentials or tokens. No test execution. No model inference. The control plane stores **references and facts**, never secrets or repositories.

An agent adding a column that could hold a token, or a code path that materialises a customer repository control-plane-side, violates this article. See ADR-0004 — Execution and identity model.

## Article IX — Policy is evaluated in the control plane

Obligation evaluation, gate decisions, and layer merging happen server-side and nowhere else. The runner gathers, filters, and reports; it never decides.

An agent moving an obligation check into the runner "for latency" has removed the product's reason to exist — a customer can then patch the runner and bypass governance entirely.

## Article X — No dependency without a portable analogue

Every control-plane dependency must be runnable in an arbitrary customer cloud account. Postgres yes; Cosmos, DynamoDB, Durable Functions, Step Functions no. See Platform Architecture.

Also: **prefer fewer components.** Each one is deployed and supported in every single-tenant install.

## Article XI — Fail loudly; never silently pass

An unknown predicate, an unproducible fact, an unreadable secret, a runner too old for the envelope — each halts the flight with a diagnosis. **None of them evaluates to false.**

A silently-false obligation is indistinguishable from a satisfied one, which is this system's most dangerous failure mode: governance that reports success while enforcing nothing.

## Article XII — Attribution is never lost

Every agent action is traceable to an identity and, where attributed, to a delegation that authorised it. Actions that cannot be attributed do not happen.

This is what makes the flight log trustworthy rather than merely detailed — see *chain of custody* in Glossary.

## Article XIII — Nothing hardens without evidence

A loop moves to a cheaper executor rung, or an obligation moves from `human` to `agent` to `machine`, **only on recorded evidence** from prior flights — never because it seems safe, and never silently. Every hardening is a reviewed change.

Hardened rungs expire unless revalidated. See Platform Architecture.

---

# Conventions

Practices we follow that **nothing currently enforces**, kept separate on purpose: an article an agent can violate without consequence teaches it that articles are optional. Each carries its promotion criteria.

## Convention 1 — Changes trace to a spec

A commit should reference the spec that authorised it, so intent is recoverable later.

**Why not an article yet:** no spec-reference mechanism, no registry, no CI check. Until those exist, "work with no traceable intent is reverted" would either never fire or fire on everything.

**Promotion criteria — all three:**

1. A commit trailer convention (`Spec: spec-NNNN`) documented in each repo's `AGENTS.md`.
2. A spec registry resolving those references, with approved specs carrying a `spec-commit`.
3. CI failing a commit whose trailer references an unknown or unapproved spec.

When all three hold this becomes Article XIV. Not Article III — that number is retired.

## Convention 2 — Diagnosability before convenience

Anything that can fail inside a customer's environment produces a diagnosis they can send us, because we cannot look. Not an article yet because there is no diagnostics bundle to conform to.

**Promotion criteria:** a diagnostics bundle export exists and CI verifies that new failure paths populate it.

---

# Amendment

Changes are ADRs against ADR-0001 — Record architecture decisions and require the same gate as a contract break. Version bumps invalidate agent caches — treat that as a feature.

**Distribution, resolved 2026-08-04.** Vendored to `.goodgrief/constitution.md` in each repo and version-pinned; every flight records the constitution version it ran under (Platform Architecture flight data model). "Which version did this agent obey" is now a field rather than an investigation — which is what had been blocking ratification.
