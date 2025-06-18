module Config

open System.Reflection
open System.IO
open System.Windows.Media.Imaging

let name = "Mercury"
let domain = "xtcy.dev"

// Read the contents of an embedded resource as bytes
let readEmbeddedResourceAsBytes (resourceName: string) =
    let assembly = Assembly.GetExecutingAssembly()
    use stream = assembly.GetManifestResourceStream resourceName
    if isNull stream then
        failwithf "Resource '%s' not found in assembly." resourceName
    else
        use memoryStream = new MemoryStream()
        stream.CopyTo memoryStream
        memoryStream.ToArray()

let imgIcon =
    let icon = readEmbeddedResourceAsBytes "F_sharp_testing.icon.png"

    let img = BitmapImage()
    img.BeginInit()
    img.StreamSource <- new MemoryStream(icon)
    img.CacheOption <- BitmapCacheOption.OnLoad
    img.EndInit()
    img.Freeze() // Freeze the image to make it cross-thread accessible
    img
