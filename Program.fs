module Somnifero.App

open FSharp.Control.Tasks

open System
open System.IO
open System.Text.Json
open System.Text.Json.Serialization
open System.Threading.Tasks

open Microsoft.AspNetCore.Authentication.Cookies
open Microsoft.AspNetCore.Builder
open Microsoft.AspNetCore.Hosting
open Microsoft.AspNetCore.Http
open Microsoft.AspNetCore.StaticFiles

open Microsoft.Extensions.Hosting
open Microsoft.Extensions.DependencyInjection

open Giraffe
open Giraffe.Razor
open Giraffe.Serialization

open Somnifero.Hubs
open Somnifero.Handlers

// ---------------------------------
// Web app
// ---------------------------------

let webApp =
    choose [ GET >=> route "/" >=> Public.Index
             route "/auth/signout"
             >=> signOut CookieAuthenticationDefaults.AuthenticationScheme
             >=> redirectTo false "/"
             POST
             >=> (choose [ route "/auth/login"
                           >=> validateAntiforgeryToken Public.InvalidCSRFToken
                           >=> Auth.Login
                           route "/auth/exists" >=> Auth.CheckExists
                           route "/auth/signup"
                           >=> validateAntiforgeryToken Public.InvalidCSRFToken
                           >=> Auth.Signup ])
             subRoute
                 "/portal"
                 (requiresAuthentication (challenge CookieAuthenticationDefaults.AuthenticationScheme)
                  >=> choose [ GET >=> route "/home" >=> Portal.Index
                               GET >=> route "/me" >=> Portal.Me ])

             subRoute
                 "/api"
                 (requiresAuthentication (challenge CookieAuthenticationDefaults.AuthenticationScheme)
                  >=> choose [ GET >=> route "/me" >=> Api.Me ])

             setStatusCode 404 >=> text "Not Found" ]


// ---------------------------------
// Config and Main
// ---------------------------------

let configureApp (app: IApplicationBuilder) =
    let env =
        app.ApplicationServices.GetService<IWebHostEnvironment>()

    if env.EnvironmentName = "Development"
    then app.UseDeveloperExceptionPage()
    else app.UseGiraffeErrorHandler(Public.ServerError)
    |> ignore
    let staticopts = StaticFileOptions()
    staticopts.OnPrepareResponse <-
        new Action<StaticFileResponseContext>(fun ctx ->
        ctx.Context.Response.Headers.Append
            ("X-Content-Type-Options", Microsoft.Extensions.Primitives.StringValues("nosniff")))
    app.UseStaticFiles(staticopts) |> ignore
    app.UseRouting() |> ignore
    app.UseAuthentication() |> ignore
    app.UseAuthorization() |> ignore
    app.UseEndpoints(fun ep ->
        ep.MapHub<StatsHub>("/stats") |> ignore
        ep.MapHub<RoomsHub>("/rooms") |> ignore)
    |> ignore
    app.Use
        (new Func<HttpContext, Func<Task>, Task>(fun context next ->
        task {
            context.Response.Headers.Add
                ("X-Content-Type-Options", Microsoft.Extensions.Primitives.StringValues("nosniff"))
            do! next.Invoke()
        } :> Task))
    |> ignore
    app.UseGiraffe(webApp)



type SystemTextJsonSerializer(options: JsonSerializerOptions) =
    interface IJsonSerializer with
        member _.Deserialize<'T>(string: string) =
            JsonSerializer.Deserialize<'T>(string, options)

        member _.Deserialize<'T>(bytes: byte []) =
            JsonSerializer.Deserialize<'T>(ReadOnlySpan bytes, options)

        member _.DeserializeAsync<'T>(stream) =
            JsonSerializer.DeserializeAsync<'T>(stream, options).AsTask()

        member _.SerializeToBytes<'T>(value: 'T) =
            JsonSerializer.SerializeToUtf8Bytes<'T>(value, options)

        member _.SerializeToStreamAsync<'T> (value: 'T) stream =
            JsonSerializer.SerializeAsync<'T>(stream, value, options)

        member _.SerializeToString<'T>(value: 'T) =
            JsonSerializer.Serialize<'T>(value, options)

let configureServices (services: IServiceCollection) =
    let sp = services.BuildServiceProvider()
    let env = sp.GetService<IWebHostEnvironment>()

    let jsonOptions = JsonSerializerOptions()
    jsonOptions.Converters.Add(JsonFSharpConverter())
    services.AddSingleton(jsonOptions) |> ignore

    services.AddSingleton<IJsonSerializer, SystemTextJsonSerializer>()
    |> ignore

    let viewsFolderPath =
        Path.Combine(env.ContentRootPath, "Views")

    services.AddRazorEngine viewsFolderPath |> ignore
    services.AddCors() |> ignore

    services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
            .AddCookie(fun o -> o.LoginPath <- PathString("/"))
    |> ignore

    services.AddSignalR() |> ignore
    services.AddGiraffe() |> ignore

[<EntryPoint>]
let main args =
    let contentRoot = Directory.GetCurrentDirectory()
    let webRoot = Path.Combine(contentRoot, "WebRoot")

    Host.CreateDefaultBuilder(args)
        .ConfigureWebHostDefaults(fun webHostBuilder ->
        webHostBuilder.UseContentRoot(contentRoot).UseWebRoot(webRoot)
                      .Configure(Action<IApplicationBuilder> configureApp).ConfigureServices(configureServices)
        |> ignore).Build().Run()

    0
