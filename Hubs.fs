namespace Somnifero.Hubs

open System.Threading.Tasks
open FSharp.Control.Tasks
open Microsoft.AspNetCore.SignalR

type IStatsHub =
    abstract SendStats: stats:string -> Task

type StatsHub() =
    inherit Hub<IStatsHub>()

    member this.SendStats stats =
        task { do! this.Clients.All.SendStats stats }


type ChatHub() =
    inherit Hub()
