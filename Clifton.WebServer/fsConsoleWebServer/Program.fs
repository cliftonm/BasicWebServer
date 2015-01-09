open System
open System.Text

// For mutable session collection.
open System.Collections.Generic

open Clifton.fsWebServer
open Clifton.fsRouter

let ajaxGetResponder kvParams (path:string, ext:string) (verb:string) =
    match Map.tryFind "number" kvParams with
    | Some x -> {defaultResponsePacket with data = "You said " + x |> encode; contentType = "text"}
    | None   -> defaultHandler (path, ext) verb

let appHandler (session:Dictionary<string, Object>) kvParams (path:string, ext:string) (requestPath:string) (verb:string) =
    match verb, requestPath with
    | verb, "/demo/redirect" when verb = post -> {defaultResponsePacket with redirect = "/demo/clicked"}
    | verb, "/demo/ajax" when verb = put -> {defaultResponsePacket with data = "You said " + (Map.find "number" kvParams) |> encode; contentType = "text"}
    | verb, "/demo/ajax" when verb = get -> ajaxGetResponder kvParams (path, ext) verb
    | _, _ -> defaultHandler (path, ext) verb

[<EntryPoint>]
let main argv = 
    startServer @"C:\BasicWebServer\ConsoleWebServer\Website" appHandler
    Console.ReadLine() |> ignore
    0 // return an integer exit code
