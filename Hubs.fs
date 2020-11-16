namespace Somnifero.Hubs

open System.Threading.Tasks
open FSharp.Control.Tasks
open Microsoft.AspNetCore.SignalR
open Microsoft.AspNetCore.Authorization

type IStatsHub =
    abstract SendStats: stats:string -> Task

type StatsHub() =
    inherit Hub<IStatsHub>()

    member this.SendStats stats =
        task { do! this.Clients.All.SendStats stats }


type IRoomsHub =
    abstract GetHubs: unit -> Task<string seq>

[<Authorize>]
type RoomsHub() =
    inherit Hub<IRoomsHub>()

    member this.GetHubs() =
        task {
            return seq {
                       "Room 1"
                       "Room 2"
                   }
        }
