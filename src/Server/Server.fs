open FSharp.Control.Tasks.V2
open Giraffe
open Microsoft.AspNetCore.Http
open Saturn
open Shared
open System.IO

let publicPath = Path.GetFullPath "../Client/public"

/// Provides some simple functions over the ISocketHub interface.
module Channel =
    let getSocketHub (ctx:HttpContext) =
        ctx.GetService<Channels.ISocketHub>()

    /// Sends a message to a specific client by their socket ID.
    let sendMessage (hub:Channels.ISocketHub) socketId (payload:WebSocketMessage) = task {
        let payload = Thoth.Json.Net.Encode.Auto.toString(0, payload)
        do! hub.SendMessageToClient "/channel" socketId "" payload
    }

    /// Sends a message to all connected clients.
    let broadcastMessage (hub:Channels.ISocketHub) (payload:WebSocketMessage) = task {
        let payload = Thoth.Json.Net.Encode.Auto.toString(0, payload)
        do! hub.SendMessageToClients "/channel" "" payload
    }

    /// Sets up the channel to listen to clients.
    let channel = channel {
        join (fun ctx socketId ->
            task {
                printfn "Client has connected. They've been assigned socket Id: %O" socketId
                return Channels.Ok
            })
    }

/// Handles broadcast messages that are posted to the server.
let webApp = router {
    post "/api/broadcast" (fun next ctx ->
        task {
            let! message = ctx.BindModelAsync()
            let hub = Channel.getSocketHub ctx
            do! Channel.broadcastMessage hub (BroadcastMessage (string message))
            return! next ctx
        })
}

let app = application {
    url "http://0.0.0.0:8085/"
    use_router webApp
    memory_cache
    use_static publicPath
    use_json_serializer(Thoth.Json.Giraffe.ThothSerializer())
    use_gzip
    add_channel "/channel" Channel.channel
}

run app