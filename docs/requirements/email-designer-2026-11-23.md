# Reusable Email Designer: requirements and architectural contract
Date: 2026-11-23
Status: proposed; requirements/design only; no implementation or tests executed.
First consumer: Unbound. Reusable Echelon capability, F# and WASM-first; component monolith consumers.

## Goals and non-goals
Build an accessible visual authoring system for branded transactional/operational email, with a versioned structured document, deterministic compilation to email-compatible HTML and plain text, portable theme references and typed token placeholders. Enable authoring in Forma Studio and embedding into apps such as Unbound. Do not make Forma own message delivery, tenant data, business triggers, recipient permission, legal approvals or token value resolution. Do not promise identical pixels across email clients. No arbitrary HTML/JS/CSS execution.

## Ownership contracts
- **Forma**: pure document schema, block/component contracts, editor UI primitives, validation interfaces, responsive preview, serialization, migration, rendering interface and test fixtures.
- **Forma Studio**: visual composition UX, palette, canvas, selection, property inspectors, undo/redo, keyboard and screen-reader authoring, preview, sample data, version comparison and publishing integration hooks.
- **Email renderer adapter**: transforms versioned document into sanitized email-compatible HTML and meaningful plain text; supports compatibility tests, fallback fonts and inline CSS where appropriate. Must be independently validated; Folio reuse is an ADR decision, not an assumption.
- **Unbound**: sponsor/client template definitions and overrides, Brand Kit, event-specific token registry, recipient authorization, disclosure policy, workflow, revision approvals, as-sent snapshot, outbox, provider and delivery audit.
- **Folio**: candidate reusable document primitives and print layouts, only if email compatibility validation proves fit. Avoid forcing PDF/browser CSS into email.

## Structured document format
Versioned EmailDocument with DocumentSchemaVersion, DocumentId, Metadata, RootLayout, Blocks, Styles, ThemeReferences, TokenBindings, AccessibilityMetadata and Extensions namespace. Use stable BlockIds and deterministic serialization. Unknown schema/block versions must fail safely or show explicit read-only fallback; no silent content loss. Document contains token identifiers, not real donor data. Renderer inputs include document version, resolved theme snapshot, typed authorized token snapshot, locale, mandatory fragments and rendering policy. Output includes HTML, plain text, diagnostics and reproducibility hash. Keep data contract language-neutral at boundary, implement F# typed model.

## Block taxonomy
Required: text/paragraph, heading, image, button/CTA, divider, spacer, single/multi-column section with safe mobile stacking, container/card, list, dynamic text/token, secure link, branding header, footer and protected legal disclosure. Optional after evidence: tables, conditional blocks, reusable sections and localization variants. Each block declares properties, permitted nesting, serialization version, rendering support, validation rules, accessibility metadata and migration behavior. Avoid uncontrolled arbitrary nesting.

## Visual authoring
Block insert/reorder/duplicate/remove, drag/drop plus complete keyboard alternative, focus and selection, property panel, text editing, alignment, semantic typography, color and spacing tokens, image crop/alt text, CTA URL binding, mobile stacking, theme application, constrained responsive layouts, undo/redo, draft autosave interface, diff/compare, synthetic preview, test send hook, error diagnostics and clear publish readiness. Must work on touch and keyboard; avoid relying on mouse-only drag interactions. Studio must not silently publish or transmit data.

## Token handling
Typed TokenDefinition with Key, Type, AllowedContexts, DataSensitivity, Required, Formatter, NullPolicy, LocaleRules and Version. Designer supports discovery, insertion and validation against supplied token schema; never fetches private donor data on its own. No arbitrary expressions, database queries, executable template syntax or cross-tenant references. Context-specific encoding for HTML, subject, URL and attributes. Required missing or unauthorized tokens block production rendering. Safe synthetic fixtures for preview. Preserve numeric reference leading zeros as strings.

## Styling and themes
Use semantic brand tokens (identity, logo variants, palette, typography with fallback, spacing, radius, buttons, header/footer). Layout must be legible with images off, dark mode, narrow widths and missing web fonts. Support sponsor locale and terminology without changing canonical financial semantics. Brand changes bind to versioned theme snapshot; historic deliveries remain reproducible.

## Rendering and compatibility
Generate safe email HTML plus independently meaningful plain text. Validate major client classes: Gmail web/mobile, Outlook desktop/web, Apple Mail/iOS Mail, and common mobile widths, with a documented supported-client matrix and fixture screenshots. Explicitly record client-specific limitations and degrade gracefully. Avoid scripts, forms, external CSS dependencies, unsafe remote assets and unverified sender domains. Rendering must be deterministic for pinned inputs and not access the network. Images hosted by the consuming application with immutable authorized references.

