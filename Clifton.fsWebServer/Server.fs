module Clifton.fsWebServer
// See here: http://fsharpforfunandprofit.com/posts/organizing-functions/
// The above is a shorthand for declaring a namespace and the module name, and the module content does not need to be indented.

// Initial startup code from here: https://sergeytihon.wordpress.com/2013/05/18/three-easy-ways-to-create-simple-web-server-with-f/

// Read about async workflows here: http://en.wikibooks.org/wiki/F_Sharp_Programming/Async_Workflows

open System
open System.Net
open System.Net.Sockets
open System.Text
open System.IO

// For mutable session management.
open System.Collections.Generic;

// Notice how we're using extension methods defined in C#.  Re-use!
open Clifton.Extensions

// Must be placed before Server.fs
open Clifton.fsRouter

let siteRoot = @"E:\BasicWebServer\ConsoleWebServer\Website";

// Don't forget, (usually) functions must be declared before you can use them!

// Returns an array of local IP address.        
let getLocalHostIPs =
    let ips = Dns.GetHostEntry(Dns.GetHostName())
    ips.AddressList |> Array.filter(fun x -> x.AddressFamily = AddressFamily.InterNetwork)

let getUrl ip =
    "http://" + ip.ToString() + "/"

// Add localhost IP's to the HTTP listener.
let addLocalIPsToListener (listener:HttpListener) =
    getLocalHostIPs |> Array.iter(fun ip ->               // or: for ip in getLocalHostIPs do 
        let url = getUrl ip
        printfn "Listening on %s" url
        listener.Prefixes.Add url)

// Return a map of parameters (parameter name and value pairs) or an empty map.
let getKeyValues (parms:string) =
    if parms.Length > 0 then 
        // Create an array of key-value tuples from the "x=y" pairs and then build a map out of the sequence.
        parms.Split('&') |> Array.map(fun p -> (p.LeftOf('='), p.RightOf('='))) |> Map.ofArray
    else 
        Map.empty

// Gotta love Stack Overflow: http://stackoverflow.com/questions/3974758/in-f-how-do-you-merge-2-collections-map-instances
let join (p:Map<'a,'b>) (q:Map<'a,'b>) = 
    Map(Seq.concat [ (Map.toSeq p) ; (Map.toSeq q) ])

// Declares the function listener which accepts a function as a parameter named "handler".
// handler is a function taking request and response parameters and returning an Async<unit> "wrapper", meaning an async function that return nothing (unit) in this case.
let listener handler =
    let httpListener = new HttpListener()
    httpListener.Prefixes.Add "http://localhost/"       // Always add localhost
    addLocalIPsToListener httpListener
    httpListener.Start()

    // We need a mutable session manager because in this implementation, we can be receiving multiple
    // requests simultaneously from the same IP, and we can't "synchronize" a non-mutable session across
    // multiple threads.

    // Conversely, if we could assign a specific thread that handles all the requests from a specific remote endpoint IP,
    // then we could maintain session for that IP by passing the new session back into the listener function "recursively."
    // However, even this isn't very efficient because multiple requests for the same IP would be handled synchronously.

    // A workaround for that would be to allow certain requests (GET on content not handled by the application, for example)
    // to create response threads that don't care about session information.

    // All of this should be doable but is a level of complexity that I don't want to get into right now, so we're going to
    // have a mutable session manager!

    let sessionManager = new Dictionary<string, Dictionary<string, Object>>()

    // Creates an asynchronous computation in given the Begin/End action pair.
    let task = Async.FromBeginEnd(httpListener.BeginGetContext, httpListener.EndGetContext)

    // Prevents blocking the current computation thread.
    async {
        while true do
            // Bind the name "context" to the result of the async operation defined by "task"
            // let! runs task on its own thread and releases the current thread back to the thread pool.
            // When let! returns, execution of the workflow will continue on the new thread, which may or may not be the same thread that the workflow started out on.
            let! context = task

            // Get the session associated with the remote endpoint IP
            let ip = context.Request.RemoteEndPoint.Address.ToString()
            let session = if sessionManager.ContainsKey ip 
                            then sessionManager.[ip]
                            else
                                let sess = new Dictionary<string, Object>()
                                sessionManager.[ip] <- sess
                                sess

            // Dump the data to the console here, before we start the async handler and another async listener.
            // This keeps the console dump synchronized but hinders server performance ever so slightly.
            printfn "%s %s %s" (context.Request.RemoteEndPoint.ToString()) context.Request.HttpMethod ("/" + context.Request.Url.AbsoluteUri.RightOf('/', 3))

            // Call the handler with the request and response objects.
            // Start the computation asynchronously -- do not await its result.
            Async.Start(handler session context.Request context.Response)
    } |> Async.Start        // And here we actually start the anonymous async function we just declared.

let startServer (websitePath:string) appHandler =
    // Call the listener and pass in our async function that takes the request and response objects when a connection is made ("let! context = task" returns with a context.)
    listener (fun session request response ->
        async {
            // Dumping data to the console in the async handler can lead to weird results because the requests can be coming in so fast, the console output collides with more than one thread.
            // printfn "%s %s %s" (request.RemoteEndPoint.ToString()) request.HttpMethod ("/" + request.Url.AbsoluteUri.RightOf('/', 3))
            let path = request.RawUrl.LeftOf("?").ToLower() // Only the path, not any of the parameters
            let verb = request.HttpMethod.ToLower()         // get, post, delete, etc.
            let parms = request.RawUrl.RightOf("?")         // Params on the URL itself follow the URL and are separated by a ?
            let kvUrlParams = getKeyValues parms
            let kvRequestParams = (new StreamReader(request.InputStream, request.ContentEncoding)).ReadToEnd() |> getKeyValues
            let kvParams = join kvUrlParams kvRequestParams

            let routeInfo = route websitePath path verb kvParams
            let responsePacket = appHandler session kvParams routeInfo path verb 

            if responsePacket.redirect = null then
                response.ContentType <- responsePacket.contentType
                response.StatusCode <- (int)HttpStatusCode.OK
                response.ContentEncoding <- responsePacket.encoding
                response.OutputStream.Write(responsePacket.data, 0, responsePacket.data.Length)
            else
                response.StatusCode <- (int)HttpStatusCode.Redirect
                response.Redirect ("http://" + request.UserHostAddress + responsePacket.redirect)

            response.OutputStream.Close()
        })
