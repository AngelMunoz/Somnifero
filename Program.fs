module Somnifero.App

open FSharp.Control.Tasks

open System
open System.IO
open System.Text.Json
open System.Text.Json.Serialization
open System.Threading.Tasks

open Microsoft.AspNetCore
open Microsoft.AspNetCore.Authentication.Cookies
open Microsoft.AspNetCore.Builder
open Microsoft.AspNetCore.Cors.Infrastructure
open Microsoft.AspNetCore.Hosting
open Microsoft.AspNetCore.SignalR

open Microsoft.Extensions.Hosting
open Microsoft.Extensions.Logging
open Microsoft.Extensions.DependencyInjection

open Giraffe
open Giraffe.Razor
open Giraffe.Serialization

open Somnifero.Hubs
open Somnifero.Handlers
open Somnifero.Models
open Microsoft.AspNetCore.Http

type SystemTextJsonSerializer(options: JsonSerializerOptions) =
    interface IJsonSerializer with
        member _.Deserialize<'T>(string: string) =
            JsonSerializer.Deserialize<'T>(string, options)

        member _.Deserialize<'T>(bytes: byte []) =
            JsonSerializer.Deserialize<'T>(ReadOnlySpan bytes, options)

        member _.DeserializeAsync<'T>(stream) =
            JsonSerializer
                .DeserializeAsync<'T>(stream, options)
                .AsTask()

        member _.SerializeToBytes<'T>(value: 'T) =
            JsonSerializer.SerializeToUtf8Bytes<'T>(value, options)

        member _.SerializeToStreamAsync<'T> (value: 'T) stream =
            JsonSerializer.SerializeAsync<'T>(stream, value, options)

        member _.SerializeToString<'T>(value: 'T) =
            JsonSerializer.Serialize<'T>(value, options)


// ---------------------------------
// Web app
// ---------------------------------

let indexHandler =
    let inline (+>) a b = a, box b

    let data =
        dict<string, obj>
            [ "Title" +> "Welcome"
              "HeaderData"
              +> { routeGroups = Seq.empty
                   isAuthenticated = false }
              "FooterData"
              +> { routeGroups = Seq.empty
                   remarks = ""
                   extraData = None } ]
        |> Some

    razorHtmlView "Index" None data None

let webApp =
    choose [ GET >=> route "/" >=> indexHandler
             POST
             >=> (choose [ route "/auth/login" >=> Auth.Login
                           route "/auth/exists" >=> Auth.CheckExists
                           routef "/auth/signup/%s" Auth.Signup
                           route "/auth/signout"
                           >=> signOut CookieAuthenticationDefaults.AuthenticationScheme
                           >=> redirectTo false "/" ])
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
// Error handler
// ---------------------------------

let errorHandler (ex: Exception) (logger: ILogger) =
    logger.LogError(ex, "An unhandled exception has occurred while executing the request.")

    clearResponse
    >=> setStatusCode 500
    >=> text ex.Message

// ---------------------------------
// Config and Main
// ---------------------------------

//let configureCors (builder: CorsPolicyBuilder) =
//    builder
//        .WithOrigins("*")
//        .AllowAnyMethod()
//        .AllowAnyHeader()
//    |> ignore

let configureApp (app: IApplicationBuilder) =
    let env =
        app.ApplicationServices.GetService<IWebHostEnvironment>()

    (match env.EnvironmentName with
     | "Development" -> app.UseDeveloperExceptionPage()
     | _ -> app.UseGiraffeErrorHandler(errorHandler))
        //.UseCors(configureCors)
        .UseStaticFiles()
        .UseRouting()
        .UseAuthentication()
        .UseAuthorization()
        .UseEndpoints(fun ep -> ep.MapHub<StatsHub>("/stats") |> ignore)
        .UseGiraffe(webApp)

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

    services
        .AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
        .AddCookie(fun o -> o.LoginPath <- PathString("/"))
    |> ignore

    services.AddSignalR() |> ignore
    services.AddGiraffe() |> ignore

[<EntryPoint>]
let main args =
    let contentRoot = Directory.GetCurrentDirectory()
    let webRoot = Path.Combine(contentRoot, "WebRoot")

    Host
        .CreateDefaultBuilder(args)
        .ConfigureWebHostDefaults(fun webHostBuilder ->
            webHostBuilder
                .UseContentRoot(contentRoot)
                .UseWebRoot(webRoot)
                .Configure(Action<IApplicationBuilder> configureApp)
                .ConfigureServices(configureServices)
            |> ignore)
        .Build()
        .Run()

    0
