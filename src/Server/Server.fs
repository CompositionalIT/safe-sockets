open FSharp.Control.Tasks.V2
open Giraffe
open Microsoft.AspNetCore.Http
open Microsoft.Extensions.Logging
open Saturn
open Shared
open System.IO

let publicPath = Path.GetFullPath "../Client/public"

/// Provides some simple functions over the ISocketHub interface.
module Channel =
    open Thoth.Json.Net

    /// Sends a message to a specific client by their socket ID.
    let sendMessage (hub:Channels.ISocketHub) socketId (payload:WebSocketServerMessage) = task {
        let payload = Encode.Auto.toString(0, payload)
        do! hub.SendMessageToClient "/channel" socketId "" payload
    }

    /// Sends a message to all connected clients.
    let broadcastMessage (hub:Channels.ISocketHub) (payload:WebSocketServerMessage) = task {
        let payload = Encode.Auto.toString(0, payload)
        do! hub.SendMessageToClients "/channel" "" payload
    }

    /// Sets up the channel to listen to clients.
    let channel = channel {
        join (fun ctx socketId ->
            task {
                ctx.GetLogger().LogInformation("Client has connected. They've been assigned socket Id: {socketId}", socketId)
                return Channels.Ok
            })
        handle "" (fun ctx message ->
            task {
                let hub = ctx.GetService<Channels.ISocketHub>()
                let message = message.Payload |> string |> Decode.Auto.unsafeFromString<WebSocketClientMessage>

                // Here we handle any websocket client messages in a type-safe manner
                match message with
                | TextMessage message ->
                    let message = sprintf "Websocket message: %s" message
                    do! broadcastMessage hub (BroadcastMessage {| Time = System.DateTime.UtcNow; Message = message |})
            })
    }



/// Handles broadcast messages that are posted to the server via HTTP.
let webApp = router {
    post "/api/broadcast" (fun next ctx ->
        task {
            let! message = ctx.BindModelAsync()
            let hub = ctx.GetService<Channels.ISocketHub>()
            let message = sprintf "HTTP message: %O" message
            do! Channel.broadcastMessage hub (BroadcastMessage {| Time = System.DateTime.UtcNow; Message = message |})
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