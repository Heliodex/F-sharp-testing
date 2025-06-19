module Window

open System.Windows
open System.Windows.Controls
open System.Windows.Media
open Config

// Really starting to get the hang of this F# thing
type Update =
    | Text of string
    | Progress of float
    | Indeterminate of bool
    | SuccessMessage of string
    | ErrorMessage of string
    | Shutdown

let messageBoxReturn = Event<MessageBoxResult>()

let SetPosition element x y =
    Canvas.SetLeft(element, x)
    Canvas.SetTop(element, y)

let windowPos s w = (s - w) / 2.

let createIcon size =
    Image(Width = size, Height = size, Source = imgIcon)

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

    let canvas = Canvas()

    ([| icon; text; progress |]: UIElement [])
    |> Array.iter (canvas.Children.Add >> ignore)

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
    ),
    text,
    progress

let ui2012 () =
    let width, height = 378., 168.
    let icon = createIcon 30

    let text =
        TextBlock(Height = 24, FontSize = 13, Text = $"Connecting to {name}...", TextAlignment = TextAlignment.Center)

    let progress = ProgressBar(Width = 287, Height = 25, IsIndeterminate = true)

    SetPosition icon 20 23
    SetPosition text 58 23
    SetPosition progress 58 51

    let canvas = Canvas()

    ([| icon; text; progress |]: UIElement [])
    |> Array.iter (canvas.Children.Add >> ignore)

    Window(
        Visibility = Visibility.Visible,
        Title = name,
        FontFamily = FontFamily "Segoe UI Variable",
        Background = SolidColorBrush(Color.FromRgb(0xf0uy, 0xf0uy, 0xf0uy)),
        ResizeMode = ResizeMode.NoResize,
        WindowStartupLocation = WindowStartupLocation.Manual,
        Left = windowPos SystemParameters.PrimaryScreenWidth width,
        Top =
            windowPos SystemParameters.PrimaryScreenHeight height
            - 24.,
        Width = width,
        Height = height,
        Content = canvas
    ),
    text,
    progress

let createWindow xfn =
    let window, text, progress = ui2016 ()
    let app = Application(MainWindow = window)
    let u = Event<Update>()

    // awesome pattern matching
    let updateMatch =
        function
        | Text t -> text.Text <- t
        | Progress p -> progress.Value <- p
        | Indeterminate d -> progress.IsIndeterminate <- d
        | SuccessMessage s ->
            MessageBox.Show(s, $"{name} launcher", MessageBoxButton.OK, MessageBoxImage.Information)
            |> ignore

            app.Shutdown()
        | ErrorMessage s ->
            MessageBox.Show(s, $"{name} launcher", MessageBoxButton.OK, MessageBoxImage.Error)
            |> ignore

            app.Shutdown()
        | Shutdown -> app.Shutdown()

    u.Publish.Add(fun upd -> app.Dispatcher.Invoke(fun () -> updateMatch upd))

    app.MainWindow.Loaded.Add(fun _ -> async { xfn u } |> Async.Start)

    app.Run() |> ignore
