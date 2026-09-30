# 08 Export, developer handoff, and agent use

## Export classes

EHA-001 Studio MUST distinguish canonical project export, developer handoff export, static preview export, and diagnostic export.

EHA-002 Canonical project export MUST contain the complete editable specification.

EHA-003 Developer handoff MUST be derivable from the canonical project.

EHA-004 Static preview MUST be treated as a projection, never as the editable source.

EHA-005 Diagnostic export MAY include invalid/incomplete projects but MUST label every blocking finding.

## Developer inspector

EHA-020 Studio MUST expose exact Forma component IDs and versions.

EHA-021 Studio MUST expose active component properties and token bindings.

EHA-022 Studio MUST expose canonical/generated semantic markup for the selected composition.

EHA-023 Generated markup MUST distinguish application placeholders/fixtures from production domain data.

EHA-024 Studio MUST expose page route/navigation definitions.

EHA-027 For a selected diagram element, developer inspection MUST expose diagram ID, profile ID/version, stable element ID, element/relationship kind, canonical geometry where applicable, containment/layer membership, semantic properties, presentation capability mapping, and source/target endpoint IDs for edges.

EHA-028 Developer inspection MUST distinguish Flow semantic/profile properties from visual/presentation properties and editor-only view/adornment state.

EHA-029 Studio SHOULD expose source documentation references for the active diagram profile and public presentation primitives where available.

EHA-025 Studio SHOULD expose source documentation references for Forma patterns and tokens.

EHA-026 Studio SHOULD expose responsive/theme preview metadata used during review.

## Agent packet

EHA-040 Studio MUST provide a machine-readable agent-oriented export.

EHA-041 The agent export MUST include project schema version, pinned Forma version, and all diagram profile IDs/versions required to interpret the project.

EHA-042 The agent export MUST include pages, routes, optional start page, component trees, diagrams, diagram nodes/edges/ports/groups/lanes/layers where supported, graph topology, authored geometry, profile semantics, property values, token bindings, scenarios, navigation actions, annotations, and validation findings.

EHA-043 The agent export MUST include stable IDs.

EHA-044 The agent export SHOULD include graph/dependency sections for page navigation, Flow relationships, cross-surface references, and requirement references.

EHA-045 The agent export SHOULD include unresolved obligations and unknowns.

EHA-046 The agent export MUST NOT include chain-of-thought or private model reasoning.

EHA-047 The agent export MUST not require image recognition to understand layout/component identity, diagram topology, or typed relationship semantics.

EHA-048 Agent output MUST clearly identify where domain/application logic remains unspecified.

EHA-049 Agent exports MUST distinguish typed diagram semantics authored in Studio from externally authoritative domain semantics and MUST identify when that authority is unknown/unlinked.

EHA-050 Agent exports SHOULD include normalized semantic diff categories for diagram topology, semantic-property, geometry-only, routing-only, and presentation-only changes when exporting review context.

## Implementation generation

EHA-060 Studio MAY generate a starter implementation shell.

EHA-061 Generated implementation MUST consume the pinned Forma package rather than copying its CSS.

EHA-062 Generated application/browser interaction MUST follow Limen boundaries when Limen generation is enabled.

EHA-063 Generated domain logic MUST NOT be fabricated from presentation alone.

EHA-064 If an application behavior is unspecified, generated output MUST preserve it as an explicit TODO/requirement/unknown.

EHA-065 Code generation MUST be deterministic for equivalent normalized project input when generator/version are fixed.

EHA-066 Generated files MUST identify generator version and source project revision in non-user-facing metadata where practical.

## HTML/static export

EHA-080 Studio SHOULD support static HTML preview export for review.

EHA-081 Static export MUST use Forma CSS and semantic patterns.

EHA-082 Static export MAY use fixture/scenario data.

EHA-083 Static export MUST not claim to implement unspecified application behavior.

EHA-084 Static export SHOULD support route/page preview through generated static paths where feasible.

## Requirements traceability

EHA-100 Pages, nodes, and annotations MAY reference external requirement IDs/URLs.

EHA-101 Studio SHOULD export a traceability matrix of requirement reference -> page/component and diagram/element.

EHA-102 Broken external references SHOULD be distinguishable from unavailable verification.

EHA-103 Agent exports SHOULD preserve requirement references verbatim.

## CLI

EHA-120 The repository SHOULD provide a CLI for validating project files outside the UI.

EHA-121 CLI output MUST support machine-readable JSON.

EHA-122 CLI validation MUST use the same domain validation rules as the editor.

EHA-123 CLI SHOULD support normalize, validate, migrate, inspect, and export commands over time.

EHA-124 Exit codes MUST be documented and stable within a schema/version.

## Interoperability

EHA-140 Figma import/export MAY be explored later as an adapter.

EHA-141 Any Figma adapter MUST map to the canonical Studio project/component contracts rather than becoming a second source of truth.

EHA-142 Screenshot/image/SVG diagram export MAY be provided for communication but MUST never be the only developer handoff artifact.

EHA-143 Future design-tool adapters MUST be isolated from the core domain model.

EHA-144 Future Visio, draw.io, Mermaid, Graphviz, BPMN, or similar adapters MAY translate to/from the canonical graph model only through explicit compatibility contracts; lossless support MUST NOT be implied when semantics differ.


## Cross-surface metadata handoff

EHA-150 Agent/developer export MUST preserve allowed project, page, component-node, diagram, and diagram-element metadata with stable object IDs and stable metadata field keys.

EHA-151 Export MUST distinguish component properties from descriptive Studio metadata so consumers do not infer that arbitrary metadata is a supported Forma runtime property.

EHA-152 Export MUST distinguish authored, defaulted, derived, and source-bound metadata where that distinction exists in canonical state.

EHA-153 Export MUST respect metadata visibility/sensitivity policy and MUST NOT include source-only/editor-only fields merely because the agent packet is machine readable.

EHA-154 Typed object references MUST be exported separately from generic metadata values with reference kind and target identity where known.

EHA-155 Metadata schema definitions needed to interpret exported values SHOULD be included or referenced by stable schema/profile identity.
