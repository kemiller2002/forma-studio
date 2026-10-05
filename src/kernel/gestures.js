// Editor-only gesture adapter (src/kernel/README.md: "editor-only DOM
// integration that contains no application meaning").
//
// A pointer drag is preview state: the node follows the pointer through a CSS
// translate while the canonical project is untouched. Releasing the pointer
// emits ONE semantic event carrying the logical delta, which the F# engine turns
// into one MoveNodes command and one history entry (FDA-027, FDA-069, Flow
// "Interaction transactions"). Arrow keys on a focused node emit the same event,
// so keyboard movement uses the identical command path (FDA-067).
//
// Dragging the corner handle is the same kind of transaction: the preview
// changes only the node's CSS size variables, and release emits ONE resize
// event with the new logical size. The inspector's Width and Height fields are
// the non-pointer path to the same command.
//
// A selected connector shows a handle at each end. Dragging a handle onto a
// node emits ONE reconnect event; pressing the handle instead starts the
// keyboard path (choose the new node from the canvas or Structure list).
//
// While a node is dragged, alignment guides appear when one of its edges or
// its center comes within a few units of another node's, and the released
// delta lands on that line. Guides are preview adorners only; grid snapping,
// when on, takes precedence and is applied by the engine.
import { editorEvents } from "./editor-events.js";

const dragThreshold = 3;
const nudge = (event) => (event.shiftKey ? 1 : 8);

const emit = (root, inputId, value) => {
  const input = root.getElementById(inputId);
  input.value = value;
  input.dispatchEvent(new Event("change", { bubbles: true }));
};
const emitMove = (root, id, dx, dy) => emit(root, editorEvents.gestureMove, `${id}|${dx}|${dy}`);
const emitResize = (root, id, w, h) => emit(root, editorEvents.gestureResize, `${id}|${w}|${h}`);
const emitReconnect = (root, end, nodeId) => emit(root, editorEvents.gestureReconnect, `${end}|${nodeId}`);

const minimumSize = 24;
// Pointer deltas are screen pixels; the engine works in logical units.
const zoomOf = (element) => Number(element.closest(".ef-diagram__canvas")?.dataset.zoom) || 1;
// The engine projects each node's logical geometry as data-x/-y/-w/-h.
const logical = (node, name) => Number(node.dataset[name]) || 0;
const guideReach = 6;

const rectOf = (node) => ({
  x: logical(node, "x"), y: logical(node, "y"),
  w: logical(node, "w"), h: logical(node, "h"),
});
// Start, center and end lines of a rectangle on one axis.
const linesOf = (start, length) => [start, start + length / 2, start + length];

// The smallest correction that puts one of the moving lines on a fixed line,
// or none when nothing is within reach.
const nearestAlignment = (moving, fixed) =>
  moving
    .flatMap((m) => fixed.map((f) => ({ line: f, correction: f - m })))
    .filter(({ correction }) => Math.abs(correction) <= guideReach)
    .reduce((best, c) => (best === null || Math.abs(c.correction) < Math.abs(best.correction) ? c : best), null);

const alignmentFor = (drag, dx, dy) => {
  const { rect, others } = drag;
  const alignX = nearestAlignment(linesOf(rect.x + dx, rect.w), others.flatMap((o) => linesOf(o.x, o.w)));
  const alignY = nearestAlignment(linesOf(rect.y + dy, rect.h), others.flatMap((o) => linesOf(o.y, o.h)));
  return { alignX, alignY, dx: dx + (alignX?.correction ?? 0), dy: dy + (alignY?.correction ?? 0) };
};

const showGuides = (canvas, { alignX, alignY }) => {
  const guide = (axis, line) => {
    const element = canvas.querySelector(`.studio-guide[data-axis="${axis}"]`) ?? canvas.appendChild(Object.assign(document.createElement("div"), { className: "studio-guide", ariaHidden: "true" }));
    element.dataset.axis = axis;
    element.hidden = line === undefined;
    if (line !== undefined) element.style.setProperty("--studio-guide", `${line}px`);
  };
  guide("x", alignX?.line);
  guide("y", alignY?.line);
};
const clearGuides = (canvas) => canvas?.querySelectorAll(".studio-guide").forEach((g) => g.remove());

const nodeOf = (target) => target instanceof Element ? target.closest(".ef-diagram__canvas article[data-node-id]") : null;
const endpointOf = (target) => target instanceof Element ? target.closest(".studio-endpoint") : null;

