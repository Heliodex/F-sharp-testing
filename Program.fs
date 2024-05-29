open System
open System.Windows
open System.Windows.Controls
open System.Windows.Media
open System.Windows.Media.Imaging
open System.Threading

type WhatToUpdate =
    | Text of string
    | Indeterminate of bool
    | Progress of float

let mutable updater: WhatToUpdate -> unit = fun _ -> ()

let updateProperty (application: Application) property =
    printfn $"Updating text to {property}"

    application.Dispatcher.InvokeAsync(Action(fun () -> updater property))
    |> ignore

let createWindow () =
    let width = 500.
    let height = 320.

    let icon =
        let size = 128.
        let uri = new Uri("./icon.png", UriKind.Relative)
        Image(Width = size, Height = size, Source = new BitmapImage(uri))

    let textLabel =
        TextBlock(
            Width = 250,
            Height = 24,
            FontSize = 15,
            FontFamily = new FontFamily "Tahoma",
            Text = "Connecting to Mercury...",
            TextAlignment = TextAlignment.Center
        )

    let progressBar = ProgressBar(Width = 450, Height = 20, IsIndeterminate = true)

    updater <-
        function
        | Text text -> textLabel.Text <- text
        | Indeterminate determinism -> progressBar.IsIndeterminate <- determinism
        | Progress progress -> progressBar.Value <- progress


    let children: UIElement [] = [| icon; textLabel; progressBar |]
    let canvas = Canvas()

    let SetPosition element x y =
        Canvas.SetLeft(element, x)
        Canvas.SetTop(element, y)

    SetPosition icon ((width - icon.Width) / 2.) 45
    SetPosition textLabel ((width - textLabel.Width) / 2.) 210
    SetPosition progressBar ((width - progressBar.Width) / 2.) 250

    children
    |> Array.iter (fun child -> canvas.Children.Add(child) |> ignore)

    let windowPos screenSize windowSize = (screenSize - windowSize) / 2.

    let mainWindow =
        Window(
            Visibility = Visibility.Visible,
            WindowStyle = WindowStyle.None,
            BorderThickness = Thickness 1,
            BorderBrush = Brushes.LightGray,
            AllowsTransparency = true,
            WindowStartupLocation = WindowStartupLocation.Manual,
            Left = windowPos SystemParameters.PrimaryScreenWidth width,
            Top = windowPos SystemParameters.PrimaryScreenHeight height,
            Width = width,
            Height = height,
            Content = canvas
        )

    Application(MainWindow = mainWindow)

let mutable app = null

let startApp () =
    app <- createWindow ()
    app.Run() |> ignore

[<EntryPoint; STAThread>]
let main _ =
    printfn "Creating window..."

    let thread = Thread(startApp)
    thread.SetApartmentState(ApartmentState.STA)
    thread.Start()

    Thread.Sleep(1000)
    printfn "Starting..."
    Thread.Sleep(1000)

    let update = updateProperty app

    update (Text "Downloading new data...")
    Thread.Sleep(1000)
    update (Indeterminate false)
    update (Text "Processing data...")

    // tween to simulate progress
    let times = 60

    for i in 0..times do
        update (Progress(float (i) / float (times) * 100.))
        Thread.Sleep(10)

    update (Indeterminate true)
    update (Text "Starting Mercury...")
    Thread.Sleep(1000)
    printfn "Done!"


    0
