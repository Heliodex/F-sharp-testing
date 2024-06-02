open System
open System.Diagnostics
open System.IO
open System.Net
open System.Net.Http
open System.Threading
open System.Windows
open FSharp.Core.Result
open LauncherWindow

type ErrorType =
    | VersionTooLong
    | VersionMissing
    | VersionFailedToGet of HttpStatusCode
    | FailedToConnect of exn
    | MercuryNotFound
    | FailedToLaunch of exn

let (>>=) f x = bind x f

let log i =
    printfn $"[LOG] {i}"
    Ok i

let url = "https://setup.mercury2.com/versin.txt"

let requestVersion () =
    update (Text "Connecting to Mercury...")
    let client = new HttpClient()

    try
        let response = (client.GetAsync url).Result

        if response.StatusCode = HttpStatusCode.OK then
            Ok(response.Content.ReadAsStringAsync().Result)
        else
            update (
                MessageBox
                    $"Failed to get version from Mercury\n\
                    Error: {response.ReasonPhrase}.\n\
                    \n\
                    Would you like to continue anyway with the latest existing version?"
            )

            printfn "Waiting..."
            // await the next event from messageBoxReturn
            let ret =
                messageBoxReturn.Publish
                |> Async.AwaitEvent
                |> Async.RunSynchronously

            printfn "Got!"

            match ret with
            | MessageBoxResult.OK -> Ok "version-17bef3811fe76890"
            | _ -> Error(VersionFailedToGet response.StatusCode)
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
    update (Text "Starting Mercury...")

    Ok [| Environment.GetFolderPath Environment.SpecialFolder.LocalApplicationData
          "Mercury"
          "Versions"
          v
          "MercuryPlayerBeta.exe" |]

let validatePath p =
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

    let result =
        requestVersion ()
        >>= log
        >>= validateVersion
        >>= log
        >>= getPath
        >>= log
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
        | FailedToConnect ex -> printfn $"Failed to connect to Mercury: {ex.Message}"
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
