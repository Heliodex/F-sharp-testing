module LauncherWindow

open System
open System.Windows
open System.Windows.Controls
open System.Windows.Media
open System.Windows.Media.Imaging

// Really starting to get the hang of this F# thing
type Update =
    | Text of string
    | Progress of float
    | Indeterminate of bool
    | Shutdown

let connect, update =
    let e = Event<Update>()
    e.Publish.Add, e.Trigger

let createWindow () =
    let width, height = 500., 320.

    let icon =
        let size = 128.
        let uri = Uri("./icon.png", UriKind.Relative)
        Image(Width = size, Height = size, Source = BitmapImage(uri))

    let text =
        TextBlock(
            Width = 250,
            Height = 24,
            FontSize = 15,
            FontFamily = FontFamily "Tahoma",
            Text = "Connecting to Mercury...",
            TextAlignment = TextAlignment.Center
        )

    let progress = ProgressBar(Width = 450, Height = 20, IsIndeterminate = true)

    let SetPosition element x y =
        Canvas.SetLeft(element, x)
        Canvas.SetTop(element, y)

    SetPosition icon ((width - icon.Width) / 2.) 45
    SetPosition text ((width - text.Width) / 2.) 210
    SetPosition progress ((width - progress.Width) / 2.) 250

    let children: UIElement [] = [| icon; text; progress |] // I have no idea whether to use a List or an Array
    let canvas = Canvas()

    children
    |> Array.iter (canvas.Children.Add >> ignore) // Function composition makes me feel like a god

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

    let app = Application(MainWindow = mainWindow)

    // awesome pattern matching
    let updateMatch =
        function
        | Text t -> text.Text <- t
        | Progress p -> progress.Value <- p
        | Indeterminate d -> progress.IsIndeterminate <- d
        | Shutdown -> app.Shutdown()

    connect (fun update ->
        let updateAction () = updateMatch update
        app.Dispatcher.Invoke(Action(updateAction)))

    app.Run() |> ignore
