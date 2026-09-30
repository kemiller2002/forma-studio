// Editor-only gesture adapter (src/kernel/README.md: "editor-only DOM
// integration that contains no application meaning").
//
// A pointer drag is preview state: the node follows the pointer through a CSS
// translate while the canonical project is untouched. Releasing the pointer
// emits ONE semantic event carrying the logical delta, which the F# engine turns
// into one MoveNodes command and one history entry (FDA-027, FDA-069, Flow
// "Interaction transactions"). Arrow keys on a focused node emit the same event,
// so keyboard movement uses the identical command path (FDA-067).
const dragThreshold = 3;
const nudge = (event) => (event.shiftKey ? 1 : 8);

const emitMove = (root, id, dx, dy) => {
  const input = root.getElementById("gesture-move");
  input.value = `${id}|${dx}|${dy}`;
  input.dispatchEvent(new Event("change", { bubbles: true }));
};

const nodeOf = (target) => target instanceof Element ? target.closest(".ef-diagram__canvas article[data-node-id]") : null;

export const installGestures = (root = document) => {
  let drag = null;
  let suppressClick = false;

  root.addEventListener("pointerdown", (event) => {
    const node = nodeOf(event.target);
    if (!node || event.button !== 0) return;
    drag = { node, id: node.dataset.nodeId, x: event.clientX, y: event.clientY, moved: false, pointer: event.pointerId };
    node.setPointerCapture(event.pointerId);
  });

  root.addEventListener("pointermove", (event) => {
    if (!drag || event.pointerId !== drag.pointer) return;
    const dx = event.clientX - drag.x;
    const dy = event.clientY - drag.y;
    if (!drag.moved && Math.hypot(dx, dy) < dragThreshold) return;
    drag.moved = true;
    drag.node.classList.add("studio-dragging");
    drag.node.style.translate = `${dx}px ${dy}px`;
  });

  const finish = (event, commit) => {
    if (!drag || event.pointerId !== drag.pointer) return;
    const { node, id, x, y, moved } = drag;
    drag = null;
    node.classList.remove("studio-dragging");
    node.style.translate = "";
    if (!moved) return;
    suppressClick = true;
    if (commit) emitMove(root, id, Math.round(event.clientX - x), Math.round(event.clientY - y));
  };
  root.addEventListener("pointerup", (event) => finish(event, true));
  root.addEventListener("pointercancel", (event) => finish(event, false));

  // A drag must not also count as a click that changes selection.
  root.addEventListener("click", (event) => {
    if (suppressClick && nodeOf(event.target)) {
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
