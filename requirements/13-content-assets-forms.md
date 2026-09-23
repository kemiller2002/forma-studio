# 13 Content, assets, and forms

## Content model

CAF-001 Component content MUST be stored as typed project data, not scraped from rendered DOM.

CAF-002 Content fields MUST preserve plain text exactly unless the component contract explicitly supports richer structured content.

CAF-003 Rich text, if introduced, MUST use a structured safe format rather than arbitrary HTML.

CAF-004 Studio MUST distinguish fixture/sample content from implementation requirements.

CAF-005 Users SHOULD be able to identify content fields that are placeholders.

CAF-006 Content MAY reference localization keys in a future mode without changing component identity.

CAF-007 Long and empty content MUST be valid preview conditions where the component contract permits them.

## Assets

CAF-020 Studio MUST provide an asset inventory for assets referenced by the project.

CAF-021 Assets MUST have stable asset IDs.

CAF-022 Asset references in component content MUST use asset IDs rather than duplicated URLs where the project owns the asset record.

CAF-023 Supported v1 asset kinds SHOULD include raster image, SVG, and external document/reference URLs.

CAF-024 SVG rendering MUST follow the security requirements for sanitization/non-execution.

CAF-025 Image assets used as meaningful content MUST require appropriate alternative-text metadata or an explicit decorative designation.

CAF-026 Missing assets MUST surface validation findings.

CAF-027 Replacing an asset source SHOULD preserve the asset ID and every project reference.

CAF-028 Deleting an in-use asset MUST surface dependent page/node references before completion.

CAF-029 Studio SHOULD show intrinsic asset dimensions and file/type metadata when available.

CAF-030 Studio SHOULD support local fixture assets without requiring a remote server.

## Forms

CAF-040 Studio MUST support visual composition of forms from canonical Forma controls.

CAF-041 Form controls MUST preserve canonical browser semantics.

CAF-042 Studio MUST model labels, supporting text, required state, disabled/read-only presentation, validation-message association, and grouping where supported by Forma.

CAF-043 A form MUST have a stable form identifier in the Studio specification when submit behavior/navigation is configured.

CAF-044 Field names/identifiers used for implementation handoff MUST be explicit project data and MUST NOT be inferred from visible labels.

CAF-045 Studio MUST prevent duplicate field identifiers within the scope where uniqueness is required.

CAF-046 Form submit navigation MAY target another page as a prototype action.

CAF-047 Form submit preview MUST NOT imply that data was actually persisted unless a scenario/effect explicitly represents that operation.

CAF-048 Studio MUST not invent validation/business rules merely because a field appears required visually.

CAF-049 Presentation-level validation examples MUST be represented as scenarios/fixtures.

CAF-050 Error-summary components SHOULD be linkable/traceable to fields in preview when Forma/application contracts expose the needed relationship.

CAF-051 Keyboard order MUST follow semantic DOM order unless an explicitly supported component contract states otherwise.

## Repeating content

CAF-060 Components that render collections MUST support fixture collections.

CAF-061 Collection item fixtures MUST have stable fixture IDs when referenced by other preview metadata.

CAF-062 Studio SHOULD support adding/removing/reordering fixture rows/items.

CAF-063 Large fixture collections SHOULD support generation without storing thousands of redundant values when a generator descriptor is sufficient.

CAF-064 Fixture generators MUST be deterministic when seeded.

## Links and media

CAF-080 Link content MUST distinguish visible label from target/action.

CAF-081 External links SHOULD expose new-context behavior when supported and should warn when inaccessible labels such as raw ambiguous URLs are used as the only name.

CAF-082 Embedded external media is deferred unless it can be represented safely without arbitrary scripts.

CAF-083 Video/audio assets, when added later, require caption/transcript/accessibility metadata contracts.

## Acceptance

CAF-100 A contact-form page with grouped fields, validation scenario, submit-to-confirmation-page prototype action, and accessible labels MUST be authorable visually.

CAF-101 Replacing a shared logo/image asset MUST update every rendering without rewriting each node reference.

CAF-102 Exported agent/developer packets MUST preserve field identifiers, asset IDs, alt/decorative metadata, and fixture-versus-production distinctions.
