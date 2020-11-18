namespace Somnifero

open FSharp.Control.Tasks
open System
open MongoDB.Driver
open MongoDB.Bson
open Somnifero.Types
open System.Threading.Tasks
open System.Text.RegularExpressions
open System.Security.Cryptography

[<RequireQualifiedAccess>]
module Database =

    let private connectionString =
        Environment.GetEnvironmentVariable "MONGO_URL"
        |> Option.ofObj
        |> Option.defaultValue "mongodb://database:27017/somniferodb"

    [<Literal>]
    let DatabaseName = "somniferodb"

    [<Literal>]
    let InvitesCollection = "somn_invites"

    let client () = MongoClient(connectionString)

    let db =
        lazy (client().GetDatabase(DatabaseName))


[<RequireQualifiedAccess>]
module Users =
    type UserWithPassword =
        {| _id: ObjectId
           name: string
           lastName: string
           email: string
           password: string
           invite: string |}

    [<Literal>]
    let private UserColName = "somn_users"

    let private users () =
        lazy (Database.db.Value.GetCollection<User> UserColName)

    let private usersWithPassword () =
        lazy (Database.db.Value.GetCollection<UserWithPassword>(UserColName))

    type UserKind =
        | User of User
        | UserWithPassword of UserWithPassword

    let TryFindUserByEmail (email: string) (withPassword: bool): Task<Option<UserKind>> =
        task {
            if withPassword then
                let find =
                    usersWithPassword().Value.Find(fun u -> u.email = email)

                let! queryResult = find.FirstOrDefaultAsync()

                let user =
                    queryResult
                    |> box
                    |> Option.ofObj
                    |> Option.map (fun u -> u :?> UserWithPassword)

                return match user with
                       | Some user -> Some(UserWithPassword user)
                       | None -> None
            else
                let find =
                    users().Value.Find(fun u -> u.email = email)

                let! queryResult = find.FirstOrDefaultAsync()

                let user =
                    queryResult
                    |> box
                    |> Option.ofObj
                    |> Option.map (fun u -> u :?> User)

                return match user with
                       | Some user -> Some(User user)
                       | None -> None
        }

    let TryCreateUser (user: {| name: string
                                lastName: string
                                email: string
                                password: string
                                invite: string |})
                      : Task<Result<bool, exn>> =
        task {
            try
                let regex =
                    Regex(@"^(?=.*[a-z])(?=.*[A-Z])(?=.*\d)[a-zA-Z\d]{8,}$")

                match regex.IsMatch(user.password) with
                | true ->
                    let hex =
                        seq {
                            for _ in 0 .. 10 do
                                RandomNumberGenerator.GetInt32(1000)
                        }
                        |> Seq.map (fun n -> n.ToString("X"))
                        |> Seq.reduce (+)


                    let user =
                        {| user with
                               password = BCrypt.Net.BCrypt.EnhancedHashPassword(user.password)
                               invite = hex.Substring(0, (if hex.Length > 10 then 10 else hex.Length - 1)) |}

                    let col =
                        Database.db.Value.GetCollection<{| name: string
                                                           lastName: string
                                                           email: string
                                                           password: string
                                                           invite: string |}>(UserColName)

                    do! col.InsertOneAsync(user)

                    return Ok true
                | false -> return Error(exn "The password doesn't match security parameters")
            with ex -> return Error ex
        }

    let EmailExists (email: string) =
        task {
            let! count = users().Value.CountDocumentsAsync(fun u -> u.email = email)
            return count > 0L
        }

    let InviteExists (invite: string) =
        task {
            let adminInvites =
                Database.db.Value.GetCollection<{| invite: string |}>(Database.InvitesCollection)

            let! invitesCount = adminInvites.CountDocumentsAsync(fun i -> i.invite = invite)

            let! count = users().Value.CountDocumentsAsync(fun u -> u.invite = invite)

            return count > 0L || invitesCount > 0L
        }

[<RequireQualifiedAccess>]
module Rooms =
    [<Literal>]
    let private RoomsColName = "somn_rooms"

    [<Literal>]
    let private RoomUsersColName = "somn_rooms_users"

    let private rooms () =
        lazy (Database.db.Value.GetCollection<Room> RoomsColName)


    let RoomExists (owner: ObjectId) (name: string) =
        task {
            let col =
                Database.db.Value.GetCollection<{| owner: ObjectId; name: string |}>(RoomsColName)

            let! count = col.CountDocumentsAsync(fun r -> r.owner = owner && r.name = name)

            return count > 0L
        }

    let CreateRoom (room: {| owner: ObjectId
                             name: string
                             topics: seq<string>
                             ``public``: bool |})
                   : Task<Result<bool, exn>> =
        task {
            let col =
                Database.db.Value.GetCollection<{| owner: ObjectId
                                                   name: string
                                                   topics: seq<string>
                                                   ``public``: bool |}>(RoomsColName)

            match! RoomExists room.owner room.name with
            | true -> return Error(exn "The Room already exists")
            | false ->
                try
                    do! col.InsertOneAsync(room)
                    return Ok true
                with ex -> return Error ex
        }

    let GetPublicRooms (page: int) (limit: int) =
        task {
            let col = rooms().Value
            let query = col.Find(fun r -> r.``public``)
            let! count = query.CountDocumentsAsync()
            let offset = limit * (page - 1)
            let limit = Nullable(limit)
            let offset = Nullable(offset)

            let result =
                query.Limit(limit).Skip(offset).ToEnumerable()

            return { items = result; count = count }
        }

    let ListRoomsByUser (user: ObjectId): Task<Result<seq<RoomUser>, exn>> =
        task {
            let col =
                Database.db.Value.GetCollection<RoomUser>(RoomUsersColName)

            try
                let! rooms = col.FindAsync(fun ru -> ru.user = user)
                return Ok(rooms.ToEnumerable())
            with ex -> return Error ex
        }

    let TryGetRoomForUser (user: ObjectId) (room: ObjectId): Task<Result<Option<RoomUser>, exn>> =
        task {
            let col =
                Database.db.Value.GetCollection<RoomUser>(RoomUsersColName)

            try
                let! room = col.FindAsync(fun ru -> ru.user = user && ru.room = room)
                let! first = room.FirstOrDefaultAsync()

                return Ok
                           (first
                            |> box
                            |> Option.ofObj
                            |> Option.map (fun ru -> ru :?> RoomUser))
            with ex -> return Error ex
        }

    let JoinRoom (room: ObjectId) (user: ObjectId) =
        task {

            match! TryGetRoomForUser user room with
            | Ok result ->
                match result with
                | Some _ ->
                    // user is already joined, no need to insert another one
                    return Ok false
                | None ->
                    let col =
                        Database.db.Value.GetCollection<{| room: ObjectId; user: ObjectId |}>(RoomUsersColName)

                    try
                        do! col.InsertOneAsync({| room = room; user = user |})
                        return Ok true
                    with ex -> return Error ex
            | Error ex -> return Error ex
        }

    let LeaveRoom (room: ObjectId) (user: ObjectId) =
        task {
            match! TryGetRoomForUser user room with
            | Ok result ->
                match result with
                | Some room ->
                    let col =
                        Database.db.Value.GetCollection<RoomUser>(RoomUsersColName)

                    let! result = col.DeleteOneAsync(fun ru -> ru._id = room._id)

                    return Ok(result.DeletedCount > 0L)
                | None -> return Ok false
            | Error ex -> return Error ex
        }
