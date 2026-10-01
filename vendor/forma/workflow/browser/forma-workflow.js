// <forma-workflow>: the embeddable Forma workflow renderer/editor.
//
// This file is browser plumbing only. It forwards DOM events and host API calls
// to the F# engine (Forma.Workflow, compiled to .NET WebAssembly) as data, renders
// the HTML the engine returns, and re-emits the engine's events as DOM events.
// It makes no workflow decision. Host boundary: forma-workflow-host/1 (see
// docs/workflow/EMBEDDING.md in kemiller2002/forma).
export const PROTOCOL = "forma-workflow-host/1";

const MODES = ["view", "inspect", "edit", "pick", "runtime"];
let defaultTransport;
let instanceCounter = 0;

/**
 * Loads the bundled .NET WebAssembly engine once per page. Every element on the
 * page shares it; each element is a separate instance inside the engine.
 */
export function createWasmTransport(baseUrl = new URL("./engine/", import.meta.url).href) {
  let exportsPromise;
  const load = () => {
    exportsPromise ??= (async () => {
      const { dotnet } = await import(new URL("_framework/dotnet.js", baseUrl).href);
      const runtime = await dotnet.create();
      const exported = await runtime.getAssemblyExports(runtime.getConfig().mainAssemblyName);
      return exported.FormaWorkflowInterop;
    })();
    return exportsPromise;
  };
  return {
    async start() { await load(); },
    async dispatch(message) { return (await load()).Dispatch(message); },
    async document(instance) { return (await load()).Document(instance); },
    async dispose(instance) { (await load()).Dispose(instance); },
    async validate(text) { return JSON.parse((await load()).Validate(text)); }
  };
}

// A selector that finds "the same control" after a re-render, so focus survives.
const sameControl = (element) => {
  if (!element) return null;
  if (element.id) return `#${CSS.escape(element.id)}`;
  const selector = ["event", "key", "arg"]
    .map((name) => [name, element.getAttribute(`data-fw-${name}`)])
    .filter(([, value]) => value !== null)
    .map(([name, value]) => `[data-fw-${name}="${CSS.escape(value)}"]`)
    .join("");
  return selector || null;
};

export class FormaWorkflowElement extends HTMLElement {
  static get observedAttributes() { return ["mode"]; }

  #transport;
  #instance;
  #root;
  #started = false;
  #queue = Promise.resolve();
  #suppressClick = false;

