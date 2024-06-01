open System
open System.Diagnostics
open System.IO
open System.Net.Http
open System.Threading
open FSharp.Core.Result
open LauncherWindow

type ErrorType =
    | VersionTooLong
    | VersionMissing
    | VersionError
    | VersionExn of exn
    | MercuryNotFound
    | FailedToLaunch of exn

let (>>=) f x = bind x f

let log i =
    printfn $"[LOG] {i}"
    Ok i

let url = "https://mercury2.com"

let getVersion () =
    try
        let version =
            task {
                use client = new HttpClient()
                let! response = client.GetAsync url
                printfn $"Response: {response}"
                return! response.Content.ReadAsStringAsync()
            }
            |> Async.AwaitTask
            |> Async.RunSynchronously

        if version.Length > 32 then
            Error VersionTooLong
        elif version.Length = 0 then
            Error VersionMissing
        else
            Ok version
    with
    | e -> Error(VersionExn e)


let getPath v =
    Ok [| Environment.GetFolderPath Environment.SpecialFolder.LocalApplicationData
          "Mercury"
          "Versions"
          v
          "MercuryPlayerBeta.exe" |]

let ensureValidPath p =
    match File.Exists p with
    | true -> Ok p
    | false -> Error MercuryNotFound

let launch (p: string) =
    try
        Ok(Process.Start p)
    with
    | e -> Error(FailedToLaunch e)

let init () =
    printfn "Creating window..."

    let thread = Thread(ThreadStart createWindow)
    thread.SetApartmentState ApartmentState.STA
    thread.Start()

    Thread.Sleep 500
    printfn "Window created!"

    Thread.Sleep 500
    update (Text "Downloading new data...")
    Thread.Sleep 500
    update (Indeterminate false)
    update (Text "Processing data...")

    // tween to simulate progress
    let times = 30

    for i in 0..times do
        update (Progress(float (i) / float (times) * 100.))
        Thread.Sleep 10

    update (Indeterminate true)
    update (Text "Starting Mercury...")

    let result =
        getVersion ()
        >>= log
        >>= getPath
        >>= log
        |> map Path.Combine
        >>= ensureValidPath
        >>= log
        >>= launch

    match result with
    | Ok s -> printfn $"Success! {s}"
    | Error e ->
        match e with
        | VersionTooLong -> printfn "Version string too long"
        | VersionMissing -> printfn "Version response was missing"
        | VersionError -> printfn "Version error"
        | VersionExn ex -> printfn $"Version exception: {ex.Message}"
        | MercuryNotFound -> printfn "Mercury not found"
        | FailedToLaunch ex -> printfn $"Failed to start Mercury: {ex.Message}"

    Thread.Sleep 500
    update Shutdown


[<EntryPoint; STAThread>]
let main _ =
    // dot net error handling bruh
    try
        init ()
    with
    | e ->
        printfn $"Error: {e.Message}"
        Environment.Exit 1

    0
