// The only browser wiring: Limen kernel + WebAssembly transport + gesture adapter.
import { BrowserKernel } from "./limen/index.js";
import { createStudioTransport } from "./transport.js";
import { installGestures } from "./gestures.js";

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
document.documentElement.dataset.studioReady = "true";
