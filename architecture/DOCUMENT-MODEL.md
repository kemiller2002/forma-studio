# Forma Studio document model

## Design principle

The saved artifact represents intent and composition, not pixels.

Stable identifiers are mandatory for projects, pages, diagrams, component nodes, diagram nodes, diagram edges, groups/lanes, scenarios, navigation actions, assets, and annotations.

## Project

A Project contains:

- schemaVersion
- projectId
- name
- description
- createdWith
- formaVersion
- startPageId
- pages
- diagrams
- scenarios
- assets
- metadata

## Page

A Page contains:

- stable pageId
- name
- optional route
- optional title/description
- root component-node list
- page-level annotations
- preview settings
- optional requirements references

Routes must be unique after normalization.

A page may exist without a route for non-routable states, but any page targeted by URL navigation must have a route.

## Diagram

A Diagram contains:

- stable diagramId
- name
- profile/kind: general, workflow, state, architecture, or a future registered profile
- nodes
- edges
- groups/containers and optional swimlanes
- diagram-level annotations
- optional requirements references
- optional presentation/view defaults that are explicitly part of authored output

A diagram is a graph specification. Rendered canvas objects are projections.

Flow node position and explicit geometry may be canonical authored data. Editor pan, zoom, current selection, hover, temporary drag state, and transient routing previews are not canonical project data.

## Diagram node

A DiagramNode contains:

- stable nodeId
- element kind
- x/y position
- size or profile-defined sizing contract
- label/content
- optional named ports
- visual properties
- optional profile-specific semantic properties
- optional group/lane membership
- optional annotations and requirement references

Typed profile semantics are explicit. Geometry or appearance alone never changes semantic node type.

## Diagram edge

A DiagramEdge contains:

- stable edgeId
- source node/port ID
- target node/port ID
- connector/relationship kind
- optional label
- optional profile-specific semantic properties
- optional explicit manual routing points
- optional annotations and requirement references

Edges reference stable identities, not rendered DOM/canvas objects. Moving or resizing a node must not change edge identity or topology.

## Diagram groups and swimlanes

Groups, containers, and lanes have stable identities and explicit membership.

General-profile grouping may be visual only. Typed profiles may assign semantic meaning such as actor, role, system, deployment boundary, or trust boundary, but that meaning must be explicit in project data and validated by the selected profile.

Visual overlap does not create membership.

## Component node

A component node contains:

- nodeId
- componentId corresponding to the canonical Forma contract
- componentVersion or inherited project Forma version
- properties
- tokenBindings
- content
- children by named slot
- optional navigation bindings
- optional annotations
- optional scenario overrides

Studio must not invent component properties that are absent from the canonical contract.

## Shared editor commands

All canonical changes to pages and diagrams use one command/transition mechanism.

Equivalent intents from drag/drop, inspector editing, structure/outline editing, keyboard/touch input, or an agent must converge on the same typed command.

Examples include:

- AddComponent / MoveComponent / SetProperty
- AddDiagram / RemoveDiagram
- AddDiagramNode / MoveDiagramNode / ResizeDiagramNode
- ConnectDiagramNodes / RemoveDiagramEdge
- SetDiagramElementProperty
- GroupDiagramNodes / MoveDiagramNodeToLane
- ApplyDiagramLayout

Commands validate against the current project state and selected surface/profile before producing a successor state. Failed commands leave the prior canonical state unchanged.

## Navigation

Navigation is represented explicitly rather than encoded in arbitrary href strings.

A NavigationAction contains:

- actionId
- sourceNodeId
- trigger
- target kind
- targetPageId or external URL
- optional query parameters
- optional fragment
- history behavior: push or replace

Internal navigation targets stable page IDs. Routes are a projection.

Deleting or changing a target page must surface obligations for affected links.

Cycles are legal. Broken targets are not.

## Scenario

A Scenario provides deterministic preview data/state without claiming to be application domain logic.

Examples:

- default
- loading
- empty
- validation-error
- network-failure
- permission-limited
- long-content
- mobile-stress

Scenario data must remain plain serializable data.

## Token binding

Visual properties that map to Forma tokens store token identifiers, not copied values.

Literal visual values are allowed only where the relevant Forma contract explicitly permits them.

## Migration

Every persisted document has a schemaVersion.

Migrations are ordered, deterministic, testable, and never silently discard unsupported data.

A document from a newer schema version must fail safely and explain the incompatibility.
