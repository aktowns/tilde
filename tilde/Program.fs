module TildeGen

open System
open System.IO
open System.Text.RegularExpressions

open tilde

let model = new TemplateModel()
let razor = new RazorHandler(model)

let postsDir = Path.Combine(Environment.CurrentDirectory,"_posts")

let postItems = 
    let rec recurseDirectories (dirInfo: DirectoryInfo) : string list = 
        let dirs = 
            dirInfo.GetDirectories() 
            |> List.ofArray
            |> List.map(recurseDirectories)
        
        let files = 
            dirInfo.GetFiles() 
            |> List.ofArray 
            |> List.map (fun x -> x.FullName)
        
        files @ (dirs |> List.concat)
    recurseDirectories (new DirectoryInfo(postsDir)) |> List.rev

let copyItems() =
    let fileHasPrefix prefix (file: FileInfo) = 
        file.Name.StartsWith(prefix) ||  file.DirectoryName.StartsWith(prefix)
        
    let currDir = DirectoryInfo(Environment.CurrentDirectory)
    
    let nameEndsWith (fi: FileInfo) (items: string list) =
        match items |> List.tryFind(fun x -> fi.Name.EndsWith(x)) with | Some(x) -> true | None -> false
    
    let nameStartsWith (fi: FileInfo) (items: string list) =
        match items |> List.tryFind(fun x -> fi.Name.StartsWith(x)) with | Some(x) -> true | None -> false
    
    let notUnderscore (item: 'T when 'T :> FileSystemInfo) = 
        not (item.FullName.Replace(Environment.CurrentDirectory + System.IO.Path.DirectorySeparatorChar.ToString(), "").StartsWith("_"))
        
    let relative (item: 'T when 'T :> FileSystemInfo) =
        (item.FullName.Replace(Environment.CurrentDirectory + System.IO.Path.DirectorySeparatorChar.ToString(), ""))
    
    currDir.GetDirectories("*", SearchOption.AllDirectories)
    |> Array.filter(notUnderscore)
    |> Array.iter(fun x -> 
        let dir = sprintf "_site/%s/" (relative x)
        printfn "Creating: %s" dir
        Directory.CreateDirectory(dir) |> ignore)
    
    currDir.GetFiles("*", SearchOption.AllDirectories)
    |> Array.filter(notUnderscore)
    |> Array.iter(fun x -> 
        if not (nameStartsWith x ["."; "_"]) &&
           not (nameEndsWith x [".dll"; ".exe"]) then
            let file = sprintf "_site/%s" (relative x)
                
            if File.Exists(file) then File.Delete(file)
            
            if nameEndsWith x [".cshtml"] then
                printfn "Compiling: %s" file
                File.WriteAllText(file.Replace(".cshtml", ".html"), razor.LoadFile(x.FullName))
            else
                printfn "Copying: %s" file                
                File.Copy(x.FullName, file))

[<EntryPoint>]
let main args =
    let posts = 
        if args.Length > 0 then 
            args 
            |> List.ofArray
            |> List.map(fun x -> (new FileInfo(x)).FullName)
        else postItems
        
    let FileName (fi: string) = (new FileInfo(fi)).Name.Replace(".md", ".html")
    
    let inferDate input = 
        let regex = (new Regex("([0-9]{4}-[0-9]{2}-[0-9]{2})"))
        if regex.IsMatch(input) then DateTime.Parse(regex.Match(input).Groups.[1].Value)
        else DateTime.Now
     
    posts
    |> List.iter (fun x ->
        let siteDir = x.Remove(0, Environment.CurrentDirectory.Length)
        
        let lastPath =             
            siteDir.Split(System.IO.Path.DirectorySeparatorChar)
            |> List.ofArray |> List.rev
            |> List.tail |> List.head 
            |> sprintf "_site/posts/%s/"
    
        printfn "Compiling markdown: %s" ((FileName x).Replace(siteDir, ""))
        let markdown = new MarkdownHandler()
        let markdownDoc = markdown.ParseFile x
        let markdownHtml = (markdown.HtmlForMarkdown markdownDoc)
        let tmpl, file = razor.LoadMarkdownFragment markdownHtml
        
        tmpl.Url <- lastPath.Substring(5) + FileName x
        tmpl.Date <- inferDate (FileName x)
        
        Site.Posts <- Array.append Site.Posts [|tmpl|]

        Directory.CreateDirectory(lastPath) |> ignore
        File.WriteAllText(lastPath + (FileName x), file))
    copyItems()
    0