## Accessibility
Semantic reading order, descriptive links, image alt text, contrast, text resizing, logical headings, touch targets, screen reader/keyboard editor access, focus management, error announcements, plain-text equivalent and reduced reliance on color. Validate HTML output and authoring UI separately. Mandatory legal text cannot be hidden or reduced to unreadable styling.

## Governance and history
Designer emits immutable publishable artifact and diagnostics, not business approval. Consumers own Draft -> Review -> Approved -> Published -> Superseded lifecycle. Preserve document, theme, layout, token schema and renderer version for as-sent reconstruction. Restore from old version creates new revision. Legal fragments cannot be removed by styling. Audit authorship and version diffs via host integration.

## APIs and component contracts (conceptual)
ParseDocument, ValidateDocument, MigrateDocument, RenderPreview, RenderEmail, DiffDocuments, ListBlocks, GetPropertySchema, ResolveThemePreview and ExportPlainText. Rendering receives explicit immutable input; editor receives permitted block/token/theme capabilities. Events: DocumentChanged, ValidationChanged, PreviewRequested, TestSendRequested and PublishRequested. Host executes permitted operations, never the designer autonomously. Typed F# interface and JSON schema boundary to be ADR-reviewed.

## Acceptance gates
A. Round-trip parse/serialize preserves every supported block and version.
B. Golden fixtures produce deterministic HTML/plain-text output for pinned inputs.
C. No private data fetched or executed by editor/renderer; XSS, URL injection, header injection and asset leakage blocked.
D. All blocks have accessible keyboard authoring and output fallbacks.
E. Cross-client screenshot and semantic content tests document degradation.
F. Sponsor brand/locale isolation, version pinning and token typing verified.
G. Historic published template and as-sent email reproducible after theme/renderer changes.
H. Responsive and dark-mode checks; images disabled still convey essential message.
I. Large templates, undo/redo, touch/mobile and schema migration tested.
J. Unbound integration can enforce nonremovable disclaimers and authorization without modifying Forma.

## Adversarial test backlog (unexecuted)
ED-001 token resolves from unauthorized tenant; ED-002 block contains script; ED-003 required disclaimer hidden by CSS; ED-004 image disabled; ED-005 narrow mobile viewport; ED-006 Outlook strips CSS; ED-007 dark mode inverts logo; ED-008 missing font; ED-009 unsupported document schema; ED-010 unknown block; ED-011 editor autosave conflict; ED-012 drag-only inaccessible; ED-013 HTML subject injection; ED-014 URL token exfiltration; ED-015 historical asset replaced; ED-016 renderer upgraded after send; ED-017 malformed nested blocks; ED-018 RTL text expansion; ED-019 numeric ID leading zeros; ED-020 token missing/Unknown; ED-021 plain text omitted; ED-022 preview accidentally uses production data; ED-023 unauthorized publish from Studio; ED-024 mandatory footer removed; ED-025 image-only email; ED-026 two brands cache collision; ED-027 huge attachment/image; ED-028 malformed imported HTML; ED-029 theme edit races queued send; ED-030 migration loses content.

## Architecture decisions to resolve before coding
ADR-01 document schema and migration strategy; ADR-02 email renderer and Folio reuse vs dedicated renderer; ADR-03 editor state model and undo/redo; ADR-04 preview sandbox and safe HTML; ADR-05 theme/token boundary; ADR-06 image asset and hosting policy; ADR-07 email-client compatibility matrix and testing infrastructure; ADR-08 accessibility standard; ADR-09 plain-text generation; ADR-10 structured blocks vs constrained HTML import; ADR-11 localization and conditional rendering; ADR-12 version pinning and history; ADR-13 extension/plugin safety; ADR-14 touch/mobile editing UX; ADR-15 performance budgets and testing strategy.

## Implementation sequencing (not authorization to implement)
1. Inventory existing Forma, Forma Studio and Folio capabilities to avoid duplicate systems.
2. Approve ownership boundaries, schema, rendering ADR and accessibility/compatibility matrix.
3. Validate schema and renderer with static fixtures and golden tests before visual editor.
4. Build reusable editing primitives and Studio UX with unit/accessibility tests.
5. Integrate Unbound token/brand/template governance using synthetic examples.
6. Run email-client compatibility, adversarial and end-to-end tests before production release.

## Explicit challenges
- Drag/drop is convenient but often harms accessibility; keyboard authoring is mandatory.
- A shared browser/print renderer is not automatically valid for email.
- Pixel-perfect fidelity is unrealistic across mail clients; prioritize content and accessibility.
- Unrestricted HTML customization increases phishing/XSS/privacy risk; advanced import must be governed.
- A universal Brand Kit must not erase channel-specific constraints.
- A designer must not become a second authority for template approval or message delivery.
