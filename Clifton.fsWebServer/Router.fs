module Clifton.fsRouter
// See here: http://fsharpforfunandprofit.com/posts/organizing-functions/
// The above is a shorthand for declaring a namespace and the module name, and the module content does not need to be indented.

// Initial startup code from here: https://sergeytihon.wordpress.com/2013/05/18/three-easy-ways-to-create-simple-web-server-with-f/

// Read about async workflows here: http://en.wikibooks.org/wiki/F_Sharp_Programming/Async_Workflows

open System
open System.Net
open System.Net.Sockets
open System.Text
open System.IO

open Clifton.Extensions

let POST = "post"
let GET = "get"
let PUT = "put"
let DELETE = "delete"

// From: http://stackoverflow.com/questions/13646272/using-functions-before-they-are-declared
// Since functions are first-class objects in F#, you can pass them around instead -- which presents a much nicer (and still immutable) solution than forward references.

// Read about classes, unions, records, and structures: http://msdn.microsoft.com/en-us/library/dd233205.aspx
// We use a record here because we want a reference and we don't need the complexity of a class.

// An enum
type RouterError =
    | OK = 0
    | ExpiredSession = 1
    | NotAuthorized = 2
    | FileNotFound = 3
    | PageNotFound = 4
    | ServerError = 5
    | UnknownType = 6
    | ValidationError = 7

// A record.  Records are annoying because the way the type system infers the
// record type is from the first field and only the first field, therefore,
// we put the the error field first as this is pretty unique.
type ResponsePacket = 
    {
        error : RouterError;
        redirect : string;
        data : byte[];
        contentType : string;
        encoding : Encoding;
    }

// Create a default record so we can clone it and can initialize only the properties we need.
let defaultResponsePacket = {error = RouterError.OK; redirect = null; data = null; contentType = null; encoding = Encoding.UTF8}

// Encode the string into a UTF8 byte array.
let encode (text:string) =
    Encoding.UTF8.GetBytes text

// HTML pages are spoofed to live in a Pages sub-folder rather than directly off the Website folder,
// as this reduces clutter in the Website folder.
let pageLoader (fullPath:string) (ext:string) =
    { defaultResponsePacket with data = File.ReadAllText(fullPath) |> encode; contentType = "text/" + ext}

// Return a ResponsePacket record with the image data.
let imageLoader (fullPath:string) (ext:string) = 
    let fStream = new FileStream(fullPath, FileMode.Open, FileAccess.Read)
    let br = new BinaryReader(fStream)
    let data = br.ReadBytes((int)fStream.Length)
    br.Close()
    fStream.Close()
    {defaultResponsePacket with data = data; contentType = "text/" + ext}

// Return a ResponsePacket with the encoded file data.
let fileLoader (fullPath:string) (ext:string) =
    {defaultResponsePacket with data = File.ReadAllText(fullPath) |> encode; contentType = "text/" + ext}

// If there's no extension, assume html.
// Returns a tuple consisting of the corrected path (including the extension) and the extension itself.
let defaultExtension (path:string) (ext:string) =
    match ext with
    | "" -> (path + ".html", "html")
    | _  -> (path, ext)

let spoofHtmlPath (websitePath:string) ((path:string), (ext:string)) =
    match ext with
    | "html" -> (websitePath + "\\Pages" + path.RightOf(websitePath), ext)
    | _ -> (path, ext)


// Handle the route.  
// TODO: Allow callbacks to the application
// Put the default handler into a function that the application calls if it doesn't handle the route itself.
let route (websitePath:string) (path:string) (verb:string) (kvParams:Map<string, string>) =
    let extAny = path.RightOfRightmostOf('.');
    let wpath = 
        match path with
        | "/"   -> websitePath + @"\index"
        | _     -> websitePath + path.Replace('/', '\\');            // Strip off leading '/' and reformat as with windows path separator.

    let pathAndExt = (defaultExtension wpath extAny) |> (spoofHtmlPath websitePath)
    let fullPath = fst pathAndExt
    let ext = snd pathAndExt

    // Now, if the file exists, we're good to go.
    if File.Exists(fullPath) 
        then 
            match ext with
            | "html" -> pageLoader fullPath ext
            | "ico"  -> imageLoader fullPath ext
            | "png"  -> imageLoader fullPath ext
            | "jpg"  -> imageLoader fullPath ext
            | "gif"  -> imageLoader fullPath ext
            | "bmp"  -> imageLoader fullPath ext
            | "css"  -> fileLoader fullPath ext
            | "js"   -> fileLoader fullPath ext
            | _      -> {defaultResponsePacket with error=RouterError.UnknownType}
        else {defaultResponsePacket with error=RouterError.FileNotFound}
