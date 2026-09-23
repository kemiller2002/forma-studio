# 01 Projects, pages, routes, and navigation

## Project lifecycle

PPN-001 Studio MUST create a project with a stable project ID.

PPN-002 Project IDs MUST remain stable across renames.

PPN-003 A project MUST contain at least one page.

PPN-004 A project MUST identify one start page.

PPN-005 Deleting the start page MUST be illegal until another start page is selected or the deletion transition selects one atomically.

PPN-006 Project metadata MUST include name, optional description, Forma version, schema version, and optional external references.

PPN-007 Project names MAY duplicate other projects; IDs MUST NOT.

PPN-008 Studio MUST expose current dirty/saved state.

## Page lifecycle

PPN-020 A user MUST be able to add, rename, duplicate, reorder, and delete pages.

PPN-021 Every page MUST have a stable page ID independent of display name and route.

PPN-022 A page MAY have an application route.

PPN-023 Routable page routes MUST be normalized and unique within a project.

PPN-024 Route conflicts MUST block validation and publication/export intended for implementation.

PPN-025 Route changes MUST NOT break internal links because internal links target stable page IDs.

PPN-026 Deleting a page with inbound navigation MUST create an explicit blocking obligation identifying affected source nodes.

PPN-027 A page MUST support title, description, annotations, and requirements references.

PPN-028 A page MUST support an ordered root-node collection.

PPN-029 A page MAY represent a non-routable state such as a modal composition, error state, or embedded flow.

PPN-030 Page duplication MUST create new IDs for the page and every duplicated descendant node/action while preserving internal relationships inside the duplicate.

PPN-031 Page duplication MUST offer a clear policy for whether outgoing links continue pointing to original targets or are remapped when duplicating a linked page set.

## Page organization

PPN-040 Studio MUST provide a page list/tree separate from the component tree for the selected page.

PPN-041 Page order is an editor organization aid and MUST NOT implicitly define navigation.

PPN-042 Studio SHOULD support page folders/groups without changing runtime route semantics.

PPN-043 Studio SHOULD support tags for large projects.

PPN-044 Studio MUST support search by page name, route, ID, and annotation text.

## Navigation graph

PPN-060 Navigation MUST be an explicit graph in the project specification.

PPN-061 Internal navigation MUST target page IDs, not copied URLs.

PPN-062 Internal navigation MAY include query parameters and fragments.

PPN-063 Navigation history behavior MUST be explicit when it differs from normal push behavior.

PPN-064 External navigation MUST store a valid absolute URL.

PPN-065 Studio MUST distinguish internal page navigation from external URL navigation.

PPN-066 Navigation actions MUST record their triggering component node.

PPN-067 Supported v1 navigation triggers MUST include activation and form submission where the underlying component contract supports them.

PPN-068 Studio MUST NOT offer a navigation trigger that the selected component cannot produce.

PPN-069 Cycles in the navigation graph MUST be legal.

PPN-070 Broken internal targets MUST be validation errors.

PPN-071 Pages with no incoming links SHOULD be reported as potentially orphaned unless they are the start page or explicitly marked as direct-entry pages.

PPN-072 Pages unreachable from the start page SHOULD be reported separately from broken links.

PPN-073 Studio SHOULD visualize the project navigation graph.

PPN-074 Graph view MUST allow selecting a page and tracing inbound/outbound links.

PPN-075 Graph view MUST distinguish unreachable, direct-entry, start, and broken-target states without relying on color alone.

## Link authoring

PPN-090 A user MUST be able to select a link-capable component and choose a destination page visually.

PPN-091 Destination choices MUST show page name and route when available.

PPN-092 Studio MUST prevent selecting the deleted/nonexistent page IDs.

PPN-093 Self-links MUST be allowed but clearly visible.

PPN-094 A user MUST be able to remove a link without removing the source component.

PPN-095 Link editing MUST participate in undo/redo.

PPN-096 Copy/paste across pages MUST preserve a link when the target page remains in the same project.

PPN-097 Copy/paste into another project MUST surface unresolved internal page references instead of silently converting them.

## Interactive preview

PPN-110 Preview mode MUST start at the project's start page by default.

PPN-111 Preview mode MUST allow starting at any selected page.

PPN-112 Internal links MUST transition to the target page in preview.

PPN-113 Browser Back and Forward SHOULD be supported through Limen navigation capability when route preview is enabled.

PPN-114 Deep-link preview SHOULD resolve known project routes.

PPN-115 Unknown preview routes MUST show an explicit not-found preview state rather than silently redirecting.

PPN-116 Preview navigation MUST NOT mutate the saved project specification.

PPN-117 Editor selection state and preview navigation state MUST be distinct.

## Acceptance scenarios

PPN-130 A three-page flow Home -> Search -> Detail -> Home MUST be constructible and navigable.

PPN-131 A page rename MUST preserve every link to that page.

PPN-132 A route rename MUST preserve internal page-ID links while updating route projection.

PPN-133 Attempting to delete a page with inbound links MUST identify every affected action.

PPN-134 Importing a document with an unknown target page ID MUST fail validation with the action ID, source node ID, and missing page ID.
