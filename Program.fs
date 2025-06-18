open System
open System.Diagnostics
open System.IO
open System.IO.Compression
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
    | FailedToDownload of exn
    | FailedToInstall of exn
    | ClientNotFound
    | FailedToLaunch of exn

let (>>=) f x = bind x f

let log i =
    printfn $"[LOG] {i}"
    Ok i

let url = $"https://setup.{domain}"
let versionUrl = $"{url}/version"

let requestVersion () =
    printfn "Requesting version..."

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
    if v.Length > 20 then
        Error VersionTooLong
    elif v.Length = 0 then
        Error VersionMissing
    else
        Ok v

let versionPath s v =
    let path = [| "Versions"; v |]

    // add the version to the path
    Path.Combine(s, path |> Path.Combine)

let playerPath s v =
    // add the version to the path
    Path.Combine(versionPath s v, $"{name}PlayerBeta.exe")

let getPath v =
    let path =
        [| Environment.GetFolderPath Environment.SpecialFolder.LocalApplicationData
           name |]

    Ok(path |> Path.Combine, v)

let downloadClient v =
    try
        use client = new HttpClient()
        use response = (client.GetAsync $"{url}/{v}").Result

        if response.StatusCode = HttpStatusCode.OK then
            Ok(response.Content.ReadAsByteArrayAsync().Result)
        else
            Error(VersionFailedToGet response.StatusCode)
    with
    | :? AggregateException as e -> Error(FailedToDownload e.InnerException)
    | e -> Error(FailedToDownload e)

let installClient p v (data: byte array) =
    let path = versionPath p v

    // we have the data, we'd like to un-gzip it
    try
        // create the directory if it doesn't exist
        printfn "Client installing at %s" path

        // DirectoryInfo(path).Create()

        use tarGz = new MemoryStream(data)
        use tar = new MemoryStream()
        use decompressor = new GZipStream(tarGz, CompressionMode.Decompress)
        decompressor.CopyTo tar

        printfn "Client extracting at %s" path

        // extract the tar file
        Formats.Tar.TarFile.ExtractToDirectory(tar, path, true)

        printfn "Client installed at %s" path

        Ok(p, v)
    with
    | e -> Error(FailedToInstall e)

let ensurePath (p, v) =
    match File.Exists(playerPath p v) with
    | true ->
        printfn "Client found at %s" p
        Ok(true, p, v)
    | _ -> Ok(false, p, v)

let launch (p, v) =
    try
        Ok(Process.Start(playerPath p v, "--API test"))
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
    | FailedToDownload ex ->
        u.Trigger(
            ErrorMessage
                $"Failed to download the {name} client.\n\
                Please check your internet connection and try again.\n\
                \n\
                Details: {ex.Message}"
        )
    | FailedToInstall ex ->
        u.Trigger(
            ErrorMessage
                $"Failed to install the {name} client.\n\
                Please make sure you have write permissions to the installation directory.\n\
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
                \n\
                Details: {ex.Message}"
        )

let yes _ x = Ok x

let downloadAndInstall (u: Event<Update>) (d, p, v) =
    if d then
        Ok(p, v)
    else
        downloadClient v
        >>= yes (u.Trigger(Text "Installing client..."))
        >>= installClient p v

let init (u: Event<Update>) =
    let result =
        requestVersion ()
        >>= yes (u.Trigger(Text $"Connecting to {name}..."))
        >>= validateVersion
        >>= getPath
        >>= yes (u.Trigger(Text "Getting client..."))
        >>= ensurePath
        >>= yes (u.Trigger(Text "Downloading client..."))
        >>= downloadAndInstall u
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
