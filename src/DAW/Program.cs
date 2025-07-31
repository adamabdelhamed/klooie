using klooie;
 
var app = new ConsoleApp();

app.Invoke(async () =>
{
    app.Sound = new MyEngine();
    var workspace = await Workspace.Bootstrap();
    var session = new WorkspaceSession() { Workspace = workspace };
    var dawPanel = ConsoleApp.Current.LayoutRoot.Add(new DAWPanel(session)).Fill();
});

app.Run();

class MyEngine : AudioPlaybackEngine
{

    protected override Dictionary<string, Func<Stream>> LoadSounds()
    {
        return new Dictionary<string, Func<Stream>>
        {

        };
    }
}