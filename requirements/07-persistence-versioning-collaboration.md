# 07 Persistence, versioning, and collaboration

## Draft persistence

PVC-001 Studio MUST keep canonical in-memory state in the F# engine.

PVC-002 Local draft persistence MUST occur through Limen Storage effects.

PVC-003 Failure to persist locally MUST not mutate canonical project state.

PVC-004 Draft save status MUST distinguish saved, saving, failed, unavailable, and outcome-unknown where applicable.

PVC-005 Studio SHOULD recover the most recent local draft after an unexpected reload.

PVC-006 Recovery MUST identify the recovered project/version and allow the user to decline it.

## File import/export

PVC-020 Studio MUST export one complete canonical project JSON document.

PVC-021 Studio MUST import canonical project JSON.

PVC-022 Import MUST validate schema before replacing the current project.

PVC-023 Import MUST be transactional: failed import leaves the current project unchanged.

PVC-024 Import MUST detect documents from newer unsupported schema versions.

PVC-025 Export MUST be deterministic apart from explicitly documented metadata.

PVC-026 Export SHOULD use stable ordering to produce useful Git diffs.

PVC-027 Studio SHOULD support downloading project JSON through an explicit Limen/browser effect boundary.

## Schema versioning

PVC-040 Every project MUST carry schemaVersion.

PVC-041 Schema migrations MUST be ordered and deterministic.

PVC-042 Every migration MUST have before/after fixtures.

PVC-043 A migration MUST preserve unknown forward-compatible metadata when safe.

PVC-044 A migration MUST never silently discard unsupported project content.

PVC-045 Migration requiring a lossy choice MUST block and produce an explicit obligation.

PVC-046 Studio SHOULD generate a migration report.

PVC-047 Downgrades are not required unless a specific reverse migration exists.

## GitHub persistence

PVC-060 GitHub persistence SHOULD be the first durable remote persistence integration.

PVC-061 GitHub access MUST use browser effects/integration boundaries and MUST NOT expose tokens to project documents.

PVC-062 Users MUST be able to select repository/path/branch for a project where permitted.

PVC-063 Studio MUST fetch the remote revision before writing.

PVC-064 Save MUST use optimistic concurrency against the revision the user opened/last synchronized.

PVC-065 A changed remote revision MUST surface a conflict instead of overwriting.

PVC-066 GitHub commit messages SHOULD be user-editable with a useful default.

PVC-067 Project saves SHOULD be able to create branches and PR-oriented workflows later without changing project format.

PVC-068 GitHub persistence MUST not be required for local-only use.

PVC-069 An unknown write outcome MUST be reconciled before automatic retry.

## Version history

PVC-080 The initial durable history model is Git commit history plus local undo/redo.

PVC-081 Studio SHOULD present project revision metadata when loaded from GitHub.

PVC-082 Studio SHOULD support comparing current project state with a selected saved revision.

PVC-083 Semantic diff SHOULD operate on pages, diagrams, component nodes, diagram nodes/edges, properties, geometry, relationships, links, groups/lanes, and tokens rather than only text lines.

PVC-085 Diagram semantic diff MUST normalize canonical geometry before comparison so transient pointer precision does not create false changes.

PVC-086 View state such as current pan, zoom, selection, hover, temporary isolation, and non-authored editor focus MUST NOT create project revisions.

PVC-084 Reverting a prior revision SHOULD create a new explicit save rather than rewriting published history.

## Collaboration

PVC-100 v1 does not require real-time multiplayer editing.

PVC-101 Git branches/commits/pull requests SHOULD be the initial collaborative review mechanism.

PVC-102 Studio SHOULD export review-friendly semantic diffs for PRs.

PVC-103 Studio MAY support annotations/comments inside the project, but comments MUST be distinguishable from implementation requirements.

PVC-104 Future real-time collaboration MUST preserve the same command/document semantics rather than replacing the project model with canvas operational data.

## Merge/conflict behavior

PVC-120 Studio SHOULD detect non-overlapping changes that can be merged mechanically.

PVC-121 Conflicting changes to the same property/slot/navigation action/diagram element or relationship MUST require explicit resolution.

PVC-122 Merge logic MUST use stable IDs.

PVC-123 Delete-versus-edit conflicts MUST not resurrect deleted nodes silently.

PVC-124 Page-route and diagram-graph conflicts MUST be validated after merge even if the structural merge succeeds.

PVC-125 A merge result MUST pass schema/structural validation before becoming canonical project state.

PVC-126 Independent edits to different diagram elements/properties SHOULD merge mechanically when stable IDs and profile rules prove they do not conflict.

PVC-127 Concurrent edits to the same canonical geometry/property MUST require deterministic conflict resolution unless a narrower merge rule proves both changes compatible.

PVC-128 Node-delete versus incident-edge-add/edit MUST be treated as a semantic conflict and MUST NOT silently resurrect the node or drop the edge.

PVC-129 Endpoint reconnection versus endpoint-node deletion MUST require explicit resolution.

PVC-130 Concurrent topology and geometry edits to the same node MAY merge only when the topology change does not replace/delete the node identity and both resulting changes validate.

PVC-131 Concurrent profile upgrade/migration and profile-specific element edits MUST block automatic merge unless the migration contract explicitly proves compatibility.

PVC-132 Merge resolution MUST preserve stable graph IDs wherever the resolved element remains the same canonical entity.

## Backup and portability

PVC-140 A user MUST be able to retain a complete project without an Echelon account or proprietary cloud backend.

PVC-141 All durable v1 project data, including Flow graph topology and authored geometry, MUST be representable in the exported project plus referenced external assets.

PVC-142 Project files MUST NOT contain secrets.

PVC-143 Studio SHOULD make it easy to create periodic user-controlled backups.
