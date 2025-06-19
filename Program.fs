open System
open System.Diagnostics
open System.IO
open System.IO.Compression
open System.Net
open System.Net.Http
open System.Threading
open System.Windows
open Microsoft.Win32
open FSharp.Core.Result
open Config
open Window

type ErrorType =
    | VersionTooLong
    | VersionMissing
    | VersionFailedToGet of HttpStatusCode
    | FailedToConnect of exn
    | FailedToDownload of exn
    | FailedToUnpack of exn
    | FailedToInstall of exn
    | ClientNotFound
    | FailedToLaunch of exn
    | BadLaunch of exn

let (>>=) f x = bind x f

let log i =
    printfn $"[LOG] {i}"
    Ok i

let url = $"https://setup.{domain}"
let versionUrl = $"{url}/version"
let authUrl = $"{url}/negotiate" // /Login/Negotiate.ashx
let joinUrl ticket = $"{url}/game/join?ticket=%s{ticket}"
let launcherScheme = $"{name.ToLowerInvariant()}-launcher"
let authTicket = "test" // LRORL

let requestVersion () =
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
    let path = [| "Versions"; $"version-{v}" |]

    // add the version to the path
    Path.Combine(s, path |> Path.Combine)

let launcherPath s v =
    Path.Combine(versionPath s v, $"{name}Launcher.exe")

let playerPath s v =
    Path.Combine(versionPath s v, $"{name}PlayerBeta.exe")

let studioPath s v =
    Path.Combine(versionPath s v, $"{name}StudioBeta.exe")

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

let ungzipClient (data: byte array) =
    // we have the data, we'd like to un-gzip it
    try
        use tarGz = new MemoryStream(data)
        let tar = new MemoryStream()
        use decompressor = new GZipStream(tarGz, CompressionMode.Decompress)
        decompressor.CopyTo tar

        tar.Seek(0L, SeekOrigin.Begin) |> ignore

        Ok tar
    with
    | e -> Error(FailedToUnpack e)

let untarClient p v (tar: MemoryStream) =
    let path = versionPath p v

    try
        // create the directory if it doesn't exist
        Directory.CreateDirectory path |> ignore

        // extract the tar file
        Formats.Tar.TarFile.ExtractToDirectory(tar, path, true)

        Ok(p, v)
    with
    | e -> Error(FailedToInstall e)

let ensurePath (p, v) = Ok(File.Exists(playerPath p v), p, v)

let launch ticket (p, v) =
    let procArgs =
        [| $"--play"
           "-a"
           authUrl
           "-t"
           authTicket
           "-j"
           joinUrl ticket |]

    try
        Ok(Process.Start(playerPath p v, procArgs))
    with
    | e -> Error(FailedToLaunch e)

// Register the protocol handler to this application
let registerURI (p, v) =
    try
        let key =
            Registry.CurrentUser.CreateSubKey($"Software\\Classes\\{launcherScheme}", true)

        // same as reg structure created by 2016 launcher
        key.SetValue("", $"URL: {name} Protocol")
        key.SetValue("URL Protocol", "")

        let shellKey = key.CreateSubKey "shell"
        let openKey = shellKey.CreateSubKey "open"
        let commandKey = openKey.CreateSubKey "command"

        let exePath = launcherPath p v
        commandKey.SetValue("", $"\"{exePath}\" %%1")

        Ok(p, v)
    with
    | e -> Error(BadLaunch e)

let checkThatItLaunchedCorrectly (p: Process) =
    try
        if p.HasExited then
            Error ClientNotFound
        else
            Ok()
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
    | FailedToUnpack ex ->
        u.Trigger(
            ErrorMessage
                $"Failed to unpack the {name} client.\n\
                \n\
                Details: {ex.Message}"
        )
    | FailedToInstall ex ->
        u.Trigger(
            ErrorMessage
                $"Failed to install the {name} client.\n\
                Please make sure you have write permissions to the installation directory, and there are no existing files with the same name.\n\
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
    | BadLaunch ex ->
        u.Trigger(
            ErrorMessage
                $"The {name} client launched, but it did not start correctly.\n\
                \n\
                Details: {ex.Message}"
        )

let yes _ x = Ok x

let downloadAndInstall (u: Event<Update>) (d, p, v) =
    if d then
        Ok(p, v)
    else
        downloadClient v
        >>= yes (u.Trigger(Text "Unpacking client..."))
        >>= ungzipClient
        >>= yes (u.Trigger(Text "Installing client..."))
        >>= untarClient p v

let launchAndComplete (u: Event<Update>) ticket (p, v) =
    if ticket = "" then
        u.Trigger(Indeterminate false)
        u.Trigger(Progress 100.)
        u.Trigger(Text "Done!")

        u.Trigger(SuccessMessage $"{name} has been successfully installed and is ready to use!")
        // TODO: redirect to site

        Ok()
    else
        u.Trigger(Text $"Starting {name}...")

        launch ticket (p, v)
        >>= yes (u.Trigger(Text $"Finishing up..."))
        >>= checkThatItLaunchedCorrectly

let init ticket (u: Event<Update>) =
    let result =
        requestVersion ()
        >>= yes (u.Trigger(Text $"Connecting to {name}..."))
        >>= validateVersion
        >>= getPath
        >>= yes (u.Trigger(Text "Getting client..."))
        >>= ensurePath
        >>= yes (u.Trigger(Text "Downloading client..."))
        >>= downloadAndInstall u
        >>= yes (u.Trigger(Text "Registering protocol..."))
        >>= registerURI
        >>= launchAndComplete u ticket

    match result with
    | Ok _ ->
        u.Trigger(Indeterminate false)
        u.Trigger(Progress 100.)
        u.Trigger(Text "Done!")
        Thread.Sleep 100 // give the UI a chance to update before closing
    | Error e -> handleError u e

    u.Trigger Shutdown

[<EntryPoint; STAThread>]
let main args =
    let ticket =
        if args = [||] then
            ""
        else
            let mainArg = args[0]

            if not (mainArg.StartsWith launcherScheme) then
                MessageBox.Show(
                    $"The first argument must be a {launcherScheme} URL.",
                    $"{name} launcher",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error
                )
                |> ignore

                Environment.Exit 1

            mainArg.Substring(launcherScheme.Length + 1)

    // dot net error handling bruh
    try
        createWindow (init ticket)
    with
    | _ -> Environment.Exit 1

    0