  /** A transport may be assigned before the element connects (for example by a .NET host such as Forma Studio). */
  set transport(value) { this.#transport = value; }
  get transport() { return this.#transport; }

  get mode() { const m = this.getAttribute("mode"); return MODES.includes(m) ? m : "view"; }
  set mode(value) { this.setAttribute("mode", value); }

  connectedCallback() {
    if (this.#started) return;
    this.#started = true;
    this.#instance = this.getAttribute("instance") || this.id || `fw${++instanceCounter}`;
    this.#transport ??= (defaultTransport ??= createWasmTransport(this.getAttribute("engine") || undefined));
    const source = this.querySelector(":scope > script.forma-workflow-source");
    const documentText = source ? source.textContent : null;
    // The static figure stays visible until the engine replaces it.
    this.#root = document.createElement("div");
    this.#root.className = "ef-workflow-host";
    this.#wire(this.#root);
    this.#send({ type: "init", mode: this.mode, document: documentText }).then(() => {
      for (const child of [...this.children]) if (child !== this.#root && child !== source) child.remove();
      this.append(this.#root);
      this.dispatchEvent(new CustomEvent("forma-workflow-ready", { bubbles: true }));
    }, (error) => this.#fail(error));
  }

  disconnectedCallback() {
    if (this.#transport && this.#instance) this.#transport.dispose(this.#instance);
    this.#started = false;
  }

  attributeChangedCallback(name, previous, next) {
    if (name === "mode" && this.#started && previous !== next) this.#send({ type: "mode", mode: this.mode });
  }

  // -- public host API (forma-workflow-host/1) -----------------------------------
  load(workflow) { return this.#send({ type: "load", document: typeof workflow === "string" ? workflow : JSON.stringify(workflow) }); }
  async getWorkflow() { const text = await this.#transport.document(this.#instance); return text ? JSON.parse(text) : null; }
  select(objects) { return this.#send({ type: "select", objects }); }
  focusObject(object) { return this.#send({ type: "focus", object }); }
  execute(command) { return this.#send({ type: "command", command }); }
  undo() { return this.#send({ type: "undo" }); }
  redo() { return this.#send({ type: "redo" }); }
  setRuntimeState(states) { return this.#send({ type: "runtime", states }); }
  validate(workflow) { return this.#transport.validate(typeof workflow === "string" ? workflow : JSON.stringify(workflow)); }

  // -- plumbing -----------------------------------------------------------------
  #send(message) {
    const run = async () => {
      const reply = JSON.parse(await this.#transport.dispatch(JSON.stringify({ protocol: PROTOCOL, instance: this.#instance, ...message })));
      this.#apply(reply);
      return reply;
    };
    this.#queue = this.#queue.then(run, run);
    return this.#queue;
  }

  #fail(error) {
    this.dataset.fwError = "true";
    this.dispatchEvent(new CustomEvent("forma-workflow-error", { bubbles: true, detail: { message: String(error && error.message || error) } }));
  }

  #apply(reply) {
    if (typeof reply.html === "string") {
      const active = this.#root.contains(document.activeElement) ? sameControl(document.activeElement) : null;
      const viewport = this.#root.querySelector(".ef-diagram__viewport");
      const scroll = viewport ? [viewport.scrollLeft, viewport.scrollTop] : null;
      // The engine builds this markup from a typed tree that escapes all text and attributes.
      this.#root.innerHTML = reply.html;
      const nextViewport = this.#root.querySelector(".ef-diagram__viewport");
      if (scroll && nextViewport) [nextViewport.scrollLeft, nextViewport.scrollTop] = scroll;
      const target = reply.focus ? this.#root.querySelector(`#${CSS.escape(reply.focus)}`) : active ? this.#root.querySelector(active) : null;
      if (target) {
        target.focus({ preventScroll: !reply.focus });
        if (reply.focus) target.scrollIntoView({ block: "nearest", inline: "nearest" });
      }
    }
    for (const event of reply.events ?? []) {
      this.dispatchEvent(new CustomEvent(`forma-workflow-${event.type}`, { bubbles: true, detail: event }));
    }
  }

  #event(name, extra) { return this.#send({ type: "event", event: { name, ...extra } }); }

  #wire(root) {
    root.addEventListener("click", (e) => {
      if (this.#suppressClick) { this.#suppressClick = false; return; }
      const control = e.target.closest("[data-fw-event]");
      if (!control || !root.contains(control)) return;
      if (control.matches("input, select, textarea, form, option")) return;
      if (control.matches("button[disabled]")) return;
      if (control.tagName === "BUTTON" && control.type === "submit") return;
      e.preventDefault();
      this.#event(control.dataset.fwEvent, {
        key: control.dataset.fwKey, arg: control.dataset.fwArg, value: control.value ?? undefined,
        toggle: e.ctrlKey || e.metaKey || e.shiftKey, shift: e.shiftKey
      });
    });
    root.addEventListener("change", (e) => {
      const control = e.target.closest("input[data-fw-event], select[data-fw-event], textarea[data-fw-event]");
      if (!control) return;
      const value = control.type === "checkbox" ? String(control.checked) : control.value;
      const fields = control.dataset.fwType ? { type: control.dataset.fwType } : undefined;
      this.#event(control.dataset.fwEvent, { key: control.dataset.fwKey, arg: control.dataset.fwArg, value, fields });
    });
    root.addEventListener("submit", (e) => {
      const form = e.target.closest("form[data-fw-event]");
      if (!form) return;
      e.preventDefault();
      const fields = Object.fromEntries([...new FormData(form)].filter(([, v]) => typeof v === "string"));
      this.#event(form.dataset.fwEvent, { key: form.dataset.fwKey, fields });
    });
    root.addEventListener("keydown", (e) => {
      const control = e.target.closest("[data-fw-key]");
      if (!control || e.target.matches("input, select, textarea")) return;
      if (!["ArrowLeft", "ArrowRight", "ArrowUp", "ArrowDown", "Delete", "Backspace", "Escape"].includes(e.key)) return;
      if (e.key.startsWith("Arrow") && this.mode !== "edit") return;
      e.preventDefault();
      this.#event("key", { key: control.dataset.fwKey, value: e.key, shift: e.shiftKey });
    });
    this.#wireGestures(root);
  }

  // Pointer gestures preview with a CSS transform and send one event when released.
  #wireGestures(root) {
    let gesture = null;
    root.addEventListener("pointerdown", (e) => {
      const handle = e.target.closest("[data-fw-drag]");
      if (!handle || this.mode !== "edit" || e.button > 0) return;
      const kind = handle.dataset.fwDrag;
      const node = handle.closest("article") || handle;
      gesture = { kind, key: handle.dataset.fwKey, node, x: e.clientX, y: e.clientY, moved: false, pointer: e.pointerId };
      handle.setPointerCapture(e.pointerId);
      if (kind !== "move") e.preventDefault();
    });
    root.addEventListener("pointermove", (e) => {
      if (!gesture || e.pointerId !== gesture.pointer) return;
      const dx = e.clientX - gesture.x;
      const dy = e.clientY - gesture.y;
      if (!gesture.moved && Math.hypot(dx, dy) < 4) return;
      gesture.moved = true;
      if (gesture.kind === "move") gesture.node.style.translate = `${dx}px ${dy}px`;
      if (gesture.kind === "resize") gesture.node.style.outline = "2px dashed currentColor";
      if (gesture.kind === "connect") gesture.node.dataset.fwConnecting = "true";
    });
    const finish = (e) => {
      if (!gesture || e.pointerId !== gesture.pointer) return;
      const g = gesture;
      gesture = null;
      g.node.style.translate = "";
      g.node.style.outline = "";
      delete g.node.dataset.fwConnecting;
      if (!g.moved || e.type === "pointercancel") return;
      this.#suppressClick = true;
      const dx = Math.round(e.clientX - g.x);
      const dy = Math.round(e.clientY - g.y);
      if (g.kind === "move") this.#event("gesture-move", { key: g.key, value: `${dx} ${dy}` });
      if (g.kind === "resize") this.#event("gesture-resize", { key: g.key, value: `${dx} ${dy}` });
      if (g.kind === "connect") {
        const over = document.elementFromPoint(e.clientX, e.clientY)?.closest("article[data-fw-key]");
        if (over && root.contains(over)) this.#event("gesture-connect", { key: g.key, value: over.dataset.fwKey });
      }
    };
    root.addEventListener("pointerup", finish);
    root.addEventListener("pointercancel", finish);
  }
}

if (!customElements.get("forma-workflow")) customElements.define("forma-workflow", FormaWorkflowElement);
