namespace Somnifero.Hubs

open System.Threading.Tasks
open FSharp.Control.Tasks
open Microsoft.AspNetCore.SignalR
open Microsoft.AspNetCore.Authorization
open MongoDB.Bson
open Somnifero
open Somnifero.Types
open System

type IStatsHub =
    abstract SendStats: stats:string -> Task

type StatsHub() =
    inherit Hub<IStatsHub>()

    member this.SendStats stats =
        task { do! this.Clients.All.SendStats stats }


type IRoomsHub =
    abstract GetPublicRooms: page:int * limit:int -> Task<PaginatedResult<Room>>
    abstract AddRoom: name:string * isPublic:bool * topics:seq<string> -> Task<unit>
    abstract UpdatePublicRoomList: unit -> Task<PaginatedResult<Room>>
    abstract SendMessageToGroup: unit -> Task<unit>
    abstract JoinRoom: name: string -> Task<unit>
    abstract LeaveRoom: name: string -> Task<unit>


[<Authorize>]
type RoomsHub() =
    inherit Hub<IRoomsHub>()

    member this.GetPublicRooms (page: int) (limit: int) =
        task { return Rooms.GetPublicRooms page limit }

    member this.UpdatePublicRoomList() =
        task {
            let! result = Rooms.GetPublicRooms 1 10
            do! (this :> Hub).Clients.All.SendAsync("UpdatePublicRoomList", result)
        }

    member this.AddRoom(name: string, isPublic: bool, topics: seq<string>): Task<unit> =
        task {
            let claim =
                this.Context.User.Claims
                |> Seq.tryFind (fun c -> c.Type.Equals("_id", StringComparison.OrdinalIgnoreCase))

            match claim with
            | Some claim ->
                let id = ObjectId.Parse claim.Value

                let! room =
                    Rooms.CreateRoom
                        {| name = name
                           ``public`` = isPublic
                           topics = topics
                           owner = id |}

                match room with
                | Ok didCreate ->
                    if didCreate
                    then do! this.Groups.AddToGroupAsync(this.Context.ConnectionId, name)
                | Error exn -> raise exn
            | None -> failwith "User could not be found"
        }

    member this.JoinRoom(group: string) = 
        task {
            do! (this :> Hub).Groups.AddToGroupAsync(this.Context.ConnectionId, group)
        }
    
    member this.LeaveRoom (group: string) = 
        task {
            do! (this :> Hub).Groups.RemoveFromGroupAsync(this.Context.ConnectionId, group)
        }

    member this.SendMessageToGroup (group: string) (message: string) =
        task { 
            do! (this :> Hub).Clients.Group(group).SendAsync("SendMessage", group, message) 
        }

