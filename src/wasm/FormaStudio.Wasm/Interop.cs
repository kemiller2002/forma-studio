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
