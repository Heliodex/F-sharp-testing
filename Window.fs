module Window

open System
open System.Windows
open System.Windows.Controls
open System.Windows.Media
open System.Windows.Media.Imaging
open Config

// Really starting to get the hang of this F# thing
type Update =
    | Text of string
    | Progress of float
    | Indeterminate of bool
    | MessageBox of string
    | Shutdown

let connect, update =
    let e = Event<Update>()
    e.Publish.Add, e.Trigger

let messageBoxReturn = Event<MessageBoxResult>()

let SetPosition element x y =
    Canvas.SetLeft(element, x)
    Canvas.SetTop(element, y)

let windowPos s w = (s - w) / 2.

let createIcon size =
    let uri = Uri("./icon.png", UriKind.Relative)
    Image(Width = size, Height = size, Source = BitmapImage uri)

let ui2016 () =
    let width, height = 500., 320.
    let icon = createIcon 128

    let text =
        TextBlock(
            Width = 250,
            Height = 24,
            FontSize = 15,
            FontFamily = FontFamily "Tahoma",
            Text = "Initialising launcher...",
            TextAlignment = TextAlignment.Center
        )

    let progress = ProgressBar(Width = 450, Height = 20, IsIndeterminate = true)

    SetPosition icon ((width - icon.Width) / 2.) 45
    SetPosition text ((width - text.Width) / 2.) 210
    SetPosition progress ((width - progress.Width) / 2.) 250

    let children: UIElement [] = [| icon; text; progress |] // I have no idea whether to use a List or an Array
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

    Application(MainWindow = mainWindow), text, progress

let ui2012 () =
    let width, height = 378., 168.
    let icon = createIcon 30

    let text =
        TextBlock(Height = 24, FontSize = 13, Text = "Connecting to Mercury...", TextAlignment = TextAlignment.Center)

    let progress = ProgressBar(Width = 287, Height = 25, IsIndeterminate = true)

    SetPosition icon 20 23
    SetPosition text 58 23
    SetPosition progress 58 51

    let children: UIElement [] = [| icon; text; progress |] // I have no idea whether to use a List or an Array
    let canvas = Canvas()

    children
    |> Array.iter (canvas.Children.Add >> ignore)

    let mainWindow =
        Window(
            Visibility = Visibility.Visible,
            Title = name,
            FontFamily = FontFamily "Segoe UI Variable",
            Background = SolidColorBrush(Color.FromRgb(0xF0uy, 0xF0uy, 0xF0uy)),
            ResizeMode = ResizeMode.NoResize,
            WindowStartupLocation = WindowStartupLocation.Manual,
            Left = windowPos SystemParameters.PrimaryScreenWidth width,
            Top =
                windowPos SystemParameters.PrimaryScreenHeight height
                - 24.,
            Width = width,
            Height = height,
            Content = canvas
        )

    Application(MainWindow = mainWindow), text, progress

let createWindow () =
    let app, text, progress = ui2016 ()

    // awesome pattern matching
    let updateMatch =
        function
        | Text t ->
            printfn "Text update: %s" t
            text.Text <- t
        | Progress p -> progress.Value <- p
        | Indeterminate d -> progress.IsIndeterminate <- d
        | MessageBox s ->
            // required to be here, as first argument being window is required for top z-index
            // also needs to be async so it dont block
            printfn "1"

            MessageBox.Show(s, "Mercury Launcher", MessageBoxButton.YesNo)
            |> messageBoxReturn.Trigger

            printfn "2"
        | Shutdown ->
            printfn "trying,"

            while app.MainWindow.Visibility = Visibility.Visible do
                printfn "trying"
                System.Threading.Thread.Sleep 100
                app.Shutdown()

    connect (fun update ->
        printfn "Update received: %A" update
        let updateAction () = updateMatch update
        app.Dispatcher.Invoke updateAction)

    update Shutdown

    app.Run() |> ignore
