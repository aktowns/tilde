namespace tilde

open FSharp.Markdown
open FSharp.CodeFormat

open System
open System.IO
open System.Reflection
open System.Text.RegularExpressions

type MarkdownHandler () = 
    let fsharpCompiler = Assembly.Load("FSharp.Compiler")
    let formattingAgent = CodeFormat.CreateAgent(fsharpCompiler)
    
    member private x.ReadBlocks input = 
        let rgx = new Regex("(?<!\ )\[(.*?)=(.*?)\]")
        if rgx.IsMatch(input) then
            let matches = rgx.Matches(input)
            let results = Array.zeroCreate<Match> matches.Count
            matches.CopyTo(results, 0)
            let matchVal (m: Match) (x: int) = (m.Groups.[x].Value)
            let tokens = results |> Array.map(fun x -> x.Groups.[0].Value)
            Some(tokens, results |> Array.map(fun x -> (matchVal x 1, matchVal x 2)) |> Map.ofArray)
        else None
        
    member x.ParseFile file =
        let md = Markdown.Parse (File.ReadAllText(file))
        let paragraphs = [
            for par in (md.Paragraphs) do
                yield 
                    match par with
                    | CodeBlock(block) ->
                        let maybeKeyvals = x.ReadBlocks block
                        let eatentokens, keyvals = 
                            match maybeKeyvals with 
                            | Some(eatentokens, keyvals) -> eatentokens, keyvals
                            | None -> [||], [] |> Map.ofList
                        let code =
                            let eaten =
                                eatentokens
                                |> Array.fold (fun (content: string) token -> content.Replace(token, "")) block
                            eaten.Trim()
                        
                        let lang = defaultArg (keyvals.TryFind "lang") ""
                        
                        let name = keyvals.TryFind "name"
                        let linenums = keyvals.TryFind "linenums"
                        let showlinenums = defaultArg linenums "true"
                        
                        let codeblock = 
                            match name with
                            | Some (name) ->
                                // Because i keep doing it.. 
                                if name.Contains("\"") then failwith "[name=.. should not contain speech marks"
                                
                                let snippet, err = formattingAgent.ParseSource(name, code.Trim())
                                let html = CodeFormat.FormatHtml(snippet, Guid.NewGuid().ToString())
                                let snippethtml = 
                                    html.Snippets |> Array.map(fun x -> x.Content) |> Array.fold(fun x y -> x + y) ""
                                snippethtml + html.ToolTip
                            | None ->
                                let addcss = if showlinenums = "true" then "linenums:1" else ""
                                sprintf "<pre class='prettyprint %s language-%s'>%s</pre>" addcss lang 
                                    (Html.htmlEncode(code))
                        HtmlBlock(codeblock)
                    | _ as ex -> ex
        ]
        MarkdownDocument(paragraphs, md.DefinedLinks)
        
    member x.HtmlForMarkdown = Markdown.WriteHtml
