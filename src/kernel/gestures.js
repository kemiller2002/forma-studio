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
const dragThreshold = 3;
const nudge = (event) => (event.shiftKey ? 1 : 8);

const emit = (root, inputId, value) => {
  const input = root.getElementById(inputId);
  input.value = value;
  input.dispatchEvent(new Event("change", { bubbles: true }));
};
const emitMove = (root, id, dx, dy) => emit(root, "gesture-move", `${id}|${dx}|${dy}`);
const emitResize = (root, id, w, h) => emit(root, "gesture-resize", `${id}|${w}|${h}`);
const emitReconnect = (root, end, nodeId) => emit(root, "gesture-reconnect", `${end}|${nodeId}`);

const minimumSize = 24;
const cssPixels = (node, name) => parseFloat(node.style.getPropertyValue(name)) || 0;

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
    drag = {
      node, id: node.dataset.nodeId, x: event.clientX, y: event.clientY, moved: false, pointer: event.pointerId,
      resizing, w: cssPixels(node, "--ef-diagram-w"), h: cssPixels(node, "--ef-diagram-h"),
    };
    node.setPointerCapture(event.pointerId);
  });

  root.addEventListener("pointermove", (event) => {
    if (!drag || event.pointerId !== drag.pointer) return;
    const dx = event.clientX - drag.x;
    const dy = event.clientY - drag.y;
    if (!drag.moved && Math.hypot(dx, dy) < dragThreshold) return;
    drag.moved = true;
    drag.node.classList.add("studio-dragging");
    if (drag.endpoint) {
      drag.node.style.translate = `calc(-50% + ${dx}px) calc(-50% + ${dy}px)`;
    } else if (drag.resizing) {
      drag.node.style.setProperty("--studio-preview-w", `${Math.max(minimumSize, drag.w + dx)}px`);
      drag.node.style.setProperty("--studio-preview-h", `${Math.max(minimumSize, drag.h + dy)}px`);
    } else {
      drag.node.style.translate = `${dx}px ${dy}px`;
    }
  });

  const finish = (event, commit) => {
    if (!drag || event.pointerId !== drag.pointer) return;
    const { node, id, x, y, moved, resizing, w, h, endpoint } = drag;
    drag = null;
    node.classList.remove("studio-dragging");
    node.style.translate = "";
    node.style.removeProperty("--studio-preview-w");
    node.style.removeProperty("--studio-preview-h");
    if (!moved) return;
    suppressClick = true;
    const dx = Math.round(event.clientX - x);
    const dy = Math.round(event.clientY - y);
    if (commit && endpoint) {
      // Find the node under the pointer, ignoring the handle being dragged.
      node.style.pointerEvents = "none";
      const dropped = nodeOf(document.elementFromPoint(event.clientX, event.clientY));
      node.style.pointerEvents = "";
      if (dropped) emitReconnect(root, endpoint, dropped.dataset.nodeId);
    } else if (commit && resizing) emitResize(root, id, Math.max(minimumSize, w + dx), Math.max(minimumSize, h + dy));
    else if (commit) emitMove(root, id, dx, dy);
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
