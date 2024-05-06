open System
open System.Windows
open System.Windows.Controls

let mutable counter = 0

let app = Application()

printfn "Hello World!"

let text = TextBlock()
text.Width <- 150
text.Height <- 20

let updateText () = text.Text <- $"Clicked {counter} times"
updateText ()

let mainWindow = Window()
mainWindow.Visibility <- Visibility.Visible
mainWindow.Width <- 200
mainWindow.Height <- 100

let mainGrid = Grid()

let layout = StackPanel()
layout.Children.Add(text) |> ignore

let button = Button()
button.Content <- "Increment"
button.Width <- 150
button.Height <- 20

button.Click.Add (fun _ ->
    counter <- counter + 1
    updateText ())

layout.Children.Add(button) |> ignore
mainGrid.Children.Add(layout) |> ignore

mainWindow.Content <- mainGrid
app.MainWindow <- mainWindow

[<STAThread>]
[<EntryPoint>]
let main _ = app.Run()
