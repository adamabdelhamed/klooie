using PowerArgs;

namespace klooie.VideoPlayer;
class Program
{
    [ArgExistingFile, ArgPosition(0)]
    public string InputFile { get; set; }
    static void Main(string[] args) => Args.InvokeMain<Program>(args);

    public void Main()
    {
        if (InputFile == null)
        {
            "No input file specified".ToRed().WriteLine();
            return;
        }

        var fancy = ConsoleProvider.Fancy;
        var app = new ConsoleApp();
        app.Invoke(() =>
        {
            var player = app.LayoutRoot.Add(new ConsoleBitmapPlayer()).Fill();
            player.Load(File.OpenRead(InputFile));
        });
        app.Run();
    }
}

