namespace tilde

open FSharp.Markdown
open FSharp.CodeFormat

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
            let tokens = [for i in results do yield i.Groups.[0].Value] 
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
                            | Some(eatentokens, keyvals) -> (eatentokens, keyvals)
                            | _ -> ([], [] |> Map.ofList)
                        let code =
                            let eaten =
                                eatentokens
                                |> List.fold (fun (content: string) (token: string) -> content.Replace(token, "")) block
                            eaten.Trim()
                            
                        let lang = match keyvals.TryFind "lang" with Some(lang) -> lang | _ -> ""
                        let name = keyvals.TryFind "name"
                        let linenums = keyvals.TryFind "linenums"
                        let showlinenums = defaultArg linenums "true"
                        
                        let codeblock = 
                            match name with
                            | Some (name) ->
                                // Because i keep doing it.. 
                                if name.Contains("\"") then failwith "[name=.. should not contain speech marks"
                                
                                let snippet, err = formattingAgent.ParseSource(name, code.Trim())
                                let html = CodeFormat.FormatHtml(snippet, System.Guid.NewGuid().ToString())
                                let snippethtml = 
                                    html.SnippetsHtml 
                                    |> Array.map(fun x -> x.Html)
                                    |> Array.fold(fun x y -> x + y) ""
                                snippethtml+ html.ToolTipHtml
                            | None ->
                                let addcss = if showlinenums = "true" then "linenums:1" else ""
                                sprintf "<pre class='prettyprint %s language-%s'>" addcss lang +
                                sprintf "%s" (Html.htmlEncode(code)) +
                                sprintf "</pre>"
                        HtmlBlock(codeblock)
                    | _ as ex -> ex
        ]
        MarkdownDocument(paragraphs, md.DefinedLinks)
        
    member x.HtmlForMarkdown = Markdown.WriteHtml