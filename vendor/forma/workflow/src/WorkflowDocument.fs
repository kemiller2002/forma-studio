namespace Forma.Workflow

open System

/// Requested interactivity for exported HTML. Static is the default and adds no script.
type Interactivity =
    | StaticOutput
    /// Wraps the static figure in the public <forma-workflow> element, which
    /// upgrades it through the @echelon-foundry/forma-workflow runtime. Without
    /// script the static figure still renders.
    | InteractiveOutput of mode: HostMode * runtimeBase: string

type DocumentOptions =
    { Render: RenderOptions
      /// URL prefix where the Forma package's dist files are served,
      /// e.g. "node_modules/@echelon-foundry/design-system/dist/" or "/assets/forma/".
      FormaBase: string
      /// Optional public Forma brand id (loads brands/<id>.css and sets data-ef-brand).
      Brand: string option
      /// Optional explicit theme ("light" or "dark").
      Theme: string option
      Interactivity: Interactivity }

[<RequireQualifiedAccess>]
module DocumentOptions =
    let defaults =
        { Render = RenderOptions.defaults
          FormaBase = "node_modules/@echelon-foundry/design-system/dist/"
          Brand = None
          Theme = None
          Interactivity = StaticOutput }

/// Complete HTML documents and interactive wrappers.
[<RequireQualifiedAccess>]
module WorkflowDocument =
    let private el = Markup.el

    let private brandPattern = System.Text.RegularExpressions.Regex("^[a-z][a-z0-9-]{0,63}$")

    let private validBrand (brand: string option) = brand |> Option.filter brandPattern.IsMatch

    let private validTheme (theme: string option) = theme |> Option.filter (fun t -> t = "light" || t = "dark")

    let private joinUrl (baseUrl: string) (path: string) =
        if baseUrl = "" then path elif baseUrl.EndsWith "/" then baseUrl + path else baseUrl + "/" + path

    /// The dependencies for given output options.
    let dependencies (opts: DocumentOptions) =
        let brand = validBrand opts.Brand |> Option.map (fun b -> Stylesheet(Render.formaPackage, Capabilities.formaVersion.ToString(), $"brands/{b}.css")) |> Option.toList
        let runtime =
            match opts.Interactivity with
            | StaticOutput -> []
            | InteractiveOutput _ ->
                [ Stylesheet(Render.workflowPackage, Render.workflowPackageVersion, "forma-workflow.css")
                  RuntimeModule(Render.workflowPackage, Render.workflowPackageVersion, "forma-workflow.js") ]
        Render.staticDependencies @ brand @ runtime

    /// The workflow wrapped for interactive output: the public custom element,
    /// its portable JSON source as an inert data block, and the static figure as
    /// fallback content.
    let interactiveElement (opts: RenderOptions) (mode: HostMode) (w: Workflow) =
        el
            "forma-workflow"
            [ "mode", HostMode.text mode; "workflow", w.Id.Value ]
            [ Markup.scriptData [ "class", "forma-workflow-source" ] (Codec.encode w); Render.figure opts w ]

    /// The body content for given options (a fragment when static).
    let content (opts: DocumentOptions) (w: Workflow) =
        match opts.Interactivity with
        | StaticOutput -> Render.figure opts.Render w
        | InteractiveOutput(mode, _) -> interactiveElement opts.Render mode w

    /// A fragment with its dependency comment, honouring interactivity.
    let fragment (opts: DocumentOptions) (w: Workflow) : RenderedHtml =
        let deps = dependencies opts
        { Html = Markup.renderAll [ Render.dependencyComment deps; content opts w ]; Dependencies = deps }

    /// A complete, valid HTML document that opens as ordinary HTML with the
    /// declared public assets.
    let document (opts: DocumentOptions) (w: Workflow) : RenderedHtml =
        let deps = dependencies opts
        let brand = validBrand opts.Brand
        let links =
            deps
            |> List.choose (function
                | Stylesheet(pkg, _, path) when pkg = Render.formaPackage -> Some(el "link" [ "rel", "stylesheet"; "href", joinUrl opts.FormaBase path ] [])
                | Stylesheet(_, _, path) ->
                    match opts.Interactivity with
                    | InteractiveOutput(_, runtimeBase) -> Some(el "link" [ "rel", "stylesheet"; "href", joinUrl runtimeBase path ] [])
                    | StaticOutput -> None
                | RuntimeModule _ -> None)
        let scripts =
            match opts.Interactivity with
            | InteractiveOutput(_, runtimeBase) -> [ Markup.moduleScript (joinUrl runtimeBase "forma-workflow.js") ]
            | StaticOutput -> []
        let htmlAttrs =
            [ yield "lang", w.Language |> Option.defaultValue "en"
              match w.TextDirection with
              | Some Rtl -> yield "dir", "rtl"
              | Some Ltr -> yield "dir", "ltr"
              | _ -> ()
              match brand with Some b -> yield "data-ef-brand", b | None -> ()
              match validTheme opts.Theme with Some t -> yield "data-ef-theme", t | None -> () ]
        let head =
            el
                "head"
                []
                ([ el "meta" [ "charset", "utf-8" ] []
                   el "meta" [ "name", "viewport"; "content", "width=device-width, initial-scale=1" ] []
                   el "title" [] [ Text w.Title ] ]
                 @ (w.Description |> Option.map (fun d -> el "meta" [ "name", "description"; "content", d ] []) |> Option.toList)
                 @ [ el "meta" [ "name", "generator"; "content", $"Forma workflow renderer {Format.current}" ] [] ]
                 @ links
                 @ scripts)
        let body = el "body" [] [ el "main" [] [ content opts w ] ]
        { Html = "<!doctype html>\n" + Markup.renderAll [ el "html" htmlAttrs [ head; body ] ]; Dependencies = deps }

    /// A machine-readable dependency manifest for an export.
    let dependencyJson (deps: Dependency list) =
        Json.Array(
            deps
            |> List.map (function
                | Stylesheet(pkg, v, path) -> Json.Object [ "kind", Json.String "stylesheet"; "package", Json.String pkg; "version", Json.String v; "path", Json.String path ]
                | RuntimeModule(pkg, v, path) -> Json.Object [ "kind", Json.String "module"; "package", Json.String pkg; "version", Json.String v; "path", Json.String path ])
        )
