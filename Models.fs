namespace Somnifero.Models
/// Put any "CLIMutable" record here, these records
/// are meant to be used with/within

open System.Collections.Generic

type Message = { Text: string }

type HeaderData =
    { routeGroups: seq<IDictionary<string, string>>
      isAuthenticated: bool }

type FooterData =
    { routeGroups: seq<IDictionary<string, string>>
      remarks: string
      extraData: Option<IDictionary<string, string>> }

type AuthResponse = { User: string }


type LoginPayload = { email: string; password: string }

type SignUpPayload =
    { email: string
      password: string
      name: string
      lastName: string }

type User =
    { name: string
      lastName: string
      email: string }
