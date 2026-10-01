# Evidence: external workflow round trip through Forma Studio

Recorded by `npm run proof:external` (tests/browser/workflow.spec.mjs) against vendored forma@eb7fcf160aeb5cc4deab6b887d407afe543b5a68.

1. The external producer (`produce.mjs`, schema only, no Forma code) wrote `mission-handoff.forma-workflow.json`.
2. Forma validation: `supported-with-preserved-extensions`, extensions org.example.scheduler.
3. Studio opened the file with the file picker. It became a first-class workflow document in the public Forma component.
4. Studio edits: renamed `checklist`, added step `task-1`, and ran Arrange (deterministic layout).
5. Studio saved the file with Download file.
6. The external consumer (`consume.mjs`) compared the producer's file with Studio's file:

```text
ok   format and workflow id
ok   workflow metadata
ok   workflow extensions
ok   node brief kept its id
ok   node brief semantics (kind, kindLabel, status, colour, references)
ok   node brief metadata
ok   node brief extensions
ok   node checklist kept its id
ok   node checklist semantics (kind, kindLabel, status, colour, references)
ok   node checklist metadata
ok   node checklist extensions
ok   node anomalies kept its id
ok   node anomalies semantics (kind, kindLabel, status, colour, references)
ok   node anomalies metadata
ok   node anomalies extensions
ok   node review kept its id
ok   node review semantics (kind, kindLabel, status, colour, references)
ok   node review metadata
ok   node review extensions
ok   node accept kept its id
ok   node accept semantics (kind, kindLabel, status, colour, references)
ok   node accept metadata
ok   node accept extensions
ok   edge h1 kept its id
ok   edge h1 semantics
ok   edge h1 metadata and extensions
ok   edge h2 kept its id
ok   edge h2 semantics
ok   edge h2 metadata and extensions
ok   edge h3 kept its id
ok   edge h3 semantics
ok   edge h3 metadata and extensions
ok   edge h4 kept its id
ok   edge h4 semantics
ok   edge h4 metadata and extensions
ok   edge h5 kept its id
ok   edge h5 semantics
ok   edge h5 metadata and extensions
ok   group outgoing

39 preserved, 0 lost
```
