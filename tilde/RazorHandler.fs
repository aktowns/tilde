namespace tilde

open System
open System.IO

open RazorEngine
open RazorEngine.Text
open RazorEngine.Templating
open RazorEngine.Configuration

type TemplateModel () =
    inherit Object()

type Site () = 
    static member val Posts : tilde.TemplateBaseExtensions<_>[] = [||] with get,set
    static member val Url = "" with get,set

type TemplateResolver () =
    interface ITemplateResolver with
        member x.Resolve name =            
            if File.Exists(name) then File.ReadAllText(name)
            elif File.Exists(name + ".cshtml") then 
                File.ReadAllText(name + ".cshtml")
            elif File.Exists("_layouts" + Path.DirectorySeparatorChar.ToString() + name) then 
                File.ReadAllText("_layouts" + Path.DirectorySeparatorChar.ToString() + name)
            elif File.Exists("_layouts" + Path.DirectorySeparatorChar.ToString() + name + ".cshtml") then 
                File.ReadAllText("_layouts" + Path.DirectorySeparatorChar.ToString() + name + ".cshtml")
            else failwithf "Could not find template file %s" name   

type RazorHandler (model) =
    do
        let config = new TemplateServiceConfiguration()
        config.Namespaces.Add("tilde") |> ignore
        config.EncodedStringFactory <- new RawStringFactory()
        config.Resolver <- new TemplateResolver()
        
        config.BaseTemplateType <- typedefof<tilde.TemplateBaseExtensions<_>>
        config.Debug <- true
        
        let templateservice = new TemplateService(config)
        Razor.SetTemplateService(templateservice)
    
    member x.LoadMarkdownFragment fragment = 
        x.viewBag <- new DynamicViewBag()
        
        let markdownGuid = (new Guid()).ToString()
        try
            Razor.Compile(fragment, markdownGuid)
            let tmpl = Razor.Resolve(markdownGuid, model)
            let result = tmpl.Run(new ExecuteContext(x.viewBag))
            let utmpl = (tmpl :?> tilde.TemplateBaseExtensions<_>)
            (utmpl, result)
        with
            | :? TemplateCompilationException as ex -> 
                printfn "-- Source Code --"
                ex.SourceCode.Split('\n')
                |> Array.iteri(printfn "%i: %s")
                ex.Errors |> Seq.iter(fun w -> printfn "%i(%i): %s" w.Line w.Column w.ErrorText)
                failwithf "Exception compiling markdown fragment: %A" ex.Message
               
    member x.LoadFile filename = 
        x.viewBag <- new DynamicViewBag()
        Razor.Parse(File.ReadAllText(filename), model, x.viewBag, null)
    
    member val viewBag = new DynamicViewBag() with get,set
