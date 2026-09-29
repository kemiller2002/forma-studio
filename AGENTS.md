# Agent instructions

Forma Studio is Echelon Foundry's visual application specification and composition environment for Forma.

## Required context

Before changing implementation or requirements:

1. Read `README.md`.
2. Read `REQUIREMENTS.md`.
3. Preserve Forma Studio's requirement to consume public Forma capabilities rather than inventing private presentation behavior.

## Working rules

- Keep changes small, coherent, and recoverable.
- Commit and push incrementally at meaningful recovery boundaries.
- Do not wait for remote CI after every push. Continue the next independent in-scope slice while CI batches or runs.
- Run local checks when they inform implementation.
- Inspect remote CI at the final implementation boundary by default.
- Inspect CI earlier only when its result gates the next action, protects a high-risk boundary, or is required for merge, release, or publication.
- Never treat queued, cancelled, unavailable, or unobserved CI as passing.
- Do not add an external dependency without explicit approval.
- Do not create a Studio-only substitute for a missing Forma capability. Record the capability gap instead.

## CI batching

Ordinary commit-driven validation uses a 10-minute quiet-period debounce. A newer commit on the same ref cancels the superseded waiting run and restarts the quiet period. Explicit command/control and irreversible release operations must remain immediate unless they use a separate cancellable pre-side-effect debounce gate.