export const installGestures = (root = document) => {
  let drag = null;
  let suppressClick = false;

  root.addEventListener("pointerdown", (event) => {
    const handle = endpointOf(event.target);
    if (handle && event.button === 0) {
      drag = { node: handle, endpoint: handle.dataset.end, x: event.clientX, y: event.clientY, moved: false, pointer: event.pointerId };
      handle.setPointerCapture(event.pointerId);
      return;
    }
    const node = nodeOf(event.target);
    if (!node || event.button !== 0) return;
    const resizing = event.target instanceof Element && event.target.classList.contains("studio-resize-handle");
    const canvas = node.closest(".ef-diagram__canvas");
    const guided = !resizing && canvas?.dataset.snap !== "true";
    const others = guided ? [...canvas.querySelectorAll("article[data-node-id]")].filter((n) => n !== node).map(rectOf) : [];
    drag = {
      canvas, guided, rect: rectOf(node), others,
      node, id: node.dataset.nodeId, x: event.clientX, y: event.clientY, moved: false, pointer: event.pointerId,
      resizing, w: logical(node, "w"), h: logical(node, "h"),
    };
    node.setPointerCapture(event.pointerId);
  });

  root.addEventListener("pointermove", (event) => {
    if (!drag || event.pointerId !== drag.pointer) return;
    const screenDx = event.clientX - drag.x;
    const screenDy = event.clientY - drag.y;
    if (!drag.moved && Math.hypot(screenDx, screenDy) < dragThreshold) return;
    const zoom = zoomOf(drag.node);
    const dx = screenDx / zoom;
    const dy = screenDy / zoom;
    drag.moved = true;
    drag.node.classList.add("studio-dragging");
    if (drag.endpoint) {
      drag.node.style.translate = `calc(-50% + ${dx}px) calc(-50% + ${dy}px)`;
    } else if (drag.resizing) {
      drag.node.style.setProperty("--studio-preview-w", `${Math.max(minimumSize, drag.w + dx)}px`);
      drag.node.style.setProperty("--studio-preview-h", `${Math.max(minimumSize, drag.h + dy)}px`);
    } else if (drag.guided) {
      const aligned = alignmentFor(drag, dx, dy);
      showGuides(drag.canvas, aligned);
      drag.node.style.translate = `${aligned.dx}px ${aligned.dy}px`;
    } else {
      drag.node.style.translate = `${dx}px ${dy}px`;
    }
  });

  const finish = (event, commit) => {
    if (!drag || event.pointerId !== drag.pointer) return;
    const current = drag;
    const { node, id, x, y, moved, resizing, w, h, endpoint } = current;
    clearGuides(current.canvas);
    drag = null;
    node.classList.remove("studio-dragging");
    node.style.translate = "";
    node.style.removeProperty("--studio-preview-w");
    node.style.removeProperty("--studio-preview-h");
    if (!moved) return;
    suppressClick = true;
    const zoom = zoomOf(node);
    const dx = Math.round((event.clientX - x) / zoom);
    const dy = Math.round((event.clientY - y) / zoom);
    if (commit && endpoint) {
      // Find the node under the pointer, ignoring the handle being dragged.
      node.style.pointerEvents = "none";
      const dropped = nodeOf(document.elementFromPoint(event.clientX, event.clientY));
      node.style.pointerEvents = "";
      if (dropped) emitReconnect(root, endpoint, dropped.dataset.nodeId);
    } else if (commit && resizing) emitResize(root, id, Math.max(minimumSize, w + dx), Math.max(minimumSize, h + dy));
    else if (commit && current.guided) {
      const aligned = alignmentFor(current, dx, dy);
      emitMove(root, id, Math.round(aligned.dx), Math.round(aligned.dy));
    } else if (commit) emitMove(root, id, dx, dy);
  };
  root.addEventListener("pointerup", (event) => finish(event, true));
  root.addEventListener("pointercancel", (event) => finish(event, false));

  // A drag must not also count as a click that changes selection.
  root.addEventListener("click", (event) => {
    if (suppressClick && (nodeOf(event.target) || endpointOf(event.target))) {
      event.stopImmediatePropagation();
      event.preventDefault();
    }
    suppressClick = false;
  }, true);

  root.addEventListener("keydown", (event) => {
    const node = nodeOf(event.target);
    if (!node || event.target !== node) return;
    const deltas = { ArrowLeft: [-nudge(event), 0], ArrowRight: [nudge(event), 0], ArrowUp: [0, -nudge(event)], ArrowDown: [0, nudge(event)] };
    if (event.key in deltas) {
      event.preventDefault();
      const [dx, dy] = deltas[event.key];
      emitMove(root, node.dataset.nodeId, dx, dy);
    } else if (event.key === "Enter" || event.key === " ") {
      event.preventDefault();
      node.click();
    }
  });
};
