module Client

open Elmish
open Elmish.React
open Fable.React
open Thoth.Fetch
open Fulma
open Browser.Types
open Shared
open System

/// Status of the websocket.
type ConnectionState = DisconnectedFromServer | ConnectedToServer | Connecting

type Model =
    { MessageToSend : string
      ReceivedMessage : string
      ConnectionState : ConnectionState }

type Msg =
    | ReceivedFromServer of WebSocketMessage
    | ConnectionChange of ConnectionState
    | MessageChanged of string
    | Broadcast of string

module Channel =
    open Browser.WebSocket

    let inline decode<'a> x = x |> unbox<string> |> Thoth.Json.Decode.Auto.unsafeFromString<'a>

    let subscription _ =
        let sub dispatch =
            /// Handles push messages from the server and relays them into Elmish messages.
            let onWebSocketMessage (msg:MessageEvent) =
                let msg = msg.data |> decode<{| Payload : string |}>
                msg.Payload
                |> decode<WebSocketMessage>
                |> ReceivedFromServer
                |> dispatch

            /// Continually tries to connect to the server websocket.
            let rec connect () =
                let ws = WebSocket.Create "ws://localhost:8085/channel"
                ws.onmessage <- onWebSocketMessage
                ws.onopen <- (fun ev ->
                    dispatch (ConnectionChange ConnectedToServer)
                    printfn "WebSocket opened")
                ws.onclose <- (fun ev ->
                    dispatch (ConnectionChange DisconnectedFromServer)
                    printfn "WebSocket closed. Retrying connection"
                    promise {
                        do! Promise.sleep 2000
                        dispatch (ConnectionChange Connecting)
                        connect() })

            connect()

        Cmd.ofSub sub

let init () =
    { MessageToSend = null
      ConnectionState = DisconnectedFromServer
      ReceivedMessage = null }, Cmd.none

let update msg model : Model * Cmd<Msg> =
    match msg with
    | MessageChanged msg ->
        { model with MessageToSend = msg }, Cmd.none
    | ConnectionChange status ->
        { model with ConnectionState = status }, Cmd.none
    | ReceivedFromServer (BroadcastMessage msg) ->
        { model with ReceivedMessage = sprintf "Broadcast from a client: '%s'" msg }, Cmd.none
    | Broadcast msg ->
        let res = Cmd.OfPromise.result (Fetch.post("/api/broadcast", msg))
        model, res

module ViewParts =
    let drawStatus connectionState =
        Tag.tag [
            Tag.Color
                (match connectionState with
                 | DisconnectedFromServer -> Color.IsDanger
                 | Connecting -> Color.IsWarning
                 | ConnectedToServer -> Color.IsSuccess)
        ] [
            match connectionState with
            | DisconnectedFromServer -> str "Disconnected from server"
            | Connecting -> str "Connecting..."
            | ConnectedToServer -> str "Connected to server"
        ]


let view (model : Model) (dispatch : Msg -> unit) =
    div [] [
        Navbar.navbar [ Navbar.Color IsPrimary ] [
            Navbar.Item.div [ ] [
                Heading.h2 [ ] [ str "SAFE Reactive Template" ]
            ]
        ]
        Container.container [] [
            Content.content [ Content.Modifiers [ Modifier.TextAlignment (Screen.All, TextAlignment.Centered) ] ] [
                Heading.h3 [] [ str "Send a message!" ]
                Input.text [ Input.OnChange(fun e -> dispatch(MessageChanged e.Value)) ]
            ]
            Button.button
                [ Button.IsFullWidth
                  Button.Color IsPrimary
                  Button.Disabled (String.IsNullOrEmpty model.MessageToSend || model.ConnectionState <> ConnectedToServer)
                  Button.OnClick (fun _ -> dispatch (Broadcast model.MessageToSend)) ]
                [ str "Click to broadcast!" ]

            ViewParts.drawStatus model.ConnectionState

            if not (String.IsNullOrEmpty model.ReceivedMessage) then
                Heading.h4 [ Heading.IsSubtitle ] [
                    str (sprintf "Received from server: %s" model.ReceivedMessage)
                ]
        ]
        Footer.footer [ ] [
            Content.content [ Content.Modifiers [ Modifier.TextAlignment (Screen.All, TextAlignment.Centered) ] ] [
                str "Demo by Compositional IT"
            ]
        ]
    ]

open Elmish.Debug
open Elmish.HMR

Program.mkProgram init update view
|> Program.withConsoleTrace
|> Program.withSubscription Channel.subscription
|> Program.withReactSynchronous "elmish-app"
|> Program.withDebugger
|> Program.run
