// Limen EngineTransport backed by the F# engine compiled to .NET WebAssembly.
// It carries serialized messages only; it makes no application decision.
import { dotnet } from "./_framework/dotnet.js";

let exportsReady;

/** The assembly exports once the runtime has started (shared with the workflow adapter). */
export const studioExports = () => exportsReady;

export const createStudioTransport = () => {
  let engine;
  return {
    async start() {
      exportsReady = (async () => {
        const runtime = await dotnet.create();
        return runtime.getAssemblyExports(runtime.getConfig().mainAssemblyName);
      })();
      engine = (await exportsReady).StudioInterop;
    },
    async dispatch(message) {
      return JSON.parse(engine.Dispatch(JSON.stringify(message)));
    }
  };
};
