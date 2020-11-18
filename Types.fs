namespace Somnifero.Types

open MongoDB.Bson
open System
open MongoDB.Bson.Serialization.Attributes

type AuthResponse = { User: string }

type LoginPayload = { email: string; password: string }

type SignUpPayload =
    { email: string
      password: string
      name: string
      lastName: string
      invite: string }

// The main reason for this is that we store the password
// but we don't actually use it every time so we prevent 
// serialization issues within the mongo driver
[<BsonIgnoreExtraElements>]
type User =
    { _id: ObjectId
      name: string
      lastName: string
      email: string
      invite: string }


type Room =
    { _id: ObjectId
      owner: ObjectId
      name: string
      topics: seq<string>
      ``public``: bool }

type RoomUser =
    { _id: ObjectId
      room: ObjectId
      user: ObjectId }


type RoomMessage =
    { _id: ObjectId
      room: ObjectId
      user: ObjectId
      date: DateTimeOffset
      content: string }

type PaginatedResult<'T> = { items: seq<'T>; count: int64 }
