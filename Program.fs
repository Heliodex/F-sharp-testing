open System
open System.Windows
open System.Windows.Controls
open System.Windows.Media
open System.Windows.Media.Imaging
open System.Threading

type Update =
    | Text of string
    | Indeterminate of bool
    | Progress of float

let windowPos screenSize windowSize = (screenSize - windowSize) / 2.

let mutable app: Application = null
let mutable updater = fun _ -> ()

let update prop =
    app.Dispatcher.InvokeAsync(Action(fun () -> updater prop))
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
        | Text t -> textLabel.Text <- t
        | Indeterminate d -> progressBar.IsIndeterminate <- d
        | Progress p -> progressBar.Value <- p


    let SetPosition element x y =
        Canvas.SetLeft(element, x)
        Canvas.SetTop(element, y)

    SetPosition icon ((width - icon.Width) / 2.) 45
    SetPosition textLabel ((width - textLabel.Width) / 2.) 210
    SetPosition progressBar ((width - progressBar.Width) / 2.) 250

    let children: UIElement [] = [| icon; textLabel; progressBar |] // I have no idea whether to use a List or an Array
    let canvas = Canvas()

    children
    |> Array.iter (canvas.Children.Add >> ignore)

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

    app <- Application(MainWindow = mainWindow)
    app.Run() |> ignore

[<EntryPoint; STAThread>]
let main _ =
    printfn "Creating window..."

    let thread = Thread createWindow
    thread.SetApartmentState ApartmentState.STA
    thread.Start()

    Thread.Sleep 1000
    printfn "Starting..."
    Thread.Sleep 1000

    update (Text "Downloading new data...")
    Thread.Sleep 1000
    update (Indeterminate false)
    update (Text "Processing data...")

    // tween to simulate progress
    let times = 60

    for i in 0..times do
        update (Progress(float (i) / float (times) * 100.))
        Thread.Sleep 10

    update (Indeterminate true)
    update (Text "Starting Mercury...")
    Thread.Sleep 1000
    printfn "Done!"
    Thread.Sleep 1000
    app.Shutdown()

    0
