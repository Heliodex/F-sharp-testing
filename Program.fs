open System
open System.Diagnostics
open System.IO
open System.Net
open System.Net.Http
open FSharp.Core.Result
open Config
open Window

type ErrorType =
    | VersionTooLong
    | VersionMissing
    | VersionFailedToGet of HttpStatusCode
    | FailedToConnect of exn
    | ClientNotFound
    | FailedToLaunch of exn

let (>>=) f x = bind x f

let log i =
    printfn $"[LOG] {i}"
    Ok i

let url = $"https://setup.{domain}"
let versionUrl = $"{url}/version.txt"

let requestVersion (u: Event<Update>) =
    printfn "Requesting version..."
    u.Trigger(Text $"Connecting to {name}...")

    try
        use client = new HttpClient()
        use response = (client.GetAsync versionUrl).Result

        if response.StatusCode = HttpStatusCode.OK then
            Ok(response.Content.ReadAsStringAsync().Result)
        else
            Error(VersionFailedToGet response.StatusCode)
    with
    | :? AggregateException as e -> Error(FailedToConnect e.InnerException)
    | e -> Error(FailedToConnect e)

let validateVersion (v: string) =
    if v.Length > 32 then
        Error VersionTooLong
    elif v.Length = 0 then
        Error VersionMissing
    else
        Ok v

let getPath (u: Event<Update>) v =
    u.Trigger(Text "Getting client...")

    let path =
        [| Environment.GetFolderPath Environment.SpecialFolder.LocalApplicationData
           name
           "Versions"
           v
           $"{name}PlayerBeta.exe" |]

    Ok(path |> Path.Combine, v)

let installClient (u: Event<Update>) (v: string) (p: string) =
    u.Trigger(Text "Installing client...")

    try
        use client = new HttpClient()
        use response = (client.GetAsync versionUrl).Result

        if response.StatusCode = HttpStatusCode.OK then
            Ok p
        else
            Error(VersionFailedToGet response.StatusCode)
    with
    | :? AggregateException as e -> Error(FailedToConnect e.InnerException)
    | e -> Error(FailedToConnect e)

let validatePath (u: Event<Update>) (v: string, p: string) =
    match File.Exists p with
    | true ->
        printfn "Client found at %s" p
        Ok p
    | _ -> installClient u v p

let launch (p: string) =
    try
        Ok(Process.Start(p, "--API test"))
    with
    | e -> Error(FailedToLaunch e)

let handleError (u: Event<Update>) =
    function
    | VersionTooLong ->
        u.Trigger(
            ErrorMessage
                $"There was an error when trying to get the version from {name}.\n\
                The version string for {name} is too long."
        )
    | VersionMissing ->
        u.Trigger(
            ErrorMessage
                $"There was an error when trying to get the version from {name}.\n\
                The version string for {name} is missing."
        )
    | VersionFailedToGet code ->
        u.Trigger(
            ErrorMessage
                $"There was an error when trying to get the version from {name}.\n\
                The server returned a {code} status code."
        )
    | FailedToConnect ex ->
        u.Trigger(
            ErrorMessage
                $"Failed to connect to {name}.\n\
                Please check your internet connection and try again.\n\
                \n\
                Details: {ex.Message}"
        )
    | ClientNotFound ->
        u.Trigger(
            ErrorMessage
                $"The {name} client was not found.\n\
                Please make sure that the client is installed and try again."
        )
    | FailedToLaunch ex ->
        u.Trigger(
            ErrorMessage
                $"Failed to launch {name}.\n\
                Please make sure that the client is installed and try again.\n\
                \n\
                Details: {ex.Message}"
        )

let init (u: Event<Update>) =
    let result =
        requestVersion u
        >>= validateVersion
        >>= getPath u
        >>= log
        >>= validatePath u
        >>= log
        >>= launch

    match result with
    | Ok s -> printfn $"Success! {s}"
    | Error e -> handleError u e

    u.Trigger Shutdown

[<EntryPoint; STAThread>]
let main _ =
    // dot net error handling bruh
    try
        createWindow init
    with
    | e ->
        printfn $"Error: {e}"
        Environment.Exit 1

    0
