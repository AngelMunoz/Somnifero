namespace Somnifero.Handlers

open System
open System.Text.Json
open System.Text.Json.Serialization
open System.Security.Claims

open FSharp.Control.Tasks

open Microsoft.AspNetCore.Authentication
open Microsoft.AspNetCore.Authentication.Cookies
open Microsoft.AspNetCore.Http

open Microsoft.Extensions.Logging

open Giraffe
open Giraffe.Razor.HttpHandlers

open BCrypt.Net

open Somnifero
open Somnifero.Types
open Somnifero.ViewModels


module Public =
    let Index =
        let inline (+>) a b = a, box b
        fun (next: HttpFunc) (ctx: HttpContext) ->
            task {
                let data =
                    dict<string, obj>
                        [ "Title" +> "Welcome"
                          "HeaderData" +> { routeGroups = Seq.empty }
                          "FooterData"
                          +> { routeGroups = Seq.empty
                               extraData = None } ]
                    |> Some

                if ctx.User.Identity.IsAuthenticated
                then return! redirectTo false "/portal/home" next ctx
                else return! razorHtmlView "Index" None data None next ctx
            }


    let InvalidCSRFToken: HttpHandler =
        clearResponse
        >=> RequestErrors.BAD_REQUEST "The CSRF token was invalid"

    let ServerError (ex: Exception) (logger: ILogger) =
        logger.LogError(ex, "An unhandled exception has occurred while executing the request.")

        clearResponse
        >=> setStatusCode 500
        >=> text "There was an error within our servers."


module Auth =

    let signin (ctx: HttpContext) (user: User) =
        task {
            let claims =
                ClaimsIdentity
                    ([ new Claim(ClaimTypes.Name, user.email)
                       new Claim("FirstName", user.name)
                       new Claim("LastName", user.lastName)
                       new Claim("Invite", user.invite)
                       new Claim("_id", user._id.ToString())
                       new Claim(ClaimTypes.Email, user.email) ],
                     CookieAuthenticationDefaults.AuthenticationScheme)

            let props = AuthenticationProperties()
            props.IsPersistent <- true
            props.AllowRefresh <- true

            props.ExpiresUtc <-
                DateTimeOffset.UtcNow.Add(TimeSpan.FromDays(1.0))
                |> Nullable

            do! ctx.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, ClaimsPrincipal(claims), props)
        }

    let Login: HttpHandler =
        fun (next: HttpFunc) (ctx: HttpContext) ->
            task {
                let! loginpayload = JsonSerializer.DeserializeAsync<LoginPayload>(ctx.Request.Body)

                let! queryResult = Users.TryFindUserByEmail loginpayload.email true

                let response =
                    task {
                        match queryResult with
                        | Some user ->
                            match user with
                            | Users.UserWithPassword user ->
                                if BCrypt.EnhancedVerify(loginpayload.password, user.password) then
                                    do! signin
                                            ctx
                                            { _id = user._id
                                              email = user.email
                                              name = user.name
                                              lastName = user.lastName
                                              invite = user.invite }

                                return! json { User = user.email } next ctx
                            | _ -> return! RequestErrors.BAD_REQUEST {| message = "Invalid Credentials" |} next ctx
                        | _ -> return! RequestErrors.BAD_REQUEST {| message = "Invalid Credentials" |} next ctx
                    }

                return! response
            }

    let CheckExists: HttpHandler =
        fun (next: HttpFunc) (ctx: HttpContext) ->
            task {
                let! payload = JsonSerializer.DeserializeAsync<{| email: string |}>(ctx.Request.Body)

                return! json {| exists = payload.email = "abc@123.com" |} next ctx
            }

    let Signup: HttpHandler =
        fun (next: HttpFunc) (ctx: HttpContext) ->
            task {
                let! signuppayload = JsonSerializer.DeserializeAsync<SignUpPayload>(ctx.Request.Body)
                let! emailExists = Users.EmailExists signuppayload.email

                let! inviteExists = Users.InviteExists signuppayload.invite

                return! task {
                            match emailExists, inviteExists with
                            | true, _ ->
                                return! RequestErrors.BAD_REQUEST {| message = "Email already exists" |} next ctx
                            | _, false ->
                                return! RequestErrors.BAD_REQUEST {| message = "Invite Does not exist" |} next ctx
                            | false, true ->
                                let! didCreate =
                                    Users.TryCreateUser
                                        {| email = signuppayload.email
                                           password = signuppayload.password
                                           name = signuppayload.name
                                           lastName = signuppayload.lastName
                                           invite = "" |}

                                match didCreate with
                                | Ok _ ->
                                    let! user = Users.TryFindUserByEmail signuppayload.email false

                                    match user with
                                    | Some user ->
                                        match user with
                                        | Users.User user -> do! signin ctx user
                                        | _ -> ()
                                    | None -> ()

                                    return! json { User = signuppayload.email } next ctx
                                | Error ex ->
                                    printfn $"Falied to create user {ex.Message} - {ex}"
                                    return! RequestErrors.UNPROCESSABLE_ENTITY
                                                {| message = "There was an error creating this account" |}
                                                next
                                                ctx

                        }


            }

module Api =
    let Me: HttpHandler =
        fun (next: HttpFunc) (ctx: HttpContext) -> task { return! json { User = "" } next ctx }

module Portal =

    let Index: HttpHandler =
        fun (next: HttpFunc) (ctx: HttpContext) -> task { return! razorHtmlView "Portal/Index" None None None next ctx }


    let Me: HttpHandler =
        fun (next: HttpFunc) (ctx: HttpContext) -> task { return! razorHtmlView "Portal/Me" None None None next ctx }
