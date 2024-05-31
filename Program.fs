open System
open System.Threading
open System.IO
open System.Diagnostics
open LauncherWindow

type ErrorType =
    | MercuryNotFound
    | FailedToStartMercury of exn

let (>>=) f x = Result.bind x f

let findPath version =
    // get from %localappdata%
    let localappdata =
        Environment.GetFolderPath Environment.SpecialFolder.LocalApplicationData

    let path =
        $"%s{localappdata}\\Mercury\\Versions\\%s{version}\\MercuryPlayerBeta.exe"

    match File.Exists path with
    | true -> Ok path
    | false -> Error MercuryNotFound

let startApp (path: string) =
    try
        Ok(Process.Start path)
    with
    | e -> Error(FailedToStartMercury e)

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


    let startApp v = Ok v >>= findPath >>= startApp

    let version = "version-17bef3811fe76890"

    match startApp version with
    | Ok s -> printfn $"Success! {s}"
    | Error e ->
        match e with
        | MercuryNotFound -> printfn "Mercury not found"
        | FailedToStartMercury ex -> printfn $"Failed to start Mercury: {ex.Message}"

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
