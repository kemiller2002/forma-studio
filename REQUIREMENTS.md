# Forma Studio Requirements

## Shared Echelon application foundations

Status: **Required**

### Forma

Forma Studio exists to specify and compose Forma. The application MUST consume a pinned Forma release for its own interactive UI and MUST dogfood the same public components, patterns, tokens, responsive rules, and accessibility contracts offered to other Echelon applications. Forma Studio MUST NOT rely on privileged private presentation behavior that ordinary Forma consumers cannot use without a recorded design-system requirement.

### Aegis

When Forma Studio introduces operational boundaries such as repository access, filesystem access, remote persistence, import/export, browser/WASM interop, or external service calls, those unexpected operational failures MUST use Aegis. Expected editor/domain outcomes remain typed application/Ordo outcomes.

Aegis user-facing presentation MUST use Forma's shared fault/error presentation components.

### Folio

Folio is **not a mandatory runtime dependency for the initial Forma-only composition scope**. It becomes mandatory when Forma Studio adds printable/PDF/paginated document composition, print preview, or Folio component authoring/testing. At that point, it MUST consume a pinned Folio release and use its public primitives rather than creating a parallel print system.

### Dependency rules

Shared dependencies MUST be pinned to released versions or immutable artifacts. Floating versions and moving repository branches are prohibited as application baselines. Any not-applicable decision or exception MUST be explicit and reviewable.
