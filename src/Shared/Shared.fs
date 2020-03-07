namespace Shared
open System

// Add more messages that can go from server -> client here...
type WebSocketServerMessage =
    | BroadcastMessage of {| Time : DateTime; Message : string |}

// Add more message that can go from client -> server here...
type WebSocketClientMessage =
    | TextMessage of string