using System.Runtime.InteropServices.JavaScript;
using System.Runtime.Versioning;
using FormaStudio.Engine;

/// Marshalling glue only. Every decision lives in the F# engine.
[SupportedOSPlatform("browser")]
public static partial class StudioInterop
{
    private static EditorState state = EditorApp.start();

    [JSExport]
    public static string Dispatch(string messageJson)
    {
        var result = EditorApp.dispatchText(messageJson, state);
        state = result.Item1;
        return result.Item2;
    }

    public static void Main() { }
}

/// Marshalling glue for the public Forma workflow component (<forma-workflow>).
/// Studio hosts the component with this transport instead of loading a second
/// .NET runtime; every decision is in Forma.Workflow (EmbedHost).
[SupportedOSPlatform("browser")]
public static partial class StudioWorkflowInterop
{
    private static Microsoft.FSharp.Collections.FSharpMap<string, Forma.Workflow.EmbedState> sessions = Forma.Workflow.EmbedHost.empty;

    [JSExport]
    public static string Dispatch(string messageJson)
    {
        var result = Forma.Workflow.EmbedHost.dispatch(Forma.Workflow.RenderOptionsModule.defaults, messageJson, sessions);
        sessions = result.Item1;
        return result.Item2;
    }

    [JSExport]
    public static string Document(string instance) => Forma.Workflow.EmbedHost.document(instance, sessions)?.Value ?? "";

    [JSExport]
    public static void Dispose(string instance) => sessions = Forma.Workflow.EmbedHost.dispose(instance, sessions);

    [JSExport]
    public static string Validate(string documentText) =>
        Forma.Workflow.JsonModule.compact(Forma.Workflow.Validation.reportJson(Forma.Workflow.Validation.load(documentText)));
}
