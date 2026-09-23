# Forma Studio document model

## Design principle

The saved artifact represents intent and composition, not pixels.

Stable identifiers are mandatory for projects, pages, component nodes, scenarios, navigation actions, assets, and annotations.

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
