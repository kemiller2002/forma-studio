// Limen EngineTransport backed by the F# engine compiled to .NET WebAssembly.
// It carries serialized messages only; it makes no application decision.
import { dotnet } from "./_framework/dotnet.js";

export const createStudioTransport = () => {
  let engine;
  return {
    async start() {
      const runtime = await dotnet.create();
      const exports = await runtime.getAssemblyExports(runtime.getConfig().mainAssemblyName);
      engine = exports.StudioInterop;
    },
    async dispatch(message) {
      return JSON.parse(engine.Dispatch(JSON.stringify(message)));
    }
  };
};
