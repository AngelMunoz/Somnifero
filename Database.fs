namespace Somnifero

open FSharp.Control.Tasks
open System
open MongoDB.Driver
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
        {| name: string
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

    let TryCreateUser (user: UserWithPassword): Task<Result<bool, exn>> =
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

                    do! usersWithPassword().Value.InsertOneAsync(user)

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
