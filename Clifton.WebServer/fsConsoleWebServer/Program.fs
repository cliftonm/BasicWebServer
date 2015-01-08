open System

open Clifton.fsWebServer

[<EntryPoint>]
let main argv = 
    startServer @"E:\BasicWebServer\ConsoleWebServer\Website"
    Console.ReadLine() |> ignore
    0 // return an integer exit code
