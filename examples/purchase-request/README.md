# Purchase request: cross-repository Workflow fixture

Generated from `Samples.purchaseWorkflow`, which builds the project only
through engine commands. The engine test `committedFixturesAreCurrent` fails
if these files drift from what the engine produces.

| File | What it is | Scope |
|---|---|---|
| `project.json` | Canonical schema v2 project (source of truth) | Editor/source. It contains the source-only `cost-center` value on purpose. |
| `agent-export.json` | Agent/developer export: topology, metadata with value status, references, style/palette/mapping/override provenance, validation | `agent-export`. `cost-center` is withheld. |
| `folio-projection/projection.json` | Versioned Folio projection manifest (source revision, bounds, object linkage, legend, relationships, provenance) | `rendered` plus non-secret provenance |
| `folio-projection/diagram.html` | Public Forma diagram markup (contract 2.0.0) | `rendered` |

Regenerate:

```bash
dotnet run --project tools/FormaStudio.Cli -- sample examples/purchase-request/project.json
dotnet run --project tools/FormaStudio.Cli -- export examples/purchase-request/project.json examples/purchase-request/agent-export.json
dotnet run --project tools/FormaStudio.Cli -- project examples/purchase-request/project.json purchase-request examples/purchase-request/folio-projection
```

What the fixture demonstrates (FDA-890..896):

- Seven nodes in three semantic lanes, a decision with two labelled outcomes,
  and a manually routed loop.
- Status, owner (derived from the lane), phase, tags, a ticket URL and a
  source-only cost center.
- Color that does not track status. `Prepare request` (In progress) and
  `Issue purchase order` (Not started) share the purple author highlight.
  `Revise request` is Blocked but authored green. `Approve spend` is Blocked
  and colored by the explicit `status-color` mapping.
- A named style on the decision.
- Typed references to REQ-42 and kemiller2002/forma#53.

kemiller2002/folio consumes `folio-projection/` to prove color, grayscale and
backgrounds-off print output.
