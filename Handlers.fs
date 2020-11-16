namespace Somnifero.Handlers

open System
open System.Text.Json
open System.Text.Json.Serialization
open System.Security.Claims

open FSharp.Control.Tasks

open Microsoft.AspNetCore.Authentication
open Microsoft.AspNetCore.Authentication.Cookies
open Microsoft.AspNetCore.Http

open Giraffe
open Giraffe.Razor.HttpHandlers

open Somnifero.Types
open Somnifero.ViewModels
open Microsoft.Extensions.Logging


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
                printfn $"{loginpayload.email} - {loginpayload.password}"

                let user =
                    { email = loginpayload.email
                      lastName = "munoz"
                      name = "Daniel" }

                do! signin ctx user

                return! json { User = user.email } next ctx
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
                printfn $"{signuppayload.email} - {signuppayload.password}"

                let user =
                    { email = signuppayload.email
                      lastName = signuppayload.name
                      name = signuppayload.email }

                do! signin ctx user

                return! json { User = user.email } next ctx
            }

module Api =
    let Me: HttpHandler =
        fun (next: HttpFunc) (ctx: HttpContext) -> task { return! json { User = "" } next ctx }

module Portal =

    let Index: HttpHandler =
        fun (next: HttpFunc) (ctx: HttpContext) -> task { return! razorHtmlView "Portal/Index" None None None next ctx }


    let Me: HttpHandler =
        fun (next: HttpFunc) (ctx: HttpContext) -> task { return! razorHtmlView "Portal/Me" None None None next ctx }
