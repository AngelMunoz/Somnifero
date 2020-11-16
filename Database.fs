namespace Somnifero

module Database =
    open MongoDB.Driver
    open Somnifero.Types
    open System

    let private connectionString =
        Environment.GetEnvironmentVariable "MONGO_URL"
        |> Option.ofObj
        |> Option.defaultValue "mongodb://localhost:27017/somniferodb"

    [<Literal>]
    let private DatabaseName = "somniferodb"

    let client () = lazy (MongoClient(connectionString))

    let db =
        lazy (client().Value.GetDatabase(DatabaseName))

    let UserCollection =
        db.Value.GetCollection<User> "somn_users"
