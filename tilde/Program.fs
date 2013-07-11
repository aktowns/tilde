module TildeGen

open System
open System.IO
open System.Text.RegularExpressions

open tilde

let model = new TemplateModel()
let razor = new RazorHandler(model)

// cross-platform
let (@@) a b = Path.Combine(a, b)
let dirSep = Path.DirectorySeparatorChar.ToString()

let postsDir = Environment.CurrentDirectory @@ "_posts"


let postItems = 
    let rec recurseDirectories (dirInfo: DirectoryInfo) = 
        let dirs = dirInfo.GetDirectories() |> Array.map(recurseDirectories)
        let files = dirInfo.GetFiles() |> Array.map (fun x -> x.FullName)
        
        Array.append files (dirs |> Array.concat)
    recurseDirectories (new DirectoryInfo(postsDir)) |> Array.rev

let copyItems() =
    let fileHasPrefix prefix (file: FileInfo) = 
        file.Name.StartsWith(prefix) || file.DirectoryName.StartsWith(prefix)
        
    let currDir = DirectoryInfo(Environment.CurrentDirectory)
    
    let nameEndsWith (fi: FileInfo) items = items |> List.exists(fun x -> fi.Name.EndsWith(x))    
    let nameStartsWith (fi: FileInfo) items = items |> List.exists(fun x -> fi.Name.StartsWith(x))
    
    let notUnderscore (item: FileSystemInfo) = 
        not (item.FullName.Replace(Environment.CurrentDirectory + dirSep, "").StartsWith("_"))
        
    let relative (item: FileSystemInfo) =
        (item.FullName.Replace(Environment.CurrentDirectory + dirSep, ""))
    
    currDir.GetDirectories("*", SearchOption.AllDirectories)
    |> Array.filter(notUnderscore)
    |> Array.iter(fun x -> 
        let dir = "_site" @@ (relative x) + dirSep
        printfn "Creating: %s" dir
        Directory.CreateDirectory(dir) |> ignore)
    
    currDir.GetFiles("*", SearchOption.AllDirectories)
    |> Array.filter(notUnderscore)
    |> Array.iter(fun x -> 
        if not (nameStartsWith x ["."; "_"]) &&
           not (nameEndsWith x [".dll"; ".exe"]) then
            let file = "_site" @@ (relative x)
            if File.Exists(file) then File.Delete(file)
            
            if nameEndsWith x [".cshtml"] then
                printfn "Compiling: %s" file
                File.WriteAllText(Path.ChangeExtension(file, ".html"), razor.LoadFile(x.FullName))
            else
                printfn "Copying: %s" file                
                File.Copy(x.FullName, file))

[<EntryPoint>]
let main args =
    let posts = 
        if args.Length > 0 then args |> Array.map(fun x -> (new FileInfo(x)).FullName)
        else postItems
    
    let fileName fi = Path.ChangeExtension((new FileInfo(fi)).Name, ".html")
    
    let inferDate input = 
        let regex = (new Regex("([0-9]{4}-[0-9]{2}-[0-9]{2})"))
        if regex.IsMatch(input) then DateTime.Parse(regex.Match(input).Groups.[1].Value)
        else DateTime.Now
     
    posts
    |> Array.iter (fun post ->
        let postsDir = "_site" @@ "posts"
        let siteDir = post.Remove(0, (Environment.CurrentDirectory @@ "_posts").Length)
        let basePath = 
            let siteDirLen = ((siteDir.Split(Path.DirectorySeparatorChar) |> Array.rev).[0].Length)
            (siteDir.Remove(siteDir.Length - siteDirLen))
        printfn "%A" basePath
        
        let lastPath = postsDir + basePath
        printfn "%A/%A" postsDir basePath
        printfn ">>>> %A" lastPath
        
        let postFilename = fileName post
        printfn "Compiling markdown: %s" (postFilename.Replace(siteDir, ""))
        let tmpl, file = 
            let markdown = new MarkdownHandler()
            let markdownDoc = markdown.ParseFile post
            let markdownHtml = markdown.HtmlForMarkdown markdownDoc
            razor.LoadMarkdownFragment markdownHtml
        
        tmpl.Url <- lastPath.Substring(5) + postFilename
        tmpl.Date <- inferDate postFilename
        
        Site.Posts <- Array.append Site.Posts [|tmpl|]
        printfn ">>> %A" lastPath
        Directory.CreateDirectory(lastPath) |> ignore
        File.WriteAllText(lastPath @@ postFilename, file))
    copyItems()
    0

