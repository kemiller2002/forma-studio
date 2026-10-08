// The only browser wiring: Limen kernel + WebAssembly transport + gesture and workflow adapters.
import { BrowserKernel } from "./limen/index.js";
import { createStudioTransport, studioExports } from "./transport.js";
import { installGestures } from "./gestures.js";
import { installWorkflows } from "./workflows.js";
import { installIconBrowser } from "./icon-browser.js";

// Bridge mechanism diagnostics only (never domain meaning).
const diagnostics = {
  report(event) {
    if (event.kind === "BridgeError") {
      console.error(`Limen ${event.phase} error: ${event.detail}`);
      document.documentElement.dataset.studioBridgeError = event.phase;
    }
  }
};

installGestures(document);
await new BrowserKernel(createStudioTransport(), document, diagnostics).start();
await installWorkflows(document, studioExports());
await installIconBrowser(document);
document.documentElement.dataset.studioReady = "true";
