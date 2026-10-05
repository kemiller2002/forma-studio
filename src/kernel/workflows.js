// Workflow surface adapter: browser plumbing between the Studio page, the public
// Forma <forma-workflow> component and the F# engine. No application meaning:
//  - the component runs on Studio's own .NET runtime through a transport onto
//    Forma.Workflow.EmbedHost (StudioWorkflowInterop), so Studio dogfoods the
//    public component and never loads a second runtime;
//  - the component's validated changes go to the Studio engine as one semantic
//    event; the engine decides what to keep;
//  - when the engine asks for a document to be shown (data-revision changes),
//    the adapter loads it into the component;
//  - file open and download are browser capabilities handled here as plumbing.
import "./forma-workflow/forma-workflow.js";
import { editorEvents } from "./editor-events.js";

const emit = (doc, inputId, value) => {
  const input = doc.getElementById(inputId);
  input.value = value;
  input.dispatchEvent(new Event("change", { bubbles: true }));
};

const download = (doc, text, fileName, type) => {
  const url = URL.createObjectURL(new Blob([text], { type }));
  const link = doc.createElement("a");
  link.href = url;
  link.download = fileName;
  doc.body.append(link);
  link.click();
  link.remove();
  setTimeout(() => URL.revokeObjectURL(url), 1000);
};

export async function installWorkflows(doc, exportsReady) {
  const api = (await exportsReady).StudioWorkflowInterop;
  const transport = {
    async start() {},
    async dispatch(message) { return api.Dispatch(message); },
    async document(instance) { return api.Document(instance); },
    async dispose(instance) { api.Dispose(instance); },
    async validate(text) { return JSON.parse(api.Validate(text)); }
  };
  const element = doc.createElement("forma-workflow");
  element.id = "studio-workflow";
  element.setAttribute("instance", "studio");
  element.setAttribute("mode", "edit");
  element.transport = transport;
  doc.getElementById("workflow-host").append(element);

  const source = doc.getElementById("workflow-source");
  const show = () => { if (source.value) element.load(source.value); };
  new MutationObserver(show).observe(source, { attributes: true, attributeFilter: ["data-revision"] });

  element.addEventListener("forma-workflow-change", (e) => emit(doc, editorEvents.workflowChanged, JSON.stringify(e.detail.workflow)));
  doc.getElementById("workflow-file").addEventListener("change", async (e) => {
    const file = e.target.files?.[0];
    if (file) emit(doc, editorEvents.workflowOpened, await file.text());
    e.target.value = "";
  });
  doc.getElementById("workflow-download").addEventListener("click", async () => {
    const current = await element.getWorkflow();
    if (current) download(doc, JSON.stringify(current, null, 2) + "\n", source.dataset.file, "application/json");
  });
  doc.getElementById("export-download").addEventListener("click", () => {
    const text = doc.getElementById("export-text").value;
    if (text) download(doc, text, doc.getElementById("export-text").dataset.file, "text/html");
  });
  doc.documentElement.dataset.studioWorkflowReady = "true";
}
