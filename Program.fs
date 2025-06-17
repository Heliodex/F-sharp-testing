open System
open System.Diagnostics
open System.IO
open System.Net
open System.Net.Http
open System.Threading
open System.Windows
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

let url = $"https://setup.{domain}/version.txt"

let requestVersionFail r =
    update (
        MessageBox
            $"\
            An error occurred when trying to get the version from {name}\n\
            Details: {r}.\n\
            \n\
            Would you like to continue anyway with the latest local version?" // todo: remove
    )

    messageBoxReturn.Publish
    |> Async.AwaitEvent
    |> Async.RunSynchronously

let requestVersion () =
    printfn "Requesting version..."
    update (Text $"Connecting to {name}...")
    let client = new HttpClient()

    try
        let response = (client.GetAsync url).Result

        if response.StatusCode = HttpStatusCode.OK then
            Ok(response.Content.ReadAsStringAsync().Result)
        else
            Error(VersionFailedToGet response.StatusCode)
    with
    | e -> Error(FailedToConnect e)

let validateVersion (v: string) =
    if v.Length > 32 then
        Error VersionTooLong
    elif v.Length = 0 then
        Error VersionMissing
    else
        Ok v

let getPath v =
    update (Text $"Starting {name}...")

    Ok [| Environment.GetFolderPath Environment.SpecialFolder.LocalApplicationData
          name
          "Versions"
          v
          $"{name}PlayerBeta.exe" |]

let validatePath p =
    match File.Exists p with
    | true -> Ok p
    | false -> Error ClientNotFound

let launch (p: string) =
    try
        Ok(Process.Start(p, "--API test"))
    with
    | e -> Error(FailedToLaunch e)

let init () =
    printfn "Creating window..."

    let thread = Thread(ThreadStart createWindow)
    thread.SetApartmentState ApartmentState.STA
    thread.Start()

    printfn "Window created"

    let result =
        requestVersion ()
        >>= log
        >>= validateVersion
        >>= log
        >>= getPath
        |> map Path.Combine
        >>= validatePath
        >>= log
        >>= launch

    match result with
    | Ok s -> printfn $"Success! {s}"
    | Error e ->
        match e with
        | VersionTooLong -> printfn "Version string too long"
        | VersionMissing -> printfn "Version response was missing"
        | VersionFailedToGet code -> printfn $"Failed to get version: Error {code}"
        | FailedToConnect ex -> printfn $"Failed to connect to {name}: {ex.Message}"
        | ClientNotFound -> printfn $"{name} not found"
        | FailedToLaunch ex -> printfn $"Failed to start {name}: {ex.Message}"

    printfn "Waiting for window to close..."
    update Shutdown
    printfn "closd."


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
